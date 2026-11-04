using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Krypta.Editor
{
 // Real play-mode effects: validates actual casts, geometry, pause/resume and bounded cleanup.
 public static class NocturnisCraftedVfxQA
 {
  const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
  static double started,waitUntil;static int stage,checks;static bool errors;static float frozen,waitScaledUntil;
  static LineRenderer frozenLine;static Color frozenColor;static Vector3 frozenPosition;
  [InitializeOnLoadMethod]static void Restore()
  {if(SessionState.GetBool("Nocturnis.CraftedVfxQA",false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}}
  public static void Run()
  {EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool("Nocturnis.CraftedVfxQA",true);EditorApplication.isPlaying=true;}
  static void Log(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)errors=true;}
  static void Check(bool pass,string message){checks++;if(!pass)throw new Exception(message);}
  static void Set(KryptaGame game,string property,object value){typeof(KryptaGame).GetProperty(property).SetValue(game,value);}
  static void Call(KryptaGame game,string method,params object[] args){typeof(KryptaGame).GetMethod(method,Private).Invoke(game,args);}
  static object Field(object item,string name){return item.GetType().GetField(name).GetValue(item);}
  static void HazardSample(KryptaGame game,object hazard,float seconds)
  {
   // Shift stored deadlines as a deterministic equivalent of a scaled-time jump.
   foreach(string field in new[]{"born","activeAt","end","tickAt","lastSample"})
    hazard.GetType().GetField(field).SetValue(hazard,(float)Field(hazard,field)-seconds);
   typeof(KryptaGame).GetField("hurtUntil",Private).SetValue(game,0f);
   Call(game,"UpdateHazards");
  }
  static void CheckHazardHitches(KryptaGame game,System.Collections.IList hazards)
  {
   game.Status.Clear();Call(game,"ResetCombatActions");Set(game,"Health",game.MaxHealth);
   var player=(ActorVisual)typeof(KryptaGame).GetField("player",Private).GetValue(game);Vector3 center=game.HeroPosition;
   Call(game,"AddHazard",center,2f,3f,true,Color.red,"ichor");Check(hazards.Count==1,"Hostile timing test creates a real floor hazard");
   object hazard=hazards[0];float before=game.Health,damage=9*(1-Mathf.Min(.45f,game.ArmorRank*.06f));
   HazardSample(game,hazard,1.3f);
   Check(game.Health==before && (float)Field(hazard,"activeAt")>Time.time+.85f,"A 1.3-second hitch preserves the hostile warning and deals no retrospective damage");
   for(int i=0;i<25 && game.Health==before;i++)HazardSample(game,hazard,.05f);
   Check(Mathf.Abs(game.Health-(before-damage))<.002f,"Warning finishes with exactly one actual periodic hit");
   float afterFirst=game.Health;player.Root.transform.position=center+Vector3.forward*4;
   HazardSample(game,hazard,1.3f);
   Check(game.Health==afterFirst && hazards.Count==1,"Active hitch does not stack overdue hits or expire the field unseen");
   for(int i=0;i<12;i++)HazardSample(game,hazard,.05f);
   Check(game.Health==afterFirst,"The next periodic tick checks the current position, not previous occupancy");
   player.Root.transform.position=center;HazardSample(game,hazard,0);
   Check(game.Health==afterFirst,"Returning to a field does not pay a backlog of missed damage");
   for(int i=0;i<60 && hazards.Count>0;i++)HazardSample(game,hazard,.05f);
   Check(hazards.Count==0 && Mathf.Abs(game.Health-(before-damage*3))<.005f,"Four scheduled ticks resolve once each: one miss, three hits, then cleanup");

   // Friendly fields expire on the real scaled clock, just like their cooldowns.
   // A hitch skips past ticks instead of extending lifetime or combining damage.
   game.Status.Clear();var victim=game.enemies[0];
   foreach(var enemy in game.enemies)enemy.hp=0;
   victim.hp=victim.maxHp=5000;victim.archetype=0;victim.armorUntil=0;victim.visual.Root.transform.position=center;
   Call(game,"AddHazard",center,2f,4f,false,Color.green,"thorns");hazard=hazards[0];float hp=victim.hp,tick=14+game.Level*2;
   HazardSample(game,hazard,0);Check(Mathf.Abs(victim.hp-(hp-tick))<.005f,"Friendly field applies its initial tick exactly once");
   HazardSample(game,hazard,1.3f);Check(Mathf.Abs(victim.hp-(hp-tick*2))<.005f,"Friendly hitch resolves one current tick and skips overdue damage");
   HazardSample(game,hazard,1.3f);HazardSample(game,hazard,1.3f);
   Check(hazards.Count==1 && Mathf.Abs(victim.hp-(hp-tick*4))<.02f,"Three friendly hitches do not accumulate skipped ticks or prolong the field");
   HazardSample(game,hazard,.11f);
   Check(hazards.Count==0 && Mathf.Abs(victim.hp-(hp-tick*4))<.02f,"Friendly trap expires after four actual scaled seconds with no final retrospective hit");
   Call(game,"AddHazard",center,2f,4f,false,Color.green,"thorns");hazard=hazards[0];hp=victim.hp;
   HazardSample(game,hazard,0);
   for(int i=0;i<90 && hazards.Count>0;i++)HazardSample(game,hazard,.05f);
   Check(hazards.Count==0 && Mathf.Abs(victim.hp-(hp-tick*7))<.02f,"At normal frame intervals a friendly trap keeps seven nominal ticks and four-second expiry");
  }
  static void CheckDelayedVoidCast(KryptaGame game,Vector3 target)
  {
   // The cast's persistent area warning and its impact effect are distinct phases.
   // Drive the real pending action at its authored deadlines, never spawn a test VFX.
   var pending=(System.Collections.IList)typeof(KryptaGame).GetField("classCasts",Private).GetValue(game);
   Check(game.PendingClassCasts==1 && pending.Count==1,"Void Q queues exactly one singularity");
   object cast=pending[0];float born=(float)Field(cast,"born"),at=(float)Field(cast,"at");
   Check((string)Field(cast,"kind")=="void" && Mathf.Abs(at-born-.85f)<.002f,"Void primary retains its authored 0.85-second warning");
   var marker=(GameObject)Field(cast,"marker");var line=marker ? marker.GetComponentInChildren<LineRenderer>() : null;
   Check(marker && marker.activeInHierarchy && Vector3.Distance(marker.transform.position,target)<.01f,"Void warning exists visibly at the targeted position");
   Check(line && line.enabled && line.loop && !line.useWorldSpace && line.positionCount==48 && line.widthMultiplier>.03f && line.sharedMaterial && line.startColor.a>.3f,"Void warning has a visible closed rendered area boundary");
   float radius=(float)Field(cast,"radius");
   for(int i=0;i<line.positionCount;i++)
   {var point=line.GetPosition(i);Check(!float.IsNaN(point.x) && Mathf.Abs(new Vector2(point.x,point.z).magnitude-radius)<.01f && point.y>.01f,"Warning geometry matches impact radius and clears the floor");}
   Check(game.ActiveCraftedVfx==0,"Charging singularity does not show an early impact effect");
   Set(game,"State",KryptaGame.Phase.Paused);Call(game,"TickClassMechanics",at+1);
   Check(game.PendingClassCasts==1 && marker.activeInHierarchy && game.ActiveCraftedVfx==0,"Paused class mechanics retain warning without resolving the impact");
   Set(game,"State",KryptaGame.Phase.Playing);Call(game,"TickClassMechanics",at-.001f);
   Check(game.PendingClassCasts==1 && game.ActiveCraftedVfx==0,"Singularity warning persists until the actual impact deadline");
   Call(game,"TickClassMechanics",at+.001f);
   Check(game.PendingClassCasts==0 && game.ActiveCraftedVfx>0,"At 0.85 seconds the real action replaces warning with impact VFX");
   int impacts=game.ActiveCraftedVfx;Call(game,"TickClassMechanics",at+1);
   Check(game.ActiveCraftedVfx==impacts,"Resolved singularity cannot create duplicate impact VFX");
  }
  static void Tick()
  {
   if(!EditorApplication.isPlaying)return;
   if(EditorApplication.timeSinceStartup-started>120){Finish(false,"Timeout");return;}
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!game || !game.View)return;
   try
   {
    if(stage==0)
    {
     KryptaGame.Testing=true;game.Begin();game.ReducedFlashes=false;
     Check(Resources.Load<Shader>("Shaders/SoftMist"),"Soft volume shader is included as a player resource");
     string[] forms={"Flaming sword","Arterial ribbon","Falling thorn","Tilted orbit","Rising thorn root"};
     for(int hero=0;hero<5;hero++)
     {
      Call(game,"ResetCraftedVfx");Set(game,"SelectedHero",hero);Call(game,"CreateHero");Call(game,"ResetCombatActions");Set(game,"Resource",75f);Set(game,"Level",4);
      Vector3 target=game.HeroPosition+Vector3.forward*2;game.ReviewCastVfx(1,target);
      Check(game.PulseReady>0,"Real primary cooldown class "+hero);
      Check(game.Resource<75 || hero==3,"Real primary resource class "+hero);
      if(hero==3)CheckDelayedVoidCast(game,target);
      Check(game.ActiveCraftedVfx>0,"Crafted cast impact exists class "+hero);
      bool found=false;foreach(var line in UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))if(line.gameObject.name==forms[hero])found=true;
      Check(found,"Distinct rendered geometry class "+hero);
      Check(UnityEngine.Object.FindObjectsByType<NocturnisSoftVfxSprite>(FindObjectsSortMode.None).Length>0,"Soft volume layers rendered for class "+hero);
      game.ReviewCastVfx(2,game.HeroPosition);Check(game.DefenseReady>0 && game.SkillActiveRemaining(2)>0,"Defense applies real timed status class "+hero);
     }
     // Real casts above leave status/root effects and may alert nearby enemies.
     // Stop those producers before measuring a single effect's lifecycle: otherwise the
     // assertion counts newly generated combat effects instead of the effect under test.
     var lingering=(System.Collections.IList)typeof(KryptaGame).GetField("hazards",Private).GetValue(game);
     foreach(var hazard in lingering){var ring=(GameObject)hazard.GetType().GetField("ring").GetValue(hazard);if(ring)UnityEngine.Object.Destroy(ring);}lingering.Clear();
     CheckHazardHitches(game,lingering);
     foreach(var enemy in game.enemies){enemy.stunUntil=Time.time+120;enemy.alerted=false;enemy.impactAt=0;}
     game.Status.Clear();Call(game,"ResetCraftedVfx");game.ClassSpellVfx(5,game.HeroPosition,5);
     frozenLine=null;foreach(var line in UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))if(line.gameObject.name=="Living sap"){frozenLine=line;break;}
     Check(frozenLine,"Visible root chosen for pause test");game.Pause();frozen=Time.time;frozenColor=frozenLine.startColor;frozenPosition=frozenLine.transform.position;
     waitUntil=EditorApplication.timeSinceStartup+.65;stage=1;
    }
    else if(stage==1 && EditorApplication.timeSinceStartup>=waitUntil)
    {
     Check(Mathf.Abs(Time.time-frozen)<.001f,"Pause freezes scaled VFX clock");
     Check(frozenLine && frozenLine.startColor==frozenColor && frozenLine.transform.position==frozenPosition,"Pause freezes effect appearance");
     game.Resume();waitScaledUntil=Time.time+3;stage=2;
    }
    else if(stage==2 && Time.time>=waitScaledUntil)
    {
     Check(game.ActiveCraftedVfx==0,"Expired VFX objects removed after resume");
     Check(UnityEngine.Object.FindObjectsByType<NocturnisSoftVfxSprite>(FindObjectsSortMode.None).Length==0,"Soft volume layers expire with their cast roots");
     game.ReducedFlashes=true;
     for(int i=0;i<90;i++)game.ClassSpellVfx(2,game.HeroPosition,1.3f);
     Check(game.ActiveCraftedVfx<=game.CraftedVfxBudget,"Burst load stays under root budget");
     var offset=(Vector3)typeof(KryptaGame).GetProperty("CombatCameraOffset",Private).GetValue(game);Check(offset==Vector3.zero,"Reduced flashes disables camera shake");
     game.Status.Clear();waitScaledUntil=Time.time+3;stage=3;
    }
    else if(stage==3 && Time.time>=waitScaledUntil)
    {
     Check(game.ActiveCraftedVfx==0,"Burst load fully expires");Check(!errors,"No Unity runtime errors");
     Finish(true,checks+" assertions: five real class casts, visible 0.85-second singularity warning and single impact, 1.3-second hazard hitches, current-position ticks, distinct geometry, status/cooldown, pause/resume, reduced flashes and bounded lifecycle.");
    }
   }
   catch(Exception e){Finish(false,e.ToString());}
  }
  static void Finish(bool pass,string result)
  {SessionState.SetBool("Nocturnis.CraftedVfxQA",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/crafted-vfx-qa-result.txt",(pass ? "PASS: " : "FAIL: ")+result);Debug.Log(result);EditorApplication.Exit(pass ? 0 : 1);}
 }
}
