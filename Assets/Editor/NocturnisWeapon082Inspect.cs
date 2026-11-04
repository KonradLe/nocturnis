using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Krypta.Editor
{
 // Diagnostic only: samples cloned actors; never saves prefabs, clips or project settings.
 public static class NocturnisWeapon082Inspect
 {
  [Serializable]sealed class Pose
  {
   public int hero;public string clip;public float t;
   public Vector3 grip,tip,shaft,edge,flatNormal,handRight,handUp,handForward,forearmRight,forearmUp,forearmForward,forearmToHand;
   public Vector3 authoredHandRight,authoredHandUp,authoredHandForward;
   public Quaternion handRotation,weaponRotation,weaponLocalRotation,cradleRotation;
  }
  [Serializable]sealed class Weapon
  {public int hero;public string asset,name;public string[] activeMeshes;public Vector3 gripLocal;public Vector3[] meshBoundsMin,meshBoundsMax;}
  [Serializable]sealed class Report
  {public bool passed;public string error="";public List<Weapon> weapons=new List<Weapon>();public List<Pose> poses=new List<Pose>();}
  static Transform Bone(GameObject root,string name){return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);}
  public static void Run()
  {
   var result=new Report();Directory.CreateDirectory("Logs/Weapon082Inspect");
   try
   {
    var art=Resources.Load<KryptaAssets>("KryptaAssets");if(!art || art.heroes.Length!=5)throw new Exception("Five configured heroes required");
    for(int hero=0;hero<5;hero++)
    {
     var character=art.heroes[hero];var actor=new ActorVisual(character.model,character.animations,Vector3.zero,1.8f,"Weapon orientation inspection");
     try
     {
      actor.Equip(character.weapon);var fit=actor.Root.GetComponentInChildren<NocturnisWeaponGrip>();
      if(!fit)throw new Exception("No equipped grip for hero "+hero);
      var item=fit.transform;var cradle=Bone(item.gameObject,"BladeRestAngle");var hand=Bone(actor.Root,"handR");var forearm=Bone(actor.Root,"forearmR");var grip=Bone(item.gameObject,"GripAnchor");var tip=Bone(item.gameObject,"TipAnchor");
      var filters=item.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.GetComponent<Renderer>() && f.GetComponent<Renderer>().enabled && f.gameObject.activeInHierarchy).ToArray();
      result.weapons.Add(new Weapon{hero=hero,asset=AssetDatabase.GetAssetPath(character.weapon),name=item.name,activeMeshes=filters.Select(f=>f.name+" / "+f.sharedMesh.name).ToArray(),gripLocal=item.InverseTransformPoint(grip.position),meshBoundsMin=filters.Select(f=>f.sharedMesh.bounds.min).ToArray(),meshBoundsMax=filters.Select(f=>f.sharedMesh.bounds.max).ToArray()});
      var anim=actor.Root.GetComponentInChildren<Animation>();
      foreach(string name in new[]{"Idle","Walking_A","Running_A","1H_Melee_Attack_Chop","1H_Melee_Attack_Slice_Horizontal","1H_Ranged_Shoot","Cast_A","2H_Melee_Attack_Spin"})
      {
       var clip=character.animations.FirstOrDefault(c=>c && c.name==name);if(!clip)continue;
       actor.Play(name,name=="Idle"||name=="Walking_A"||name=="Running_A",.6f);
       foreach(float t in new[]{0f,.15f,.25f,.40f,.42f,.44f,.5f,.75f,1f})
       {
        clip.SampleAnimation(anim.gameObject,t*clip.length);
        var pose=new Pose{hero=hero,clip=name,t=t,authoredHandRight=hand.right,authoredHandUp=hand.up,authoredHandForward=hand.forward};
        fit.Apply();Quaternion frame=cradle ? cradle.localRotation : Quaternion.identity;
        pose.grip=grip.position;pose.tip=tip ? tip.position : grip.position+item.TransformDirection(frame*Vector3.up);
        pose.shaft=item.TransformDirection(frame*Vector3.up);pose.edge=item.TransformDirection(frame*-Vector3.right);pose.flatNormal=item.TransformDirection(frame*Vector3.forward);
        pose.handRight=hand.right;pose.handUp=hand.up;pose.handForward=hand.forward;
        pose.forearmRight=forearm.right;pose.forearmUp=forearm.up;pose.forearmForward=forearm.forward;pose.forearmToHand=(hand.position-forearm.position).normalized;
        pose.handRotation=hand.rotation;pose.weaponRotation=item.rotation;pose.weaponLocalRotation=item.localRotation;pose.cradleRotation=frame;
        result.poses.Add(pose);
       }
      }
     }
     finally{UnityEngine.Object.DestroyImmediate(actor.Root);}
    }
    result.passed=true;
   }
   catch(Exception e){result.error=e.ToString();Debug.LogException(e);}
   File.WriteAllText("Logs/Weapon082Inspect/axes.json",JsonUtility.ToJson(result,true));
   Debug.Log("WEAPON082_INSPECT_COMPLETE "+result.passed+" poses="+result.poses.Count);EditorApplication.Exit(result.passed ? 0 : 1);
  }
 }
}