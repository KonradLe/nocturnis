using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  void ForestTree(int x,int z,bool distant=false)
  {
   // Integer hashing keeps the landscape stable across saves and zone visits.
   uint seed=unchecked((uint)(x*73856093 ^ z*19349663 ^ Zone*83492791));
   int index=(int)(seed%(uint)art.trees.Length);
   var prefab=art.trees[index];var tree=Instantiate(prefab,world);
   float jitterX=((seed>>8)%100)/100f-.5f,jitterZ=((seed>>16)%100)/100f-.5f;
   tree.transform.position=World(x,z)+new Vector3(jitterX*.65f,0,jitterZ*.65f);
   tree.transform.rotation=Quaternion.Euler(0,seed%360,0);
   float size=.7f+(seed%43)/100f;tree.transform.localScale=Vector3.one*(distant ? size*1.2f : size);
   if(NocturnisActILibrary.Current)
   {
    Bounds b;if(ModelBounds.TryGet(tree,out b))
    {
     float sizeLimit=distant?4.8f:4.3f;float canopyLimit=distant?3.5f:2.8f;
     tree.transform.localScale*=Mathf.Min(1,sizeLimit/Mathf.Max(.1f,b.size.y),canopyLimit/Mathf.Max(.1f,Mathf.Max(b.size.x,b.size.z)));
    }
   }
   PolishForestColors(tree);
   if(!tree.GetComponent<NocturnisForestTree>())tree.AddComponent<NocturnisForestTree>();
   tree.name="Drzewo • "+prefab.name;
  }
  void BuildForestDepth()
  {
   // A second row beyond the playable verge makes the clearing part of a forest.
   int placed=0;
   for(int x=-3;x<DungeonGrid.Width+3;x++)for(int z=-3;z<DungeonGrid.Height+3;z++)
   {
    if(placed>=56 || grid.Walkable(x,z) || grid.Border(x,z) || (x*3+z*7)%3!=0)continue;
    bool near=false;
    for(int dx=-3;dx<=3 && !near;dx++)for(int dz=-3;dz<=3;dz++)
     if(grid.Walkable(x+dx,z+dz)){near=true;break;}
    if(near){ForestTree(x,z,true);placed++;}
   }
  }
 }
}
