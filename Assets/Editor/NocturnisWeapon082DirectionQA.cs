using System;
using System.Linq;
using UnityEngine;
namespace Krypta.Editor {
 // Independent geometry/trajectory oracle. No FacingRoll values are read here.
 public static class NocturnisWeapon082DirectionQA {
  struct Contact { public Vector3 position,edge,shaft; }
  static Transform Find(GameObject root,string name){return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);}
  static MeshFilter Geometry(Transform weapon){return weapon.GetComponentsInChildren<MeshFilter>(true).First(f=>f.GetComponent<Renderer>() && f.GetComponent<Renderer>().enabled && f.gameObject.activeInHierarchy);}
  static Contact Sample(ActorVisual actor,AnimationClip[] clips,NocturnisWeaponGrip grip,string name,float phase,Vector3 meshPoint){
   var clip=clips.First(c=>c && c.name==name);actor.Play(name,false,.6f);
   var animation=actor.Root.GetComponentInChildren<Animation>();
   // Isolate the sampled clip exactly as the authored contact after its crossfade.
   animation.Play(name,PlayMode.StopAll);animation[name].time=phase*clip.length;
   animation[name].weight=1;animation.Sample();grip.Apply();
   var mesh=Geometry(grip.transform);
   return new Contact { position=mesh.transform.TransformPoint(meshPoint),edge=mesh.transform.TransformDirection(-Vector3.right).normalized,shaft=mesh.transform.TransformDirection(Vector3.up).normalized };
  }
  static void Strike(ActorVisual actor,AnimationClip[] clips,NocturnisWeaponGrip grip,int hero,string name,Action<bool,string> check){
   var vertices=Geometry(grip.transform).sharedMesh.vertices;
   // The sword point is its highest mesh vertex. The axe contact is the actual
   // outer bevel (most negative X), not an author-defined TipAnchor or profile.
   float extreme=hero==0 ? vertices.Max(v=>v.y) : vertices.Min(v=>v.x);
   var points=vertices.Where(v=>hero==0 ? v.y>=extreme-.002f : v.x<=extreme+.002f).ToArray();
   Vector3 point=Vector3.zero;foreach(var v in points)point+=v;point/=points.Length;
   var before=Sample(actor,clips,grip,name,.40f,point);var contact=Sample(actor,clips,grip,name,.42f,point);var after=Sample(actor,clips,grip,name,.44f,point);
   Vector3 velocity=Vector3.ProjectOnPlane(after.position-before.position,contact.shaft);
   check(velocity.magnitude>.0001f,"moving mesh cutting point: "+hero+" / "+name);
   float alignment=Vector3.Dot(contact.edge,velocity.normalized);
   // A double-edged sword can lead with either edge. The axe cannot lead with its poll.
   if(hero==0)alignment=Mathf.Abs(alignment);
   check(alignment>.80f,"mesh edge leads contact: "+hero+" / "+name+" alignment="+alignment.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
  }
  static void Carry(ActorVisual actor,string clip,float phase,Action<bool,string> check){
   var measured=NocturnisWeapon082CarryInspect.Measure(actor,clip,phase,clip=="Running_A"?50f:45f);
   check(measured.headTriangles>50,"actual axe head geometry present: "+clip+" / "+phase);
   check(measured.intersections==0 && measured.minGap>.025f,
    "axe head clears CPU-skinned legs: "+clip+" / "+phase+" intersections="+measured.intersections+" gap="+measured.minGap.ToString("F4",System.Globalization.CultureInfo.InvariantCulture));
  }
  public static void Verify(Action<bool,string> check){
   var art=Resources.Load<KryptaAssets>("KryptaAssets");
   check(art && art.heroes.Length==5,"five configured weapon profiles");
   for(int hero=0;hero<5;hero++){
    var config=art.heroes[hero];var actor=new ActorVisual(config.model,config.animations,Vector3.zero,1.8f,"Weapon direction QA");
    try {
     actor.Equip(config.weapon);var grip=actor.Root.GetComponentInChildren<NocturnisWeaponGrip>();
     check(grip!=null,"equipped grip "+hero);var mesh=Geometry(grip.transform);var hand=Find(actor.Root,"handR");
     if(hero==0){Strike(actor,config.animations,grip,hero,"1H_Melee_Attack_Chop",check);Strike(actor,config.animations,grip,hero,"2H_Melee_Attack_Spin",check);}
     if(hero==4){Strike(actor,config.animations,grip,hero,"1H_Melee_Attack_Slice_Horizontal",check);Strike(actor,config.animations,grip,hero,"2H_Melee_Attack_Spin",check);}
     if(hero==1){
      var cast=Sample(actor,config.animations,grip,"Cast_A",.42f,Vector3.zero);
      check(Vector3.Dot(cast.edge,actor.Root.transform.forward)>.70f,"scythe hook faces away from caster");
     }
     if(hero==2){
      Sample(actor,config.animations,grip,"1H_Ranged_Shoot",.42f,Vector3.zero);
      // The visible bowstring is +X from the offset grip in the source mesh.
      Vector3 towardString=mesh.transform.TransformDirection(Vector3.right).normalized;
      check(Vector3.Dot(towardString,-actor.Root.transform.forward)>.95f,"bowstring faces draw hand, away from arrow flight");
     }
     if(hero==3){
      var cast=Sample(actor,config.animations,grip,"Cast_A",.42f,Vector3.zero);
      check(Vector3.Dot(cast.shaft,Vector3.up)>.85f,"staff crown remains above grip while casting");
     }
     foreach(float time in new[]{.15f,.42f,.8f}){
      actor.PreviewIdle(time*actor.Root.GetComponentInChildren<Animation>()["Idle"].length);
      if(hero==4)Carry(actor,"Idle",time,check);
      Quaternion expected=grip.transform.rotation,expectedHand=hand.rotation;Vector3 expectedPoint=grip.GripCenter;
      // Reproduce both components being scheduled in the adverse order in one frame:
      // HUD.Update samples its portrait; Grip.Update then removes fitting; LateUpdate reapplies.
      grip.SendMessage("Update",SendMessageOptions.RequireReceiver);grip.Apply(false);
      check(Quaternion.Angle(expected,grip.transform.rotation)<.05f && Quaternion.Angle(expectedHand,hand.rotation)<.05f,"paused same-frame Update/LateUpdate orientation: "+hero+" / "+time);
      check(Vector3.Distance(expectedPoint,grip.GripCenter)<.0005f,"same-frame grip anchor retained: "+hero+" / "+time);
      var handle=Find(grip.gameObject,"GripAnchor");check(Vector3.Distance(handle.position,grip.GripCenter)<.0005f,"mesh handle stays in palm: "+hero+" / "+time);
      check(Vector3.Dot(mesh.transform.up.normalized,-hand.right)>.999f,"handle shaft crosses curled palm: "+hero+" / "+time);
     }
     if(hero==4)foreach(string gait in new[]{"Walking_A","Running_A"})foreach(float phase in new[]{.15f,.42f,.8f}){
      Sample(actor,config.animations,grip,gait,phase,Vector3.zero);
      Carry(actor,gait,phase,check);
     }
     actor.PreviewIdle(.42f);Vector3 original=actor.Root.transform.InverseTransformDirection(mesh.transform.right);
     actor.Root.transform.rotation=Quaternion.Euler(0,73,0);actor.PreviewIdle(.42f);
     check(Vector3.Angle(original,actor.Root.transform.InverseTransformDirection(mesh.transform.right))<.05f,"weapon facing follows actor yaw: "+hero);
    } finally {UnityEngine.Object.DestroyImmediate(actor.Root);}
   }
  }
 }
}
