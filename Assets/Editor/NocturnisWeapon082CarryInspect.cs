using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace Krypta.Editor {
 public static class NocturnisWeapon082CarryInspect {
  [Serializable]public sealed class Entry {public string clip;public float phase,angle,rightmost,minGap;public int intersections,headTriangles,legTriangles;}
  [Serializable]sealed class Report{public bool passed;public string error="";public List<Entry> entries=new List<Entry>();}
  struct Tri{public Vector3 a,b,c;public Bounds box;public Tri(Vector3 x,Vector3 y,Vector3 z){a=x;b=y;c=z;box=new Bounds(x,Vector3.zero);box.Encapsulate(y);box.Encapsulate(z);}}
  static Transform Find(GameObject r,string n){return r.GetComponentsInChildren<Transform>(true).First(t=>t.name==n);}
  static bool Leg(Transform t){return t && (t.name.StartsWith("thigh") || t.name.StartsWith("shin") || t.name.StartsWith("foot"));}
  static List<Tri> LegGeometry(GameObject actor){
   var result=new List<Tri>();
   foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()){
    if(!skin.enabled || !skin.gameObject.activeInHierarchy)continue;var vs=skin.sharedMesh.vertices;var ts=skin.sharedMesh.triangles;var ws=skin.sharedMesh.boneWeights;var bs=skin.bones;var bind=skin.sharedMesh.bindposes;var matrices=new Matrix4x4[bs.Length];for(int j=0;j<bs.Length;j++)matrices[j]=bs[j].localToWorldMatrix*bind[j];var isLeg=new bool[vs.Length];
    for(int i=0;i<vs.Length;i++){var source=vs[i];var w=ws[i];vs[i]=matrices[w.boneIndex0].MultiplyPoint3x4(source)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(source)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(source)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(source)*w.weight3;float weight=(Leg(bs[w.boneIndex0])?w.weight0:0)+(Leg(bs[w.boneIndex1])?w.weight1:0)+(Leg(bs[w.boneIndex2])?w.weight2:0)+(Leg(bs[w.boneIndex3])?w.weight3:0);isLeg[i]=weight>.35f;}
    for(int i=0;i<ts.Length;i+=3)if(isLeg[ts[i]] || isLeg[ts[i+1]] || isLeg[ts[i+2]])result.Add(new Tri(vs[ts[i]],vs[ts[i+1]],vs[ts[i+2]]));
   }return result;
  }
  static Vector3 Closest(Vector3 p,Tri t){
   Vector3 ab=t.b-t.a,ac=t.c-t.a,ap=p-t.a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0 && d2<=0)return t.a;
   Vector3 bp=p-t.b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0 && d4<=d3)return t.b;
   float vc=d1*d4-d3*d2;if(vc<=0 && d1>=0 && d3<=0)return t.a+ab*(d1/(d1-d3));
   Vector3 cp=p-t.c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0 && d5<=d6)return t.c;
   float vb=d5*d2-d1*d6;if(vb<=0 && d2>=0 && d6<=0)return t.a+ac*(d2/(d2-d6));
   float va=d3*d6-d5*d4;if(va<=0 && d4-d3>=0 && d5-d6>=0)return t.b+(t.c-t.b)*((d4-d3)/((d4-d3)+(d5-d6)));
   float denominator=va+vb+vc;if(Mathf.Abs(denominator)<1e-12f)return t.a;return t.a+ab*(vb/denominator)+ac*(vc/denominator);
  }
  static bool Cross(Vector3 a,Vector3 b,Tri t){Vector3 d=b-a,e1=t.b-t.a,e2=t.c-t.a,h=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,h);if(Mathf.Abs(det)<1e-9f)return false;float inv=1/det;Vector3 s=a-t.a;float u=inv*Vector3.Dot(s,h);if(u<0 || u>1)return false;Vector3 q=Vector3.Cross(s,e1);float v=inv*Vector3.Dot(d,q);if(v<0 || u+v>1)return false;float hit=inv*Vector3.Dot(e2,q);return hit>=0 && hit<=1;}
  static float SegmentGap(Vector3 p,Vector3 q,Vector3 r,Vector3 s){
   Vector3 d1=q-p,d2=s-r,w=p-r;float a=Vector3.Dot(d1,d1),e=Vector3.Dot(d2,d2),f=Vector3.Dot(d2,w),u,v;
   if(a<1e-12f && e<1e-12f)return w.sqrMagnitude;
   if(a<1e-12f){u=0;v=Mathf.Clamp01(f/e);}
   else {float c=Vector3.Dot(d1,w);if(e<1e-12f){v=0;u=Mathf.Clamp01(-c/a);}
    else {float b=Vector3.Dot(d1,d2),den=a*e-b*b;u=Mathf.Abs(den)>1e-12f?Mathf.Clamp01((b*f-c*e)/den):0;v=(b*u+f)/e;
     if(v<0){v=0;u=Mathf.Clamp01(-c/a);}else if(v>1){v=1;u=Mathf.Clamp01((b-c)/a);}}
   }return (p+d1*u-r-d2*v).sqrMagnitude;
  }
  static float BoundsGap(Bounds a,Bounds b){Vector3 d=Vector3.Max(Vector3.zero,Vector3.Max(a.min-b.max,b.min-a.max));return d.sqrMagnitude;}
  public static Entry Measure(ActorVisual actor,string clip,float phase,float angle){
   var weapon=actor.Root.GetComponentInChildren<NocturnisWeaponGrip>();var mesh=weapon.GetComponentsInChildren<MeshFilter>(true).First(f=>f.GetComponent<Renderer>() && f.GetComponent<Renderer>().enabled && f.gameObject.activeInHierarchy);
   var vertices=mesh.sharedMesh.vertices;var indices=mesh.sharedMesh.triangles;var heads=new List<Tri>();var bounds=new Bounds();bool first=true;float rightmost=float.NegativeInfinity;
   for(int i=0;i<indices.Length;i+=3){var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
    if(a.y<.70f || b.y<.70f || c.y<.70f || Mathf.Min(a.x,Mathf.Min(b.x,c.x))>=-.07f)continue;
    a=mesh.transform.TransformPoint(a);b=mesh.transform.TransformPoint(b);c=mesh.transform.TransformPoint(c);var tri=new Tri(a,b,c);heads.Add(tri);if(first){bounds=tri.box;first=false;}else bounds.Encapsulate(tri.box);
    foreach(var point in new[]{a,b,c})rightmost=Mathf.Max(rightmost,actor.Root.transform.InverseTransformPoint(point).x);
   }
   bounds.Expand(.6f);var legs=LegGeometry(actor.Root).Where(t=>t.box.Intersects(bounds)).ToList();float gap=float.PositiveInfinity;int intersects=0;
   foreach(var h in heads)foreach(var l in legs){
    if(BoundsGap(h.box,l.box)>gap*gap)continue;
    var hp=new[]{h.a,h.b,h.c};var lp=new[]{l.a,l.b,l.c};
    for(int i=0;i<3;i++)for(int j=0;j<3;j++)gap=Mathf.Min(gap,Mathf.Sqrt(SegmentGap(hp[i],hp[(i+1)%3],lp[j],lp[(j+1)%3])));
    foreach(var point in new[]{h.a,h.b,h.c})gap=Mathf.Min(gap,Vector3.Distance(point,Closest(point,l)));
    foreach(var point in new[]{l.a,l.b,l.c})gap=Mathf.Min(gap,Vector3.Distance(point,Closest(point,h)));
    if(h.box.Intersects(l.box) && (Cross(h.a,h.b,l)||Cross(h.b,h.c,l)||Cross(h.c,h.a,l)||Cross(l.a,l.b,h)||Cross(l.b,l.c,h)||Cross(l.c,l.a,h)))intersects++;
   }
   return new Entry{clip=clip,phase=phase,angle=angle,rightmost=rightmost,minGap=float.IsInfinity(gap)?1:gap,intersections=intersects,headTriangles=heads.Count,legTriangles=legs.Count};
  }
  [Serializable]sealed class Verification{public bool passed;public int assertions;public string error="";public List<string> evidence=new List<string>();}
  public static void VerifyRun(){var result=new Verification();
   try{NocturnisWeapon082DirectionQA.Verify((ok,label)=>{result.assertions++;result.evidence.Add(label);if(!ok)throw new Exception(label);});result.passed=true;}
   catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
   Directory.CreateDirectory("Logs/Weapon082Inspect");File.WriteAllText("Logs/Weapon082Inspect/direction-verify.json",JsonUtility.ToJson(result,true));
   Debug.Log("WEAPON082_DIRECTION_VERIFY "+result.passed+" assertions="+result.assertions);EditorApplication.Exit(result.passed?0:1);
  }
  public static void Run(){var result=new Report();ActorVisual actor=null;
   try{var config=Resources.Load<KryptaAssets>("KryptaAssets").heroes[4];actor=new ActorVisual(config.model,config.animations,Vector3.zero,1.8f,"Carry geometry inspection");actor.Equip(config.weapon);var fit=actor.Root.GetComponentInChildren<NocturnisWeaponGrip>();var animation=actor.Root.GetComponentInChildren<Animation>();var upper=Find(actor.Root,"armR");
    foreach(float angle in new[]{25f,30f,35f,40f,45f,50f,55f})foreach(string clip in new[]{"Idle","Walking_A","Running_A"})foreach(float phase in new[]{.15f,.42f,.8f}){
     actor.Play(clip,true,0);animation.Play(clip,PlayMode.StopAll);animation[clip].time=phase*animation[clip].length;animation[clip].weight=1;animation.Sample();fit.Apply();
     upper.rotation=Quaternion.AngleAxis(-(angle-(clip=="Running_A"?50f:45f)),actor.Root.transform.forward)*upper.rotation;
     var entry=Measure(actor,clip,phase,angle);result.entries.Add(entry);Debug.Log("CARRY082 "+clip+" phase="+phase+" angle="+angle+" intersections="+entry.intersections+" gap="+entry.minGap);
    }result.passed=true;
   }catch(Exception e){result.error=e.ToString();Debug.LogException(e);}finally{if(actor!=null)UnityEngine.Object.DestroyImmediate(actor.Root);}
   Directory.CreateDirectory("Logs/Weapon082Inspect");File.WriteAllText("Logs/Weapon082Inspect/carry.json",JsonUtility.ToJson(result,true));EditorApplication.Exit(result.passed?0:1);
  }
 }
}
