using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta.Editor
{
 public static class NocturnisForestImport
 {
  public const string Output="Assets/Generated/Forest";
  const string Sources="Assets/Art/Rakshi";
  public static void Generate()
  {
   Directory.CreateDirectory(Output);AssetDatabase.Refresh();
   var paths=Directory.GetFiles(Sources,"*.prefab",SearchOption.AllDirectories)
    .Where(p=>!p.EndsWith("Tree2_branch.prefab")).OrderBy(p=>p).ToArray();
   if(paths.Length!=10)throw new Exception("Expected ten Rakshi source trees");
   foreach(string raw in paths)
   {
    string path=raw.Replace('\\','/'),name=Path.GetFileNameWithoutExtension(path);
    var objects=AssetDatabase.LoadAllAssetsAtPath(path);
    var original=objects.OfType<Mesh>().Single();
    Mesh copy=UnityEngine.Object.Instantiate(original);copy.name=name;
    // Root at trunk base. Preserve source proportions, UVs, normals and submeshes.
    Bounds b=copy.bounds;Vector3 origin=new Vector3(b.center.x,b.min.y,b.center.z);
    float scale=6f/Mathf.Max(.1f,b.size.y);
    copy.vertices=copy.vertices.Select(v=>(v-origin)*scale).ToArray();copy.RecalculateBounds();
    string meshPath=Output+"/"+name+".asset";
    Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
    if(mesh){EditorUtility.CopySerialized(copy,mesh);UnityEngine.Object.DestroyImmediate(copy);}
    else {mesh=copy;AssetDatabase.CreateAsset(mesh,meshPath);}
    var materials=new Material[mesh.subMeshCount];
    for(int i=0;i<materials.Length;i++)
    {
     bool leaves=i>0;
     var source=objects.OfType<Material>().Single(m=>m.name==(leaves ? "Optimized Leaf Material" : "Optimized Bark Material"));
     var texture=source.GetTexture("_MainTex") as Texture2D;
     if(!texture)throw new Exception("Missing source atlas: "+path);
     var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
     importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
     importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
     importer.mipmapEnabled=true;importer.mipMapsPreserveCoverage=true;importer.alphaTestReferenceValue=.38f;
     importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
     string matPath=Output+"/"+name+(leaves ? "-leaves.mat" : "-bark.mat");
     var shader=Shader.Find("Nocturnis/ForestSurface");if(!shader)throw new Exception("Forest shader missing");
     var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
     if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}else mat.shader=shader;
     mat.SetTexture("_MainTex",texture);mat.SetColor("_Color",leaves ? new Color(.68f,.70f,.61f,1) : new Color(.68f,.65f,.59f,1));
     mat.SetFloat("_Cutoff",leaves ? .38f : .01f);mat.SetFloat("_Wind",leaves ? .045f : 0);
     mat.SetFloat("_Glossiness",.08f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);materials[i]=mat;
    }
    var root=new GameObject(name);
    root.AddComponent<MeshFilter>().sharedMesh=mesh;
    var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;
    renderer.shadowCastingMode=ShadowCastingMode.TwoSided;renderer.receiveShadows=true;
    // Navigation already blocks the verges; no legacy Tree component or script survives.
    root.AddComponent<NocturnisForestTree>();
    PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);
    Debug.Log("FOREST_CONVERTED "+name+" triangles="+mesh.triangles.Length/3);
   }
   string earthPath=Output+"/ForestEarth.mat";
   var earth=AssetDatabase.LoadAssetAtPath<Material>(earthPath);
   if(!earth){earth=new Material(Shader.Find("Nocturnis/ForestGround"));AssetDatabase.CreateAsset(earth,earthPath);}
   EditorUtility.SetDirty(earth);AssetDatabase.SaveAssets();
   Debug.Log("FOREST_IMPORT_PASS: 10 independent runtime prefabs");
  }
  public static GameObject[] LoadTrees()
  {
   if(!Directory.Exists(Output))throw new Exception("Run NocturnisForestImport.Generate first");
   var prefabs=Directory.GetFiles(Output,"*.prefab").OrderBy(p=>p).Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\','/'))).ToArray();
   if(prefabs.Length!=10 || prefabs.Any(p=>!p))throw new Exception("Incomplete forest prefabs");
   return prefabs;
  }
 }
}
