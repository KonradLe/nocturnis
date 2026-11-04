using System;
using UnityEngine;

namespace Krypta {
 public sealed class NocturnisActILibrary : ScriptableObject {
  [Serializable] public sealed class Entry { public string id; public GameObject prefab; public AnimationClip[] clips; }
  public Entry[] entries;
  static NocturnisActILibrary cached;
  public static NocturnisActILibrary Current { get { if(!cached)cached=Resources.Load<NocturnisActILibrary>("ActILibrary");return cached; } }
  public Entry Find(string id) { return Array.Find(entries,e=>e.id==id); }
  public GameObject Model(string id) { var e=Find(id);return e==null?null:e.prefab; }
  public KryptaAssets.Character Character(string id) {
   var e=Find(id);return e==null || e.clips==null || e.clips.Length==0 ? null : new KryptaAssets.Character{model=e.prefab,animations=e.clips};
  }
 }
}
