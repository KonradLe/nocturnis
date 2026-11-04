using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
namespace Krypta.Editor {
 public static class NocturnisMotionGeometryDump {
  [Serializable] class Record { public string id;public Vector3[] vertices;public Bounds bounds;public Vector3[] sourceGrip; }
  public static void Run(){
   var folder="Logs/MotionGeometry";Directory.CreateDirectory(folder);
   foreach(var id in new[]{"hero_inquisitor","hero_blood_weaver","hero_dusk_hunter","hero_void_herald","hero_root_guardian","weapon_sword","weapon_scythe","weapon_bow","weapon_staff","weapon_axe"}){
    var original=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/NocturnisActI/Prefabs/"+id+".prefab"));
    var filters=original.GetComponentsInChildren<MeshFilter>(true);var filter=filters.FirstOrDefault(f=>f.name.EndsWith("_LOD1"))??filters[0];
    var points=filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();var bounds=new Bounds(points[0],Vector3.zero);foreach(var v in points)bounds.Encapsulate(v);
    var r=new Record{id=id,vertices=points,bounds=bounds};
    File.WriteAllText(folder+"/"+id+".json",JsonUtility.ToJson(r));UnityEngine.Object.DestroyImmediate(original);
   }
   Debug.Log("MOTION_GEOMETRY_DUMP_COMPLETE");
  }
 }
}
