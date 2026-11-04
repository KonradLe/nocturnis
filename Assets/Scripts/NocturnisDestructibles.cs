using System;
using System.Collections.Generic;
using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  sealed class Breakable
  {
   public string id,name; public int x,z,kind,roll; public float hp; public Vector3 p; public GameObject root,halo; public bool broken,cracked;
  }
  sealed class ContainerLoot {public Breakable owner;public GameObject root;public Vector3 position;public int kind,amount;}
  readonly List<Breakable> containers=new List<Breakable>();
  readonly List<ContainerLoot> containerLoot=new List<ContainerLoot>();
  readonly HashSet<string> brokenContainerIds=new HashSet<string>();
  readonly List<Mesh> containerMeshes=new List<Mesh>();
  Material wood,iron,clay,crackMaterial;Texture2D woodTexture;Mesh staveMesh,hoopMesh,urnMesh,vesselMesh;
  Breakable propTarget;
  public int ContainersStanding {get{int n=0;foreach(var b in containers)if(!b.broken)n++;return n;}}
  public int ContainerLootOnGround {get{return containerLoot.Count;}}
  public string[] CaptureBrokenContainers(){var ids=new string[brokenContainerIds.Count];brokenContainerIds.CopyTo(ids);Array.Sort(ids,StringComparer.Ordinal);return ids;}
  public void ResetBreakablesProgress(){brokenContainerIds.Clear();}
  public void RestoreBrokenContainers(string[] saved)
  {
   brokenContainerIds.Clear();if(saved==null)return;
   foreach(string id in saved)
   {
    if(string.IsNullOrEmpty(id)||id.Length>24)continue;
    string value=id.StartsWith("L:",StringComparison.Ordinal)?id.Substring(2):id;var bits=value.Split(':');int zone,index;
    if(bits.Length==2 && int.TryParse(bits[0],out zone) && int.TryParse(bits[1],out index) && zone>=0 && zone<Campaign.Count && index>=0 && index<40)brokenContainerIds.Add(id);
   }
  }
  static uint PropHash(int zone,int x,int z){unchecked{uint h=(uint)(zone*73856093 ^ x*19349663 ^ z*83492791);h^=h>>13;h*=1274126177;return h^(h>>16);}}
  void EnsureContainerMaterials()
  {
   if(wood)return;
   var shader=Shader.Find("Standard");if(!shader)shader=art.wallMaterial.shader;
   wood=new Material(shader){name="Stare debowe klepki",color=new Color(.43f,.29f,.16f)};
   iron=new Material(shader){name="Patynowane zelazo",color=new Color(.20f,.22f,.21f)};
   clay=new Material(shader){name="Pekajaca wypalona glina",color=new Color(.39f,.24f,.18f)};
   crackMaterial=new Material(art.effectMaterial){name="Ciemne pekniecia",color=new Color(.055f,.03f,.02f)};
   wood.SetFloat("_Glossiness",.12f);iron.SetFloat("_Metallic",.72f);iron.SetFloat("_Glossiness",.28f);clay.SetFloat("_Glossiness",.08f);
   woodTexture=new Texture2D(128,256,TextureFormat.RGB24,false){name="Uslojenie debu",wrapMode=TextureWrapMode.Repeat};
   for(int y=0;y<256;y++)for(int x=0;x<128;x++)
   {float n=Mathf.PerlinNoise(x*.2f,y*.014f),vein=Mathf.Pow(Mathf.Abs(Mathf.Sin(x*.48f+n*3)),14);float c=.63f+n*.32f-vein*.2f;woodTexture.SetPixel(x,y,new Color(c,c*.95f,c*.85f));}
   woodTexture.Apply(false,true);wood.mainTexture=woodTexture;
   staveMesh=PropWedge(new[]{0f,.18f,.65f,1.04f,1.2f},new[]{.34f,.43f,.48f,.42f,.34f},27.8f);
   hoopMesh=PropWedge(new[]{-.045f,.045f},new[]{.465f,.465f},360f);
   urnMesh=PropWedge(new[]{0f,.12f,.42f,.66f,.88f,1.03f,1.08f},new[]{.22f,.29f,.43f,.44f,.28f,.19f,.24f},29.5f);
   vesselMesh=PropWedge(new[]{0f,.04f,.68f,.74f},new[]{.27f,.29f,.32f,.29f},59.5f);
  }
  Mesh PropWedge(float[] ys,float[] radii,float angle)
  {
   int segments=angle>300?36:Mathf.Max(2,Mathf.CeilToInt(angle/8));var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
   for(int surface=0;surface<2;surface++)for(int row=0;row<ys.Length;row++)for(int i=0;i<=segments;i++)
   {float a=(i/(float)segments-.5f)*angle*Mathf.Deg2Rad;float radius=Mathf.Max(.03f,radii[row]-(surface==1?.035f:0));v.Add(new Vector3(Mathf.Sin(a)*radius,ys[row],Mathf.Cos(a)*radius));uv.Add(new Vector2(i/(float)segments,ys[row]));}
   int stride=segments+1,face=ys.Length*stride;
   for(int surface=0;surface<2;surface++)for(int row=0;row<ys.Length-1;row++)for(int i=0;i<segments;i++)
   {int a=surface*face+row*stride+i,b=a+1,c=a+stride,d=c+1;if(surface==0){tri.AddRange(new[]{a,c,b,b,c,d});}else{tri.AddRange(new[]{a,b,c,b,d,c});}}
   for(int row=0;row<ys.Length-1;row++)foreach(int edge in new[]{0,segments})
   {int a=row*stride+edge,b=a+stride,c=a+face,d=b+face;tri.AddRange(edge==0?new[]{a,c,b,b,c,d}:new[]{a,b,c,b,d,c});}
   for(int i=0;i<segments;i++)foreach(int row in new[]{0,ys.Length-1})
   {int a=row*stride+i,b=a+1,c=a+face,d=b+face;tri.AddRange(row==0?new[]{a,b,c,b,d,c}:new[]{a,c,b,b,c,d});}
   var mesh=new Mesh{name="Rekwizyt rozdzielony na fragmenty"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();containerMeshes.Add(mesh);return mesh;
  }
  GameObject PropPiece(Transform parent,Mesh mesh,Material mat,string name,Vector3 pos,float yaw=0)
  {
   var piece=new GameObject(name);piece.transform.SetParent(parent,false);piece.transform.localPosition=pos;piece.transform.localRotation=Quaternion.Euler(0,yaw,0);piece.AddComponent<MeshFilter>().sharedMesh=mesh;piece.AddComponent<MeshRenderer>().sharedMaterial=mat;return piece;
  }
  bool PropReserved(int x,int z)
  {
   Vector3 p=World(x,z);
   if(Vector3.Distance(p,SpawnPosition)<5 || Vector3.Distance(p,ExitPosition)<5 || QuestPropNearby(p,3.2f))return true;
   foreach(var c in citizens)if(Vector3.Distance(c.p,p)<3.2f)return true;
   foreach(var s in shrines)if(Vector3.Distance(s.pos,p)<2.7f)return true;
   foreach(var e in enemies)if(Vector3.Distance(e.Position,p)<1.7f)return true;
   return false;
  }
  bool TryBlockPropCell(int x,int z)
  {
   grid.CloseCell(x,z);grid.BuildDistances(Cell(SpawnPosition.x),Cell(SpawnPosition.z));
   for(int a=0;a<DungeonGrid.Width;a++)for(int b=0;b<DungeonGrid.Height;b++)if(grid.Walkable(a,b) && grid.Distance(a,b)<0){grid.OpenCell(x,z);return false;}
   playerRoute.CloseCell(x,z);return true;
  }
  void BuildBreakables()
  {
   containers.Clear();containerLoot.Clear();propTarget=null;if(Zone==Campaign.Town || Zone==Campaign.Heart)return;
   EnsureContainerMaterials();var candidates=new List<Vector2Int>();
   for(int z=3;z<DungeonGrid.Height-2;z++)for(int x=2;x<DungeonGrid.Width-2;x++)
   {
    if(!grid.Walkable(x,z)||PropReserved(x,z))continue;
    int n=0;foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})if(grid.Walkable(x+d.x,z+d.y))n++;
    if(n>=3 && (n==3 || PropHash(Zone,x,z)%5==0))candidates.Add(new Vector2Int(x,z));
   }
   candidates.Sort((a,b)=>PropHash(Zone,a.x,a.y).CompareTo(PropHash(Zone,b.x,b.y)));
   foreach(var c in candidates)
   {
    if(containers.Count>=22)break;
    bool close=false;foreach(var other in containers)if(Vector3.Distance(other.p,World(c.x,c.y))<3.2f){close=true;break;}if(close)continue;
    if(!TryBlockPropCell(c.x,c.y))continue;
    int index=containers.Count,kind=(int)(PropHash(Zone,c.x,c.y)%4);
    var b=new Breakable{id=Zone+":"+index,x=c.x,z=c.y,p=World(c.x,c.y),kind=kind,roll=(int)((PropHash(Zone+17,c.x,c.y))%100),hp=kind==2?34:kind==0?26:18,name=new[]{"Beczka","Urna pogrzebowa","Skrzynia zapasów","Żelazna puszka"}[kind]};
    containers.Add(b);
    if(brokenContainerIds.Contains(b.id))
    {b.broken=true;MakeContainerRubble(b);SpawnContainerLoot(b);continue;}
    b.root=new GameObject(b.name+" [LPM]");b.root.transform.SetParent(world);b.root.transform.position=b.p;b.root.transform.rotation=Quaternion.Euler(0,PropHash(Zone,c.x,c.y)%360,0);
    if(kind==0)
    {
     for(int i=0;i<12;i++)PropPiece(b.root.transform,staveMesh,wood,"Dębowa klepka",Vector3.zero,i*30);
     foreach(float y in new[]{.24f,.95f})PropPiece(b.root.transform,hoopMesh,iron,"Żelazna obręcz",Vector3.up*y);
     for(int i=-2;i<=2;i++){var slat=CubeOn(b.root.transform,"Wieko",new Vector3(i*.12f,1.19f,0),new Vector3(.11f,.055f,.57f-Mathf.Abs(i)*.05f),wood);}
    }
    else if(kind==1){for(int i=0;i<12;i++)PropPiece(b.root.transform,urnMesh,clay,"Ceramiczny odłamek",Vector3.zero,i*30);}
    else if(kind==2)
    {
     for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)
     {CubeOn(b.root.transform,"Deska skrzyni",new Vector3(side*.43f,.16f+i*.23f,0),new Vector3(.09f,.21f,.9f),wood);CubeOn(b.root.transform,"Deska skrzyni",new Vector3(0,.16f+i*.23f,side*.43f),new Vector3(.78f,.21f,.09f),wood);}
     for(int i=0;i<5;i++)CubeOn(b.root.transform,"Wieko skrzyni",new Vector3((i-2)*.17f,.99f,0),new Vector3(.16f,.07f,.92f),wood);
     foreach(int side in new[]{-1,1}){var brace=CubeOn(b.root.transform,"Ukośne wzmocnienie",new Vector3(0,.52f,side*.49f),new Vector3(.12f,1.15f,.06f),iron);brace.transform.localRotation=Quaternion.Euler(0,0,side*37);}
    }
    else
    {for(int i=0;i<6;i++)PropPiece(b.root.transform,vesselMesh,iron,"Blacha naczynia",Vector3.zero,i*60);var handle=PropPiece(b.root.transform,hoopMesh,iron,"Ucho naczynia",Vector3.up*.75f);handle.transform.localScale=new Vector3(.58f,.8f,.58f);}
    b.halo=Ring(b.root.transform,.64f,.025f,art.goldMaterial);b.halo.SetActive(false);
   }
   foreach(var b in containers)if(b.broken){grid.OpenCell(b.x,b.z);playerRoute.OpenCell(b.x,b.z);}
   grid.BuildDistances(Cell(SpawnPosition.x),Cell(SpawnPosition.z));
  }
  void MakeContainerRubble(Breakable b)
  {
   var root=new GameObject("Pozostałości: "+b.name);root.transform.SetParent(world);root.transform.position=b.p;
   for(int i=0;i<4;i++){float a=i*2.1f+(b.roll*.1f);var piece=CubeOn(root.transform,"Odłamek",new Vector3(Mathf.Cos(a)*.4f,.035f,Mathf.Sin(a)*.4f),new Vector3(.24f,.05f,.09f),b.kind==0||b.kind==2?wood:b.kind==1?clay:iron);piece.transform.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);}
  }
  Breakable FindBreakable(Vector3 aim)
  {Breakable nearest=null;float d=.85f;foreach(var b in containers)if(!b.broken){float r=Vector3.Distance(b.p,aim);if(r<d){d=r;nearest=b;}}return nearest;}
  bool PropVisible(Vector3 from,Breakable b)
  {Vector3 direction=b.p-from;if(direction.magnitude<1.45f)return true;return Visible(from,b.p-direction.normalized*1.35f);}
  Vector3 ApproachProp(Breakable b)
  {
   Vector3 best=HeroPosition;float cost=float.MaxValue;
   foreach(var offset in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
   {int x=b.x+offset.x,z=b.z+offset.y;if(!grid.Walkable(x,z))continue;playerRoute.BuildDistances(x,z);int d=playerRoute.Distance(Cell(HeroPosition.x),Cell(HeroPosition.z));if(d>=0 && d<cost){cost=d;best=World(x,z);}}
   playerRoute.BuildDistances(Cell(best.x),Cell(best.z));return RouteDirection(playerRoute,HeroPosition,best);
  }
  void StrikeBreakables(Vector3 origin,Vector3 direction,float range,float damage)
  {
   direction.y=0;if(direction.sqrMagnitude<.01f)return;direction.Normalize();Breakable closest=null;float distance=float.MaxValue;
   foreach(var b in containers)
   {
    Vector3 delta=b.p-origin;if(b.broken||delta.magnitude>range+.45f||!PropVisible(origin,b))continue;
    if(Vector3.Dot(delta.normalized,direction)<(range>3?.92f:.2f))continue;
    if(range>3){if(delta.magnitude<distance){closest=b;distance=delta.magnitude;}}
    else DamageContainer(b,damage);
   }
   if(closest!=null)DamageContainer(closest,damage);
  }
  void DamageBreakables(Vector3 center,float radius,float damage)
  {foreach(var b in containers)if(!b.broken && Vector3.Distance(center,b.p)<radius+.35f && PropVisible(center,b))DamageContainer(b,damage);}
  void DamageContainer(Breakable b,float amount)
  {
   if(b.broken||amount<=0)return;b.hp-=amount;
   Sparks(b.p,b.kind==1?new Color(.55f,.35f,.23f):new Color(.75f,.58f,.3f),5);Sound("hit");
   if(b.hp>0)
   {
    if(!b.cracked){b.cracked=true;for(int i=0;i<3;i++){var crack=new GameObject("Pęknięcie");crack.transform.SetParent(b.root.transform,false);var l=crack.AddComponent<LineRenderer>();l.useWorldSpace=false;l.sharedMaterial=art.effectMaterial;l.startColor=l.endColor=crackMaterial.color;l.widthMultiplier=.026f;l.positionCount=5;float a=i*2.1f;for(int j=0;j<5;j++){float h=.2f+j*.16f,r=b.kind==2?.50f:.46f;l.SetPosition(j,new Vector3(Mathf.Sin(a+(j%2)*.05f)*r,h,Mathf.Cos(a+(j%2)*.05f)*r));}}}
    Floating(b.p,b.name,new Color(.77f,.63f,.43f));return;
   }
   b.broken=true;brokenContainerIds.Add(b.id);grid.OpenCell(b.x,b.z);playerRoute.OpenCell(b.x,b.z);navAt=0;
   if(b.halo)b.halo.SetActive(false);if(propTarget==b)propTarget=null;
   if(b.root)
   {
    int i=0;foreach(Transform piece in b.root.transform){if(piece.gameObject==b.halo||piece.GetComponent<LineRenderer>()){piece.gameObject.SetActive(false);continue;}var debris=piece.gameObject.AddComponent<NocturnisPropFragment>();float a=i++*2.399f;debris.velocity=new Vector3(Mathf.Sin(a)*(1.2f+(i%3)*.4f),2f+(i%4)*.35f,Mathf.Cos(a)*1.7f);debris.angular=new Vector3(120+i*11,70-i*13,90+i*7);}
    effects.Add(new Effect{obj=b.root,end=Time.time+5.5f,duration=5.5f});
   }
   Sparks(b.p,new Color(.55f,.43f,.3f),12);Sound(b.kind==1?"potbreak":b.kind==3?"metalbreak":"woodbreak");SpawnContainerLoot(b);SaveCheckpoint();
  }
  void SpawnContainerLoot(Breakable b)
  {
   if(brokenContainerIds.Contains("L:"+b.id)||b.roll>=35)return;
   int kind=b.roll<15?0:b.roll<22?1:b.roll<28?2:3;
   var root=new GameObject(kind==3?"Relikt z pojemnika":"Zawartość: "+b.name);root.transform.SetParent(world);root.transform.position=b.p;
   var icon=GameObject.CreatePrimitive(kind==3?PrimitiveType.Cube:kind==2?PrimitiveType.Sphere:PrimitiveType.Cylinder);icon.transform.SetParent(root.transform,false);icon.transform.localScale=kind==3?Vector3.one*.32f:kind==2?new Vector3(.24f,.4f,.24f):new Vector3(.3f,.08f,.3f);icon.transform.localRotation=Quaternion.Euler(kind==3?45:0,0,kind==3?45:0);icon.GetComponent<Renderer>().sharedMaterial=kind==3?art.tealMaterial:kind==2?art.redMaterial:art.goldMaterial;Destroy(icon.GetComponent<Collider>());
   Ring(root.transform,.35f,.035f,kind==3?art.tealMaterial:art.goldMaterial);
   if(kind==3){var beam=new GameObject("Blask reliktu");beam.transform.SetParent(root.transform,false);var line=beam.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=art.effectMaterial;line.startColor=art.tealMaterial.color;line.endColor=new Color(.2f,1,.7f,0);line.positionCount=2;line.SetPosition(0,Vector3.zero);line.SetPosition(1,Vector3.up*2.2f);line.startWidth=.14f;line.endWidth=.02f;}
   containerLoot.Add(new ContainerLoot{owner=b,root=root,position=b.p,kind=kind,amount=kind==3?Mathf.Clamp(1+CampaignDepth/3+(b.roll%2),1,4):kind==0?3+b.roll%6:1});
  }
  void UpdateContainerLoot()
  {
   foreach(var b in containers)if(b.halo)b.halo.SetActive(!b.broken && propTarget==b);
   for(int i=containerLoot.Count-1;i>=0;i--)
   {
    var drop=containerLoot[i];float d=Vector3.Distance(drop.position,HeroPosition);
    if(d<3.2f && Visible(drop.position,HeroPosition))drop.position=Vector3.MoveTowards(drop.position,HeroPosition,6*Time.deltaTime);
    drop.root.transform.position=drop.position+Vector3.up*(.35f+Mathf.Sin(Time.time*3+i)*.1f);drop.root.transform.Rotate(0,Time.deltaTime*65,0);
    if(d>.7f)continue;
    if(drop.kind==3 && Inventory.Count>=InventoryCapacity)
    {
     // Transfer the intact item to persistent world loot instead of destroying
     // it. Its stable container ID is consumed in the same saved snapshot.
     if(brokenContainerIds.Add("L:"+drop.owner.id))
     {
      int slot=(drop.owner.x+drop.owner.z)%3;
      CreateRelicDropData(drop.position,RollRelic(slot,drop.amount,drop.owner.x*31+drop.owner.z*17));
      ShowNotice("Pełny plecak [I] • relikt z pojemnika czeka na ziemi");
     }
     Destroy(drop.root);containerLoot.RemoveAt(i);SaveCheckpoint();continue;
    }
    if(brokenContainerIds.Add("L:"+drop.owner.id))
    {
     string message;
     if(drop.kind==0){Coins+=drop.amount;message="+"+drop.amount+" zł";}
     else if(drop.kind==1){Materials+=drop.amount;message="+1 odłamek rzemieślniczy";}
     else if(drop.kind==2){if(Potions<5){Potions++;message="+1 mikstura";}else{Coins+=5;message="Pełny pas • +5 zł";}}
     else
     {int slot=(drop.owner.x+drop.owner.z)%3;var relic=RollRelic(slot,drop.amount,drop.owner.x*31+drop.owner.z*17);Inventory.Add(relic);message="Znaleziono: "+RelicName(relic)+" [I]";}
     ShowNotice(message);Floating(HeroPosition,message,art.tealMaterial.color);Sound(drop.kind==3?"shrine":"coin");SaveCheckpoint();
    }
    Destroy(drop.root);containerLoot.RemoveAt(i);
   }
  }
  void ResetContainerMaterials()
  {foreach(var m in containerMeshes)if(m)Destroy(m);containerMeshes.Clear();foreach(var m in new[]{wood,iron,clay,crackMaterial})if(m)Destroy(m);if(woodTexture)Destroy(woodTexture);}
  public Vector3[] ReviewContainerPositions(){var points=new Vector3[containers.Count];for(int i=0;i<points.Length;i++)points[i]=containers[i].p;return points;}
  public bool ReviewBreakContainer(int index,float damage){if(!Testing||index<0||index>=containers.Count)return false;DamageContainer(containers[index],damage);return containers[index].broken;}
 }
 public sealed class NocturnisPropFragment:MonoBehaviour
 {
  public Vector3 velocity,angular;float age;Vector3 scale;bool landed;
  void Start(){scale=transform.localScale;}
  void Update()
  {
   float dt=Time.deltaTime;age+=dt;if(!landed){velocity+=Vector3.down*6.5f*dt;transform.position+=velocity*dt;transform.Rotate(angular*dt,Space.Self);if(transform.position.y<.04f){var p=transform.position;p.y=.04f;transform.position=p;velocity=Vector3.zero;landed=true;}}
   if(age>3.4f)transform.localScale=scale*Mathf.Clamp01((5.3f-age)/1.9f);
  }
 }
}
