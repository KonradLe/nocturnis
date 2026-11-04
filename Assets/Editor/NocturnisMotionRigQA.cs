using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace Krypta.Editor {
 public static class NocturnisMotionRigQA {
  [Serializable] sealed class ActorResult {public string id;public float maxGripGap,weaponSurfaceGap,idleMotion,runFootHeightRange,runStanceTravel,expectedRunTravel,walkFootHeightRange,walkStanceTravel,expectedWalkTravel,maxLoopSeam,bowStringGap;public int checkedPoses,minGripSectors=8;public float maxShaftAngle;}
  [Serializable] sealed class Report {public bool passed;public string version="0.7.5",error;public int assertions;public List<ActorResult> actors=new List<ActorResult>();}
  static Report report;
  static void Check(bool ok,string message){report.assertions++;if(!ok)throw new Exception(message);}
  public static void Run(){
   report=new Report();Directory.CreateDirectory("Logs/MotionRig");
   try{
    NocturnisIntegratedBuild.Validate();var config=Resources.Load<KryptaAssets>("KryptaAssets");
    foreach(var character in config.heroes){
     var profile=character.model.GetComponent<ActorModelProfile>();float h=profile.bodyHeight;
     var actor=new ActorVisual(character.model,character.animations,Vector3.zero,h,character.model.name);actor.Equip(character.weapon);
     var skin=actor.Root.GetComponentInChildren<SkinnedMeshRenderer>();var anim=actor.Root.GetComponentInChildren<Animation>();var transforms=actor.Root.GetComponentsInChildren<Transform>(true);
     var hand=transforms.First(t=>t.name=="handslot.r");var left=transforms.First(t=>t.name=="handslot.l");var foot=transforms.First(t=>t.name=="footR");var grip=transforms.FirstOrDefault(t=>t.name=="GripAnchor");
     var result=new ActorResult{id=character.model.name};report.actors.Add(result);Check(grip,"Missing authored weapon anchor "+result.id);Check(Vector3.Distance(hand.position,grip.position)<.001f,"Weapon root detached from hand "+result.id);
     float weaponGap=float.MaxValue,vertexGap=float.MaxValue;string weaponDebug="";
     foreach(var filter in grip.parent.GetComponentsInChildren<MeshFilter>()){
      var points=filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();var tri=filter.sharedMesh.triangles;foreach(var point in points)vertexGap=Mathf.Min(vertexGap,Vector3.Distance(point,grip.position));weaponDebug+=" mesh="+filter.name+" verts="+points.Length+" triangles="+tri.Length+" localAnchor="+filter.transform.InverseTransformPoint(grip.position)+" meshBounds="+filter.sharedMesh.bounds;
      for(int i=0;i<tri.Length;i+=3)weaponGap=Mathf.Min(weaponGap,PointTriangleDistance(grip.position,points[tri[i]],points[tri[i+1]],points[tri[i+2]]));
     }
     result.weaponSurfaceGap=weaponGap;Check(weaponGap<.04f,"Authored weapon handle misses the grip anchor "+result.id+": "+weaponGap+" nearestVertex="+vertexGap+weaponDebug);
     int handIndex=Array.IndexOf(skin.bones,hand.parent);var handWeights=skin.sharedMesh.boneWeights;
     var bindPositions=skin.bones.ToDictionary(b=>b,b=>b.localPosition);var baked=new Mesh();
     foreach(var clip in character.animations){
      actor.Play(clip.name,clip.isLooping,.5f);

      foreach(float t in new[]{0f,.12f,.25f,.42f,.58f,.75f,.92f,1f}){
       clip.SampleAnimation(anim.gameObject,t*clip.length);
       var fit=actor.Root.GetComponentInChildren<NocturnisWeaponGrip>();if(fit)fit.Apply();
       Check(skin.bones.Where(b=>b.name!="hips").All(b=>(b.localPosition-bindPositions[b]).sqrMagnitude<.000001f),"Local joint offsets collapsed while sampling "+result.id+"/"+clip.name);
       skin.BakeMesh(baked);float gap=float.MaxValue;
       var points=baked.vertices;int sectors=0;
       for(int i=0;i<points.Length;i++){
        var point=points[i];CheckFinite(point,result.id,clip.name);
        var weight=handWeights[i];float influence=(weight.boneIndex0==handIndex?weight.weight0:0)+(weight.boneIndex1==handIndex?weight.weight1:0)+(weight.boneIndex2==handIndex?weight.weight2:0)+(weight.boneIndex3==handIndex?weight.weight3:0);
        if(influence<.9f)continue;
        var worldPoint=skin.transform.TransformPoint(point);gap=Mathf.Min(gap,Vector3.Distance(worldPoint,grip.position));
        var radial=hand.parent.InverseTransformPoint(worldPoint)-hand.localPosition;
        float radius=Mathf.Sqrt(radial.y*radial.y+radial.z*radial.z);
        if(Mathf.Abs(radial.x)<h*.04f && radius>h*.003f && radius<h*.035f){
         int sector=Mathf.Clamp(Mathf.FloorToInt((Mathf.Atan2(radial.z,radial.y)+Mathf.PI)/(2*Mathf.PI)*8),0,7);sectors|=1<<sector;
        }
       }
       int covered=0;for(int bit=0;bit<8;bit++)if((sectors&(1<<bit))!=0)covered++;
       var cradle=grip.parent.GetComponentsInChildren<Transform>().FirstOrDefault(v=>v.name=="BladeRestAngle");
       Vector3 shaft=grip.parent.TransformDirection(cradle?cradle.localRotation*Vector3.up:Vector3.up);
       float angle=Vector3.Angle(shaft,-hand.parent.right);
       result.minGripSectors=Mathf.Min(result.minGripSectors,covered);result.maxShaftAngle=Mathf.Max(result.maxShaftAngle,angle);
       Check(angle<1,"Handle axis crosses the closed fingers: "+result.id+"/"+clip.name+" angle="+angle);
       Check(covered>=4,"Glove must surround at least half the handle cross-section: "+result.id+"/"+clip.name+" sectors="+covered);

       result.maxGripGap=Mathf.Max(result.maxGripGap,gap);result.checkedPoses++;
       Check(gap<h*.025f,"Grip is outside the visible palm "+result.id+"/"+clip.name+"/"+t+": "+gap);
       Check(baked.bounds.size.magnitude<h*3,"Exploded skin "+result.id+"/"+clip.name);
      }
     }
     var idle=character.animations.First(c=>c.name=="Idle");idle.SampleAnimation(anim.gameObject,0);var start=hand.position;idle.SampleAnimation(anim.gameObject,idle.length*.25f);result.idleMotion=Vector3.Distance(start,hand.position);
     Check(result.idleMotion>.002f&&result.idleMotion<h*.045f,"Idle does not have restrained breathing "+result.id);
     var run=character.animations.First(c=>c.name=="Running_A");
     var walk=character.animations.First(c=>c.name=="Walking_A");
     if(AssetDatabase.GetAssetPath(run).StartsWith(NocturnisUalImport.Folder+"/")){
      var runPlant=MeasurePlant(run,anim.gameObject,foot,h,profile.runStridePerHeight,result.id);
      result.runStanceTravel=runPlant.travel;result.expectedRunTravel=runPlant.expected;result.runFootHeightRange=runPlant.heightRange;
      Check(Mathf.Abs(result.runStanceTravel-result.expectedRunTravel)<h*.06f,"UAL running stance speed mismatches configured cadence "+result.id);
      Check(result.runFootHeightRange<h*.035f,"UAL planted running ankle lifts "+result.id);
      var walkPlant=MeasurePlant(walk,anim.gameObject,foot,h,profile.walkStridePerHeight,result.id);
      result.walkStanceTravel=walkPlant.travel;result.expectedWalkTravel=walkPlant.expected;result.walkFootHeightRange=walkPlant.heightRange;
      Check(Mathf.Abs(result.walkStanceTravel-result.expectedWalkTravel)<h*.04f,"UAL walking stance speed mismatches configured cadence "+result.id);
      Check(result.walkFootHeightRange<h*.025f,"UAL planted walking ankle lifts "+result.id);
     }else{
      run.SampleAnimation(anim.gameObject,run.length*.04f);var first=foot.position;run.SampleAnimation(anim.gameObject,run.length*.28f);var last=foot.position;
      result.runStanceTravel=first.z-last.z;result.expectedRunTravel=h*1.45f*(.28f-.04f);result.runFootHeightRange=Mathf.Abs(first.y-last.y);
      Check(Mathf.Abs(result.runStanceTravel-result.expectedRunTravel)<h*.06f,"Foot stance speed mismatches locomotion "+result.id);
      Check(result.runFootHeightRange<h*.035f,"Planted ankle lifts during stance "+result.id+": "+result.runFootHeightRange);
      walk.SampleAnimation(anim.gameObject,walk.length*.08f);first=foot.position;walk.SampleAnimation(anim.gameObject,walk.length*.52f);last=foot.position;
      result.walkStanceTravel=first.z-last.z;result.expectedWalkTravel=h*.60f*(.52f-.08f);result.walkFootHeightRange=Mathf.Abs(first.y-last.y);
      Check(Mathf.Abs(result.walkStanceTravel-result.expectedWalkTravel)<h*.04f,"Walking stance speed mismatches locomotion "+result.id);
      Check(result.walkFootHeightRange<h*.025f,"Walking ankle lifts during stance "+result.id);
     }
     foreach(var loop in new[]{idle,run,walk}){
      loop.SampleAnimation(anim.gameObject,0);var atStart=skin.bones.Select(b=>b.position).ToArray();loop.SampleAnimation(anim.gameObject,loop.length);
      for(int i=0;i<skin.bones.Length;i++)result.maxLoopSeam=Mathf.Max(result.maxLoopSeam,Vector3.Distance(atStart[i],skin.bones[i].position));
     }
     Check(result.maxLoopSeam<h*.001f,"Visible discontinuity at locomotion/idle loop seam "+result.id);
     if(result.id=="hero_dusk_hunter"){
      var bow=character.animations.First(c=>c.name=="1H_Ranged_Shoot");actor.Play(bow.name,false,.5f);bow.SampleAnimation(anim.gameObject,bow.length*.42f);
      var bowFit=actor.Root.GetComponentInChildren<NocturnisWeaponGrip>();if(bowFit)bowFit.Apply();
      var stringPoint=grip.position+grip.rotation*(Vector3.right*(.29f*1.25f/1.37327f));result.bowStringGap=Vector3.Distance(left.position,stringPoint);Check(result.bowStringGap<.11f,"Drawing hand misses the bowstring: "+result.bowStringGap);
     }
     UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(actor.Root);
    }
    report.passed=true;
   }catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
   File.WriteAllText("Logs/MotionRig/rig-report.json",JsonUtility.ToJson(report,true));EditorApplication.Exit(report.passed?0:1);
  }
  struct Plant {public float travel,expected,heightRange;}
  static Plant MeasurePlant(AnimationClip clip,GameObject model,Transform foot,float height,float stride,string id){
   // Search two periods so support intervals crossing the loop seam remain
   // continuous. The oracle derives support from ankle height and travel,
   // independently from the importer's per-frame median speed estimator.
   const int rate=240;var samples=new Vector3[rate*2+1];
   for(int i=0;i<samples.Length;i++){clip.SampleAnimation(model,Mathf.Repeat(i/(float)rate,1)*clip.length);samples[i]=foot.position;}
   float low=samples.Min(p=>p.y);int begin=-1,bestStart=-1,bestEnd=-1;
   for(int i=1;i<samples.Length;i++){
    bool support=samples[i].y<=low+height*.018f&&samples[i-1].y<=low+height*.018f&&samples[i].z<samples[i-1].z-height*.00001f;
    if(support&&begin<0)begin=i-1;
    if((!support||i==samples.Length-1)&&begin>=0){
     int end=support?i:i-1;if(end-begin>bestEnd-bestStart){bestStart=begin;bestEnd=end;}begin=-1;
    }
   }
   Check(bestStart>=0&&bestEnd-bestStart>=rate*.10f,"No sustained ground support found in UAL "+id+"/"+clip.name);
   // Exclude transition-to-contact and toe-off (10 percent at each edge).
   int trim=Mathf.Max(1,(bestEnd-bestStart)/10);bestStart+=trim;bestEnd-=trim;
   float travel=samples[bestStart].z-samples[bestEnd].z;
   float expected=height*stride*(bestEnd-bestStart)/(float)rate;
   float high=samples.Skip(bestStart).Take(bestEnd-bestStart+1).Max(p=>p.y);
   float bottom=samples.Skip(bestStart).Take(bestEnd-bestStart+1).Min(p=>p.y);
   // Verify distributed contact as well as endpoints: a foot skating forwards
   // midway through a stance cannot cancel out in the endpoint measurement.
   for(int i=bestStart+1;i<=bestEnd;i++)Check(samples[i].z<=samples[i-1].z+height*.0001f,"UAL planted foot reverses direction "+id+"/"+clip.name);
   Debug.Log("UAL_PLANT "+id+"/"+clip.name+" phase="+bestStart/(float)rate+".."+bestEnd/(float)rate+" actual="+travel+" expected="+expected+" stride="+stride);
   return new Plant{travel=travel,expected=expected,heightRange=high-bottom};
  }
  static float PointTriangleDistance(Vector3 p,Vector3 a,Vector3 b,Vector3 c){
   var ab=b-a;var ac=c-a;
   if(Vector3.Cross(ab,ac).sqrMagnitude<1e-16f)return Mathf.Min(PointSegmentDistance(p,a,b),Mathf.Min(PointSegmentDistance(p,b,c),PointSegmentDistance(p,c,a)));
   var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return ap.magnitude;
   var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return bp.magnitude;
   float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return (p-(a+ab*(d1/(d1-d3)))).magnitude;
   var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return cp.magnitude;
   float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return (p-(a+ac*(d2/(d2-d6)))).magnitude;
   float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return (p-(b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6))))).magnitude;
   float inv=1/(va+vb+vc);return (p-(a+ab*(vb*inv)+ac*(vc*inv))).magnitude;
  }
  static float PointSegmentDistance(Vector3 p,Vector3 a,Vector3 b){var d=b-a;float t=d.sqrMagnitude>1e-16f?Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude):0;return (p-a-d*t).magnitude;}
  static void CheckFinite(Vector3 p,string actor,string clip){if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z))throw new Exception("Invalid skin "+actor+"/"+clip);}
 }
}
