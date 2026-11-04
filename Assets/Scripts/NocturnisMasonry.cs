using UnityEngine;
namespace Krypta {
 public static class NocturnisMasonry {
  public const string ShaderName="Nocturnis/WeatheredMasonry";
  public static string Profile(int zone) {
   if(zone==Campaign.Kings || zone==Campaign.Heart || zone==Campaign.Threshold || zone==Campaign.Forge || zone==Campaign.Chapel)return "slate_floor_02";
   if(zone==Campaign.Cellar || zone==Campaign.Cistern || zone==Campaign.Ossuary)return "stone_floor";
   return "monastery_stone_floor";
  }
  public static Material Create(int zone) {
   var source=Resources.Load<Material>("Masonry/"+Profile(zone)+"/Floor");
   if(!source)return null;
   var material=new Material(source){name="Weathered masonry / "+Profile(zone)+" / "+zone};
   Color tint=zone==Campaign.Forge?new Color(.95f,.84f,.75f):
    zone==Campaign.Cistern?new Color(.76f,.90f,.88f):
    zone==Campaign.Heart||zone==Campaign.Threshold?new Color(.84f,.86f,.98f):
    zone==Campaign.Kings||zone==Campaign.Chapel?new Color(.98f,.95f,.85f):
    zone==Campaign.Cellar?new Color(.95f,.89f,.78f):new Color(.87f,.92f,.96f);
   material.SetColor("_Color",tint);
   return material;
  }

  public static float TopHeight(GameObject prefab) {
   float top=float.NegativeInfinity;
   foreach(var f in prefab.GetComponentsInChildren<MeshFilter>(true))
    foreach(var vertex in f.sharedMesh.vertices)top=Mathf.Max(top,f.transform.localToWorldMatrix.MultiplyPoint3x4(vertex).y);
   if(float.IsNaN(top)||float.IsInfinity(top))throw new System.InvalidOperationException("Missing floor mesh");
   return top;
  }

  public static Matrix4x4 ExposeMetalDetail(Mesh mesh,int subMesh,Matrix4x4 matrix,float floorY) {
   // Grate modules have a lower authored top than the bevel crest of plain
   // slabs. Keep their tops just above the replacement stone, not buried in it.
   var vertices=mesh.vertices;float top=float.NegativeInfinity;
   foreach(int index in mesh.GetTriangles(subMesh))top=Mathf.Max(top,matrix.MultiplyPoint3x4(vertices[index]).y);
   if(float.IsInfinity(top))return matrix;
   return Matrix4x4.Translate(Vector3.up*Mathf.Max(0,floorY+.012f-top))*matrix;
  }
  public static bool IsMasonry(Material material) {return material&&material.shader&&material.shader.name==ShaderName;}
  public static void MapMesh(Mesh mesh,Material material) {
   if(!IsMasonry(material))return;
   // Combined mesh vertices are in the world's horizontal frame. Re-project
   // once so the pattern crosses every tile boundary without restarting.
   var vertices=mesh.vertices;var uv=new Vector2[vertices.Length];
   float metres=Mathf.Max(.5f,material.GetFloat("_TileMeters"));
   for(int i=0;i<vertices.Length;i++)uv[i]=new Vector2(vertices[i].x,vertices[i].z)/metres;
   mesh.uv=uv;mesh.RecalculateTangents();
   // Positions, triangles, normals, collision and navigation are untouched.
  }
 }
}
