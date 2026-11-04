using System;
using System.Collections.Generic;
using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  [Serializable]sealed class Checkpoint
  {public int version=5,hero,rune,zone,coins,level,xp,potions,defeated,weapon,armor,charm,unlocked,cleared,materials,gems,potionRank,edra;public int garb;public int[] gemBag,gearEnchants;public Relic[] equippedRelics;public List<int> sockets;public int[] slain;public List<Relic> inventory;public bool quest;public float elapsed,health,resource,shadow;public int sideAccepted,sideClaimed,clearRewarded;public int[] sideObjects;public string[] brokenContainers,usedShrines;public List<PendingWorldLoot> pendingWorldLoot;public int[] stageEvents;public int stageRewarded;public TalentSave talents;public RecallSave recall;public MarketSave market;public string[] trackedQuestIds;public bool mainActFinished;}
  sealed class RelicDrop { public GameObject root;public Vector3 position;public int slot,rank;public Relic item;public bool blockedNotice; }
  readonly List<RelicDrop> relics=new List<RelicDrop>();
  public static bool Testing;
  static bool QaProfile {get{return Application.isBatchMode || Testing;}}
  static string ProfilePrefix {get{return QaProfile ? "Nocturnis.QA" : "Nocturnis";}}
  static int CurrentProfile {get{return Mathf.Clamp(PlayerPrefs.GetInt(ProfilePrefix+".ActiveProfile",1),1,3);}}
  static string ProfileCheckpointKey(int profile)
  {return QaProfile && profile==1 ? "Nocturnis.QA.Checkpoint" : ProfilePrefix+".Profile"+profile+".Checkpoint";}
  static string CheckpointKey {get{EnsureProfileMigration();return ProfileCheckpointKey(CurrentProfile);}}
  public int ActiveProfile {get{return CurrentProfile;}}
  static void EnsureProfileMigration()
  {
   if(NocturnisAudioShowcase.Requested)return;
   string marker=ProfilePrefix+".ProfileMigrationDone";if(PlayerPrefs.GetInt(marker,0)==1)return;
   string target=ProfileCheckpointKey(1),legacy=QaProfile ? "Nocturnis.QA.LegacyCheckpoint" : "Nocturnis.Checkpoint.v2";
   if(!PlayerPrefs.HasKey(target))
   {
    string json=PlayerPrefs.GetString(legacy,"");
    if(string.IsNullOrEmpty(json) && !QaProfile)json=PlayerPrefs.GetString("Nocturnis.Checkpoint.v1","");
    if(!string.IsNullOrEmpty(json))PlayerPrefs.SetString(target,json);
   }
   // Keep the old key as a recovery copy. The marker prevents a deleted or
   // replaced profile from resurrecting that old journey on a later launch.
   PlayerPrefs.SetInt(marker,1);PlayerPrefs.Save();
  }
  public bool SelectProfile(int profile)
  {
   if(State!=Phase.Title || profile<1 || profile>3)return false;
   EnsureProfileMigration();PlayerPrefs.SetInt(ProfilePrefix+".ActiveProfile",profile);PlayerPrefs.Save();return true;
  }
  public string SavedProfileSummary(int profile)
  {
   var saved=profile>=1 && profile<=3 ? ReadProfileCheckpoint(profile) : null;
   return saved==null ? "Profil "+profile+" • pusta kronika" : "Profil "+profile+" • "+NocturnisData.Heroes[saved.hero].person+" • poz. "+saved.level+"\n"+Campaign.Names[saved.zone];
  }
  public int WeaponRank {get;private set;}
  public int ArmorRank {get;private set;}
  public int CharmRank {get;private set;}
  public bool EquipmentOpen {get;private set;}
  public static bool HasCheckpoint {get{return ReadCheckpoint()!=null;}}
  static readonly string[] GearNames={"Ostrze ostatniej przysięgi","Pancerz pielgrzyma","Pieczęć upadłej gwiazdy"};
  public string GearName(int slot){return slot==0 ? new[]{"Miecz ostatniej przysięgi","Sztylet tętnic","Kusza zmierzchu","Kostur pustki","Topór korzeni"}[SelectedHero] : GearNames[Mathf.Clamp(slot,0,2)];}
  public int GearRank(int slot){return slot==0 ? WeaponRank : slot==1 ? ArmorRank : CharmRank;}
  public int ForgePrice(int slot){return 25+GearRank(slot)*20;}
  static Checkpoint ReadCheckpoint(){return ReadProfileCheckpoint(CurrentProfile);}
  static Checkpoint ReadProfileCheckpoint(int profile)
  {
   try
   {
    EnsureProfileMigration();string json=PlayerPrefs.GetString(ProfileCheckpointKey(profile),"");if(string.IsNullOrEmpty(json))return null;
    var s=JsonUtility.FromJson<Checkpoint>(json);
    if(s==null || s.version<1 || s.version>5 || s.hero<0 || s.hero>4 || s.zone<0 || s.zone>=Campaign.Count || s.level<1 || s.level>100 || s.coins<0 || s.coins>1000000 || s.xp<0 || s.weapon<0 || s.weapon>5 || s.armor<0 || s.armor>5 || s.charm<0 || s.charm>5)return null;
    if(s.version>=4 && (!Finite(s.health) || s.health<=0 || !Finite(s.resource) || !Finite(s.shadow)))return null;
    if(s.version==1){s.unlocked=1|(1<<Campaign.Road)|(1<<s.zone);s.slain=new int[Campaign.Count];s.inventory=new List<Relic>();s.version=2;}
    if(s.slain==null)s.slain=new int[Campaign.Count];
    else if(s.slain.Length!=Campaign.Count){var old=s.slain;s.slain=new int[Campaign.Count];Array.Copy(old,s.slain,Math.Min(old.Length,Campaign.Count));}
    if(s.version<3)
    {
     // Reordered old actors retain their defeats; added actors remain alive.
     // A fully cleared old map remains cleared despite the encounter expansion.
     for(int z=0;z<Campaign.Count;z++)s.slain[z]=(s.cleared&(1<<z))!=0 ? (1<<Campaign.EncounterBudget(z))-1 : Campaign.MigrateLegacyKills(z,s.slain[z]);
     s.clearRewarded=s.cleared;s.version=3;
    }
    if(s.inventory==null)s.inventory=new List<Relic>();
    s.inventory.RemoveAll(r=>r==null || r.slot<0 || r.slot>2 || r.rank<1 || r.rank>5);
    foreach(var item in s.inventory)NormalizeRelic(item);
    if(s.inventory.Count>InventoryCapacity)s.inventory.RemoveRange(InventoryCapacity,s.inventory.Count-InventoryCapacity);
    return s;
   }
   catch(ArgumentException){return null;}
  }
  static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}
  // A checkpoint starts at the zone entrance, with the saved living hero's
  // current vitals. Preview actors and a fatal frame cannot replace that save.
  bool SaveCheckpoint()
  {
   if(NocturnisAudioShowcase.Requested)return false;
   if(State==Phase.Title || State==Phase.Dead || player==null || !player.Root || !world || Health<=0 || !Finite(Health) || !Finite(Resource) || !Finite(Shadow))return false;
   var s=new Checkpoint{hero=SelectedHero,rune=Rune,zone=Zone,coins=Coins,level=Level,xp=Experience,potions=Potions,defeated=Defeated,weapon=WeaponRank,armor=ArmorRank,charm=CharmRank,quest=QuestAccepted,elapsed=Elapsed,health=Health,resource=Resource,shadow=Shadow,unlocked=Unlocked,cleared=Cleared,slain=slain,materials=Materials,gems=Gems,potionRank=PotionRank,edra=EdraChoice,inventory=Inventory,gemBag=GemBag,sockets=SocketedGems,gearEnchants=gearEnchants,equippedRelics=CaptureRelicGear(),garb=Garb,sideAccepted=sideAccepted,sideClaimed=sideClaimed,sideObjects=sideObjects,clearRewarded=clearRewarded,brokenContainers=CaptureBrokenContainers(),usedShrines=CaptureShrineUsage(),pendingWorldLoot=CaptureWorldLoot(),stageEvents=stageEvents,stageRewarded=stageRewarded,talents=CaptureTalents(),recall=CaptureRecall(),market=CaptureMarket(),trackedQuestIds=CaptureQuestTracking(),mainActFinished=JournalActFinished};
   WriteCheckpoint(s);return true;
  }
  [Serializable]sealed class CheckpointVersion {public int version;}
  // Preserve the exact pre-upgrade payload, including unknown fields, once
  // per save slot and legacy schema. Flushing it first keeps rollback data
  // durable before the live key is changed. QA uses its own key namespace.
  static void WriteCheckpoint(Checkpoint data)
  {
   string key=CheckpointKey,next=JsonUtility.ToJson(data),previous=PlayerPrefs.GetString(key,"");
   CheckpointVersion legacy=null;
   if(!string.IsNullOrEmpty(previous))
   {
    try{legacy=JsonUtility.FromJson<CheckpointVersion>(previous);}
    catch(ArgumentException){/* A malformed old payload is not a versioned checkpoint. */}
   }
   if(legacy!=null && legacy.version>=1 && legacy.version<=4)
   {
    string backup=key+".Backup.v"+legacy.version;
    if(!PlayerPrefs.HasKey(backup))
    {
     PlayerPrefs.SetString(backup,previous);PlayerPrefs.Save();
    }
   }
   PlayerPrefs.SetString(key,next);PlayerPrefs.Save();
  }
  // Used by the menu and native window/application exit. Automated reviews
  // already use an isolated checkpoint and must never persist an exit fixture.
  public bool SaveBeforeQuit()
  {
   if(Testing || Application.isBatchMode)return false;
   return SaveCheckpoint();
  }
  void ClearCheckpoint(){PlayerPrefs.DeleteKey(CheckpointKey);PlayerPrefs.Save();}
  public bool ContinueCheckpoint()
  {
   var s=ReadCheckpoint();if(s==null)return false;
   State=Phase.Title;SelectedHero=s.hero;Rune=Mathf.Clamp(s.rune,0,1);Level=s.level;Experience=s.xp;Coins=s.coins;Potions=Mathf.Clamp(s.potions,0,5);Defeated=s.defeated;Elapsed=s.elapsed;QuestAccepted=s.quest;
   WeaponRank=s.weapon;ArmorRank=s.armor;CharmRank=s.charm;Unlocked=s.unlocked|(1<<s.zone);Cleared=s.cleared;slain=s.slain;Materials=Mathf.Clamp(s.materials,0,100000);Gems=Mathf.Clamp(s.gems,0,5);PotionRank=Mathf.Clamp(s.potionRank,0,3);EdraChoice=Mathf.Clamp(s.edra,0,2);Inventory.Clear();Inventory.AddRange(s.inventory);RestoreArtisans(s.gemBag,s.sockets,s.gearEnchants,s.garb,s.gems);RestoreRelicGear(s.equippedRelics);
   RestoreTalents(s.talents);RestoreRecall(s.recall);RestoreMarket(s.market);
   RestoreSideQuests(s.sideAccepted,s.sideClaimed,s.sideObjects,s.clearRewarded);RestoreQuestTracking(s.trackedQuestIds,s.version<5 ? PlayerPrefs.GetInt(ExplorePrefix+"quest",-1) : -1);RestoreJournalActFinished(s.mainActFinished);RestoreStageEvents(s.stageEvents,s.stageRewarded);RestoreBrokenContainers(s.brokenContainers);RestoreShrineUsage(s.usedShrines);RestoreWorldLoot(s.pendingWorldLoot);
   ResetCombatActions();
   bool hasVitals=s.version>=4;
   LoadZone(s.zone);
   // LoadZone creates the hero and applies entrance healing. Restore after
   // that step so reloading never grants healing or refills spent resources.
   Health=hasVitals ? Mathf.Min(MaxHealth,s.health) : MaxHealth;
   Resource=hasVitals ? Mathf.Clamp(s.resource,0,100) : 50;
   Shadow=hasVitals ? Mathf.Clamp(s.shadow,0,100) : 60;
   Resume();
   // Older saves did not record vitals. Give their historical initial values
   // once, then immediately upgrade the checkpoint to the current format.
   if(!hasVitals || s.version<5)SaveCheckpoint();
   ShowNotice("Wczytano początek lokacji • "+NocturnisData.Zones[Zone]);return true;
  }
  public void OpenEquipment(){Pause();TalentsOpen=JournalOpen=BookOpen=false;Service="";EquipmentOpen=true;Sound("triptych");}
  public void OpenJournal(){Pause();TalentsOpen=EquipmentOpen=BookOpen=false;Service="";JournalOpen=true;Sound("triptych");}
  public void SaveAndReturnToTitle(){SaveCheckpoint();Restart();}
  public bool Forge(int slot)
  {
   if(Service!="forge" || slot<0 || slot>2 || GearRank(slot)>=5 || Coins<ForgePrice(slot))return false;
   Coins-=ForgePrice(slot);SetGear(slot,GearRank(slot)+1);SaveCheckpoint();Sound("shrine");return true;
  }
  void SetGear(int slot,int rank){if(slot==0)WeaponRank=rank;else if(slot==1)ArmorRank=rank;else CharmRank=rank;}
  void DropRelic(Vector3 p,int rank)
  {
   int slot=(Defeated/4+Zone)%3;rank=Mathf.Clamp(rank+CampaignDepth/3,1,5);
   CreateRelicDropData(p,RollRelic(slot,rank,Defeated*31+Mathf.RoundToInt(p.x*7+p.z*13)));
  }
  void CreateRelicDrop(Vector3 p,int rank,int slot)
  {CreateRelicDropData(p,new Relic{slot=slot,rank=rank,identified=rank<4});}
  void CreateRelicDropData(Vector3 p,Relic source)
  {
   var item=CopyRelic(source);NormalizeRelic(item);int slot=item.slot,rank=item.rank;
   var root=new GameObject("Relikt: "+GearNames[slot]);root.transform.SetParent(world);root.transform.position=p;
   var crystal=GameObject.CreatePrimitive(PrimitiveType.Cube);crystal.transform.SetParent(root.transform,false);crystal.transform.localPosition=Vector3.up*.6f;crystal.transform.localScale=Vector3.one*.27f;crystal.transform.localRotation=Quaternion.Euler(45,0,45);crystal.GetComponent<Renderer>().sharedMaterial=art.tealMaterial;Destroy(crystal.GetComponent<Collider>());
   Ring(root.transform,.4f,.06f,art.tealMaterial);
   var light=root.AddComponent<LineRenderer>();light.useWorldSpace=false;light.positionCount=2;light.SetPosition(0,Vector3.up*.15f);light.SetPosition(1,Vector3.up*3);light.startWidth=.22f;light.endWidth=.025f;light.startColor=new Color(.3f,1,.8f,.8f);light.endColor=new Color(.3f,1,.8f,0);light.sharedMaterial=art.effectMaterial;
   relics.Add(new RelicDrop{root=root,position=p,slot=slot,rank=rank,item=item});
  }
  void UpdateRelics()
  {
   bool collected=false;
   for(int i=relics.Count-1;i>=0;i--)
   {
    var r=relics[i];r.root.transform.Rotate(0,Time.deltaTime*40,0);
    float d=Vector3.Distance(r.position,HeroPosition);bool full=Inventory.Count>=InventoryCapacity;
    if(d>4)r.blockedNotice=false;
    if(!full && d<4 && Visible(r.position,HeroPosition))r.position=Vector3.MoveTowards(r.position,HeroPosition,7*Time.deltaTime);
    r.root.transform.position=r.position;
    if(d>.8f)continue;
    if(full)
    {
     if(!r.blockedNotice){ShowNotice("Pełny plecak [I] • relikt czeka na ziemi");r.blockedNotice=true;}
     continue;
    }
    bool identified=r.item.identified;
    var worn=EquippedRelic(r.slot);
    // Existing affixes can be a deliberate build choice: compare and equip
    // those upgrades manually instead of replacing a chosen effect by rank.
    if(identified && r.rank>GearRank(r.slot) && worn.affix==0 && worn.legend==0)
    {
     if(worn.rank>0)Inventory.Add(worn);
     EquipRelicData(r.item);Health=Mathf.Min(Health,MaxHealth);ShowNotice("Założono: "+GearName(r.slot)+" • +"+r.rank);Floating(HeroPosition,"RELIKT +"+r.rank,art.tealMaterial.color);
    }
    else{Inventory.Add(CopyRelic(r.item));ShowNotice(identified ? "Relikt w plecaku [I]" : "Nieznany relikt • zanieś Malakorowi");}
    Sound("shrine");Sparks(HeroPosition,art.tealMaterial.color,18);Destroy(r.root);relics.RemoveAt(i);collected=true;
   }
   if(collected)SaveCheckpoint();
  }
 }
}
