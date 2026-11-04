using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Krypta.Editor
{
 public static class NocturnisExpandedAct1QA
 {
  const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
  static int step,checks;
  static double started,next;
  static bool errors;
  [InitializeOnLoadMethod]static void Restore(){if(!SessionState.GetBool("Nocturnis.ExpandedAct1QA",false))return;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
  public static void Run(){NocturnisIntegratedBuild.Validate();EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool("Nocturnis.ExpandedAct1QA",true);EditorApplication.isPlaying=true;}
  static void Log(string m,string s,LogType t){if(t==LogType.Exception || t==LogType.Error || t==LogType.Assert)errors=true;}
  static void Check(bool condition,string name){checks++;if(!condition)throw new Exception(name);}
  static object Call(KryptaGame g,string name,params object[] a){return typeof(KryptaGame).GetMethod(name,Private).Invoke(g,a);}
  static void Set(KryptaGame g,string name,object value){typeof(KryptaGame).GetProperty(name).SetValue(g,value);}
  static void Field(KryptaGame g,string name,object value){typeof(KryptaGame).GetField(name,Private).SetValue(g,value);}
  static void Pos(KryptaGame g,Vector3 p){((ActorVisual)typeof(KryptaGame).GetField("player",Private).GetValue(g)).Root.transform.position=p;}
  static void Clear(KryptaGame g){foreach(var e in g.enemies.ToArray())if(e.hp>0)Call(g,"Damage",e,100000f);}
  // UAL intentionally copies the reviewed prefab to add cadence metadata.
  // Asset identity and rest-rig equivalence remain required despite its new path.
  static string RigPath(Transform bone,Transform root)
  {return bone ? AnimationUtility.CalculateTransformPath(bone,root) : "<null>";}
  static void ReviewedHeroSource(GameObject model,NocturnisActILibrary original,int index)
  {
   string id=NocturnisUalImport.Heroes[index];
   var entry=original.Find(id);
   Check(entry!=null && entry.prefab,"Reviewed source backup exists: "+id);
   var source=entry.prefab;
   Check(AssetDatabase.GetAssetPath(source).StartsWith("Assets/NocturnisActI/Runtime/",StringComparison.Ordinal),"Original hero remains the reviewed Act I source: "+id);
   Check(AssetDatabase.GetAssetPath(model)==NocturnisUalImport.Folder+"/"+id+"/"+id+".prefab","Hero model uses the reviewed UAL copy: "+id);
   var before=source.GetComponentsInChildren<Transform>(true).ToDictionary(t=>RigPath(t,source.transform));
   var after=model.GetComponentsInChildren<Transform>(true).ToDictionary(t=>RigPath(t,model.transform));
   Check(before.Count==after.Count && before.Keys.All(after.ContainsKey),"Hero rig hierarchy is preserved: "+id);
   int meshCount=0;
   foreach(var pair in before)
   {
    string label=id+"/"+pair.Key;var old=pair.Value;var current=after[pair.Key];
    Check(Vector3.Distance(old.localPosition,current.localPosition)<.00001f && Quaternion.Angle(old.localRotation,current.localRotation)<.001f && Vector3.Distance(old.localScale,current.localScale)<.00001f,"Hero rest transform is unchanged: "+label);
    var oldMeshes=old.GetComponents<MeshFilter>();var currentMeshes=current.GetComponents<MeshFilter>();
    Check(oldMeshes.Length==currentMeshes.Length,"Hero static mesh count is preserved: "+label);
    for(int i=0;i<oldMeshes.Length;i++)
    {Check(oldMeshes[i].sharedMesh==currentMeshes[i].sharedMesh,"Hero static source mesh is identical: "+label);meshCount++;}
    var oldRenderers=old.GetComponents<Renderer>();var currentRenderers=current.GetComponents<Renderer>();
    Check(oldRenderers.Length==currentRenderers.Length,"Hero renderer count is preserved: "+label);
    for(int i=0;i<oldRenderers.Length;i++)
    {
     Check(oldRenderers[i].GetType()==currentRenderers[i].GetType(),"Hero renderer type is preserved: "+label);
     Check(oldRenderers[i].sharedMaterials.SequenceEqual(currentRenderers[i].sharedMaterials),"Hero source materials are identical: "+label);
     var oldSkin=oldRenderers[i] as SkinnedMeshRenderer;var currentSkin=currentRenderers[i] as SkinnedMeshRenderer;
     if(oldSkin)
     {
      Check(currentSkin && oldSkin.sharedMesh==currentSkin.sharedMesh,"Hero skinned source mesh is identical: "+label);meshCount++;
      Check(RigPath(oldSkin.rootBone,source.transform)==RigPath(currentSkin.rootBone,model.transform),"Hero skin root is preserved: "+label);
      Check(oldSkin.bones.Select(b=>RigPath(b,source.transform)).SequenceEqual(currentSkin.bones.Select(b=>RigPath(b,model.transform))),"Hero skin bone order and paths are preserved: "+label);
     }
    }
   }
   Check(meshCount>0,"Reviewed hero geometry is present: "+id);
  }
  static void Tick()
  {
   if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.2;
   var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g || !g.View)return;
   if(EditorApplication.timeSinceStartup-started>210){Finish(false,"Timeout");return;}
   try
   {
    KryptaGame.Testing=true;
    if(step==0)
    {
     Check(g.State==KryptaGame.Phase.Title && g.Zone==Campaign.Road,"Title starts at prologue");
     var previewZones=(int[])typeof(KryptaGame).GetField("ActPreviewZones",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
     Check(previewZones.Length==Campaign.Count && previewZones.Distinct().Count()==Campaign.Count,"Preview lists every Act I map exactly once");
     Check(previewZones.OrderBy(z=>z).SequenceEqual(Enumerable.Range(0,Campaign.Count)),"Preview includes all nineteen valid map IDs");
     Check(previewZones[0]==Campaign.Road && previewZones[1]==Campaign.Town && previewZones[2]==Campaign.Fields && previewZones[previewZones.Length-1]==Campaign.Heart,"Preview follows the journey and retains Town to Fields shortcut");
     var forestAssets=Resources.Load<KryptaAssets>("KryptaAssets");
     Check(forestAssets.heroes.Length==5,"Five heroes");
     if(forestAssets.version>=8)
     {
     NocturnisUalImport.AssertApplied();
     var originalHeroes=AssetDatabase.LoadAssetAtPath<NocturnisActILibrary>(NocturnisUalImport.OriginalLibrary);
     Check(originalHeroes,"Original reviewed hero library is retained");
     for(int heroIndex=0;heroIndex<forestAssets.heroes.Length;heroIndex++)
     {
      var hero=forestAssets.heroes[heroIndex];
      ReviewedHeroSource(hero.model,originalHeroes,heroIndex);
      Check(heroIndex==0 || hero.weapon,"Other four heroes retain their weapons");
      foreach(string name in NocturnisKayKitImport.ClipNames)
       Check(hero.animations.Any(clip=>clip && clip.name==name && clip.legacy && clip.length>.1f),"Required runtime animation: "+heroIndex+"/"+name);
     }
     Check(forestAssets.skeletonVariants!=null && forestAssets.skeletonVariants.Length==4,"Four skeleton roles");
     }
     Check(forestAssets.version==10 && forestAssets.trees.Length==4,"Four new original tree models");
     foreach(var tree in forestAssets.trees)Check(tree.GetComponentsInChildren<MeshFilter>(true).Length>0,"Tree geometry "+tree.name);
     foreach(string file in new[]{"UI/GothicHUD","UI/MenuCathedral"})Check(Resources.Load<Texture2D>(file),"UI texture "+file);
     for(int z=0;z<Campaign.Count;z++)
     {
      var map=new DungeonGrid(z);int sx=z==0 ? 11 : z==3 || z==5 || z==9 ? 12 : 5;int sz=z==0 ? 5 : 3;map.BuildDistances(sx,sz);
      int ex=z==0 ? 20 : z==3 || z==9 ? 12 : 17,ez=z==0 ? 10 : z==3 || z==9 ? 37 : 38;
      Check(map.Distance(ex,ez)>0,"Reachable exit zone "+z);
      for(int x=0;x<DungeonGrid.Width;x++)for(int y=0;y<DungeonGrid.Height;y++)if(map.Walkable(x,y))Check(map.Distance(x,y)>=0,"Disconnected tile "+z+"/"+x+"/"+y);
     }
     g.Begin();Check(g.Zone==Campaign.Road && g.Remaining==Campaign.EncounterBudget(Campaign.Road),"Prologue encounter");
     Clear(g);Check(g.WeaponRank>=2,"Overseer gives class rare weapon");Pos(g,g.ExitPosition);g.Interact();Check(g.Zone==0,"Drawbridge leads to Oakhaven");
     Pos(g,new Vector3(19.8f,0,14.4f));g.Interact();Check(!g.QuestAccepted,"Irma guides player without granting the main quest");g.Resume();
     Pos(g,new Vector3(27,0,14.4f));g.Interact();Check(g.QuestAccepted,"Malakor grants main quest");g.Resume();
    }
    else if(step==1)
    {
     Set(g,"Coins",500);Pos(g,new Vector3(14.4f,0,10.8f));g.Interact();Check(g.Service=="forge","Forge citizen");int rank=g.WeaponRank,coins=g.Coins,price=g.ForgePrice(0);Check(g.Forge(0) && g.WeaponRank==rank+1 && g.Coins==coins-price,"Forge transaction");g.Resume();
     Check(!g.Forge(0),"Forge unavailable outside NPC");
     Pos(g,new Vector3(14.4f,0,23.4f));g.Interact();Check(g.Service=="alchemy","Vespera");Check(g.Alchemy(1) && g.PotionRank==1,"Alchemy upgrade");
     Set(g,"Coins",2000);Check(!g.Alchemy(-1) && !g.CombineGem(2) && !g.SocketGem(3),"Invalid artisan actions rejected");
     float baseAttack=g.AttackPower;for(int i=0;i<3;i++)Check(g.BuyGem(),"Buy raw gem");
     Check(g.CombineGem(0) && g.GemBag[0]==0 && g.GemBag[1]==1,"Three gems combine without duplication");
     Check(g.SocketGem(1) && g.GemBag[1]==0 && g.SocketedGems.Count==1 && g.AttackPower==baseAttack+4,"Grade two gem provides four attack");
     Check(g.ExtractGem() && g.GemBag[1]==1 && g.SocketedGems.Count==0 && g.AttackPower==baseAttack,"Extraction preserves grade and removes bonus");
     Check(!g.ExtractGem(),"Empty sockets cannot produce gems");g.SocketGem(1);
     Check(g.EnchantGear(0,2) && g.MaxHealth==g.Hero.health+(g.Level-1)*15+g.WeaponRank*5,"Endurance enchant affects maximum health");
     coins=g.Coins;Check(!g.EnchantGear(0,2) && g.Coins==coins,"Repeated enchant costs nothing");
     g.Resume();Check(!g.BuyGem() && !g.ExtractGem() && !g.EnchantGear(0,1),"Artisans require their NPC");
     Pos(g,new Vector3(12.6f,0,23.4f));g.Interact();Check(g.Service=="wardrobe" && g.ChangeGarb(2),"Wardrobe at altar");g.Resume();
     g.Inventory.Add(new KryptaGame.Relic{slot=0,rank=2,enchant=3});g.OpenEquipment();Check(g.EquipItem(g.Inventory.Count-1) && g.GearEnchant(0)==3,"Relic keeps enchant when equipped");Check(g.Inventory.Exists(r=>r.slot==0 && r.enchant==2),"Removed gear retains previous enchant");g.Resume();
     Call(g,"SaveCheckpoint");Check(g.ContinueCheckpoint() && g.SocketedGems.Count==1 && g.SocketedGems[0]==2 && g.GearEnchant(0)==3 && g.Garb==2,"Save roundtrip preserves artisan state");
     Call(g,"RestoreArtisans",null,null,null,0,3);Check(g.SocketedGems.Count==3 && g.Gems==3,"Legacy three gems migrate to three grade one sockets");
     g.OpenBook();g.SetRune(1);Check(g.Rune==1,"Rune book selection");g.Resume();
     g.Inventory.Add(new KryptaGame.Relic{slot=2,rank=4,identified=false});Pos(g,new Vector3(27,0,14.4f));g.Interact();Check(g.Identify(),"Malakor identifies");g.Resume();g.OpenEquipment();Check(g.EquipItem(g.Inventory.Count-1) && g.CharmRank==4,"Equip identified legendary");g.Resume();
     Pos(g,g.ExitPosition);g.Interact();Check(g.Zone==Campaign.Fields,"Town to fields");
     int gold=g.Coins,gear=g.CharmRank;Clear(g);Call(g,"SaveCheckpoint");Set(g,"Coins",0);Check(g.ContinueCheckpoint() && g.Coins==gold && g.CharmRank==gear && g.Remaining==0,"Save restores loot and slain enemies without respawning");
    }
    else if(step>=2 && step<7)
    {
     int hero=step-2;Set(g,"State",KryptaGame.Phase.Title);g.SelectHero(hero);g.Resume();Call(g,"LoadZone",Campaign.Forest);Set(g,"Level",5);Set(g,"Resource",100f);Set(g,"Shadow",100f);Field(g,"pulseAt",0f);Field(g,"defenseAt",0f);Field(g,"extraAt",new float[3]);
     var e=g.enemies.Find(v=>v.hp>0);Vector3 attackSpot=e.Position;bool reachable=false;foreach(var dir in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right}){Vector3 p=e.Position+dir*.8f;if(g.MapFloor(Mathf.RoundToInt(p.x/1.8f),Mathf.RoundToInt(p.z/1.8f)) && (bool)Call(g,"Visible",p,e.Position)){attackSpot=p;reachable=true;break;}}Check(reachable,"Valid attack position class "+hero);Pos(g,attackSpot);float hp=e.hp;Field(g,"strikeAt",0f);Call(g,"Strike",e.Position);g.ReviewCompleteStrike();Check(e.hp<hp,"Basic class "+hero);{g.CastSkill(e.Position);g.ReviewCompletePower();}Check(g.PulseReady>0,"Skill class "+hero);{g.CastDefense();g.ReviewCompletePower();}Check(g.DefenseReady>0,"Defense class "+hero);
     for(int i=0;i<3;i++){Set(g,"Resource",100f);{g.CastAdditional(i,e.Position);g.ReviewCompletePower();}Check(g.ExtraReady(i)>0,"Additional skill "+hero+"/"+i);}
    }
    else if(step==7)
    {
     var previewZones=(int[])typeof(KryptaGame).GetField("ActPreviewZones",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
     foreach(int z in previewZones)
     {Call(g,"OpenCryptPreviewZone",z);Check(g.Zone==z && g.State==KryptaGame.Phase.Playing && KryptaGame.Testing,"Actual preview opens map in isolated playing state "+z);var backing=GameObject.Find(Campaign.Outdoors(z) ? "Ground without seams" : "Podłoże bez szczelin");Check(backing && backing.GetComponent<MeshFilter>().sharedMesh.vertexCount>100,"Continuous floor "+z);foreach(var e in g.enemies)Check(g.MapFloor(Mathf.RoundToInt(e.Position.x/1.8f),Mathf.RoundToInt(e.Position.z/1.8f)),"Enemy on valid tile "+z+" "+e.label);
      if(Campaign.Underground(z)){
       var currentWorld=(Transform)typeof(KryptaGame).GetField("world",Private).GetValue(g);var floorRoot=currentWorld.Find("Posadzka katakumb");var floor=floorRoot ? floorRoot.GetComponent<NocturnisCryptGeometry>() : null;Check(floor && floor.meshes.Count>0,"New crypt floor exists "+z);
       foreach(var mesh in floor.meshes)Check(mesh.bounds.size.y<.5f && mesh.bounds.max.y<.08f,"Crypt slabs are horizontal and remain at walking height "+z);
      }
      if(Campaign.Outdoors(z))
      {
       var scene=(Transform)typeof(KryptaGame).GetField("world",Private).GetValue(g);
       var trees=scene.GetComponentsInChildren<NocturnisForestTree>();if(z==Campaign.Ravine){Check(trees.Length==0,"Mountain pass is above the tree line");int rocks=0;foreach(var tr in scene.GetComponentsInChildren<Transform>())if(tr.name=="Mountain shale bank"){rocks++;Check(!g.MapFloor(Mathf.RoundToInt(tr.position.x/1.8f),Mathf.RoundToInt(tr.position.z/1.8f)),"Mountain rock outside navigation");}Check(rocks>=20,"Rocky banks frame the mountain pass");}else Check(trees.Length>10,"Trees present in zone "+z);
       foreach(var tree in trees)Check(!g.MapFloor(Mathf.RoundToInt(tree.transform.position.x/1.8f),Mathf.RoundToInt(tree.transform.position.z/1.8f)),"Tree trunk outside navigation "+z);
      }
     }
     Call(g,"LoadZone",Campaign.Heart);var boss=g.enemies.Find(e=>e.boss);Check(boss!=null && boss.label.Contains("MALAZAR"),"Malazar present");Pos(g,boss.Position-Vector3.forward*5);boss.alerted=true;boss.hp=boss.maxHp*.49f;Call(g,"UpdateSpecialEnemy",boss);Check(boss.mutated,"Phase two at half health");Check(g.MapFloor(12,25) && g.MapFloor(12,37),"Safe center and exit remain walkable");
     Clear(g);Pos(g,g.ExitPosition);g.Interact();Check(g.State==KryptaGame.Phase.Won,"Heart recovery completes Act I");
    }
    else {Finish(!errors,"Act I: "+checks+" assertions; 19 connected maps, prologue, townsfolk, service transactions, inventory, saves, 5 classes/6 skills, continuous floors, Malazar mutation and completion.");return;}
    step++;
   }
   catch(Exception e){Finish(false,e.ToString());}
  }
  static void Finish(bool success,string message){SessionState.SetBool("Nocturnis.ExpandedAct1QA",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/expanded-act1-result.txt",(success ? "PASS: " : "FAIL: ")+message);Debug.Log(message);EditorApplication.Exit(success ? 0 : 1);}
 }
}
