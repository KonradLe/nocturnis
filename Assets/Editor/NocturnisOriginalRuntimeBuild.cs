using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Krypta.Editor
{
    public static class NocturnisOriginalRuntimeBuild
    {
        public const string HeroFolder="Assets/Art/Nocturnis/TrellisInquisitor-v01";
        public const string Version="0.4.7-original.1";
        public const int Configuration=9;
        public static readonly string[] OptionalClips={"Walking_A","Hit_A","Defense_A","Cast_A","Block"};
        [Serializable] sealed class MaterialSpec { public string name; public float metallic,roughness; }
        [Serializable] sealed class MaterialDocument { public MaterialSpec[] materials; }
        static string PrefabPath { get { return HeroFolder+"/TrellisInquisitor.prefab"; } }
        public static bool IsReady { get {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                && NocturnisKayKitImport.ClipNames.Concat(OptionalClips).All(name=>AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroFolder+"/Animations/"+name+".anim"));
        } }
        static void Require(bool pass,string message) { if(!pass)throw new InvalidOperationException(message); }

        public static void Prepare()
        {
            NocturnisArtBuild.Prepare();
            NocturnisOriginalFloorSetup.Prepare();
            ImportHero();
            var config=Resources.Load<KryptaAssets>("KryptaAssets");
            Require(config && config.version>=8,"An existing five-hero configuration is required.");
            Apply(config);EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
            ValidateReady();
        }

        public static void Apply(KryptaAssets config)
        {
            Require(IsReady,"The original runtime character has not been prepared.");
            Require(config && config.heroes!=null && config.heroes.Length==5,"Five existing heroes are required.");
            var clips=NocturnisKayKitImport.ClipNames.Concat(OptionalClips)
                .Select(name=>AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroFolder+"/Animations/"+name+".anim")).Where(clip=>clip).ToArray();
            config.heroes[0]=new KryptaAssets.Character {
                model=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                weapon=NocturnisKayKitImport.Model("Adventurers","Weapons","sword_1handed"), animations=clips };
            config.knight=config.heroes[0].model;config.knightAnimations=clips;config.version=Configuration;
        }

        public static void ValidateReady()
        {
            var config=Resources.Load<KryptaAssets>("KryptaAssets");
            Require(IsReady && config && config.version>=Configuration && config.heroes.Length==5,"The original runtime configuration is incomplete.");
            Require(AssetDatabase.GetAssetPath(config.heroes[0].model)==PrefabPath,"The configured first hero is not the reviewed TRELLIS model.");
            foreach(string name in OptionalClips)
                Require(config.heroes[0].animations.Any(clip=>clip && clip.name==name && clip.legacy && clip.length>.1f),"Missing TRELLIS action clip: "+name);
            foreach(var hero in config.heroes)
            {
                Require(hero!=null && hero.model && hero.weapon,"A hero or equipped weapon is missing.");
                foreach(string name in NocturnisKayKitImport.ClipNames)
                    Require(hero.animations.Any(clip=>clip && clip.name==name && clip.legacy && clip.length>.1f),"Missing required clip: "+name);
            }
            var floor=Resources.Load<NocturnisOriginalFloorKit>("OriginalCryptFloor");
            Require(floor && floor.IsReady,"The original floor resource is missing or incomplete.");
            Require(Resources.Load<Texture2D>("UI/StorybookMenu"),"The current menu artwork is missing.");
        }

        static void ImportHero()
        {
            Require(File.Exists(HeroFolder+"/manifest.json"),"The final runtime export manifest is missing.");
            string sourcePath=HeroFolder+"/TrellisInquisitor.fbx";
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(sourcePath) as ModelImporter;
            Require(importer,"The final rigged FBX is missing.");
            importer.globalScale=1;importer.useFileScale=true;importer.isReadable=true;
            importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;
            importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects=false;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.importNormals=ModelImporterNormals.Import;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            string texturePath=HeroFolder+"/Trellis_BaseColor.png";
            var textureImporter=AssetImporter.GetAtPath(texturePath) as TextureImporter;
            Require(textureImporter,"The explicit full-resolution color atlas is missing.");
            textureImporter.textureType=TextureImporterType.Default;textureImporter.sRGBTexture=true;
            textureImporter.mipmapEnabled=true;textureImporter.maxTextureSize=2048;
            textureImporter.textureCompression=TextureImporterCompression.Compressed;
            textureImporter.wrapMode=TextureWrapMode.Clamp;textureImporter.SaveAndReimport();
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Require(atlas && atlas.width>=1024 && atlas.height>=1024,"The character atlas has insufficient dimensions.");
            var specs=JsonUtility.FromJson<MaterialDocument>(File.ReadAllText(HeroFolder+"/materials.json"));
            Require(specs!=null && specs.materials!=null && specs.materials.Length>0,"Material parameters are missing.");
            var shader=Shader.Find("Standard");Require(shader,"Built-in Standard shader is unavailable.");
            NocturnisKayKitImport.Folder(HeroFolder+"/Materials");NocturnisKayKitImport.Folder(HeroFolder+"/Animations");
            var materials=specs.materials.ToDictionary(spec=>spec.name,spec=> {
                string path=HeroFolder+"/Materials/"+spec.name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
                material.shader=shader;material.color=Color.white;material.mainTexture=atlas;
                material.SetFloat("_Metallic",spec.metallic);material.SetFloat("_Glossiness",1-spec.roughness);
                EditorUtility.SetDirty(material);return material;
            });
            var imported=AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
                .Where(clip=>!clip.name.StartsWith("__preview__",StringComparison.Ordinal)).ToArray();
            foreach(string name in NocturnisKayKitImport.ClipNames.Concat(OptionalClips))
            {
                var matches=imported.Where(clip=>clip.name==name || clip.name.EndsWith("|"+name,StringComparison.Ordinal)).ToArray();
                Require(matches.Length==1,"Expected one exported clip named "+name+"; found "+matches.Length);
                string path=HeroFolder+"/Animations/"+name+".anim";
                var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(!saved){saved=new AnimationClip();AssetDatabase.CreateAsset(saved,path);}
                EditorUtility.CopySerialized(matches[0],saved);saved.name=name;saved.legacy=true;
                saved.wrapMode=name=="Idle" || name=="Running_A" || name=="Walking_A" ? WrapMode.Loop : WrapMode.ClampForever;
                EditorUtility.SetDirty(saved);
            }
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);Require(source,"The rigged FBX did not import.");
            var model=UnityEngine.Object.Instantiate(source);model.name="TrellisInquisitor";
            try
            {
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(material=> {
                        Require(material && materials.ContainsKey(material.name),"Unknown FBX material slot: "+(material ? material.name : "null"));
                        return materials[material.name];
                    }).ToArray();
                    if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
                }
                var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(HeroFolder+"/Animations/Idle.anim");
                idle.SampleAnimation(model,.15f);
                Bounds bounds=NocturnisOriginalModelQA.SkinBounds(model);
                Require(bounds.size.y>1.5f && bounds.size.y<2.2f,"Unexpected exported body height: "+bounds.size.y);
                var profile=model.GetComponent<ActorModelProfile>();if(!profile)profile=model.AddComponent<ActorModelProfile>();
                profile.bodyHeight=bounds.size.y;profile.groundOffset=-bounds.min.y;
                profile.fitActionDuration=true;profile.garbMaterialPrefix="Trellis_Cloth";
                Require(model.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="handslot.r"),"The right-hand weapon slot is missing.");
                var left=model.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name=="handslot.l");
                Require(left,"The left-hand shield slot is missing.");
                var shield=UnityEngine.Object.Instantiate(NocturnisKayKitImport.Model("Adventurers","Weapons","shield_badge"),left);
                shield.name="Badge_Shield";shield.transform.localPosition=Vector3.zero;shield.transform.localRotation=Quaternion.identity;
                PrefabUtility.SaveAsPrefabAsset(model,PrefabPath);
            }
            finally{UnityEngine.Object.DestroyImmediate(model);}
            AssetDatabase.SaveAssets();
        }

        public static void Linux(){Build(BuildTarget.StandaloneLinux64,"Builds/OriginalRuntimeReview/Linux/Nocturnis.x86_64");}
        public static void Windows(){Build(BuildTarget.StandaloneWindows64,"Builds/OriginalRuntimeReview/Windows/Nocturnis.exe");}
        static void Build(BuildTarget target,string path)
        {
            ValidateReady();
            var scenes=EditorBuildSettings.scenes.Where(scene=>scene.enabled).Select(scene=>scene.path).ToArray();
            Require(scenes.Length>0 && scenes.All(scene=>!scene.Contains("OriginalArtReview")),"Only enabled game scenes may enter the playable build.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));string previous=PlayerSettings.bundleVersion;
            try {
                PlayerSettings.bundleVersion=Version;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=path,target=target,options=BuildOptions.None});
                Require(report.summary.result==BuildResult.Succeeded,"Original runtime build failed: "+report.summary.result);
                Debug.Log("Original runtime "+Version+" built: "+path);
            }
            finally{PlayerSettings.bundleVersion=previous;}
        }
    }
}
