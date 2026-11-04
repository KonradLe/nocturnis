using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta.Editor
{
    public static class NocturnisKnightImport
    {
        public const string Folder="Assets/Art/Nocturnis/Inquisitor-Xali";
        const string Model=Folder+"/Inquisitor.fbx";
        const string Prefab=Folder+"/Inquisitor.prefab";
        static readonly string[] Names={"Idle","Running_A","1H_Melee_Attack_Chop","1H_Melee_Attack_Slice_Horizontal","2H_Melee_Attack_Spin","Dodge_Forward","Death_A","1H_Ranged_Shoot"};
        [Serializable] sealed class MaterialList { public SourceMaterial[] materials; }
        [Serializable] sealed class SourceMaterial {public string name;public float[] color;public float metallic,roughness;public string @base,rough;}
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}

        public static KryptaAssets.Character Load()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            Require(model,"Prepare the Xali knight before configuring the game");
            return new KryptaAssets.Character{model=model,weapon=null,animations=Names.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/"+n+".anim")).ToArray()};
        }

        public static void Prepare()
        {
            var importer=AssetImporter.GetAtPath(Model) as ModelImporter;
            Require(importer,"Xali FBX missing");
            importer.animationType=ModelImporterAnimationType.Legacy;
            importer.importAnimation=true;importer.isReadable=true;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var root=UnityEngine.Object.Instantiate(source);
            root.name="Nocturnis_Inquisitor_Xali";
            var originals=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
            Debug.Log("Xali imported clips: "+string.Join(", ",originals.Select(c=>c.name)));
            foreach(string name in Names)
            {
                var original=originals.SingleOrDefault(c=>c.name==name || c.name.EndsWith("|"+name));
                Require(original,"Xali clip missing: "+name);
                string path=Folder+"/"+name+".anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(!clip){clip=UnityEngine.Object.Instantiate(original);AssetDatabase.CreateAsset(clip,path);}
                else EditorUtility.CopySerialized(original,clip);
                clip.name=name;clip.legacy=true;clip.wrapMode=(name=="Idle" || name=="Running_A")?WrapMode.Loop:WrapMode.ClampForever;
                EditorUtility.SetDirty(clip);
            }
            var description=JsonUtility.FromJson<MaterialList>(File.ReadAllText(Folder+"/materials.json"));
            var materials=new Dictionary<string,Material>();int materialIndex=0;
            foreach(var sourceMat in description.materials.OrderBy(m=>m.name,StringComparer.Ordinal))
            {
                string path=Folder+"/Material_"+(materialIndex++)+"_"+sourceMat.name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
                mat.shader=Shader.Find(sourceMat.metallic>.5f ? "Nocturnis/WeatheredArmor" : "Standard");
                mat.name=sourceMat.name;
                mat.color=new Color(sourceMat.color[0],sourceMat.color[1],sourceMat.color[2],sourceMat.color[3]);
                mat.SetFloat("_Metallic",sourceMat.metallic);mat.SetFloat("_Glossiness",1-sourceMat.roughness);
                if(!string.IsNullOrEmpty(sourceMat.@base))mat.mainTexture=Texture(Folder+"/"+sourceMat.@base,true);
                if(!string.IsNullOrEmpty(sourceMat.rough))
                {
                    var rough=Texture(Folder+"/"+sourceMat.rough,false);var pixels=rough.GetPixels();
                    for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(sourceMat.metallic,0,0,1-Mathf.Max(.42f,pixels[i].r));
                    var packed=new Texture2D(rough.width,rough.height,TextureFormat.RGBA32,false,true);packed.SetPixels(pixels);packed.Apply();
                    string packedPath=Folder+"/PBR_"+sourceMat.name+".png";File.WriteAllBytes(packedPath,packed.EncodeToPNG());UnityEngine.Object.DestroyImmediate(packed);
                    AssetDatabase.ImportAsset(packedPath,ImportAssetOptions.ForceSynchronousImport);
                    mat.SetTexture("_MetallicGlossMap",Texture(packedPath,false));mat.EnableKeyword("_METALLICGLOSSMAP");
                }
                EditorUtility.SetDirty(mat);materials.Add(sourceMat.name,mat);
            }
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.TryGetValue(m.name,out var replacement)?replacement:throw new InvalidOperationException("Unmapped Xali material: "+m.name)).ToArray();
                renderer.updateWhenOffscreen=true;
                renderer.quality=SkinQuality.Bone4;
            }
            var profile=root.AddComponent<ActorModelProfile>();profile.bodyHeight=1.8f;profile.groundOffset=0;
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        }

        static Texture2D Texture(string path,bool srgb)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;Require(importer,"Missing texture: "+path);
            importer.sRGBTexture=srgb;importer.isReadable=true;importer.mipmapEnabled=true;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);Require(texture && texture.width>32,"Invalid texture: "+path);return texture;
        }

        public static void Run()
        {
            var report=new StringBuilder();
            try
            {
                Prepare();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var character=Load();var root=UnityEngine.Object.Instantiate(character.model);
                foreach(var a in root.GetComponentsInChildren<Animation>())a.enabled=false;
                var renderer=root.GetComponentInChildren<SkinnedMeshRenderer>();Require(renderer,"Missing skinned mesh");
                Require(renderer.sharedMesh.vertexCount>20000,"Source geometry missing");
                report.AppendLine($"Mesh: {renderer.sharedMesh.vertexCount} vertices; {renderer.sharedMesh.triangles.Length/3} triangles; {renderer.bones.Length} bones");
                foreach(var clip in character.animations)
                {
                    Require(clip && clip.length>.1f,"Missing or empty animation");
                    clip.SampleAnimation(root,0);var first=new Mesh();renderer.BakeMesh(first,false);
                    clip.SampleAnimation(root,clip.length*.4f);var next=new Mesh();renderer.BakeMesh(next,false);
                    float change=0;var a=first.vertices;var b=next.vertices;
                    for(int i=0;i<a.Length;i++)change=Mathf.Max(change,Vector3.Distance(a[i],b[i]));
                    Require(change>.0001f,"Frozen animation: "+clip.name);
                    report.AppendLine($"{clip.name}: {clip.length:F3}s; deformation {change:F4}");
                    UnityEngine.Object.DestroyImmediate(first);UnityEngine.Object.DestroyImmediate(next);
                }
                foreach(var mat in renderer.sharedMaterials)Require(mat && mat.shader.isSupported,"Invalid shader");
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.28f,.31f,.36f);RenderSettings.fog=false;
                Light("Key",new Vector3(40,-30,0),new Color(1,.87f,.74f),1.5f);
                Light("Rim",new Vector3(25,150,0),new Color(.65f,.8f,1),1.1f);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*10;
                var material=new Material(Shader.Find("Standard"));material.color=new Color(.035f,.04f,.05f);floor.GetComponent<Renderer>().sharedMaterial=material;
                var camera=new GameObject("Knight review").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.035f,.045f);
                camera.transform.position=new Vector3(3,2.4f,6);camera.transform.LookAt(new Vector3(0,1,0));camera.orthographic=true;camera.orthographicSize=1.65f;
                foreach(string name in new[]{"Idle","Running_A","1H_Melee_Attack_Chop"})
                {
                    var clip=character.animations.Single(c=>c.name==name);clip.SampleAnimation(root,clip.length*.4f);
                    var head=renderer.bones.Single(b=>b.name=="head");
                    var feet=renderer.bones.Where(b=>b.name.EndsWith(".foot1")).ToArray();
                    Require(head.position.y>1 && head.position.y<2.3f,"Invalid skeleton scale: "+head.position);
                    Require(feet.Length==2 && feet.All(f=>f.position.y>-.1f && f.position.y<1),"Foot placement outside expected range");
                    report.AppendLine(name+" head height: "+head.position.y.ToString("F3")+"; feet: "+string.Join(",",feet.Select(f=>f.position.y.ToString("F3"))));
                    var target=new RenderTexture(800,1000,24);target.antiAliasing=4;target.Create();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                    var image=new Texture2D(800,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,1000),0,0);image.Apply();
                    File.WriteAllBytes("art-direction/realism-source-review/xali-unity-"+name+".png",image.EncodeToPNG());
                    camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
                }
                File.WriteAllText("Logs/xali-unity-result.txt","PASS\n"+report);Debug.Log(report.ToString());EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText("Logs/xali-unity-result.txt","FAIL\n"+e+"\n"+report);Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Light(string name,Vector3 angles,Color color,float intensity)
        {var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Directional;l.transform.eulerAngles=angles;l.color=color;l.intensity=intensity;l.shadows=LightShadows.Soft;}
    }
}
