using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Krypta.Editor
{
 public static class KryptaSmoke
 {
  static double started;
  static bool failed;
  static int step,hero;
  static double next;
  const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
  [InitializeOnLoadMethod]static void Restore()
  {
   if(!SessionState.GetBool("Krypta.Smoke",false))return;
   started=EditorApplication.timeSinceStartup;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
  }
  public static void Run()
  {KryptaSetup.Prepare();EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");started=EditorApplication.timeSinceStartup;EditorApplication.update+=StartWhenReady;}
  static void StartWhenReady()
  {if(EditorApplication.timeSinceStartup-started<4 || EditorApplication.isUpdating || EditorApplication.isCompiling)return;EditorApplication.update-=StartWhenReady;SessionState.SetBool("Krypta.Smoke",true);EditorApplication.isPlaying=true;}
  static void Log(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)failed=true;}
  static void Check(bool value,string label){if(!value)throw new Exception(label);}
  static object Call(KryptaGame g,string method,params object[] args){return typeof(KryptaGame).GetMethod(method,Private).Invoke(g,args);}
  static void Set(KryptaGame g,string property,object value){typeof(KryptaGame).GetProperty(property).SetValue(g,value,null);}
  static void Field(KryptaGame g,string field,object value){typeof(KryptaGame).GetField(field,Private).SetValue(g,value);}
  static void Position(KryptaGame g,float x,float z)
  {var a=(ActorVisual)typeof(KryptaGame).GetField("player",Private).GetValue(g);a.Root.transform.position=new Vector3(x*1.8f,0,z*1.8f);}
  static void CheckGround(KryptaGame g)
  {
   var backing=GameObject.Find("Podłoże bez szczelin");Check(backing && backing.GetComponent<MeshFilter>().sharedMesh.vertexCount>300,"Continuous ground geometry");
   var config=Resources.Load<KryptaAssets>("KryptaAssets");
   Check(config.floorMaterial && config.effectMaterial && config.particleMaterial,"Ground and VFX materials assigned");
   int floor=0;foreach(var obj in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
   {
    if(obj.name!="Posadzka")continue;Bounds b;
    Check(ModelBounds.TryGet(obj.gameObject,out b) && b.size.x>1.7f && b.size.z>1.7f && Mathf.Abs(b.max.y)<.05f,"Ground tile has geometry at walking level");floor++;
   }
   if(g.Zone==2)Check(floor>400,"Crypt floor coverage");
  }
  static void Tick()
  {
   if(EditorApplication.timeSinceStartup-started>150){Finish("Timeout",false);return;}
   if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
   var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g || !g.View)return;
   next=EditorApplication.timeSinceStartup+.2;
   try
   {
    if(step==0)
    {
     Check(g.State==KryptaGame.Phase.Title && g.Zone==0 && g.Remaining==0,"Initial town/title");
     var config=Resources.Load<KryptaAssets>("KryptaAssets");Check(config.surfaces!=null && config.surfaces.Length==4,"Atlas override configuration");
     foreach(var surface in config.surfaces)Check(surface.material && surface.material.GetTexture("baseColorTexture"),"Atlas texture "+surface.source);
     CheckGround(g);
     g.SelectHero(hero);step=1;
    }
    else if(step==1)
    {
     var obj=GameObject.Find("Bohater");Bounds b;Check(obj && ModelBounds.TryGet(obj,out b) && b.size.y>1 && b.size.y<3,"Hero bounds "+hero);
     Check(obj.GetComponentInChildren<Animation>()["Idle"]!=null,"Hero animation "+hero);
     if(++hero<5)step=0;else{g.SelectHero(0);g.Begin();Position(g,11,7);g.Interact();Check(g.QuestAccepted && g.State==KryptaGame.Phase.Dialogue,"NPC quest");g.Resume();Position(g,20,10);g.Interact();Check(g.Zone==1 && g.Remaining==12,"Forest transition");hero=0;step=2;}
    }
    else if(step==2)
    {
     Set(g,"State",KryptaGame.Phase.Title);g.SelectHero(hero);Set(g,"State",KryptaGame.Phase.Playing);
     Set(g,"Resource",70f);Field(g,"strikeAt",0f);Field(g,"pulseAt",0f);Field(g,"defenseAt",0f);
     var e=g.enemies.Find(x=>x.hp>0);Position(g,e.Position.x/1.8f,e.Position.z/1.8f-1.1f);
     float hp=e.hp;Call(g,"Strike",e.Position);g.ReviewCompleteStrike();Check(e.hp<hp,"Basic attack class "+hero);
     g.Rune=hero%2;g.CastSkill(e.Position);Check(g.PulseReady>0,"Class skill cooldown "+hero);g.CastDefense();Check(g.DefenseReady>0,"Defense cooldown "+hero);
     if(++hero==5)step=3;
    }
    else if(step==3)
    {
     Call(g,"ActivateShrine","slaughter");Check(g.Status.Count("slaughter",Time.time)==1,"Pylon buff");
     Check(Mathf.Abs((float)Call(g,"Cooldown",4f)-1)<.01f,"Pylon cooldown reduction");
     g.Status.Apply("ichor","Ichor",Time.time,10,10,true);Set(g,"Health",50f);Call(g,"Heal",10f);Check(Mathf.Abs(g.Health-59)<.01f,"Ichor healing reduction");
     Call(g,"ActivateShrine","renewal");Check(g.Status.Count("ichor",Time.time)==0 && g.Health==g.MaxHealth,"Cleanse and healing");
     int gear=g.WeaponRank+g.ArmorRank+g.CharmRank;Call(g,"DropRelic",g.HeroPosition,2);Call(g,"UpdateRelics");Check(g.WeaponRank+g.ArmorRank+g.CharmRank>gear,"Relic pickup equips upgrade");
     Set(g,"Coins",500);g.OpenEquipment();int rank=g.WeaponRank,price=g.ForgePrice(0);Check(g.Forge(0) && g.WeaponRank==rank+1 && g.Coins==500-price,"Forge consumes gold and upgrades");g.Resume();
     Check(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length>5,"Fire and spell particles exist");
     foreach(var e in g.enemies)if(e.hp>0)Call(g,"Damage",e,10000f);
     Check(g.Remaining==0,"Forest cleared");Position(g,17,38);g.Interact();Check(g.Zone==2 && g.Remaining==12,"Crypt transition");step=4;
    }
    else if(step==4)
    {
     CheckGround(g);int coins=g.Coins,rank=g.WeaponRank;
     Set(g,"Health",1f);Set(g,"Coins",0);Check(g.ContinueCheckpoint() && g.Zone==2 && g.Health==g.MaxHealth && g.Coins==coins && g.WeaponRank==rank,"Checkpoint restores zone, health, coins and gear");
     Check(g.enemies.Exists(e=>e.boss && e.maxHp==440),"Bishop spawned");
     foreach(var e in g.enemies)if(e.hp>0)Call(g,"Damage",e,10000f);
     Position(g,17,38);g.Interact();Check(g.State==KryptaGame.Phase.Won && !KryptaGame.HasCheckpoint,"Campaign chapter win clears checkpoint");step=5;next=EditorApplication.timeSinceStartup+3;
    }
    else if(step==5)Finish("Five classes, combat, quest and 3 zones; visible floor geometry/materials; fire and magic particles; relic pickup and paid upgrades; checkpoint restore; pylons and Ichor; boss and chapter completion",!failed);
   }
   catch(Exception e){Finish(e.ToString(),false);}
  }
  static void Finish(string message,bool success)
  {
   SessionState.SetBool("Krypta.Smoke",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/smoke-result.txt",(success ? "PASS: " : "FAIL: ")+message+Environment.NewLine);
   Debug.Log("Nocturnis smoke: "+message);EditorApplication.Exit(success ? 0 : 1);
  }
 }
}
