using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta.Editor
{
    // Run only in the isolated asset review project. Scenes remain in memory;
    // menu code, UI images and saved game scenes are never changed here.
    public static class NocturnisKayKitQA
    {
        const string Output="Logs/KayKit2026";
        [Serializable] sealed class ClipResult
        {
            public string name;
            public int bindings;
            public float duration,maximumVertexDisplacement;
        }
        [Serializable] sealed class CharacterResult
        {
            public string name,pack;
            public int skinnedMeshes,vertices;
            public float worldHeight,minimumY;
            public List<ClipResult> clips=new List<ClipResult>();
        }
        [Serializable] sealed class Result
        {
            public string status="RUNNING",error="";
            public int assertions,dungeonPrefabs,configVersion;
            public List<CharacterResult> characters=new List<CharacterResult>();
            public List<string> screenshots=new List<string>();
        }
        static Result result;

        static void Require(bool condition,string message)
        {
            result.assertions++;
            if(!condition)throw new InvalidOperationException(message);
        }

        public static void Run()
        {
            Directory.CreateDirectory(Output);result=new Result();
            try
            {
                NocturnisKayKitImport.Prepare();
                var config=LoadStageConfiguration();
                Require(config,"Missing stage KryptaAssets configuration");
                NocturnisKayKitImport.Apply(config);AssetDatabase.SaveAssets();
                Require(config.version>=8 && config.heroes.Length==5 && config.skeletonVariants.Length==4,"Stage config is incomplete");
                result.configVersion=config.version;
                EmptyScene();
                foreach(string name in new[]{"Knight","Barbarian","Mage","Ranger","Rogue","Rogue_Hooded"})
                    ValidateCharacter("Adventurers",name);
                foreach(string name in NocturnisKayKitImport.SkeletonModels)ValidateCharacter("Skeletons",name);
                ValidateDungeon();
                RenderHeroes(config,false);
                RenderHeroes(config,true);
                RenderSkeletons(config);
                RenderCrypt(config);
                result.status="PASS";WriteResult();
                Debug.Log("KAYKIT_QA PASS: "+result.characters.Count+" characters, "+result.dungeonPrefabs+" dungeon models, "+result.assertions+" assertions; captures in "+Output);
                EditorApplication.Exit(0);
            }
            catch(Exception exception)
            {
                result.status="FAIL";result.error=exception.ToString();WriteResult();
                Debug.LogException(exception);EditorApplication.Exit(1);
            }
        }

        static void WriteResult()
        {
            File.WriteAllText(Output+"/result.json",JsonUtility.ToJson(result,true));
            var lines=new List<string>{result.status+": KayKit character and environment review",
                "Characters: "+result.characters.Count+"; dungeon prefabs: "+result.dungeonPrefabs+"; assertions: "+result.assertions,
                "Config version: "+result.configVersion};
            foreach(var character in result.characters)
            {
                lines.Add(character.pack+"/"+character.name+": "+character.vertices+" skinned vertices, height "+character.worldHeight.ToString("F3")+"m");
                foreach(var clip in character.clips)lines.Add("  "+clip.name+": "+clip.bindings+" bindings, "+clip.duration.ToString("F3")+"s, deformation "+clip.maximumVertexDisplacement.ToString("F6")+"m");
            }
            lines.AddRange(result.screenshots);if(result.error.Length>0)lines.Add(result.error);
            File.WriteAllLines(Output+"/result.txt",lines);
        }

        static KryptaAssets LoadStageConfiguration()
        {
            const string scriptPath="Assets/Scripts/KryptaAssets.cs";
            const string assetPath="Assets/Resources/KryptaAssets.asset";
            var diagnostics=new List<string>();
            Action<string> inspect=phase=>
            {
                var script=AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                var scriptClass=script?script.GetClass():null;
                var main=AssetDatabase.LoadMainAssetAtPath(assetPath);
                string scriptGuid=AssetDatabase.AssetPathToGUID(scriptPath);
                diagnostics.Add(phase+": dataPath="+Application.dataPath);
                diagnostics.Add("expectedType="+typeof(KryptaAssets).AssemblyQualifiedName);
                diagnostics.Add("scriptExists="+File.Exists(scriptPath)+"; scriptGuid="+scriptGuid+"; guidPath="+AssetDatabase.GUIDToAssetPath(scriptGuid));
                diagnostics.Add("scriptClass="+(scriptClass==null?"null":scriptClass.AssemblyQualifiedName));
                diagnostics.Add("assetExists="+File.Exists(assetPath)+"; assetGuid="+AssetDatabase.AssetPathToGUID(assetPath));
                diagnostics.Add("mainAsset="+(main?main.name+" / "+main.GetType().AssemblyQualifiedName:"null"));
                diagnostics.Add("allAssets="+string.Join(" | ",AssetDatabase.LoadAllAssetsAtPath(assetPath).Select(a=>a?a.name+" / "+a.GetType().FullName:"null")));
                if(File.Exists(assetPath))diagnostics.AddRange(File.ReadLines(assetPath).Where(line=>line.Contains("m_Script:") || line.Contains("m_EditorClassIdentifier:")));
            };
            inspect("Before force import");
            AssetDatabase.ImportAsset(scriptPath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            inspect("After force import");
            File.WriteAllLines(Output+"/config-diagnostics.txt",diagnostics);
            Debug.Log("KAYKIT_CONFIG_DIAGNOSTICS\n"+string.Join("\n",diagnostics));
            return AssetDatabase.LoadAssetAtPath<KryptaAssets>(assetPath);
        }

        static void EmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.21f,.23f,.29f);
            RenderSettings.fog=false;
            QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.High;
            QualitySettings.shadowDistance=35;
        }

        static void ValidateMaterials(GameObject root,string label)
        {
            var renderers=root.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length>0,label+": no renderers");
            foreach(var renderer in renderers)
                foreach(var material in renderer.sharedMaterials)
                {
                    Require(material && material.shader && material.shader.isSupported,label+": unsupported material/shader");
                    Require(material.shader.name!="Hidden/InternalErrorShader",label+": error shader");
                    if(material.name.IndexOf("Glow",StringComparison.OrdinalIgnoreCase)>=0)continue;
                    var textureSlots=material.GetTexturePropertyNames();
                    bool hasAtlas=textureSlots.Any(slot=>material.GetTexture(slot) && material.GetTexture(slot).width>=512 && material.GetTexture(slot).height>=512);
                    Require(hasAtlas,label+": missing full-resolution atlas on "+material.name);
                }
        }

        static Animation ActorAnimation(ActorVisual actor)
        {
            var animation=actor.Root.GetComponentInChildren<Animation>(true);
            Require(animation,actor.Root.name+": ActorVisual created no Animation component");
            return animation;
        }

        static void Sample(ActorVisual actor,string name,float fraction)
        {
            var animation=ActorAnimation(actor);
            var state=animation[name];Require(state!=null,actor.Root.name+": missing runtime animation "+name);
            // Sample on ActorVisual's actual animation root, not on a convenient
            // alternate root; this also exercises the game's glTF binding paths.
            animation.Stop();state.clip.SampleAnimation(animation.gameObject,state.length*Mathf.Clamp01(fraction));
            Physics.SyncTransforms();
        }

        static Vector3[][] BakedVertices(SkinnedMeshRenderer[] renderers)
        {
            var vertices=new Vector3[renderers.Length][];
            for(int i=0;i<renderers.Length;i++)
            {
                var mesh=new Mesh();
                try
                {
                    renderers[i].BakeMesh(mesh,false);var points=mesh.vertices;
                    for(int v=0;v<points.Length;v++)points[v]=renderers[i].transform.TransformPoint(points[v]);
                    vertices[i]=points;
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
            return vertices;
        }

        static Bounds BakedBounds(Vector3[][] points)
        {
            Require(points.Length>0 && points[0].Length>0,"No baked skin geometry");
            var bounds=new Bounds(points[0][0],Vector3.zero);
            foreach(var part in points)foreach(var point in part)
            {
                Require(!float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z)
                    && !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z),"Invalid skinned vertex");
                bounds.Encapsulate(point);
            }
            return bounds;
        }

        static Vector3[][] WorldSkinVertices(SkinnedMeshRenderer[] skins)
        {
            // A glTF skin's mesh object and skeleton occupy separate hierarchy
            // branches. Compute world skinning directly instead of applying a
            // second actor scale to BakeMesh's already-scaled bone transforms.
            return skins.Select(skin=>
            {
                var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                var poses=mesh.bindposes;var bones=skin.bones;
                var matrices=bones.Select((bone,index)=>bone.localToWorldMatrix*poses[index]).ToArray();
                for(int v=0;v<vertices.Length;v++)
                {
                    var p=vertices[v];var w=weights[v];
                    vertices[v]=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0
                        +matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1
                        +matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2
                        +matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                }
                return vertices;
            }).ToArray();
        }

        static void ValidateCharacter(string pack,string name)
        {
            var character=NocturnisKayKitImport.Character(pack,name);
            var actor=new ActorVisual(character.model,character.animations,Vector3.zero,1.8f,name+" QA");
            try
            {
                ValidateMaterials(actor.Root,name);
                if(name=="Knight" || name=="Skeleton_Warrior")
                {
                    var leftHand=actor.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="handslot.l");
                    var shield=leftHand?leftHand.Find("Badge_Shield"):null;
                    Require(shield && shield.gameObject.activeInHierarchy,name+": native shield missing or hidden");
                    Require(shield.GetComponentsInChildren<Renderer>().Length>0,name+": shield has no visible geometry");
                }
                var animation=ActorAnimation(actor);
                var skins=actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Require(skins.Length>0,name+": no skinned mesh");
                foreach(var skin in skins)
                {
                    Require(skin.sharedMesh && skin.sharedMesh.vertexCount>20,name+": empty skin");
                    Require(skin.bones.Length>0 && skin.bones.All(b=>b),name+": missing skin bones");
                }
                Sample(actor,"Idle",.12f);
                var body=BakedBounds(WorldSkinVertices(skins));
                Require(body.size.y>1.70f && body.size.y<1.90f,name+": unreasonable normalized body height "+body.size.y);
                Require(body.min.y>-.04f && body.min.y<.04f,name+": feet detached from floor: "+body.min.y);
                var report=new CharacterResult{name=name,pack=pack,skinnedMeshes=skins.Length,
                    vertices=skins.Sum(s=>s.sharedMesh.vertexCount),worldHeight=body.size.y,minimumY=body.min.y};
                foreach(string clipName in NocturnisKayKitImport.ClipNames)
                {
                    var clip=character.animations.Single(c=>c.name==clipName);
                    Require(clip.legacy && clip.length>.05f,name+": invalid runtime clip "+clipName);
                    var bindings=AnimationUtility.GetCurveBindings(clip);
                    Require(bindings.Length>0,name+": clip has no curves: "+clipName);
                    foreach(var binding in bindings)
                    {
                        var bound=string.IsNullOrEmpty(binding.path)?animation.transform:animation.transform.Find(binding.path);
                        Require(bound,name+"/"+clipName+": missing bound transform "+binding.path);
                    }
                    Sample(actor,clipName,.10f);var first=BakedVertices(skins);
                    Sample(actor,clipName,.61f);var second=BakedVertices(skins);
                    float largest=0;
                    for(int part=0;part<first.Length;part++)
                    {
                        Require(first[part].Length==second[part].Length,name+": topology changed while animating");
                        for(int v=0;v<first[part].Length;v++)largest=Mathf.Max(largest,Vector3.Distance(first[part][v],second[part][v]));
                    }
                    Require(largest>.00003f,name+"/"+clipName+": frozen skin despite animation curves");
                    Require(largest<8,name+"/"+clipName+": exploding skin, displacement "+largest);
                    var posed=BakedBounds(second);
                    Require(posed.size.magnitude<9 && posed.center.magnitude<5,name+"/"+clipName+": implausible animated geometry bounds");
                    report.clips.Add(new ClipResult{name=clipName,bindings=bindings.Length,duration=clip.length,maximumVertexDisplacement=largest});
                }
                result.characters.Add(report);
            }
            finally{UnityEngine.Object.DestroyImmediate(actor.Root);}
        }

        static void ValidateDungeon()
        {
            string folder=NocturnisKayKitImport.Output+"/Dungeon/Models";
            var paths=Directory.GetFiles(folder,"*.prefab");
            Require(paths.Length==211,"Expected all 211 dungeon model prefabs, found "+paths.Length);
            foreach(string path in paths)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/'));
                Require(prefab,"Missing dungeon prefab "+path);
                ValidateMaterials(prefab,Path.GetFileName(path));
                Bounds bounds;Require(ModelBounds.TryGet(prefab,out bounds) && bounds.size.sqrMagnitude>.00001f,"Empty dungeon model: "+path);
            }
            result.dungeonPrefabs=paths.Length;
        }

        static Material Surface(string name,Color color,float smoothness)
        {
            var shader=Shader.Find("Standard");Require(shader && shader.isSupported,"Studio Standard shader is unavailable");
            var material=new Material(shader){name=name,color=color};material.SetFloat("_Glossiness",smoothness);return material;
        }

        static void Directional(string name,Vector3 rotation,Color color,float intensity,bool shadows)
        {
            var light=new GameObject(name).AddComponent<Light>();light.type=LightType.Directional;
            light.transform.eulerAngles=rotation;light.color=color;light.intensity=intensity;
            light.shadows=shadows?LightShadows.Soft:LightShadows.None;
        }

        static Camera Studio(int count)
        {
            EmptyScene();
            Directional("Large warm studio key",new Vector3(38,-32,0),new Color(1,.87f,.72f),1.55f,true);
            Directional("Cool edge light",new Vector3(25,150,0),new Color(.48f,.65f,1),1.15f,false);
            Directional("Soft front fill",new Vector3(18,5,0),new Color(.78f,.86f,1),.35f,false);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Charcoal studio floor";
            ground.transform.position=Vector3.down*.13f;ground.transform.localScale=Vector3.one*4;
            ground.GetComponent<Renderer>().sharedMaterial=Surface("Charcoal stone",new Color(.055f,.066f,.08f),.22f);
            var platformMaterial=Surface("Cut slate",new Color(.10f,.12f,.15f),.32f);
            var bandMaterial=Surface("Muted brass",new Color(.28f,.22f,.12f),.55f);
            for(int i=0;i<count;i++)
            {
                float x=(i-(count-1)*.5f)*2.35f;
                var band=GameObject.CreatePrimitive(PrimitiveType.Cylinder);band.name="Plinth trim "+i;
                band.transform.position=new Vector3(x,-.075f,0);band.transform.localScale=new Vector3(1.67f,.027f,1.67f);
                band.GetComponent<Renderer>().sharedMaterial=bandMaterial;
                var platform=GameObject.CreatePrimitive(PrimitiveType.Cylinder);platform.name="Slate plinth "+i;
                platform.transform.position=new Vector3(x,-.035f,0);platform.transform.localScale=new Vector3(1.72f,.035f,1.72f);
                platform.GetComponent<Renderer>().sharedMaterial=platformMaterial;
            }
            var camera=CameraAt(new Vector3(.3f,4.05f,10),new Vector3(0,.85f,0),3.35f);
            return camera;
        }

        static Camera CameraAt(Vector3 position,Vector3 target,float size)
        {
            var camera=new GameObject("Asset review camera").AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.025f,.04f);
            camera.transform.position=position;camera.transform.LookAt(target);
            camera.orthographic=true;camera.orthographicSize=size;camera.nearClipPlane=.1f;camera.farClipPlane=75;
            camera.allowHDR=false;camera.allowMSAA=true;return camera;
        }

        static ActorVisual Equipped(KryptaAssets.Character character,Vector3 position,string name,float angle)
        {
            var actor=new ActorVisual(character.model,character.animations,position,1.8f,name);
            actor.Equip(character.weapon);actor.Root.transform.rotation=Quaternion.Euler(0,angle,0);
            var slot=actor.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="handslot.r");
            Require(slot && slot.childCount>0,name+": no equipped weapon in right hand");
            Require(slot.GetComponentsInChildren<Renderer>(true).Any(r=>r.gameObject.activeInHierarchy),name+": equipped weapon is invisible");
            ValidateMaterials(actor.Root,name+" with weapon");return actor;
        }

        static void RenderHeroes(KryptaAssets config,bool combat)
        {
            var camera=Studio(5);
            string[] attacks={"1H_Melee_Attack_Chop","1H_Melee_Attack_Slice_Horizontal","1H_Ranged_Shoot","1H_Ranged_Shoot","2H_Melee_Attack_Spin"};
            for(int i=0;i<5;i++)
            {
                var actor=Equipped(config.heroes[i],new Vector3((i-2)*2.35f,0,0),NocturnisKayKitImport.HeroModels[i],combat?5:12);
                Sample(actor,combat?attacks[i]:"Idle",combat?.43f:.2f+i*.08f);
            }
            Capture(camera,combat?"heroes-combat.png":"heroes.png");
        }

        static void RenderSkeletons(KryptaAssets config)
        {
            var camera=Studio(4);camera.orthographicSize=2.9f;
            for(int i=0;i<4;i++)
            {
                var actor=Equipped(config.skeletonVariants[i],new Vector3((i-1.5f)*2.35f,0,0),NocturnisKayKitImport.SkeletonModels[i],12);
                Sample(actor,"Idle",.2f+i*.12f);
            }
            Capture(camera,"skeletons.png");
        }

        static GameObject Dungeon(string name,Vector3 position,Vector3 scale,float angle=0)
        {
            var source=NocturnisKayKitImport.Model("Dungeon","Models",name);
            var instance=UnityEngine.Object.Instantiate(source);instance.name=name;
            instance.transform.position=position;instance.transform.localScale=scale;
            instance.transform.rotation=Quaternion.Euler(0,angle,0);return instance;
        }

        static void RenderCrypt(KryptaAssets config)
        {
            EmptyScene();RenderSettings.ambientLight=new Color(.12f,.15f,.22f);
            Directional("Cold crypt skylight",new Vector3(45,145,0),new Color(.48f,.65f,1),.9f,true);
            Directional("Warm camera fill",new Vector3(50,-20,0),new Color(1,.75f,.48f),.6f,false);
            for(int x=-3;x<=3;x+=2)for(int z=-2;z<=2;z+=2)
                Dungeon(((x+z)&2)==0?"floor_tile_small_decorated":"floor_tile_small",new Vector3(x,0,z),Vector3.one);
            for(int x=-3;x<=3;x+=2)Dungeon("wall_half",new Vector3(x,0,-3),new Vector3(1,.65f,1));
            for(int z=-2;z<=2;z+=2)Dungeon("wall_half",new Vector3(-4,0,z),new Vector3(1,.65f,1),90);
            Dungeon("pillar_decorated",new Vector3(-3.4f,0,-2.4f),Vector3.one*.65f);
            Dungeon("pillar_decorated",new Vector3(3.4f,0,-2.4f),Vector3.one*.65f);
            Dungeon("chest_gold",new Vector3(2.1f,.05f,-1.8f),Vector3.one*.8f,170);
            Dungeon("barrel_large",new Vector3(-3,.05f,1.5f),Vector3.one*.58f);
            Dungeon("rubble_half",new Vector3(-2.8f,.05f,-.5f),Vector3.one*.65f,20);
            Dungeon("candle_triple",new Vector3(2.6f,.05f,.7f),Vector3.one*.9f);
            Dungeon("banner_patternA_red",new Vector3(0,1.9f,-2.66f),Vector3.one*.7f);
            foreach(float x in new[]{-2.2f,2.2f})
            {
                Dungeon("torch_mounted",new Vector3(x,1.6f,-2.7f),Vector3.one*.75f);
                var light=new GameObject("Torch pool").AddComponent<Light>();light.type=LightType.Point;
                light.transform.position=new Vector3(x,2,-2.25f);light.color=new Color(1,.49f,.15f);light.range=5;light.intensity=2.8f;
            }
            var hero=Equipped(config.heroes[0],new Vector3(-.8f,.08f,1),"Knight in crypt",-140);
            Sample(hero,"1H_Melee_Attack_Chop",.40f);
            var enemy=Equipped(config.skeletonVariants[1],new Vector3(.65f,.08f,-.3f),"Warrior in crypt",35);
            Sample(enemy,"1H_Melee_Attack_Slice_Horizontal",.3f);
            var camera=CameraAt(new Vector3(8,10,12),new Vector3(-.3f,.45f,-.2f),5.2f);
            Capture(camera,"crypt.png");
        }

        static void Capture(Camera camera,string name)
        {
            const int width=1920,height=1080;
            var render=new RenderTexture(width,height,24){antiAliasing=2};render.Create();
            var previous=RenderTexture.active;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=render;camera.Render();RenderTexture.active=render;
                texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                var pixels=texture.GetPixels32();int magenta=0;byte minimum=255,maximum=0;
                for(int i=0;i<pixels.Length;i+=7)
                {
                    var pixel=pixels[i];byte brightness=(byte)((pixel.r+pixel.g+pixel.b)/3);
                    minimum=Math.Min(minimum,brightness);maximum=Math.Max(maximum,brightness);
                    if(pixel.r>210 && pixel.b>210 && pixel.g<35)magenta++;
                }
                Require(maximum-minimum>70,name+": render lacks visible geometry/lighting contrast");
                Require(magenta<100,name+": visible error-shader pixels");
                File.WriteAllBytes(Output+"/"+name,texture.EncodeToPNG());result.screenshots.Add(Output+"/"+name);
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;render.Release();
                UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
