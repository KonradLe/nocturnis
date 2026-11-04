using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Krypta.Editor
{
    public static class KryptaSetup
    {
        const string ConfigPath="Assets/Resources/KryptaAssets.asset";
        const string ScenePath="Assets/Scenes/Krypta.unity";
        static readonly string[] ClipNames={"Idle","Running_A","1H_Melee_Attack_Chop","1H_Melee_Attack_Slice_Horizontal","2H_Melee_Attack_Spin","Dodge_Forward","Death_A","1H_Ranged_Shoot"};

        [InitializeOnLoadMethod]
        static void Schedule() { EditorApplication.delayCall+=AutoPrepare; }

        static void AutoPrepare()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var existing=AssetDatabase.LoadAssetAtPath<KryptaAssets>(ConfigPath);
            if (existing && existing.version>=NocturnisOriginalRuntimeBuild.Configuration && File.Exists(ScenePath)) return;
            PlayerSettings.bundleVersion="0.4.3";
            if (existing && existing.version>=7 && File.Exists(ScenePath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall+=AutoPrepare; return; }
            try { Prepare(); }
            catch (Exception e) { Debug.LogError("Krypta: przygotowanie nie powiodło się. Po imporcie modeli wybierz Krypta > Przygotuj projekt. " + e.Message); }
        }

        [MenuItem("Krypta/Przygotuj projekt")]
        public static void Prepare()
        {
            EnsureFolder("Assets/Resources"); EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Generated"); EnsureFolder("Assets/Generated/Animations"); EnsureFolder("Assets/Generated/Materials");
            KryptaAssets config=AssetDatabase.LoadAssetAtPath<KryptaAssets>(ConfigPath);
            if(config && config.version>=10){NocturnisIntegratedBuild.Validate();return;}
            // Keep the explicitly prepared original model/floor configuration during
            // editor startup and regression suites that call the legacy setup.
            if (config && config.version>=NocturnisOriginalRuntimeBuild.Configuration && File.Exists(ScenePath))
            {
                NocturnisOriginalRuntimeBuild.ValidateReady();
                return;
            }
            bool fresh=!config;
            if (fresh) config=ScriptableObject.CreateInstance<KryptaAssets>();
            config.knight=Model("Adventurers","Knight.glb");
            config.skeleton=Model("Skeletons","Skeleton_Warrior.glb");
            config.floor=Model("Dungeon","floor_tile_small.gltf.glb");
            config.pillar=Model("Dungeon","pillar.gltf.glb");
            config.chest=Model("Dungeon","chest.glb");
            config.torch=Model("Dungeon","torch.gltf.glb");
            config.sword=Model("Adventurers","sword_1handed.gltf");
            config.knightAnimations=Animations(config.knight,"Knight");
            config.skeletonAnimations=Animations(config.skeleton,"Skeleton");
            config.wallMaterial=MaterialAsset("Stone",new Color(.25f,.24f,.29f),false);
            config.floorMaterial=MaterialAsset("CryptFlagstone",new Color(.48f,.44f,.39f),false);
            config.effectMaterial=ShaderMaterial("SpellLight","Nocturnis/SpellLight");
            config.particleMaterial=ShaderMaterial("SoftEmber","Nocturnis/SoftEmber");
            config.cryptFloors=new[]{config.floor,Model("Dungeon","floor_tile_small_decorated.gltf.glb")};
            config.cryptWall=Model("Dungeon","wall_half.gltf.glb");
            config.coffin=ModelAt("Assets/Art/Kenney/Graveyard","coffin.glb");
            config.altar=ModelAt("Assets/Art/Kenney/Graveyard","altar-stone.glb");
            config.candles=Model("Dungeon","candle_triple.gltf.glb");
            config.rubble=Model("Dungeon","rubble_half.gltf.glb");
            config.banner=Model("Dungeon","banner_red.gltf.glb");
            config.goldMaterial=MaterialAsset("Gold",new Color(1,.70f,.22f),true);
            config.tealMaterial=MaterialAsset("Teal",new Color(.22f,.94f,.82f),true);
            config.redMaterial=MaterialAsset("Danger",new Color(1,.22f,.24f),true);
            config.earthMaterial=MaterialAsset("ForestEarth",new Color(.13f,.12f,.11f),false);
            config.violetMaterial=MaterialAsset("Ichor",new Color(.7f,.27f,1),true);
            config.greenMaterial=MaterialAsset("Renewal",new Color(.4f,.95f,.55f),true);
            string[] models={"Knight.glb","Rogue_Hooded.glb","Rogue.glb","Mage.glb","Barbarian.glb"};
            string[] weapons={"sword_1handed.gltf","dagger.gltf","crossbow_1handed.gltf","staff.gltf","axe_2handed.gltf"};
            config.heroes=new KryptaAssets.Character[5];
            for(int i=0;i<5;i++)
            {
                var model=Model("Adventurers",models[i]);
                config.heroes[i]=new KryptaAssets.Character{model=model,weapon=Model("Adventurers",weapons[i]),animations=Animations(model,"Hero"+i)};
            }
            var zombie=ModelAt("Assets/Art/Kenney/Graveyard","character-zombie.glb");
            config.zombie=new KryptaAssets.Character{model=zombie,animations=Animations(zombie,"Zombie",true)};
            var bishop=Model("Skeletons","Skeleton_Mage.glb");
            config.bishop=new KryptaAssets.Character{model=bishop,weapon=Model("Adventurers","staff.gltf"),animations=Animations(bishop,"Bishop")};
            config.trees=new[]{Model("Forest","Tree_1_A_Color1.gltf"),Model("Forest","Tree_2_B_Color1.gltf"),Model("Forest","Tree_Bare_1_A_Color1.gltf"),Model("Graveyard","tree_dead_large.gltf")};
            config.rocks=new[]{Model("Forest","Rock_1_A_Color1.gltf"),Model("Forest","Rock_2_B_Color1.gltf")};
            config.graves=new[]{Model("Graveyard","grave_A.gltf"),Model("Graveyard","gravestone.gltf"),Model("Graveyard","gravemarker_A.gltf")};
            config.buildings=new[]{Model("Medieval","building_blacksmith_blue.gltf"),Model("Medieval","building_tavern_blue.gltf"),Model("Medieval","building_barracks_blue.gltf"),Model("Medieval","building_market_blue.gltf")};
            config.church=Model("Medieval","building_church_blue.gltf");config.well=Model("Medieval","building_well_blue.gltf");
            config.tent=Model("Medieval","tent.gltf");
            config.fence=ModelAt("Assets/Art/Kenney/Graveyard","iron-fence.glb");
            config.skull=Model("Graveyard","skull.gltf");
            config.crypt=ModelAt("Assets/Art/Kenney/Graveyard","crypt.glb");
            config.bridge=Model("Medieval","building_bridge_A.gltf");
            ConfigureUI();
            config.surfaces=new[]{
                Atlas("hexagons_medieval",config.buildings[0],"Assets/Art/KayKit/Medieval/addons/kaykit_medieval_hexagon_pack/Assets/gltf/buildings/blue/hexagons_medieval.png"),
                Atlas("forest",config.trees[0],"Assets/Art/KayKit/Forest/Assets/gltf/forest_texture.png"),
                Atlas("HalloweenBits",config.graves[0],"Assets/Art/KayKit/Graveyard/addons/kaykit_halloween_bits/Assets/gltf/halloweenbits_texture.png"),
                Atlas("colormap",config.zombie.model,"Assets/Art/Kenney/Graveyard/Models/GLB format/Textures/colormap.png")
            };
            // Resolve the legacy atlas above before replacing trees with the new sources.
            config.trees=NocturnisForestImport.LoadTrees();
            config.earthMaterial=AssetDatabase.LoadAssetAtPath<Material>(NocturnisForestImport.Output+"/ForestEarth.mat");
            if(!config.earthMaterial)throw new InvalidOperationException("Forest ground material missing");
            if(NocturnisKayKitImport.IsReady){NocturnisKayKitImport.Apply(config);config.version=8;}
            else {config.heroes[0]=NocturnisKnightImport.Load();config.skeletonVariants=null;config.version=7;}
            if(NocturnisOriginalRuntimeBuild.IsReady)NocturnisOriginalRuntimeBuild.Apply(config);
            if (fresh) AssetDatabase.CreateAsset(config,ConfigPath);
            EditorUtility.SetDirty(config);

            PlayerSettings.companyName="Nocturnis";
            PlayerSettings.productName="Nocturnis — Żniwo Upadłych Gwiazd";
            PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=720;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=true;
            PlayerSettings.bundleVersion="0.4.3";
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            // This prototype uses Unity's built-in keyboard and mouse input.
            UnityEngine.Object settings=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            SerializedObject serialized=new SerializedObject(settings);
            SerializedProperty input=serialized.FindProperty("activeInputHandler");
            if (input != null && input.intValue != 0) { input.intValue=0; serialized.ApplyModifiedPropertiesWithoutUndo(); }

            if (!File.Exists(ScenePath))
            {
                Scene previous=SceneManager.GetActiveScene();
                bool single=string.IsNullOrEmpty(previous.path);
                if (single && previous.isDirty && !Application.isBatchMode)
                    throw new InvalidOperationException("Zapisz bieżącą scenę przed przygotowaniem krypty.");
                Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,single ? NewSceneMode.Single : NewSceneMode.Additive);
                GameObject root=new GameObject("Krypta");
                SceneManager.MoveGameObjectToScene(root,scene);
                root.AddComponent<KryptaGame>();
                EditorSceneManager.SaveScene(scene,ScenePath);
                if (!single)
                {
                    EditorSceneManager.CloseScene(scene,true);
                    if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                }
            }
            EditorBuildSettings.scenes=new[] {new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("Krypta: projekt przygotowany. Otwórz Assets/Scenes/Krypta.unity i naciśnij Play.");
        }

        static void ConfigureUI()
        {
            foreach(string path in Directory.GetFiles("Assets/Resources/UI","*.png"))
            {
                var t=AssetImporter.GetAtPath(path) as TextureImporter;if(t==null)continue;
                t.textureType=TextureImporterType.Default;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.wrapMode=TextureWrapMode.Clamp;t.maxTextureSize=4096;t.textureCompression=TextureImporterCompression.Uncompressed;t.SaveAndReimport();
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }

        static GameObject Model(string pack,string filename)
        {
            return ModelAt("Assets/Art/KayKit/"+pack,filename);
        }
        static GameObject ModelAt(string directory,string filename)
        {
            string[] matches=Directory.GetFiles(directory,filename,SearchOption.AllDirectories);
            if (matches.Length != 1) throw new InvalidOperationException("Nie znaleziono jednoznacznie modelu: " + filename);
            string path=matches[0].Replace('\\','/');
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new InvalidOperationException("Model nie jest jeszcze zaimportowany przez glTFast: " + path);
            return prefab;
        }

        static AnimationClip[] Animations(GameObject model,string prefix,bool kenney=false)
        {
            EnsureFolder("Assets/Generated/Animations/"+prefix);
            var originals=new Dictionary<string,AnimationClip>();
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)))
            {
                AnimationClip clip=obj as AnimationClip;
                if (clip) originals[clip.name]=clip;
            }
            var output=new List<AnimationClip>();
            foreach (string name in ClipNames)
            {
                AnimationClip original;
                string source=name;
                if(kenney)source=name=="Idle" ? "idle" : name=="Running_A" ? "walk" : name=="Death_A" ? "die" : name=="Dodge_Forward" ? "sprint" : "attack-melee-right";
                if (!originals.TryGetValue(source,out original))
                {
                    if(name=="1H_Ranged_Shoot") original=originals["1H_Melee_Attack_Chop"];
                    else throw new InvalidOperationException("Brak animacji " + prefix + "/" + source);
                }
                string path="Assets/Generated/Animations/"+prefix+"/"+name+".anim";
                AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (!clip) { clip=UnityEngine.Object.Instantiate(original); AssetDatabase.CreateAsset(clip,path); }
                else EditorUtility.CopySerialized(original,clip);
                clip.name=name; clip.legacy=true;
                clip.wrapMode=name == "Idle" || name == "Running_A" ? WrapMode.Loop : WrapMode.ClampForever;
                EditorUtility.SetDirty(clip);
                output.Add(clip);
            }
            return output.ToArray();
        }

        static Material MaterialAsset(string name,Color color,bool unlit)
        {
            string path="Assets/Generated/Materials/"+name+".mat";
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                Shader shader=Shader.Find(unlit ? "Unlit/Color" : "Standard");
                if (!shader) throw new InvalidOperationException("Brak shadera dla materiału " + name);
                material=new Material(shader);
                AssetDatabase.CreateAsset(material,path);
            }
            material.color=color;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness",.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material ShaderMaterial(string name,string shaderName)
        {
            string path="Assets/Generated/Materials/"+name+".mat";
            Shader shader=Shader.Find(shaderName);
            if(!shader)throw new InvalidOperationException("Missing shader: "+shaderName);
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            else material.shader=shader;
            material.color=Color.white;EditorUtility.SetDirty(material);return material;
        }

        static KryptaAssets.Surface Atlas(string name,GameObject model,string texturePath)
        {
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(!texture)throw new InvalidOperationException("Missing atlas: "+texturePath);
            Material original=null;
            foreach(var r in model.GetComponentsInChildren<Renderer>(true))
                foreach(var m in r.sharedMaterials)if(m && m.name==name)original=m;
            if(!original)throw new InvalidOperationException("Missing source material: "+name);
            string path="Assets/Generated/Materials/Atlas-"+name+".mat";
            Material result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!result){result=new Material(original);AssetDatabase.CreateAsset(result,path);}else EditorUtility.CopySerialized(original,result);
            result.name="Atlas-"+name;
            string[] properties={"baseColorTexture","_BaseColorTexture","_BaseMap","_MainTex"};bool assigned=false;
            foreach(string property in properties)if(result.HasProperty(property)){result.SetTexture(property,texture);assigned=true;}
            if(!assigned)throw new InvalidOperationException("No base texture slot on "+name+" / "+result.shader.name);
            EditorUtility.SetDirty(result);
            Debug.Log("Nocturnis atlas: "+name+" / "+result.shader.name+" / "+texture.width+"x"+texture.height);
            return new KryptaAssets.Surface{source=name,material=result};
        }

        [MenuItem("Krypta/Otwórz scenę gry")]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) Prepare();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Krypta/Zbuduj wersję Linux")]
        public static void BuildLinux() { Build(BuildTarget.StandaloneLinux64,"Builds/Linux/Nocturnis.x86_64"); }
        [MenuItem("Krypta/Zbuduj wersję Windows")]
        public static void BuildWindows() { Build(BuildTarget.StandaloneWindows64,"Builds/Windows/Nocturnis.exe"); }
        [MenuItem("Krypta/Zbuduj wersję Web")]
        public static void BuildWeb() { Build(BuildTarget.WebGL,"Builds/Web"); }

        public static void BuildRelease() { BuildLinux();BuildWindows(); }

        static void Build(BuildTarget target,string destination)
        {
            Prepare();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target) && target != BuildTarget.WebGL)
                throw new InvalidOperationException("Najpierw zainstaluj moduł platformy " + target);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[] {ScenePath}, locationPathName=destination, target=target, options=BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Budowanie gry nie powiodło się: " + report.summary.result);
            Debug.Log("Krypta: zbudowano " + destination);
        }
    }
}
