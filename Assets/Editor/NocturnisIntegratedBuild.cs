using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Krypta.Editor {
 public static class NocturnisIntegratedBuild {
  const string Library="Assets/NocturnisActI",Runtime=Library+"/Runtime";
  public const string Version="0.7.0";
  static readonly string[] Names={"Idle","Walking_A","Running_A","1H_Melee_Attack_Chop","1H_Melee_Attack_Slice_Horizontal","2H_Melee_Attack_Spin","Dodge_Forward","Death_A","1H_Ranged_Shoot","Hit_A","Block","Defense_A","Cast_A"};
  static readonly string[] Heroes={"hero_inquisitor","hero_blood_weaver","hero_dusk_hunter","hero_void_herald","hero_root_guardian"};
  static string PathOf(string id){return Runtime+"/"+id;}
  static void Need(bool yes,string message){if(!yes)throw new Exception(message);}
  public static void Prepare() {
   try {
    Directory.CreateDirectory(Runtime);AssetDatabase.Refresh();
    var catalog=JsonUtility.FromJson<NocturnisAssetLibrary.Catalog>(File.ReadAllText(Library+"/catalog.json"));
    var entries=new List<NocturnisActILibrary.Entry>();
    foreach(var e in catalog.assets) {
     var source=AssetDatabase.LoadAssetAtPath<GameObject>(Library+"/Prefabs/"+e.id+".prefab");Need(source,"Missing library prefab "+e.id);
     var importer=AssetImporter.GetAtPath(Library+"/"+e.model) as ModelImporter;
     if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
     bool actor=e.id.StartsWith("hero_")||e.id.StartsWith("npc_")||e.id.StartsWith("enemy_")||e.id.StartsWith("boss_");
     var item=new NocturnisActILibrary.Entry{id=e.id,prefab=source};
     if(actor) MakeActor(e,source,item);
     entries.Add(item);Debug.Log("ACTI_ASSET "+e.id);
    }
    var lib=AssetDatabase.LoadAssetAtPath<NocturnisActILibrary>("Assets/Resources/ActILibrary.asset");
    if(!lib){lib=ScriptableObject.CreateInstance<NocturnisActILibrary>();AssetDatabase.CreateAsset(lib,"Assets/Resources/ActILibrary.asset");}
    lib.entries=entries.ToArray();EditorUtility.SetDirty(lib);
    var config=Resources.Load<KryptaAssets>("KryptaAssets");Need(config&&config.heroes.Length==5,"Existing campaign configuration missing");
    for(int i=0;i<5;i++){
     config.heroes[i]=lib.Character(Heroes[i]);config.heroes[i].weapon=MakeWeapon(lib,i==0?"weapon_sword":i==2?"weapon_bow":i==4?"weapon_axe":i==1?"weapon_scythe":"weapon_staff");
    }
    config.knight=config.heroes[0].model;config.knightAnimations=config.heroes[0].animations;
    config.zombie=lib.Character("enemy_penitent");config.bishop=lib.Character("boss_malazar");
    config.skeletonVariants=new[]{lib.Character("enemy_penitent"),lib.Character("enemy_royal_guard"),lib.Character("enemy_skeleton_archer"),lib.Character("enemy_blood_cultist")};
    config.skeleton=config.skeletonVariants[0].model;config.skeletonAnimations=config.skeletonVariants[0].animations;
    config.trees=new[]{lib.Model("tree_pine"),lib.Model("tree_dead_oak"),lib.Model("tree_birch"),lib.Model("tree_corrupted")};
    config.rocks=new[]{lib.Model("rock_boulder"),lib.Model("rubble_pile")};
    config.buildings=new[]{lib.Model("building_house"),lib.Model("building_forge"),lib.Model("building_house"),lib.Model("building_watchtower")};
    config.church=lib.Model("building_chapel");config.crypt=lib.Model("building_mausoleum");
    config.graves=new[]{lib.Model("grave_marker"),lib.Model("grave_cross"),lib.Model("grave_marker")};
    config.fence=lib.Model("cemetery_fence");config.pillar=lib.Model("crypt_pillar");config.chest=lib.Model("crypt_chest");
    config.torch=lib.Model("crypt_brazier");config.cryptWall=lib.Model("crypt_wall");config.coffin=lib.Model("prop_sarcophagus");
    config.altar=lib.Model("crypt_altar");config.rubble=lib.Model("rubble_pile");
    config.cryptFloors=new[]{lib.Model("crypt_floor_slabs"),lib.Model("crypt_floor_seal"),lib.Model("crypt_floor_grate")};
    config.floor=config.cryptFloors[0];config.version=10;EditorUtility.SetDirty(config);
    var palette=Resources.Load<NocturnisCryptPalette>("CryptPalette");
    if(palette){palette.wall=palette.wallCracked=palette.wallShelves=config.cryptWall;palette.wallLow=lib.Model("crypt_wall_half");palette.pillar=palette.pillarDecorated=config.pillar;palette.torchMounted=lib.Model("crypt_torch");EditorUtility.SetDirty(palette);}
    AssetDatabase.SaveAssets();Validate();File.WriteAllText("Logs/integration-prepare.json","{\"passed\":true,\"models\":62,\"riggedActors\":20,\"clipsPerActor\":13}");
    Debug.Log("ACTI_PREPARED 62 assets, 20 skinned actors");
   }catch(Exception e){Debug.LogException(e);File.WriteAllText("Logs/integration-error.txt",e.ToString());EditorApplication.Exit(1);}
  }
  static GameObject MakeWeapon(NocturnisActILibrary lib,string id) {
   string path=Runtime+"/"+id+"_equipped.prefab";var root=new GameObject(id+"_equipped");
   var obj=UnityEngine.Object.Instantiate(lib.Model(id),root.transform);var renderers=obj.GetComponentsInChildren<Renderer>();
   var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
   float size=id=="weapon_staff"||id=="weapon_scythe"?1.7f:id=="weapon_bow"?1.25f:1.05f;
   float scale=size/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
   // Authored handle coordinates in the imported prefab frame. The asymmetric
   // scythe/axe blade and bow string must never be used to centre the grip.
   Vector3 handle=id=="weapon_sword"?new Vector3(0,.14f,0):id=="weapon_axe"?new Vector3(0,.38f,0):id=="weapon_staff"?new Vector3(0,.88f,0):id=="weapon_scythe"?new Vector3(0,.82f,0):new Vector3(-.29f,.72f,0);
   obj.transform.localScale*=scale;obj.transform.localPosition=-handle*scale;
   // A relaxed blade hangs forward/down. Long staves and bows remain upright.
   if(id=="weapon_sword"||id=="weapon_axe"){
    var cradle=new GameObject("BladeRestAngle").transform;cradle.SetParent(root.transform,false);
    obj.transform.SetParent(cradle,false);cradle.localRotation=Quaternion.Euler(155,0,-5);
   }
   if(id=="weapon_sword"||id=="weapon_axe"){
    var tip=new GameObject("TipAnchor").transform;tip.SetParent(obj.transform,false);
    Vector3 sourceTip=id=="weapon_sword"?new Vector3(0,1.278f,0):new Vector3(-.42f,.95f,0);
    tip.localPosition=Quaternion.Inverse(obj.transform.localRotation)*sourceTip;
   }
   var grip=new GameObject("GripAnchor").transform;grip.SetParent(root.transform,false);
   var saved=PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);return saved;
  }
  static float PalmHeight(string id){return id=="hero_inquisitor"?.475f:id=="hero_blood_weaver"?.505f:id=="hero_dusk_hunter"?.51f:id=="hero_void_herald"?.515f:.475f;}
  static Vector3 PalmCentre(Vector3[] vertices,float h,float bottom,float width,string id,int sign){
   float y=PalmHeight(id);var points=vertices.Where(v=>Mathf.Abs((v.y-bottom)/h-y)<.012f&&v.x*sign>width*.36f).ToArray();
   Need(points.Length>12,"Unable to locate palm surface "+id+"/"+sign);
   return new Vector3(points.Select(v=>v.x).OrderBy(v=>v).ElementAt(points.Length/2),points.Select(v=>v.y).OrderBy(v=>v).ElementAt(points.Length/2),points.Select(v=>v.z).OrderBy(v=>v).ElementAt(points.Length/2));
  }
  static bool PalmVertex(Vector3 v,Vector3 palm,float h,int sign){
   return v.x*sign>Mathf.Abs(palm.x)-.038f*h&&v.y<palm.y+.024f*h&&v.y>palm.y-.085f*h&&Mathf.Abs(v.z-palm.z)<.085f*h;
  }
  static void MakeActor(NocturnisAssetLibrary.Entry spec,GameObject source,NocturnisActILibrary.Entry item) {
   string folder=PathOf(spec.id);Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var original=UnityEngine.Object.Instantiate(source);var filter=original.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name.EndsWith("_LOD1"));
   var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=spec.id+"_skin";
   var verts=mesh.vertices;var normals=mesh.normals;var matrix=filter.transform.localToWorldMatrix;
   for(int i=0;i<verts.Length;i++){verts[i]=matrix.MultiplyPoint3x4(verts[i]);normals[i]=matrix.inverse.transpose.MultiplyVector(normals[i]).normalized;}
   mesh.vertices=verts;mesh.normals=normals;
   // FBX can contain a reflected root transform. Baking that transform into
   // positions must also reverse each triangle, otherwise the skin is inside out.
   if(matrix.determinant<0)for(int sub=0;sub<mesh.subMeshCount;sub++){var indices=mesh.GetTriangles(sub);for(int i=0;i<indices.Length;i+=3){int tmp=indices[i];indices[i]=indices[i+2];indices[i+2]=tmp;}mesh.SetTriangles(indices,sub);}
   Debug.Log("ACTOR_TRANSFORM "+spec.id+" determinant="+matrix.determinant+" bounds="+mesh.bounds);
   mesh.RecalculateBounds();
   var root=new GameObject(spec.id);var bones=new List<Transform>();var map=new Dictionary<string,Transform>();
   float h=mesh.bounds.size.y;float bottom=mesh.bounds.min.y;float width=mesh.bounds.size.x;
   bool beast=spec.id=="enemy_mutant_hound"||spec.id=="enemy_ichor_spitter";
   bool four=spec.id=="boss_malazar_mutated";bool hero=spec.id.StartsWith("hero_");
   var palms=new Dictionary<string,Vector3>();
   if(hero){
    foreach(int sign in new[]{-1,1})palms[sign<0?"R":"L"]=PalmCentre(verts,h,bottom,width,spec.id,sign);
    // Close the dangling fingers around the handle while retaining the palm,
    // thumb roots, UVs and silhouette of the original generated glove.
    var palm=palms["R"];float knuckle=palm.y-.008f*h;
    for(int i=0;i<verts.Length;i++)if(PalmVertex(verts[i],palm,h,-1)&&verts[i].y<knuckle){
     float distance=knuckle-verts[i].y;float angle=Mathf.Min(2.5f,distance/(.027f*h));
     float depth=verts[i].z-palm.z;
     verts[i].y=knuckle-Mathf.Sin(angle)*(.027f*h-depth);
     verts[i].z=palm.z+(1-Mathf.Cos(angle))*.027f*h+depth*Mathf.Cos(angle);
     normals[i]=Quaternion.AngleAxis(-angle*Mathf.Rad2Deg,Vector3.right)*normals[i];
    }
    // Preserve the imported smooth normals and UV seams outside the fingers.
    mesh.vertices=verts;mesh.normals=normals;mesh.RecalculateBounds();
   }
   Action<string,string,Vector3> bone=(name,parent,pos)=>{var t=new GameObject(name).transform;t.SetParent(parent==null?root.transform:map[parent]);t.position=pos;map[name]=t;bones.Add(t);};
   Func<float,float,float,Vector3> p=(x,y,z)=>new Vector3(x*h,y*h+bottom,z*h);
   bone("hips",null,p(0,beast?.52f:.53f,0));bone("chest","hips",p(0,beast?.58f:.71f,beast?.12f:0));bone("head","chest",p(0,beast?.66f:.86f,beast?.32f:0));
   float wristX=Mathf.Min(width*.45f/h,.31f),shoulderX=Mathf.Min(width*.27f/h,.19f);
   foreach(int sign in new[]{-1,1}){
    string s=sign<0?"R":"L";
    if(beast){
     bone("arm"+s,"chest",p(sign*.19f,.56f,.25f));bone("forearm"+s,"arm"+s,p(sign*.20f,.27f,.26f));bone("hand"+s,"forearm"+s,p(sign*.20f,.08f,.29f));
     bone("thigh"+s,"hips",p(sign*.20f,.53f,-.32f));bone("shin"+s,"thigh"+s,p(sign*.21f,.26f,-.33f));bone("foot"+s,"shin"+s,p(sign*.21f,.06f,-.30f));
    }else{
     bone("arm"+s,"chest",p(sign*shoulderX,.77f,0));bone("forearm"+s,"arm"+s,p(sign*(shoulderX+wristX)*.5f,.63f,0));bone("hand"+s,"forearm"+s,hero?palms[s]+new Vector3(0,.04f*h,-.006f*h):p(sign*wristX,.49f,0));
     bone("thigh"+s,"hips",p(sign*.085f,.53f,0));bone("shin"+s,"thigh"+s,p(sign*.10f,.29f,0));bone("foot"+s,"shin"+s,p(sign*.11f,.075f,.015f));
    }
    bone("handslot."+(s=="R"?"r":"l"),"hand"+s,hero?palms[s]+new Vector3(0,0,.018f*h):map["hand"+s].position+p(0,-.025f,.015f)-p(0,0,0));
    if(four){bone("extraArm"+s,"chest",p(sign*.17f,.65f,0));bone("extraHand"+s,"extraArm"+s,p(sign*.25f,.48f,0));}
   }
   var index=bones.Select((b,i)=>new{b.name,i}).ToDictionary(x=>x.name,x=>x.i);
   var weight=new float[verts.Length,bones.Count];
   for(int i=0;i<verts.Length;i++){
    var v=verts[i];float y=(v.y-bottom)/h,x=Mathf.Abs(v.x)/h,z=v.z/h;string s=v.x<0?"R":"L";
    string a,b;float blend;
    if(beast){
     if(y>.54f){a=z>.20f?"head":"chest";b="hips";blend=z<-.1f?.8f:.1f;}
     else {bool front=z>0;a=front?"arm"+s:"thigh"+s;b=front?"forearm"+s:"shin"+s;blend=1-Mathf.InverseLerp(.20f,.48f,y);if(y<.15f){a=b;b=front?"hand"+s:"foot"+s;blend=1-Mathf.InverseLerp(.05f,.15f,y);}}
    }else if(y>.83f){a="chest";b="head";blend=Mathf.InverseLerp(.82f,.89f,y);}
    else if(x>Mathf.Lerp(shoulderX*.75f,wristX*.77f,Mathf.InverseLerp(.78f,.46f,y)) && y>.40f){
     a="arm"+s;b="forearm"+s;blend=1-Mathf.InverseLerp(.59f,.68f,y);
     if(y<.56f){a="forearm"+s;b="hand"+s;blend=1-Mathf.InverseLerp(.47f,.56f,y);}
     if(four && y<.62f){a="extraArm"+s;b="extraHand"+s;blend=1-Mathf.InverseLerp(.46f,.61f,y);}
    }else if(y>.50f){a="hips";b="chest";blend=Mathf.InverseLerp(.52f,.68f,y);}
    else{
     a="thigh"+s;b="shin"+s;blend=1-Mathf.InverseLerp(.25f,.34f,y);
     if(y>.46f){a="hips";b="thigh"+s;blend=1-Mathf.InverseLerp(.46f,.53f,y);}
     if(y<.13f){a="shin"+s;b="foot"+s;blend=1-Mathf.InverseLerp(.06f,.13f,y);}
     // Connected coat skirts share softened leg influence across their centre.
     if(y>.19f && x<.055f){weight[i,index["hips"]]=.45f;}
    }
    float available=1-weight[i,index["hips"]];weight[i,index[a]]+=available*(1-blend);weight[i,index[b]]+=available*blend;
   }
   // Smooth neighbouring influences across seams while retaining the four strongest weights.
   var neighbors=new List<int>[verts.Length];for(int i=0;i<verts.Length;i++)neighbors[i]=new List<int>();
   var tri=mesh.triangles;for(int i=0;i<tri.Length;i+=3)for(int j=0;j<3;j++){int a=tri[i+j],b=tri[i+(j+1)%3];neighbors[a].Add(b);neighbors[b].Add(a);}
   for(int iteration=0;iteration<2;iteration++){
    var next=(float[,])weight.Clone();for(int i=0;i<verts.Length;i++)if(neighbors[i].Count>0)for(int b=0;b<bones.Count;b++){float sum=0;foreach(int n in neighbors[i])sum+=weight[n,b];next[i,b]=weight[i,b]*.6f+sum/neighbors[i].Count*.4f;}weight=next;
   }
   // The grip surface follows a single hand rigidly. Smoothing forearm weights
   // into the palm would visibly detach the fingers as the wrist bends.
   if(hero)for(int i=0;i<verts.Length;i++)foreach(int sign in new[]{-1,1}){
    string side=sign<0?"R":"L";if(!PalmVertex(verts[i],palms[side],h,sign))continue;
    for(int j=0;j<bones.Count;j++)weight[i,j]=0;weight[i,index["hand"+side]]=1;
   }
   var bw=new BoneWeight[verts.Length];for(int i=0;i<verts.Length;i++){
    int v=i;var top=Enumerable.Range(0,bones.Count).OrderByDescending(b=>weight[v,b]).Take(4).ToArray();float sum=top.Sum(b=>weight[v,b]);
    bw[i]=new BoneWeight{boneIndex0=top[0],boneIndex1=top[1],boneIndex2=top[2],boneIndex3=top[3],weight0=weight[i,top[0]]/sum,weight1=weight[i,top[1]]/sum,weight2=weight[i,top[2]]/sum,weight3=weight[i,top[3]]/sum};
   }
   mesh.boneWeights=bw;mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*root.transform.localToWorldMatrix).ToArray();mesh.RecalculateTangents();
   string meshPath=folder+"/Skin.asset";var savedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(savedMesh){EditorUtility.CopySerialized(mesh,savedMesh);UnityEngine.Object.DestroyImmediate(mesh);mesh=savedMesh;}else AssetDatabase.CreateAsset(mesh,meshPath);
   var skin=root.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterials=filter.GetComponent<Renderer>().sharedMaterials;skin.bones=bones.ToArray();skin.rootBone=map["hips"];skin.updateWhenOffscreen=true;skin.localBounds=new Bounds(Vector3.up*h*.6f,new Vector3(h*2,h*2,h*2));
   var profile=root.AddComponent<ActorModelProfile>();profile.bodyHeight=h;profile.groundOffset=-bottom;profile.fitActionDuration=true;profile.garbMaterialPrefix="";
   var clips=new List<AnimationClip>();foreach(string name in Names)clips.Add(MakeClip(name,root,bones,h,beast,spec.id,folder));
   item.clips=clips.ToArray();item.prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+spec.id+".prefab");
   UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(original);
  }
  static float PoseKey(float t,float windup,float contact,float follow){
   float[] times={0,.25f,.42f,.58f,1};float[] values={0,windup,contact,follow,0};
   for(int i=1;i<times.Length;i++)if(t<=times[i])return Mathf.Lerp(values[i-1],values[i],Mathf.SmoothStep(0,1,Mathf.InverseLerp(times[i-1],times[i],t)));return 0;
  }
  static void LegPose(float t,bool running,float h,float hipY,out float thigh,out float shin,out float foot){
   float duty=running?.35f:.62f,stride=running?1.45f:.60f,span=stride*duty;
   float z,lift=0,footPitch=0;
   if(t<duty){float p=t/duty;z=Mathf.Lerp(span*.5f,-span*.5f,p);footPitch=Mathf.Lerp(-5,12,p);}
   else{float p=(t-duty)/(1-duty);z=Mathf.Lerp(-span*.5f,span*.5f,Mathf.SmoothStep(0,1,p));lift=Mathf.Sin(p*Mathf.PI)*(running?.085f:.045f);footPitch=-14*Mathf.Sin(p*Mathf.PI);}
   float upper=.24f*h,lower=Mathf.Sqrt(.215f*.215f+.015f*.015f)*h;
   float dz=(z+.015f)*h,dy=(running?.085f:.075f)*h+lift*h-hipY;
   float distance=Mathf.Clamp(Mathf.Sqrt(dz*dz+dy*dy),Mathf.Abs(upper-lower)+.001f,(upper+lower)*.998f);
   float direction=Mathf.Atan2(-dz,-dy),a=Mathf.Acos(Mathf.Clamp((upper*upper+distance*distance-lower*lower)/(2*upper*distance),-1,1));
   float knee=Mathf.PI-Mathf.Acos(Mathf.Clamp((upper*upper+lower*lower-distance*distance)/(2*upper*lower),-1,1));
   float bindLower=Mathf.Atan2(-.015f,.215f)*Mathf.Rad2Deg;
   thigh=(direction-a)*Mathf.Rad2Deg;shin=knee*Mathf.Rad2Deg-bindLower;foot=-(thigh+shin)+footPitch;
  }
  static void PoseBone(string clip,string n,float t,float h,bool beast,string id,ref Vector3 position,out Vector3 e){
   float phase=t*Mathf.PI*2;int side=n.EndsWith("R")?-1:1;e=Vector3.zero;
   bool run=clip=="Running_A",walk=clip=="Walking_A",moving=run||walk;
   // A quiet asymmetric ready pose, with breathing in the torso instead of
   // rotating the entire character like a display figurine.
   if(n=="chest")e=new Vector3(1,0,0);if(n=="armR")e.x=-5;if(n=="forearmR")e.x=-12;
   if(n=="armL")e.x=-3;if(n=="forearmL")e.x=-5;
   if(clip=="Idle"){
    if(n=="chest"){e.x+=Mathf.Sin(phase)*.85f;e.z=Mathf.Sin(phase)*.3f;}
    if(n=="head")e=new Vector3(Mathf.Sin(phase+.4f)*.45f,Mathf.Sin(phase)*1.8f,0);
    if(n=="hips"){position.y+=Mathf.Sin(phase)*h*.0018f;position.x+=Mathf.Sin(phase)*h*.0015f;}
    if(n.StartsWith("arm"))e.z=side*(1.5f+Mathf.Sin(phase)*.35f);
    if(n.StartsWith("forearm"))e.x+=Mathf.Sin(phase)*.5f;
   }else if(moving&&!beast){
    float legT=Mathf.Repeat(t+(side>0?.5f:0),1),hipOffset=run?-.075f+Mathf.Pow(Mathf.Sin(phase),2)*.008f:-.04f+Mathf.Pow(Mathf.Sin(phase),2)*.003f;
    LegPose(legT,run,h,(.53f+hipOffset)*h,out float thigh,out float shin,out float foot);
    if(n.StartsWith("thigh"))e=new Vector3(thigh,0,0);if(n.StartsWith("shin"))e=new Vector3(shin,0,0);if(n.StartsWith("foot"))e=new Vector3(foot,0,0);
    float swing=Mathf.Sin(phase+(side>0?Mathf.PI:0));bool held=n.EndsWith("R")&&id.StartsWith("hero_");
    if(n.StartsWith("arm"))e=new Vector3(-(run?12:5)-swing*(held?9:run?22:14),0,side*2);
    if(n.StartsWith("forearm"))e.x=-(held?22:run?35:14)-Mathf.Max(0,-swing)*(run?12:6);
    if(n=="chest")e=new Vector3(run?9:4,Mathf.Sin(phase)*4,Mathf.Sin(phase)*.7f);
    if(n=="head")e=new Vector3(run?-5:-2,-Mathf.Sin(phase)*2,0);
    if(n=="hips"){position.y+=hipOffset*h;e.y=-Mathf.Sin(phase)*2;}
   }else if(moving){
    float swing=Mathf.Sin(phase+(side>0?Mathf.PI:0));
    if(n.StartsWith("thigh")||n.StartsWith("arm"))e.x=swing*(run?26:17);
    if(n.StartsWith("shin")||n.StartsWith("forearm"))e.x=-Mathf.Max(0,-swing)*36;
    if(n=="hips")position.y+=Mathf.Sin(phase*2)*h*.008f;
   }else if(clip=="Death_A"){
    float fall=Mathf.SmoothStep(0,1,Mathf.Clamp01(t*1.4f));if(n=="hips"){e.x=-82*fall;position.y-=h*.39f*fall;}
    if(n.StartsWith("thigh"))e.x=30*fall;if(n.StartsWith("shin"))e.x=-55*fall;if(n.StartsWith("arm"))e.z=side*35*fall;
   }else if(clip=="Dodge_Forward"){
    float weight=Mathf.Sin(Mathf.PI*t);if(n=="chest")e.x=28*weight;if(n=="hips")position.y-=h*.07f*weight;
    if(n.StartsWith("thigh"))e.x=-30*weight;if(n.StartsWith("shin"))e.x=55*weight;if(n.StartsWith("arm"))e.x=-22*weight;
   }else if(clip=="Hit_A"){
    float hit=t<.18f?Mathf.SmoothStep(0,1,t/.18f):1-Mathf.SmoothStep(0,1,(t-.18f)/.82f);
    if(n=="chest")e=new Vector3(-12*hit,8*hit,2*hit);if(n=="head")e.x=-8*hit;if(n=="hips")position.z-=h*.009f*hit;
   }else{
    bool guard=clip=="Block"||clip=="Defense_A",ranged=clip=="1H_Ranged_Shoot",cast=clip=="Cast_A"||(ranged&&id!="hero_dusk_hunter"),spin=clip=="2H_Melee_Attack_Spin",slice=clip=="1H_Melee_Attack_Slice_Horizontal";
    if(guard){
     if(n=="chest")e=new Vector3(PoseKey(t,6,9,6),PoseKey(t,-12,-14,-8),0);
     if(n=="armL")e=new Vector3(PoseKey(t,-68,-78,-60),0,PoseKey(t,24,30,24));
     if(n=="forearmL")e.x=PoseKey(t,-58,-64,-54);if(n=="armR")e.x+=PoseKey(t,-20,-25,-18);
    }else if(cast){
     if(n=="chest")e=new Vector3(PoseKey(t,-5,8,10),PoseKey(t,-12,16,20),0);
     if(n=="armL")e=new Vector3(PoseKey(t,-62,-92,-75),PoseKey(t,10,0,-12),PoseKey(t,12,18,26));
     if(n=="forearmL")e.x=PoseKey(t,-50,-12,-4);if(n=="handL")e.x=PoseKey(t,-15,18,22);
     if(n=="armR")e.x+=PoseKey(t,-10,-18,-12);if(n=="forearmR")e.x+=PoseKey(t,-8,-4,0);
     if(n=="head")e.x=PoseKey(t,5,-4,-3);
    }else if(ranged){
     // The archery arm chains are solved against the actual bow/string below.
     if(n=="chest")e=new Vector3(PoseKey(t,0,3,4),PoseKey(t,-12,-12,-6),0);
     if(n=="head")e.y=PoseKey(t,10,10,6);
    }else{
     if(n=="chest")e=new Vector3(PoseKey(t,-7,8,12),PoseKey(t,slice?-26:-14,slice?22:12,slice?34:20),0);
     if(n=="armR")e=new Vector3(PoseKey(t,slice?-65:-115,-54,slice?-35:-20)-5,PoseKey(t,slice?-22:0,slice?30:0,slice?45:0),PoseKey(t,slice?-32:-12,slice?18:-6,slice?38:0));
     if(n=="forearmR")e.x=PoseKey(t,-28,-8,8)-12;
     if(n=="armL")e=new Vector3(PoseKey(t,-20,-12,-6),0,PoseKey(t,12,18,22));
     if(n=="forearmL")e.x=PoseKey(t,-24,-18,-10)-5;
     if(n=="head")e.y=-PoseKey(t,slice?-12:-6,slice?12:6,slice?16:8);
     if(spin&&n=="hips")e.y=360*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.84f,t));
    }
    if(n=="hips")position.y+=PoseKey(t,-.012f,-.006f,-.016f)*h;
    if(n.StartsWith("thigh"))e.x+=PoseKey(t,-5,-3,-7);if(n.StartsWith("shin"))e.x+=PoseKey(t,10,6,14);
    if(beast&&n=="head")e.x=PoseKey(t,-18,24,16);
   }
   if(n.StartsWith("extraArm")){e.x=clip=="Idle"?Mathf.Sin(phase)*1.2f:-Mathf.Sin(phase+side)*12;e.z=clip=="Idle"?0:side*10;}
   if(n.StartsWith("extraHand"))e.x=clip=="Idle"?Mathf.Sin(phase):-10-Mathf.Sin(phase)*7;
  }
  static void SolveArm(Dictionary<string,Transform> map,Dictionary<string,Vector3> rest,string side,Vector3 grip,Vector3 pole,Quaternion handRotation){
   var upper=map["arm"+side];var lower=map["forearm"+side];var hand=map["hand"+side];var slot=map["handslot."+(side=="R"?"r":"l")];
   Vector3 wrist=grip-handRotation*rest[slot.name],start=upper.position,delta=wrist-start;float a=rest[lower.name].magnitude,b=rest[hand.name].magnitude,d=Mathf.Clamp(delta.magnitude,.015f,a+b-.001f);var direction=delta.normalized;
   float along=(a*a+d*d-b*b)/(2*d),outward=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
   var bend=Vector3.ProjectOnPlane(pole-start,direction).normalized;var elbow=start+direction*along+bend*outward;
   upper.rotation=Quaternion.FromToRotation(rest[lower.name],elbow-start);lower.rotation=Quaternion.FromToRotation(rest[hand.name],start+direction*d-elbow);hand.rotation=handRotation;
  }
  static AnimationClip MakeClip(string name,GameObject root,List<Transform> bones,float height,bool beast,string id,string folder){
   bool loop=name=="Idle"||name=="Running_A"||name=="Walking_A";float duration=name=="Idle"?3.6f:name=="Death_A"?1.1f:name=="Walking_A"?1.12f:name=="Running_A"?.76f:.72f;
   var clip=new AnimationClip{name=name,legacy=true,frameRate=60,wrapMode=loop?WrapMode.Loop:WrapMode.ClampForever};
   var map=bones.ToDictionary(b=>b.name);var rest=bones.ToDictionary(b=>b.name,b=>b.localPosition);var curves=bones.ToDictionary(b=>b.name,b=>Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray());
   var previous=bones.ToDictionary(b=>b.name,b=>Quaternion.identity);int frames=Mathf.CeilToInt(duration*60);
   var fractions=Enumerable.Range(0,frames+1).Select(f=>(float)f/frames).Concat(new[]{.25f,.42f,.58f}).Distinct().OrderBy(t=>t);
   foreach(float t in fractions){
    foreach(var bone in bones){var p=rest[bone.name];PoseBone(name,bone.name,t,height,beast,id,ref p,out Vector3 e);bone.localPosition=p;bone.localRotation=Quaternion.Euler(e);}
    bool longWeapon=id=="hero_dusk_hunter"||id=="hero_blood_weaver"||id=="hero_void_herald";
    if(longWeapon&&name!="Death_A"&&name!="Dodge_Forward"){
     var hand=map["handR"];hand.rotation=Quaternion.Euler(3,id=="hero_dusk_hunter"?25:0,id=="hero_dusk_hunter"?0:-4);
    }
    if(id=="hero_dusk_hunter"&&name=="1H_Ranged_Shoot"&&t>0&&t<1){
     float ready=PoseKey(t,1,1,.82f);var right=map["handslot.r"].position;var left=map["handslot.l"].position;
     var draw=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.35f,t));var release=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.42f,.52f,t));
     var bowTarget=new Vector3(-.10f,.68f,.25f)*height;var stringTarget=bowTarget+new Vector3(release*.035f,0,Mathf.Lerp(-.10f,-.16f,draw)+release*.025f)*height;
     var bowRotation=Quaternion.Slerp(map["handR"].rotation,Quaternion.Euler(0,90,0),ready);
     SolveArm(map,rest,"R",Vector3.Lerp(right,bowTarget,ready),Vector3.Lerp(map["forearmR"].position,new Vector3(-.27f,.60f,.10f)*height,ready),bowRotation);
     SolveArm(map,rest,"L",Vector3.Lerp(left,stringTarget,ready),Vector3.Lerp(map["forearmL"].position,new Vector3(.22f,.64f,-.06f)*height,ready),Quaternion.Slerp(map["handL"].rotation,Quaternion.Euler(0,-80,0),ready));
    }
    foreach(var bone in bones){var q=bone.localRotation;if(Quaternion.Dot(previous[bone.name],q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous[bone.name]=q;var c=curves[bone.name];float time=t*duration;
     c[0].AddKey(time,q.x);c[1].AddKey(time,q.y);c[2].AddKey(time,q.z);c[3].AddKey(time,q.w);c[4].AddKey(time,bone.localPosition.x);c[5].AddKey(time,bone.localPosition.y);c[6].AddKey(time,bone.localPosition.z);
    }
   }
   foreach(var bone in bones){string path=AnimationUtility.CalculateTransformPath(bone,root.transform);var c=curves[bone.name];
    for(int k=0;k<c.Length;k++)for(int f=0;f<c[k].length;f++){AnimationUtility.SetKeyLeftTangentMode(c[k],f,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(c[k],f,AnimationUtility.TangentMode.ClampedAuto);}
    string[] properties={"localRotation.x","localRotation.y","localRotation.z","localRotation.w","localPosition.x","localPosition.y","localPosition.z"};for(int k=0;k<7;k++)clip.SetCurve(path,typeof(Transform),properties[k],c[k]);
    bone.localPosition=rest[bone.name];bone.localRotation=Quaternion.identity;
   }
   clip.EnsureQuaternionContinuity();string assetPath=folder+"/"+name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);if(existing){EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);clip=existing;}else AssetDatabase.CreateAsset(clip,assetPath);return clip;
  }
  public static void Validate(){
   var lib=Resources.Load<NocturnisActILibrary>("ActILibrary");Need(lib&&lib.entries.Length==62,"Expected 62 models");
   var actors=lib.entries.Where(e=>e.clips!=null&&e.clips.Length>0).ToArray();Need(actors.Length==20,"Expected 20 actors");
   foreach(var a in actors){Need(a.prefab.GetComponentInChildren<SkinnedMeshRenderer>(),"No skin "+a.id);Need(a.clips.Length>=13&&Names.All(n=>a.clips.Any(c=>c&&c.name==n))&&a.clips.All(c=>c&&c.legacy&&c.length>.1f),"Incomplete animation "+a.id);}
  }
  public static void Polish(){
   var config=Resources.Load<KryptaAssets>("KryptaAssets");
   var source=AssetDatabase.LoadAssetAtPath<Material>(Library+"/Materials/Nocturnis_CutStone.mat");
   string path=Runtime+"/ForestGround.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!mat){mat=new Material(source);AssetDatabase.CreateAsset(mat,path);}
   mat.SetColor("_Color",new Color(.25f,.21f,.16f));mat.SetFloat("_UseMR",0);mat.SetFloat("_Roughness",.95f);mat.SetFloat("_Metallic",0);
   config.earthMaterial=mat;config.floorMaterial=source;EditorUtility.SetDirty(config);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();
  }
  public static void Windows(){Build(BuildTarget.StandaloneWindows64,"Builds/Windows/Nocturnis.exe");}
  public static void Linux(){Build(BuildTarget.StandaloneLinux64,"Builds/Linux/Nocturnis.x86_64");}
  static void Build(BuildTarget target,string path){try{Validate();PlayerSettings.bundleVersion=Version;Directory.CreateDirectory(Path.GetDirectoryName(path));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=path,target=target,options=BuildOptions.None});Need(report.summary.result==BuildResult.Succeeded,"Build failed");File.WriteAllText("Logs/build-"+target+".json","{\"passed\":true,\"bytes\":"+report.summary.totalSize+"}");}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 }
}
