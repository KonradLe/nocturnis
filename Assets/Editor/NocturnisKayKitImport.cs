using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Krypta.Editor
{
    // Asset-only import. No UI images, menu scripts, scenes or player settings are changed.
    public static class NocturnisKayKitImport
    {
        public const string Source="Assets/Art/KayKit2026";
        public const string Output="Assets/Generated/KayKit2026";
        public static readonly string[] ClipNames={"Idle","Running_A","1H_Melee_Attack_Chop","1H_Melee_Attack_Slice_Horizontal","2H_Melee_Attack_Spin","Dodge_Forward","Death_A","1H_Ranged_Shoot"};
        public static readonly string[] HeroModels={"Knight","Rogue_Hooded","Ranger","Mage","Barbarian"};
        public static readonly string[] SkeletonModels={"Skeleton_Minion","Skeleton_Warrior","Skeleton_Rogue","Skeleton_Mage"};
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();

        public static bool IsReady
        {
            get
            {
                // An in-progress asset transfer must not break a concurrent menu build.
                if(!File.Exists(Output+"/ready.txt"))return false;
                bool PrefabReady(string pack,string category,string name)
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/"+pack+"/"+category+"/"+name+".prefab");
                    if(!prefab)return false;
                    var renderers=prefab.GetComponentsInChildren<Renderer>(true);
                    if(renderers.Length==0)return false;
                    foreach(var renderer in renderers)
                    {
                        var sharedMaterials=renderer.sharedMaterials;
                        if(sharedMaterials.Length==0 || sharedMaterials.Any(m=>!m))return false;
                        if(renderer is SkinnedMeshRenderer skin)
                        {
                            if(!skin.sharedMesh)return false;
                        }
                        else if(renderer is MeshRenderer)
                        {
                            var filter=renderer.GetComponent<MeshFilter>();
                            if(!filter || !filter.sharedMesh)return false;
                        }
                    }
                    return true;
                }
                bool CharacterReady(string pack,string name)
                {
                    if(!PrefabReady(pack,"Characters",name))return false;
                    return ClipNames.All(clipName=>
                    {
                        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Output+"/"+pack+"/Characters/"+name+"/"+clipName+".anim");
                        return clip && clip.legacy && clip.length>0;
                    });
                }
                return HeroModels.All(name=>CharacterReady("Adventurers",name))
                    && SkeletonModels.All(name=>CharacterReady("Skeletons",name))
                    && new[]{"sword_1handed","dagger","crossbow_1handed","staff","axe_2handed"}.All(name=>PrefabReady("Adventurers","Weapons",name))
                    && new[]{"Skeleton_Blade","Skeleton_Axe","Skeleton_Crossbow","Skeleton_Staff"}.All(name=>PrefabReady("Skeletons","Weapons",name))
                    && new[]{"floor_tile_small","floor_tile_small_decorated","pillar","chest","torch","wall_half","candle_triple","rubble_half","banner_red"}
                        .All(name=>PrefabReady("Dungeon","Models",name));
            }
        }

        public static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        public static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }

        [MenuItem("Krypta/Assety/Importuj KayKit 2026")]
        public static void Prepare()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Folder(Output);materials.Clear();
            foreach(string pack in new[]{"Adventurers","Skeletons"})
            {
                foreach(string path in Directory.GetFiles(Source+"/"+pack+"/Characters","*.glb"))Import(path.Replace('\\','/'),pack,"Characters",true);
                foreach(string path in Directory.GetFiles(Source+"/"+pack+"/Weapons","*.gltf"))Import(path.Replace('\\','/'),pack,"Weapons",false);
            }
            foreach(string path in Directory.GetFiles(Source+"/Dungeon/Models","*.gltf"))Import(path.Replace('\\','/'),"Dungeon","Models",false);
            AttachShield("Adventurers","Knight","shield_badge");
            AttachShield("Skeletons","Skeleton_Warrior","Skeleton_Shield_Large_A");
            AssetDatabase.SaveAssets();
            File.WriteAllText(Output+"/ready.txt","KayKit 2026 import complete: 10 characters, 80 clips, 211 dungeon models.\n");
            AssetDatabase.ImportAsset(Output+"/ready.txt",ImportAssetOptions.ForceSynchronousImport);
        }

        static void AttachShield(string pack,string character,string shield)
        {
            string path=Output+"/"+pack+"/Characters/"+character+".prefab";
            var model=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var slot=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="handslot.l");
                Require(slot,character+": missing left-hand equipment slot");
                foreach(var child in slot.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
                var item=UnityEngine.Object.Instantiate(Model(pack,"Weapons",shield),slot);
                // ActorVisual retains this off-hand attachment while replacing the right-hand weapon.
                item.name="Badge_Shield";item.transform.localPosition=Vector3.zero;item.transform.localRotation=Quaternion.identity;
                PrefabUtility.SaveAsPrefabAsset(model,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(model);}
        }

        static void Import(string path,string pack,string category,bool animated)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(source,"Not imported: "+path);
            string name=Path.GetFileNameWithoutExtension(path),folder=Output+"/"+pack+"/"+category;Folder(folder);
            var model=UnityEngine.Object.Instantiate(source);model.name=name;
            try
            {
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>MaterialFor(m,pack,Path.GetDirectoryName(path).Replace('\\','/'))).ToArray();
                    if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
                }
                Bounds ignored;Require(ModelBounds.TryGet(model,out ignored),"Empty geometry: "+path);
                if(animated)
                {
                    string clipsFolder=folder+"/"+name;Folder(clipsFolder);
                    var originals=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
                    foreach(string clipName in ClipNames)
                    {
                        var original=originals.SingleOrDefault(c=>c.name==clipName);Require(original,"Missing "+name+"/"+clipName);
                        string destination=clipsFolder+"/"+clipName+".anim";
                        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
                        if(!clip){clip=UnityEngine.Object.Instantiate(original);AssetDatabase.CreateAsset(clip,destination);}
                        else EditorUtility.CopySerialized(original,clip);
                        clip.name=clipName;clip.legacy=true;clip.wrapMode=clipName=="Idle" || clipName=="Running_A"?WrapMode.Loop:WrapMode.ClampForever;
                        EditorUtility.SetDirty(clip);
                    }
                    // glTF skin culling bounds include animation travel and are larger
                    // than the standing body. Normalize actors using actual idle geometry.
                    var animator=model.GetComponentInChildren<Animator>();
                    originals.Single(c=>c.name=="Idle").SampleAnimation(animator?animator.gameObject:model,.12f);
                    Bounds body=new Bounds();bool hasBody=false;
                    foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mesh=new Mesh();
                        try
                        {
                            skin.BakeMesh(mesh,false);
                            foreach(var vertex in mesh.vertices)
                            {
                                var point=model.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex));
                                if(!hasBody){body=new Bounds(point,Vector3.zero);hasBody=true;}else body.Encapsulate(point);
                            }
                        }
                        finally{UnityEngine.Object.DestroyImmediate(mesh);}
                    }
                    Require(hasBody && body.size.y>.2f,"Missing standing body: "+name);
                    var profile=model.GetComponent<ActorModelProfile>() ?? model.AddComponent<ActorModelProfile>();
                    profile.bodyHeight=body.size.y;profile.groundOffset=-body.min.y;
                }
                PrefabUtility.SaveAsPrefabAsset(model,folder+"/"+name+".prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(model);}
        }

        static Material MaterialFor(Material original,string pack,string textureFolder)
        {
            Require(original,"Missing source material");
            string key=pack+"_"+original.name;
            if(materials.TryGetValue(key,out var found))return found;
            Folder(Output+"/Materials");string path=Output+"/Materials/"+key+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(original);AssetDatabase.CreateAsset(material,path);}
            else EditorUtility.CopySerialized(original,material);
            material.name="KayKit2026_"+key;
            string textureName=pack=="Dungeon" ? "dungeon_texture" : pack=="Skeletons" ? "skeleton_texture" : original.name+"_texture";
            string texturePath=textureFolder+"/"+textureName+".png";
            if(original.name!="Glow")
            {
                var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;
                Require(importer,"Missing atlas: "+texturePath);
                importer.sRGBTexture=true;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);Require(texture && texture.width>=512,"Invalid atlas: "+texturePath);
                string slot=new[]{"baseColorTexture","_BaseColorTexture","_BaseMap","_MainTex"}.FirstOrDefault(material.HasProperty);
                Require(slot!=null,"No atlas slot in "+material.shader.name);material.SetTexture(slot,texture);
                if(material.HasProperty("roughnessFactor"))material.SetFloat("roughnessFactor",.72f);
            }
            Require(material.shader && material.shader.isSupported,"Unsupported character shader");
            EditorUtility.SetDirty(material);materials.Add(key,material);return material;
        }

        public static GameObject Model(string pack,string category,string name)
        {
            string path=Output+"/"+pack+"/"+category+"/"+name+".prefab";
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(model,"Missing prepared asset: "+path);return model;
        }
        public static KryptaAssets.Character Character(string pack,string name,string weapon=null)
        {
            var clips=ClipNames.Select(c=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Output+"/"+pack+"/Characters/"+name+"/"+c+".anim")).ToArray();
            Require(clips.All(c=>c && c.legacy && c.length>0),"Incomplete animations: "+name);
            return new KryptaAssets.Character{model=Model(pack,"Characters",name),animations=clips,weapon=weapon==null?null:Model(pack,"Weapons",weapon)};
        }

        public static void Apply(KryptaAssets config)
        {
            Require(config,"Missing game config");
            string[] weapons={"sword_1handed","dagger","crossbow_1handed","staff","axe_2handed"};
            config.heroes=HeroModels.Select((name,index)=>Character("Adventurers",name,weapons[index])).ToArray();
            string[] skeletonWeapons={"Skeleton_Blade","Skeleton_Axe","Skeleton_Crossbow","Skeleton_Staff"};
            config.skeletonVariants=SkeletonModels.Select((name,index)=>Character("Skeletons",name,skeletonWeapons[index])).ToArray();
            config.knight=config.heroes[0].model;config.knightAnimations=config.heroes[0].animations;config.sword=config.heroes[0].weapon;
            config.skeleton=config.skeletonVariants[1].model;config.skeletonAnimations=config.skeletonVariants[1].animations;config.bishop=config.skeletonVariants[3];
            config.floor=Model("Dungeon","Models","floor_tile_small");config.cryptFloors=new[]{config.floor,Model("Dungeon","Models","floor_tile_small_decorated")};
            config.pillar=Model("Dungeon","Models","pillar");config.chest=Model("Dungeon","Models","chest");config.torch=Model("Dungeon","Models","torch");
            config.cryptWall=Model("Dungeon","Models","wall_half");config.candles=Model("Dungeon","Models","candle_triple");config.rubble=Model("Dungeon","Models","rubble_half");config.banner=Model("Dungeon","Models","banner_red");
            config.version=Mathf.Max(config.version,8);EditorUtility.SetDirty(config);
        }
    }
}
