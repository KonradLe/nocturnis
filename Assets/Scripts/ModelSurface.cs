using UnityEngine;
namespace Krypta
{
 public static class ModelSurface
 {
  // Explicit texture assets survive headless glTF import; source shaders retain glTF UV conventions.
  public static void Apply(GameObject obj)
  {
   var assets=Resources.Load<KryptaAssets>("KryptaAssets");if(!assets || assets.surfaces==null)return;
   foreach(var r in obj.GetComponentsInChildren<Renderer>(true))
   {
    var mats=r.sharedMaterials;bool changed=false;
    for(int i=0;i<mats.Length;i++)if(mats[i])foreach(var s in assets.surfaces)
     if(mats[i].name==s.source || mats[i].name==s.source+" (Instance)"){mats[i]=s.material;changed=true;break;}
    if(changed)r.sharedMaterials=mats;
   }
  }
 }
}
