using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta.Editor
{
    // Isolated import review: never changes the hero config or saves a game scene.
    public static class NocturnisArtStudyQA
    {
        const string ModelPath="Assets/Art/Nocturnis/Inquisitor-v01/inquisitor-v01.glb";
        const string Output="art-direction/inquisitor-v01";
        static void Require(bool condition,string message)
        { if(!condition)throw new InvalidOperationException(message); }

        public static void Run()
        {
            var report=new StringBuilder();
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                Require(prefab,"GLB prefab did not import");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=UnityEngine.Object.Instantiate(prefab);
                root.name="Seweryn_Voss_Study";
                var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>();
                Require(renderers.Length==1,"Expected one skinned character mesh");
                var renderer=renderers[0];var mesh=renderer.sharedMesh;
                Require(mesh && mesh.vertexCount>1000,"Imported geometry is empty");
                Require(renderer.bones.Length==21,"Skeleton lost bones during import");
                Require(mesh.uv.Length==mesh.vertexCount,"Missing texture coordinates");
                Require(mesh.boneWeights.Length==mesh.vertexCount,"Missing skin weights");
                report.AppendLine($"Mesh: {mesh.vertexCount} vertices; {mesh.triangles.Length/3} triangles; {renderer.bones.Length} bones");
                var clips=AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
                string[] expected={"Idle","Running_A","1H_Melee_Attack_Slice_Horizontal"};
                foreach(var name in expected)Require(clips.Any(c=>c.name==name && c.length>0),"Missing clip: "+name);
                foreach(var animator in root.GetComponentsInChildren<Animator>())animator.enabled=false;
                foreach(var clip in clips)
                {
                    clip.SampleAnimation(root,0);var first=new Mesh();renderer.BakeMesh(first);
                    clip.SampleAnimation(root,clip.length*.3f);var next=new Mesh();renderer.BakeMesh(next);
                    var a=first.vertices;var b=next.vertices;float maxChange=0;
                    for(int i=0;i<a.Length;i++)maxChange=Mathf.Max(maxChange,Vector3.Distance(a[i],b[i]));
                    Require(maxChange>.0001f,"Animation did not deform the mesh: "+clip.name);
                    report.AppendLine($"Animation: {clip.name}; {clip.length:F2}s; maximum displacement {maxChange:F4}m");
                    UnityEngine.Object.DestroyImmediate(first);UnityEngine.Object.DestroyImmediate(next);
                }
                clips.Single(c=>c.name=="Idle").SampleAnimation(root,0);
                Require(renderer.sharedMaterials.Length==1,"Expected one atlas material");
                var mat=renderer.sharedMaterial;
                Require(mat && mat.shader && mat.shader.isSupported,"PBR shader unavailable");
                // glTFast on this server creates 4x4 placeholders for embedded PNGs.
                // Use the same exported atlases as explicit Unity texture assets.
                const string folder="Assets/Art/Nocturnis/Inquisitor-v01/";
                var sourceMaterial=mat;
                string materialPath=folder+"Inquisitor-PBR.mat";
                mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!mat){mat=new Material(sourceMaterial);AssetDatabase.CreateAsset(mat,materialPath);}
                else EditorUtility.CopySerialized(sourceMaterial,mat);
                mat.name="Nocturnis Inquisitor PBR";
                ConfigureTexture(mat,"baseColorTexture",folder+"inquisitor_basecolor.png",false,true);
                ConfigureTexture(mat,"metallicRoughnessTexture",folder+"inquisitor_orm.png",false,false);
                ConfigureTexture(mat,"normalTexture",folder+"inquisitor_normal.png",true,false);
                renderer.sharedMaterial=mat;EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();
                report.AppendLine("Embedded image placeholders replaced with explicit Blender atlas assets.");
                var maps=mat.GetTexturePropertyNames().Where(n=>mat.GetTexture(n)).ToArray();
                Require(maps.Length>=3,"Expected base color, normal and metallic/roughness textures");
                report.AppendLine("Material: "+mat.shader.name);
                foreach(var name in maps)
                {
                    var tex=mat.GetTexture(name);
                    Require(tex.width==2048 && tex.height==2048,"Unexpected atlas size: "+name);
                    report.AppendLine($"Texture: {name}; {tex.width}x{tex.height}");
                }
                PrefabUtility.SaveAsPrefabAsset(root,folder+"Inquisitor-Study.prefab");
                var posed=new Mesh();renderer.BakeMesh(posed);
                var points=posed.vertices;
                var bounds=new Bounds(renderer.transform.TransformPoint(points[0]),Vector3.zero);
                foreach(var point in points)bounds.Encapsulate(renderer.transform.TransformPoint(point));
                UnityEngine.Object.DestroyImmediate(posed);
                Require(bounds.size.y>1.8f && bounds.size.y<2.3f,"Unexpected imported scale");
                RenderSettings.ambientMode=AmbientMode.Flat;
                RenderSettings.ambientLight=new Color(.16f,.18f,.22f);
                RenderSettings.fog=false;
                Light("Warm key",new Vector3(35,-35,0),new Color(1,.85f,.7f),1.7f);
                Light("Cool rim",new Vector3(30,145,0),new Color(.6f,.72f,1),1.1f);
                var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.transform.position=new Vector3(0,bounds.min.y-.005f,0);
                ground.transform.localScale=Vector3.one*20;
                var groundMat=new Material(Shader.Find("Standard"));groundMat.color=new Color(.035f,.038f,.043f);
                groundMat.SetFloat("_Glossiness",.12f);ground.GetComponent<Renderer>().sharedMaterial=groundMat;
                var camera=new GameObject("Art review camera").AddComponent<Camera>();
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.03f,.04f);
                camera.transform.position=bounds.center+new Vector3(3.2f,1.6f,6f);
                camera.transform.LookAt(bounds.center);camera.orthographic=true;camera.orthographicSize=1.36f;
                camera.nearClipPlane=.1f;camera.farClipPlane=50;camera.allowHDR=false;
                var target=new RenderTexture(1100,1400,24);target.antiAliasing=4;target.Create();
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                var image=new Texture2D(1100,1400,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1100,1400),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Output,"unity-import.png"),image.EncodeToPNG());
                RenderTexture.active=null;camera.targetTexture=null;target.Release();
                report.Insert(0,"PASS: Blender GLB imported into Unity; geometry, skeleton, textures and three moving clips verified.\n");
                report.AppendLine("This is an art study. Combat animation completeness and visual quality are not certified by this check.");
                File.WriteAllText(Path.Combine(Output,"unity-import-report.txt"),report.ToString());
                Debug.Log(report.ToString());EditorApplication.Exit(0);
            }
            catch(Exception e)
            {
                File.WriteAllText(Path.Combine(Output,"unity-import-report.txt"),"FAIL: "+e+"\n"+report);
                Debug.LogException(e);EditorApplication.Exit(1);
            }
        }

        static void Light(string name,Vector3 angles,Color color,float intensity)
        {
            var light=new GameObject(name).AddComponent<Light>();light.type=LightType.Directional;
            light.transform.eulerAngles=angles;light.color=color;light.intensity=intensity;
            light.shadows=LightShadows.Soft;
        }

        static void ConfigureTexture(Material material,string slot,string path,bool normal,bool srgb)
        {
            Require(material.HasProperty(slot),"Missing PBR slot: "+slot);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer,"Missing external atlas: "+path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=srgb;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=true;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(texture && texture.width==2048 && texture.height==2048,"Invalid atlas: "+path);
            material.SetTexture(slot,texture);
        }
    }
}
