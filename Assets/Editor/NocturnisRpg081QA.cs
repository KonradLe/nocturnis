using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Krypta.Editor
{
 // Run with -executeMethod Krypta.Editor.NocturnisRpg081QA.Run -rpg081-editor.
 // This is a PlayMode fixture; no IMGUI drawing method is called by the test.
 public static class NocturnisRpg081QA
 {
  const string Session="Nocturnis.Rpg081",SnapshotKey=Session+".Preferences";
  const string CheckpointKey="Nocturnis.QA.Profile3.Checkpoint";
  const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
  const BindingFlags S=BindingFlags.Static|BindingFlags.NonPublic;
  static int stage,checks;
  static double start;
  static string error="";
  static bool finishing;
  static readonly List<string> evidence=new List<string>();
  [Serializable]sealed class Report
  {public bool passed;public int assertions;public string error;public string[] evidence;}
  [Serializable]sealed class Preference
  {public string key,value;public bool exists,isInt;public int number;}
  [Serializable]sealed class Preferences
  {public List<Preference> entries=new List<Preference>();}
  [Serializable]sealed class SavedInventory
  {public List<KryptaGame.Relic> inventory;}
  [Serializable]sealed class Bag
  {public List<KryptaGame.Relic> items;}

  [InitializeOnLoadMethod]
  static void Restore()
  {
   if(!SessionState.GetBool(Session,false))return;
   KryptaGame.Testing=true;
   Attach();
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void BeforeScene()
  {
   if(SessionState.GetBool(Session,false) ||
      Array.IndexOf(Environment.GetCommandLineArgs(),"-rpg081-editor")>=0)
    KryptaGame.Testing=true;
  }
  public static void Run()
  {
   KryptaGame.Testing=true;stage=checks=0;error="";finishing=false;evidence.Clear();
   CapturePreferences();
   PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",3);
   PlayerPrefs.SetInt("Nocturnis.QA.ProfileMigrationDone",1);PlayerPrefs.Save();
   SessionState.SetBool(Session,true);
   Attach();
   EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");
   EditorApplication.isPlaying=true;
  }
  static void Attach()
  {
   start=EditorApplication.timeSinceStartup;
   EditorApplication.update-=Tick;EditorApplication.update+=Tick;
   Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
  }
  static void Log(string message,string stack,LogType type)
  {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)error=message+"\n"+stack;}
  static void Check(bool condition,string label)
  {checks++;if(!condition)throw new Exception(label);}
  static object Call(object target,string name,params object[] args)
  {
   var method=target.GetType().GetMethod(name,P);
   if(method==null)throw new MissingMethodException(target.GetType().Name,name);
   return method.Invoke(target,args);
  }
  static object Field(object target,string name)
  {return target.GetType().GetField(name,P).GetValue(target);}
  static void Field(object target,string name,object value)
  {target.GetType().GetField(name,P).SetValue(target,value);}
  static void Set(KryptaGame game,string name,object value)
  {typeof(KryptaGame).GetProperty(name).SetValue(game,value);}
  static string Relic(KryptaGame.Relic item){return JsonUtility.ToJson(item);}
  static string Inventory(KryptaGame game){return JsonUtility.ToJson(new Bag{items=game.Inventory});}
  static string Gear(KryptaGame game)
  {return Relic(game.EquippedRelic(0))+Relic(game.EquippedRelic(1))+Relic(game.EquippedRelic(2));}

  static void Tick()
  {
   if(finishing)return;
   if(EditorApplication.timeSinceStartup-start>300){Finish(false,"Timeout waiting for RPG081 fixture");return;}
   if(!EditorApplication.isPlaying)return;
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();
   if(!game || !game.View)return;
   var hud=game.GetComponent<KryptaHUD>();
   if(!hud || Field(hud,"game")==null)return;
   try
   {
    if(error.Length>0)throw new Exception(error);
    Check(KryptaGame.Testing,"QA namespace must be enabled before the fixture");
    Check(game.ActiveProfile==3,"QA must use its isolated third profile");
    game.enabled=false;hud.enabled=false;Time.timeScale=0;
    switch(stage)
    {
     case 0: Atlas();Geometry();break;
     case 1: SelectionAndEquipment(game,hud);break;
     case 2: InventoryProtection(game);break;
     case 3: ClassIcons(game,hud);break;
     case 4: TalentSelection(game,hud);break;
     default:Finish(true,"");return;
    }
    stage++;
   }
   catch(Exception exception){Finish(false,exception.ToString());}
  }
  static void Fixture(KryptaGame game)
  {
   game.Resume();
   Set(game,"State",KryptaGame.Phase.Title);
   Call(game,"ResetCampaign");
   Set(game,"Level",8);
   Set(game,"Unlocked",(1<<Campaign.Town)|(1<<Campaign.Road)|(1<<Campaign.Crypt));
   Call(game,"LoadZone",Campaign.Town);
   Set(game,"State",KryptaGame.Phase.Playing);
   Set(game,"Health",game.MaxHealth);Set(game,"Coins",1000);
   Field(game,"introUntil",0f);game.Status.Clear();Time.timeScale=0;
  }
  static void Atlas()
  {
   const string path="Assets/Resources/UI/RpgItemAtlas.png";
   var atlas=Resources.Load<Texture2D>("UI/RpgItemAtlas");
   Check(atlas!=null,"RPG item atlas must load from Resources");
   Check(atlas.width==1254 && atlas.height==1254,"Imported RPG atlas must retain its 1254 square resolution");
   var importer=AssetImporter.GetAtPath(path) as TextureImporter;
   Check(importer!=null,"Atlas must have a TextureImporter");
   Check(!importer.mipmapEnabled && atlas.mipmapCount==1,"Interface atlas must have no mipmaps");
   Check(importer.npotScale==TextureImporterNPOTScale.None,"NPOT atlas must not be rescaled");
   Check(importer.DoesSourceTextureHaveAlpha() && importer.alphaIsTransparency,
    "Atlas must preserve source transparency");
   var decoded=new Texture2D(2,2,TextureFormat.RGBA32,false);
   try
   {
    Check(ImageConversion.LoadImage(decoded,File.ReadAllBytes(path),false),"Atlas PNG must decode");
    bool transparent=false,visible=false;
    foreach(var pixel in decoded.GetPixels32())
    {if(pixel.a<16)transparent=true;if(pixel.a>239)visible=true;if(transparent && visible)break;}
    Check(transparent && visible,"Atlas must contain both transparent background and opaque icon pixels");
   }
   finally{UnityEngine.Object.DestroyImmediate(decoded);}
   evidence.Add("Resource atlas: 1254 x 1254, real alpha, NPOT preserved, no mipmaps.");
  }
  static bool Inside(Rect outer,Rect inner)
  {return inner.width>0 && inner.height>0 && inner.xMin>=outer.xMin && inner.yMin>=outer.yMin && inner.xMax<=outer.xMax && inner.yMax<=outer.yMax;}
  static void Rectangles(List<Rect> rectangles,Rect bounds,string label)
  {
   Rect screen=new Rect(0,0,1280,720);
   for(int i=0;i<rectangles.Count;i++)
   {
    Check(Inside(screen,rectangles[i]) && Inside(bounds,rectangles[i]),label+" "+i+" must remain inside its panel and 1280 x 720");
    for(int j=0;j<i;j++)Check(!rectangles[i].Overlaps(rectangles[j]),label+" "+i+" must not overlap "+j);
   }
  }
  static void Geometry()
  {
   Check(KryptaGame.InventoryCapacity==24,"Backpack uses exactly 24 actual inventory slots");
   var bag=new List<Rect>();for(int i=0;i<24;i++)bag.Add(KryptaHUD.RpgInventorySlotBounds(i));
   Rectangles(bag,new Rect(464,101,408,485),"Backpack slot");
   for(int i=0;i<24;i++)
   {
    Check(Mathf.Approximately(bag[i].x,bag[i%6].x),"Backpack columns align");
    Check(Mathf.Approximately(bag[i].y,bag[(i/6)*6].y),"Backpack rows align");
   }
   var worn=new List<Rect>();for(int i=0;i<3;i++)worn.Add(KryptaHUD.RpgEquippedSlotBounds(i));
   Rectangles(worn,new Rect(36,101,410,485),"Equipped slot");
   foreach(var a in worn)foreach(var b in bag)Check(!a.Overlaps(b),"Equipped and backpack slots must be separate");
   var nodeMethod=typeof(KryptaHUD).GetMethod("TalentNodeRect",S);
   Check(nodeMethod!=null,"Talent node layout helper must exist");
   var nodes=new List<Rect>();for(int i=0;i<9;i++)nodes.Add((Rect)nodeMethod.Invoke(null,new object[]{i}));
   Rectangles(nodes,new Rect(73,139,752,477),"Talent node");
   for(int branch=0;branch<3;branch++)
    Check(nodes[branch*3].center.y>nodes[branch*3+1].center.y &&
          nodes[branch*3+1].center.y>nodes[branch*3+2].center.y,
          "Talent branch "+branch+" progresses from bottom to top");
   evidence.Add("Layout bounds: all 24 backpack cells, 3 actual equipment slots and 9 talent nodes; no overlap in the 1280 x 720 design space.");
  }
  static void SelectionAndEquipment(KryptaGame game,KryptaHUD hud)
  {
   Fixture(game);game.OpenEquipment();
   var old=new KryptaGame.Relic{slot=0,rank=2,enchant=1,affix=2,affixValue=3,protectedItem=true};
   var next=new KryptaGame.Relic{slot=0,rank=4,enchant=2,affix=1,affixValue=4,legend=1,legendHero=game.SelectedHero,protectedItem=true};
   var unknown=new KryptaGame.Relic{slot=1,rank=4,identified=false};
   Call(game,"EquipRelicData",old);game.Inventory.Add(next);game.Inventory.Add(unknown);
   int gold=game.Coins;string beforeGear=Gear(game),beforeBag=Inventory(game);
   Field(hud,"inspectGround",true);Field(hud,"inspectedEquipmentSlot",2);
   Call(hud,"SelectRpgInventoryItem",0);
   Check((int)Field(hud,"equipmentSelection")==0 && !(bool)Field(hud,"inspectGround") &&
         (int)Field(hud,"inspectedEquipmentSlot")==-1,"Bag click changes selection mode only");
   Check(game.Coins==gold && Gear(game)==beforeGear && Inventory(game)==beforeBag,
    "Bag selection must never equip, remove an item or spend currency");
   Call(hud,"SelectRpgEquippedItem",2);
   Check((int)Field(hud,"inspectedEquipmentSlot")==2 && !(bool)Field(hud,"inspectGround"),
    "Worn click selects the actual equipped slot");
   Check(!(bool)Call(hud,"EquipRpgInventorySelection"),"Worn inspection cannot trigger bag Equip");
   Check(game.Coins==gold && Gear(game)==beforeGear && Inventory(game)==beforeBag,
    "Worn selection cannot mutate equipment, backpack or currency");
   Call(hud,"SelectRpgEquippedItem",3);
   Check((int)Field(hud,"inspectedEquipmentSlot")==2,"Invalid worn slot preserves selection");
   Call(hud,"SelectRpgInventoryItem",0);Call(hud,"SelectRpgInventoryItem",-1);Call(hud,"SelectRpgInventoryItem",24);
   Check((int)Field(hud,"equipmentSelection")==0,"Invalid bag indices preserve selection");
   Check((bool)Call(hud,"EquipRpgInventorySelection"),"Explicit Equip accepts selected identified item");
   Check(Relic(game.EquippedRelic(0))==Relic(next),"Equipping preserves rank, enchant, affix, legend and protection");
   Check(game.Inventory.Count==2 && object.ReferenceEquals(game.Inventory[0],unknown) &&
         Relic(game.Inventory[1])==Relic(old),"Equip returns the previous worn item once, without losing metadata");
   Check(game.Coins==gold && (int)Field(hud,"inspectedEquipmentSlot")==0,"Equip spends no coins and focuses the worn slot");
   string afterGear=Gear(game),afterBag=Inventory(game);
   Check(!(bool)Call(hud,"EquipRpgInventorySelection") && Gear(game)==afterGear && Inventory(game)==afterBag,
    "Repeated Equip without another selection cannot duplicate or swap back");
   Call(hud,"SelectRpgInventoryItem",0);
   Check(!(bool)Call(hud,"EquipRpgInventorySelection") && Gear(game)==afterGear && Inventory(game)==afterBag,
    "Unidentified item cannot equip");
   Call(hud,"SelectRpgInventoryItem",1);Field(hud,"inspectGround",true);
   Check(!(bool)Call(hud,"EquipRpgInventorySelection"),"Ground comparison cannot equip a stale bag selection");
   Field(hud,"inspectGround",false);game.Resume();
   Check(!(bool)Call(hud,"EquipRpgInventorySelection"),"Closed equipment screen rejects Equip");
   game.OpenEquipment();Call(hud,"SelectRpgInventoryItem",1);game.Inventory.Clear();
   Field(hud,"equipmentSelection",23);Field(hud,"groundSelection",9);Field(hud,"inspectGround",true);
   Call(hud,"NormalizeRpgInventorySelection",0);
   Check((int)Field(hud,"equipmentSelection")==0 && (int)Field(hud,"groundSelection")==0 && !(bool)Field(hud,"inspectGround"),
    "Empty backpack and ground lists reset stale cursors safely");
   Check(!(bool)Call(hud,"EquipRpgInventorySelection") && game.Inventory.Count==0,"Empty selection cannot equip");
   evidence.Add("Bag and worn clicks are inspection only; explicit Equip swaps exactly once, preserves every item field, and rejects stale, unknown, empty or closed-screen selections.");
  }
  static void InventoryProtection(KryptaGame game)
  {
   Fixture(game);
   var item=new KryptaGame.Relic{slot=1,rank=1,enchant=1,affix=2,affixValue=4};
   game.Inventory.Add(item);Call(game,"SaveCheckpoint");
   string saved=PlayerPrefs.GetString(CheckpointKey,"");
   Check(!game.ToggleInventoryLock(0) && !item.protectedItem &&
         PlayerPrefs.GetString(CheckpointKey,"")==saved,"Playing-state lock attempt is rejected without a save mutation");
   game.Pause();
   Check(!game.ToggleInventoryLock(0) && !item.protectedItem,"Paused without equipment cannot toggle protection");
   game.OpenEquipment();
   Check(!game.ToggleInventoryLock(-1) && !game.ToggleInventoryLock(1),"Protection rejects out-of-range indices");
   game.Inventory.Add(null);
   Check(!game.ToggleInventoryLock(1),"Protection rejects null inventory slots");
   game.Inventory.RemoveAt(1);
   Set(game,"State",KryptaGame.Phase.Dead);
   Check(!game.ToggleInventoryLock(0) && !item.protectedItem,"A stale equipment flag cannot protect during a forbidden game phase");
   Set(game,"State",KryptaGame.Phase.Paused);
   int gold=game.Coins,materials=game.Materials;
   Check(game.ToggleInventoryLock(0) && item.protectedItem,"Equipment protection toggles on");
   var checkpoint=JsonUtility.FromJson<SavedInventory>(PlayerPrefs.GetString(CheckpointKey,""));
   Check(checkpoint!=null && checkpoint.inventory!=null && checkpoint.inventory.Count==1 &&
         checkpoint.inventory[0].protectedItem,"Protection writes a durable checkpoint immediately");
   Check(game.ContinueCheckpoint() && game.Inventory.Count==1 && game.Inventory[0].protectedItem,
    "Protection survives the real checkpoint load path");
   Time.timeScale=0;
   Call(game,"Talk","QA","QA","market");
   Check(!game.SellMarketItem(0) && game.Inventory.Count==1 && game.Coins==gold,
    "Protected inventory cannot be sold");
   Check(game.SellCommonMarketItems()==0 && game.Inventory.Count==1,"Bulk sale cannot remove a protected relic");
   Call(game,"Talk","QA","QA","forge");
   Check(!game.Salvage(0) && game.Inventory.Count==1 && game.Materials==materials,
    "Protected inventory cannot be salvaged");
   game.Resume();game.OpenEquipment();
   Check(game.ToggleInventoryLock(0) && !game.Inventory[0].protectedItem,"The same equipment action removes protection");
   checkpoint=JsonUtility.FromJson<SavedInventory>(PlayerPrefs.GetString(CheckpointKey,""));
   Check(checkpoint!=null && !checkpoint.inventory[0].protectedItem,"Removing protection is saved too");
   Call(game,"Talk","QA","QA","forge");
   Check(game.Salvage(0) && game.Inventory.Count==0 && game.Materials==materials+3,
    "An explicitly unprotected item can still be salvaged through the existing service");
   evidence.Add("Inventory protection: phase/index/null guards, immediate durable save, real reload, merchant/bulk-sale/salvage prevention and explicit unlock.");
  }
  static void ClassIcons(KryptaGame game,KryptaHUD hud)
  {
   int prior=game.SelectedHero;
   int[] armor={5,5,6,7,6},charm={8,8,8,9,10};
   // Only the class field is varied: this tests mapping without rebuilding five preview actors.
   for(int hero=0;hero<5;hero++)
   {
    Set(game,"SelectedHero",hero);
    Check((int)Call(hud,"RpgRelicAtlasIndex",new KryptaGame.Relic{slot=0})==hero,"Hero "+hero+" gets its weapon icon");
    Check((int)Call(hud,"RpgRelicAtlasIndex",new KryptaGame.Relic{slot=1})==armor[hero],"Hero "+hero+" gets its armor icon");
    Check((int)Call(hud,"RpgRelicAtlasIndex",new KryptaGame.Relic{slot=2})==charm[hero],"Hero "+hero+" gets its charm icon");
   }
   Set(game,"SelectedHero",prior);
   evidence.Add("All five classes map to their own weapon icons (0-4), with corresponding armor and charm icon families.");
  }
  static void TalentSelection(KryptaGame game,KryptaHUD hud)
  {
   Fixture(game);game.OpenTalents();
   string before=JsonUtility.ToJson(game.CaptureTalents());int points=game.TalentPoints,gold=game.Coins;
   for(int node=0;node<9;node++)
   {
    Call(hud,"SelectTalentNode",node);
    Check((int)Field(hud,"talentSelection")==node,"Talent click selects node "+node);
    Check(game.TalentPoints==points && game.Coins==gold && JsonUtility.ToJson(game.CaptureTalents())==before,
     "Talent selection must not spend points, learn ranks or spend currency");
   }
   Call(hud,"SelectTalentNode",-1);Call(hud,"SelectTalentNode",9);
   Check((int)Field(hud,"talentSelection")==8,"Invalid talent nodes leave the current selection intact");
   evidence.Add("Talent node selection (all nine nodes) is separate from learning; no hidden currency or point expenditure.");
  }
  static void CapturePreferences()
  {
   var snapshot=new Preferences();
   foreach(string prefix in new[]{"Nocturnis.QA","Nocturnis"})
   {
    foreach(string tail in new[]{".ActiveProfile",".ProfileMigrationDone"})
    {
     string key=prefix+tail;
     snapshot.entries.Add(new Preference{key=key,isInt=true,exists=PlayerPrefs.HasKey(key),number=PlayerPrefs.GetInt(key,0)});
    }
    foreach(string tail in new[]{".Checkpoint",".Profile1.Checkpoint",".Profile2.Checkpoint",".Profile3.Checkpoint"})
     foreach(string suffix in new[]{"",".Backup.v1",".Backup.v2",".Backup.v3",".Backup.v4"})
     {
      string key=prefix+tail+suffix;
      snapshot.entries.Add(new Preference{key=key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key,"")});
     }
   }
   SessionState.SetString(SnapshotKey,JsonUtility.ToJson(snapshot));
  }
  static void RestorePreferences()
  {
   string raw=SessionState.GetString(SnapshotKey,"");
   if(string.IsNullOrEmpty(raw))throw new Exception("QA preference snapshot is missing");
   var snapshot=JsonUtility.FromJson<Preferences>(raw);
   bool productionUnchanged=true;
   foreach(var p in snapshot.entries)
   {
    if(!p.key.StartsWith("Nocturnis.QA.",StringComparison.Ordinal))
    {
     productionUnchanged&=PlayerPrefs.HasKey(p.key)==p.exists &&
      (p.isInt ? PlayerPrefs.GetInt(p.key,0)==p.number : PlayerPrefs.GetString(p.key,"")==p.value);
     continue;
    }
    if(!p.exists)PlayerPrefs.DeleteKey(p.key);
    else if(p.isInt)PlayerPrefs.SetInt(p.key,p.number);
    else PlayerPrefs.SetString(p.key,p.value);
   }
   PlayerPrefs.Save();SessionState.EraseString(SnapshotKey);
   Check(productionUnchanged,"Production save keys and profile selection must remain unchanged");
   evidence.Add("Production save keys unchanged; prior QA checkpoints, backups and profile preferences restored.");
  }
  static void Finish(bool passed,string message)
  {
   if(finishing)return;finishing=true;
   SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
   try{RestorePreferences();}
   catch(Exception exception){passed=false;message+="\n"+exception;}
   Directory.CreateDirectory("Logs");
   File.WriteAllText("Logs/rpg081-qa.json",JsonUtility.ToJson(new Report{passed=passed,assertions=checks,error=message,evidence=evidence.ToArray()},true));
   if(!passed)Debug.LogError(message);
   Time.timeScale=1;EditorApplication.Exit(passed?0:1);
  }
 }
}
