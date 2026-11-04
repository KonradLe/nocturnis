using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Krypta
{
 public sealed partial class KryptaGame : MonoBehaviour
 {
  public enum Phase { Title, Playing, Paused, Dialogue, Dead, Won }
  public Phase State { get; private set; }
  public int SelectedHero { get; private set; }
  public int Rune { get; set; }
  public int Zone { get; private set; }
  public int Coins { get; private set; }
  public int Level { get; private set; } = 1;
  public int Experience { get; private set; }
  public int Potions { get; private set; } = 3;
  public int Defeated { get; private set; }
  public float Health { get; private set; }
  public float Resource { get; private set; } = 50;
  public float MaxHealth { get { return Hero.health+(Level-1)*15+EnchantBonus(2)+TalentHealthBonus; } }
  public HeroClass Hero { get { return NocturnisData.Heroes[SelectedHero]; } }
  public float PulseReady { get { return Mathf.Max(0,pulseAt-Time.time); } }
  public float DashReady { get { return Mathf.Max(0,dashAt-Time.time); } }
  public float DefenseReady { get { return Mathf.Max(0,defenseAt-Time.time); } }
  public int Remaining { get { int n=0;foreach(var e in enemies)if(e.hp>0)n++;return n; } }
  public float Elapsed { get; private set; }
  public Camera View { get; private set; }
  public string Notice { get; private set; }
  public float NoticeUntil { get; private set; }
  public string Interaction { get; private set; }
  public string DialogueName { get; private set; }
  public string DialogueText { get; private set; }
  public bool QuestAccepted { get; private set; }
  public bool JournalOpen { get; private set; }
  public Vector3 HeroPosition { get { return player==null ? Vector3.zero : player.Root.transform.position; } }
  public Vector3 ObjectivePosition { get { if(Campaign.EventStage(Zone) && !StageObjectivesComplete)return ActiveStageObjectivePosition;return Zone==0 && !QuestAccepted ? World(15,8) : exit; } }
  public bool MapFloor(int x,int z) { return grid!=null && grid.Walkable(x,z); }
  public string Objective { get { return CampaignObjective; } }
  public readonly List<Enemy> enemies=new List<Enemy>();
  public readonly StatusBook Status=new StatusBook();
  public sealed class Enemy
  {
   public ActorVisual visual;
   public float hp,maxHp,attackAt,impactAt,slowUntil,stunUntil,armorUntil,spellAt;
   public bool boss,elite,zombie,alerted,mutated,sacrificial,rewarded;
   public int archetype,spawnId; public Vector3 corpse; public float corpseHideAt; public float specialAt,chargeAt; public Vector3 chargeTarget;
   public string label;
   public GameObject warning;
   public Vector3 Position { get { return visual.Root.transform.position; } }
  }
  const float Tile=1.8f;
  DungeonGrid grid,playerRoute;
  KryptaAssets art;
  ActorVisual player;
  Transform world;
  Vector3 destination,facing=Vector3.forward,dashDirection;
  bool hasDestination;
  Enemy target;
  float strikeAt,pulseAt,dashAt,defenseAt,dashEnd,hurtUntil,navAt,graceAt;
  int killStreak;
  float introUntil;
  float cameraZoom=8f;
  public bool IntroActive {get{return introUntil>Time.unscaledTime;}}
  Material classMaterial;
  GameObject aura;
  Vector3 exit;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Bootstrap() { if (!FindFirstObjectByType<KryptaGame>()) new GameObject("Nocturnis").AddComponent<KryptaGame>(); }
  void Start()
  {
   if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-masonry-review")>=0 || System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-masonry-editor-qa")>=0 || System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-gothic-ui-review")>=0 || System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-development080-editor")>=0 || System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-rpg081-editor")>=0 || System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-portrait082-editor")>=0)Testing=true;
   Time.timeScale=1; art=Resources.Load<KryptaAssets>("KryptaAssets");
   ReducedFlashes=PlayerPrefs.GetInt("Nocturnis.ReducedFlashes",1)==1;
   NocturnisSettings.Ensure(gameObject);
   if (!art || art.version<5 || art.heroes==null || art.heroes.Length!=5)
   { Debug.LogError("Przygotuj projekt: Krypta > Przygotuj projekt."); enabled=false; return; }
   classMaterial=new Material(art.goldMaterial);
   if(!Application.isBatchMode){audioSystem=gameObject.AddComponent<NocturnisAudio>();musicSystem=gameObject.AddComponent<NocturnisMusic>();}
   CreateCamera(); LoadZone(Campaign.Road); gameObject.AddComponent<KryptaHUD>(); State=Phase.Title; Time.timeScale=0;
   TryStartCryptPreview();
   if(State==Phase.Title)NocturnisCinematic.TryPlay(this,null);
  }
  public void SelectHero(int index)
  {
   if (State!=Phase.Title || index<0 || index>=5) return;
   SelectedHero=index; Rune=0; CreateHero();
  }
  void CreateHero()
  {
   CancelCombatAction();
   if (player!=null) { player.Root.SetActive(false); Destroy(player.Root); }
   var h=art.heroes[SelectedHero];
   player=new ActorVisual(h.model,h.animations,SpawnPosition,1.8f,"Bohater");
   player.Root.transform.SetParent(world); player.Equip(h.weapon);ApplyGarb(player.Root,Garb); classMaterial.color=Hero.color;
   buffHalo=null;
   Ring(player.Root.transform,.55f,.045f,classMaterial);
   aura=Ring(player.Root.transform,.85f,.065f,art.goldMaterial); aura.SetActive(false); Health=MaxHealth;
  }
  static Vector3 World(int x,int z) { return new Vector3(x*Tile,0,z*Tile); }
  static int Cell(float x) { return Mathf.FloorToInt(x/Tile+.5f); }
  void Update()
  {
   if(NocturnisCinematic.BlocksInput)return;
   if(CryptPreviewInput())return;
   if (player==null) return;
   var hud=GetComponent<KryptaHUD>();if(hud && hud.HandleMenuInput())return;
   if(introUntil>0)
   {if(IntroActive && !Input.GetKeyDown(KeyCode.Space) && !Input.GetKeyDown(KeyCode.Escape))return;introUntil=0;Time.timeScale=1;}
   if (Input.GetKeyDown(KeyCode.Escape))
   { if (State==Phase.Playing) Pause(); else if (State==Phase.Paused || State==Phase.Dialogue) Resume(); }
   if (NocturnisInput.Down("journal") && (State==Phase.Playing || State==Phase.Paused))
   { if (JournalOpen) Resume(); else OpenJournal(); }
   if(NocturnisInput.Down("inventory") && (State==Phase.Playing || State==Phase.Paused))
   {if(EquipmentOpen)Resume();else OpenEquipment();}
   if(NocturnisInput.Down("talents") && (State==Phase.Playing || State==Phase.Paused)){if(TalentsOpen)Resume();else OpenTalents();}
   if(NocturnisInput.Down("mute"))ToggleAudio();
   if(NocturnisInput.Down("book") && (State==Phase.Playing || State==Phase.Paused)){if(BookOpen)Resume();else OpenBook();}
   if(NocturnisInput.Down("town") && State==Phase.Playing && Zone!=0 && (Unlocked & 1)!=0)ReturnToTown();
   if (State!=Phase.Playing) return;
   TickQuestJournal();if(TickRecall())return;
   Elapsed+=Time.deltaTime; Status.Expire(Time.time);UpdateQualityOfLife();
   if (SelectedHero==2 || SelectedHero==4) Resource=Mathf.Min(100,Resource+Time.deltaTime*(SelectedHero==2 ? 8 : 4));
   aura.SetActive(Status.Entries.Count>0); aura.transform.localRotation=Quaternion.Euler(0,Time.time*60,0);
   Color auraColor=Status.Count("ichor",Time.time)>0 ? art.violetMaterial.color : Status.Count("slaughter",Time.time)>0 ? art.redMaterial.color : Status.Count("grace",Time.time)>0 ? art.goldMaterial.color : classMaterial.color;
   aura.GetComponent<LineRenderer>().startColor=aura.GetComponent<LineRenderer>().endColor=auraColor;
   aura.transform.localScale=Vector3.one*(1+Mathf.Sin(Time.time*5)*.1f);
   UpdateInteraction();
   if (NocturnisInput.Down("interact")) { Interact(); if (State!=Phase.Playing) return; }
   UpdatePlayer();TickMeleeActions(Time.time);
   if (Time.time>=navAt) { grid.BuildDistances(Cell(player.Root.transform.position.x),Cell(player.Root.transform.position.z)); navAt=Time.time+.2f; }
   for(int i=enemies.Count-1;i>=0;i--) UpdateEnemy(enemies[i]);
   UpdateCampaign();
   UpdatePickups(); UpdateRelics(); UpdateContainerLoot(); UpdateEffects(); UpdateHazards();
  }
  void LateUpdate()
  {
   if (player==null || !View) return;
   if(State==Phase.Dead)player.AdvancePausedAnimation(Time.unscaledDeltaTime);
   Vector3 hero=player.Root.transform.position;
   Shader.SetGlobalVector("_NocturnisHero",new Vector4(hero.x,hero.y,hero.z,State==Phase.Title ? 0 : 1));
   View.enabled=true;
   View.cullingMask=State==Phase.Title ? 0 : ~(1<<30);
   Vector3 desired=player.Root.transform.position+new Vector3(9,15,-11);
   float intro=IntroActive ? Mathf.Clamp01((introUntil-Time.unscaledTime)/3) : 0;
   if(State==Phase.Playing && !OverHUD())cameraZoom=Mathf.Clamp(cameraZoom-Input.mouseScrollDelta.y*.65f,6.5f,11f);
   View.orthographicSize=Mathf.Lerp(cameraZoom,19,Mathf.SmoothStep(0,1,intro));desired+=Vector3.up*(intro*12);
   if(PlayerPrefs.GetInt("Nocturnis.CameraShake",1)==1)desired+=CombatCameraOffset;
   View.transform.position=Vector3.Lerp(View.transform.position,desired,1-Mathf.Exp(-8*Time.unscaledDeltaTime));
  }
  bool OverHUD()
  {
   float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
   float y=(Screen.height-Input.mousePosition.y-(Screen.height-720*scale)*.5f)/scale;
   float x=(Input.mousePosition.x-(Screen.width-1280*scale)*.5f)/scale;
   return (TutorialEnabled && TutorialStep<6 && !Testing && new Rect(20,358,330,158).Contains(new Vector2(x,y))) || (!string.IsNullOrEmpty(SideMechanicHint) && new Rect(360,110,550,60).Contains(new Vector2(x,y))) || KryptaHUD.BeltContains(new Vector2(x,y)) || y<100 || new Rect(24,22,385,128).Contains(new Vector2(x,y)) || (x>1115 && y<335) || (TrackedQuestCount>0 && new Rect(1000,342,255,34+50*TrackedQuestCount).Contains(new Vector2(x,y))) || new Rect(945,535,310,28).Contains(new Vector2(x,y)) || new Rect(24,535,254,28).Contains(new Vector2(x,y));
  }
  void UpdatePlayer()
  {
   if(RecallActive){player.Tick(false);return;}
   Vector3 pos=player.Root.transform.position,aim=pos+facing;float navigationLimit=float.PositiveInfinity;
   float hit; Ray ray=View.ScreenPointToRay(Input.mousePosition);
   if (new Plane(Vector3.up,Vector3.zero).Raycast(ray,out hit)) aim=ray.GetPoint(hit);
   bool click=Input.GetMouseButton(0) && !OverHUD();
   if (click)
   {
    target=FindEnemyAtAim(aim);
    propTarget=target==null ? FindBreakable(aim) : null;
    if(propTarget!=null)hasDestination=false;
    if (target==null && propTarget==null && grid.Walkable(Cell(aim.x),Cell(aim.z)))
    { destination=grid.CanStand(aim.x/Tile,aim.z/Tile,.32f/Tile) ? aim : World(Cell(aim.x),Cell(aim.z)); hasDestination=true; playerRoute.BuildDistances(Cell(aim.x),Cell(aim.z)); }
    else if (target!=null) hasDestination=false;
   }
   Vector3 right=View.transform.right; right.y=0; right.Normalize();
   Vector3 forward=Vector3.Cross(right,Vector3.up),move=Vector3.zero;
   if (NocturnisInput.Held("up") || Input.GetKey(KeyCode.UpArrow)) move+=forward;
   if (NocturnisInput.Held("down") || Input.GetKey(KeyCode.DownArrow)) move-=forward;
   if (NocturnisInput.Held("right") || Input.GetKey(KeyCode.RightArrow)) move+=right;
   if (NocturnisInput.Held("left") || Input.GetKey(KeyCode.LeftArrow)) move-=right;
   bool keyboard=move.sqrMagnitude>0;
   if(NocturnisInput.Down("dodge"))BeginPlayerDodge(keyboard ? move : facing);
   else if(NocturnisInput.Down("defense") || Input.GetKeyDown(KeyCode.Alpha1))CastDefense();
   else if(NocturnisInput.Down("extra2"))CastAdditional(2,aim);
   else if(NocturnisInput.Down("extra1"))CastAdditional(1,aim);
   else if(NocturnisInput.Down("extra0"))CastAdditional(0,aim);
   else if(ShouldCastPrimary(NocturnisInput.Down("skill"),Input.GetMouseButtonDown(1),OverHUD()))CastSkill(aim);
   if (keyboard) { if (click) Strike(aim); hasDestination=false; target=null; propTarget=null; move.Normalize(); }
   else if(propTarget!=null && !propTarget.broken)
   {if(Vector3.Distance(pos,propTarget.p)<=Hero.range+.3f && PropVisible(pos,propTarget))Strike(propTarget.p);else move=ApproachProp(propTarget);}
   else if (target!=null && target.hp>0)
   {
    if (Vector3.Distance(pos,target.Position)>Hero.range-.3f || !Visible(pos,target.Position))
    { playerRoute.BuildDistances(Cell(target.Position.x),Cell(target.Position.z)); Vector3 waypoint=RouteWaypoint(playerRoute,pos,target.Position);move=(waypoint-pos).normalized;navigationLimit=Vector3.Distance(pos,waypoint); }
    else Strike(target.Position);
   }
   else if (hasDestination)
   { if (Vector3.Distance(pos,destination)<.2f) hasDestination=false; else {Vector3 waypoint=RouteWaypoint(playerRoute,pos,destination);move=(waypoint-pos).normalized;navigationLimit=Vector3.Distance(pos,waypoint);} }
   if (Input.GetKey(KeyCode.LeftShift) && click) { move=Vector3.zero; hasDestination=false; Strike(aim); }
   if(NocturnisInput.Down("potion"))UsePotion();
   if (Time.time<dashEnd){move=dashDirection*3.7f;navigationLimit=float.PositiveInfinity;}
   MovePlayer(move,Time.deltaTime,navigationLimit);
  }
  // Human control and opt-in QA use the same status, windup, facing and collision rules.
  public float PlayerMovementSpeed
  {get
  {
   float speed=Hero.speed*TalentMoveMultiplier*(1+.15f*Status.Count("grace",Time.time));
   if (Status.Count("slow",Time.time)>0) speed*=.5f;
   if (Status.Count("shadow",Time.time)>0) speed*=1.35f;
   return speed*(MeleeWindupActive || PowerWindupActive || (player!=null && player.MovementCommitted) ? 0 : 1)*(CarryingEmber ? .8f : 1);
  }}
  void MovePlayer(Vector3 move,float deltaTime,float maxDistance)
  {
   bool requested=move.sqrMagnitude>.01f;
   player.PrepareLocomotion(requested);
   if(MeleeWindupActive){facing=MeleeWindupFacing;player.Root.transform.rotation=Quaternion.LookRotation(facing);}
   Vector3 before=HeroPosition,delta=move*PlayerMovementSpeed*Mathf.Max(0,deltaTime);
   if(!float.IsPositiveInfinity(maxDistance))delta=Vector3.ClampMagnitude(delta,Mathf.Max(0,maxDistance));
   Move(player.Root.transform,delta,.28f);
   Vector3 travelled=HeroPosition-before;travelled.y=0;
   if(travelled.sqrMagnitude>.0000001f && !MeleeWindupActive)
   {facing=travelled.normalized;FaceTravel(player,travelled,deltaTime);}
   player.Tick(travelled.sqrMagnitude>.0000001f);
  }
  Vector3 RouteWaypoint(DungeonGrid route,Vector3 pos,Vector3 goal)
  {
   float x,z;
   return grid.TryWaypoint(route,pos.x/Tile,pos.z/Tile,goal.x/Tile,goal.z/Tile,.32f/Tile,out x,out z) ? new Vector3(x*Tile,0,z*Tile) : pos;
  }
  Vector3 RouteDirection(DungeonGrid route,Vector3 pos,Vector3 goal)
  {return (RouteWaypoint(route,pos,goal)-pos).normalized;}
  bool Visible(Vector3 a,Vector3 b) { return grid.Visible(a.x/Tile,a.z/Tile,b.x/Tile,b.z/Tile); }
  void Move(Transform actor,Vector3 delta,float radius)
  {
   if(EnemyRootBlocksMovement(actor))return;
   int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.12f)); Vector3 step=delta/steps;
   for (int i=0;i<steps;i++)
   { Vector3 p=actor.position; if (grid.CanStand((p.x+step.x)/Tile,p.z/Tile,radius/Tile)) p.x+=step.x; if (grid.CanStand(p.x/Tile,(p.z+step.z)/Tile,radius/Tile)) p.z+=step.z; actor.position=p; }
  }
  public void ShowNotice(string message) { Notice=message; NoticeUntil=Time.time+5; }
  public bool UsePotion()
  {if(State!=Phase.Playing || Potions<=0 || Health>=MaxHealth)return false;Potions--;Heal((60+PotionRank*20)*TalentPotionMultiplier);Burst(HeroPosition,1.5f,art.greenMaterial);Sound("potion");ShowNotice("Mikstura • odnowa zdrowia");return true;}
  public void Begin() { if(State!=Phase.Title)return; ResetCampaign();tutorialLoaded=false; State=Phase.Playing; Time.timeScale=1; LoadZone(Campaign.Road); SaveCheckpoint();if(!Testing && !Application.isBatchMode){introUntil=Time.unscaledTime+3;Time.timeScale=0;}ShowNotice("WASD / LPM • ruszaj traktem. Q / PPM — moc. Spacja — unik."); }
  public void Pause() { CancelRecall();State=Phase.Paused; Time.timeScale=0; }
  public void Resume() { TalentsOpen=false;JournalOpen=false;EquipmentOpen=false;BookOpen=false;Service=""; State=Phase.Playing; Time.timeScale=1; }
  public void Restart() { Time.timeScale=1; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
  void OnDestroy() { DisposeSoftVfxResources();ResetStageGuardians();ResetCraftedVfx();ResetContainerMaterials();Time.timeScale=1; if (classMaterial) Destroy(classMaterial);if(groundMesh)Destroy(groundMesh); }
 }
}
