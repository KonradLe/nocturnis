using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Krypta.Editor
{
 public static class NocturnisAct1QA
 {
  const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
  static int step,checks;
  static double started,next;
  static bool errors;
  [InitializeOnLoadMethod]static void Restore(){if(!SessionState.GetBool("Nocturnis.Act1QA",false))return;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
  public static void Run(){KryptaSetup.Prepare();EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool("Nocturnis.Act1QA",true);EditorApplication.isPlaying=true;}
  static void Log(string m,string s,LogType t){if(t==LogType.Exception || t==LogType.Error || t==LogType.Assert)errors=true;}
  static void Check(bool condition,string name){checks++;if(!condition)throw new Exception(name);}
  static object Call(KryptaGame g,string name,params object[] a){return typeof(KryptaGame).GetMethod(name,Private).Invoke(g,a);}
  static void Set(KryptaGame g,string name,object value){typeof(KryptaGame).GetProperty(name).SetValue(g,value);}
  static void Field(KryptaGame g,string name,object value){typeof(KryptaGame).GetField(name,Private).SetValue(g,value);}
  static void Pos(KryptaGame g,Vector3 p){((ActorVisual)typeof(KryptaGame).GetField("player",Private).GetValue(g)).Root.transform.position=p;}
  static void Clear(KryptaGame g){foreach(var e in g.enemies.ToArray())if(e.hp>0)Call(g,"Damage",e,100000f);}
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
     var forestAssets=Resources.Load<KryptaAssets>("KryptaAssets");
     Check(forestAssets.heroes.Length==5,"Five heroes");
     if(forestAssets.version>=8)
     {
     foreach(var hero in forestAssets.heroes)
     {
      Check(AssetDatabase.GetAssetPath(hero.model).StartsWith(NocturnisKayKitImport.Output),"Hero comes from supplied KayKit 2.0");
      Check(hero.weapon && hero.animations.Length==8,"Equipped hero with complete movement/combat clips");
      foreach(var clip in hero.animations)Check(clip && clip.legacy && clip.length>.1f,"Playable KayKit animation");
     }
     Check(forestAssets.skeletonVariants!=null && forestAssets.skeletonVariants.Length==4,"Four skeleton roles");
     }
     Check(forestAssets.version>=6 && forestAssets.trees.Length==10,"Ten converted forest sources");
     foreach(var tree in forestAssets.trees)
     {
      Check(tree.GetComponent<MeshFilter>().sharedMesh.vertexCount>100,"Tree geometry "+tree.name);
      Check(!tree.GetComponent<Tree>(),"Legacy tree generator removed "+tree.name);
      foreach(var material in tree.GetComponent<MeshRenderer>().sharedMaterials)
       Check(material && material.shader.name=="Nocturnis/ForestSurface" && material.mainTexture && material.mainTexture.width>=1024,"Runtime tree atlas "+tree.name);
     }
     foreach(string file in new[]{"UI/GothicHUD","UI/MenuCathedral"})Check(Resources.Load<Texture2D>(file),"UI texture "+file);
     for(int z=0;z<Campaign.Count;z++)
     {
      var map=new DungeonGrid(z);int sx=z==0 ? 11 : z==3 || z==5 || z==9 ? 12 : 5;int sz=z==0 ? 5 : 3;map.BuildDistances(sx,sz);
      int ex=z==0 ? 20 : z==3 || z==9 ? 12 : 17,ez=z==0 ? 10 : z==3 || z==9 ? 37 : 38;
      Check(map.Distance(ex,ez)>0,"Reachable exit zone "+z);
      for(int x=0;x<DungeonGrid.Width;x++)for(int y=0;y<DungeonGrid.Height;y++)if(map.Walkable(x,y))Check(map.Distance(x,y)>=0,"Disconnected tile "+z+"/"+x+"/"+y);
     }
     g.Begin();Check(g.Zone==Campaign.Road && g.Remaining==5,"Prologue encounter");
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
     Pos(g,new Vector3(10.8f,0,23.4f));g.Interact();Check(g.Service=="wardrobe" && g.ChangeGarb(2),"Wardrobe at altar");g.Resume();
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
     var e=g.enemies.Find(v=>v.hp>0);Pos(g,e.Position-Vector3.forward*1.8f);float hp=e.hp;Field(g,"strikeAt",0f);Call(g,"Strike",e.Position);g.ReviewCompleteStrike();Check(e.hp<hp,"Basic class "+hero);g.CastSkill(e.Position);Check(g.PulseReady>0,"Skill class "+hero);g.CastDefense();Check(g.DefenseReady>0,"Defense class "+hero);
     for(int i=0;i<3;i++){Set(g,"Resource",100f);g.CastAdditional(i,e.Position);Check(g.ExtraReady(i)>0,"Additional skill "+hero+"/"+i);}
    }
    else if(step==7)
    {
     for(int z=0;z<Campaign.Count;z++)
     {Call(g,"LoadZone",z);var backing=GameObject.Find("Podłoże bez szczelin");Check(backing && backing.GetComponent<MeshFilter>().sharedMesh.vertexCount>100,"Continuous floor "+z);foreach(var e in g.enemies)Check(g.MapFloor(Mathf.RoundToInt(e.Position.x/1.8f),Mathf.RoundToInt(e.Position.z/1.8f)),"Enemy on valid tile "+z+" "+e.label);
      if(Campaign.Outdoors(z))
      {
       var scene=(Transform)typeof(KryptaGame).GetField("world",Private).GetValue(g);
       var trees=scene.GetComponentsInChildren<NocturnisForestTree>();Check(trees.Length>10,"Trees present in zone "+z);
       foreach(var tree in trees)Check(!g.MapFloor(Mathf.RoundToInt(tree.transform.position.x/1.8f),Mathf.RoundToInt(tree.transform.position.z/1.8f)),"Tree trunk outside navigation "+z);
      }
     }
     Call(g,"LoadZone",Campaign.Heart);var boss=g.enemies.Find(e=>e.boss);Check(boss!=null && boss.label.Contains("MALAZAR"),"Malazar present");Pos(g,boss.Position-Vector3.forward*5);boss.alerted=true;boss.hp=boss.maxHp*.49f;Call(g,"UpdateSpecialEnemy",boss);Check(boss.mutated,"Phase two at half health");Check(g.MapFloor(12,25) && g.MapFloor(12,37),"Safe center and exit remain walkable");
     Clear(g);Pos(g,g.ExitPosition);g.Interact();Check(g.State==KryptaGame.Phase.Won,"Heart recovery completes Act I");
    }
    else {Finish(!errors,"Act I: "+checks+" assertions; 11 connected maps, prologue, townsfolk, service transactions, inventory, saves, 5 classes/6 skills, continuous floors, Malazar mutation and completion.");return;}
    step++;
   }
   catch(Exception e){Finish(false,e.ToString());}
  }
  static void Finish(bool success,string message){SessionState.SetBool("Nocturnis.Act1QA",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/act1-result.txt",(success ? "PASS: " : "FAIL: ")+message);Debug.Log(message);EditorApplication.Exit(success ? 0 : 1);}
 }
}
