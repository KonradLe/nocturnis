using UnityEngine;

namespace Krypta
{
 // No Update: the game's scaled-time effect lifecycle advances these cached sprites.
 public sealed class NocturnisSoftVfxSprite:MonoBehaviour
 {
  Renderer surface;MaterialPropertyBlock properties;Color tint;Vector3 origin,drift;Vector2 size;
  float seed;bool floor,flame;
  public void Prepare(Renderer renderer,Vector3 local,Vector2 extent,Color color,Vector3 movement,bool ground,bool fire,float randomSeed)
  {surface=renderer;properties=new MaterialPropertyBlock();origin=local;size=extent;tint=color;drift=movement;floor=ground;flame=fire;seed=randomSeed;}
  public void Advance(Camera camera,float t,float fade,bool reduced)
  {
   if(!surface || !camera)return;
   float movement=reduced ? .45f : 1;float sway=flame ? Mathf.Sin(t*6+seed)*.10f*movement : Mathf.Sin(t*2.4f+seed)*.12f*movement;
   transform.position=transform.parent.TransformPoint(origin+drift*t+Vector3.right*sway);
   transform.rotation=floor ? Quaternion.Euler(90,0,0) : camera.transform.rotation*Quaternion.Euler(0,0,flame ? sway*11 : seed*23+t*12);
   float grow=flame ? Mathf.Lerp(.78f,1.2f,Mathf.Min(1,t*3)) : Mathf.Lerp(.82f,1.35f,t);
   transform.localScale=new Vector3(size.x*grow,size.y*grow,1);
   Color c=tint;c.a*=fade*Mathf.SmoothStep(0,1,Mathf.Clamp01(t*9));properties.SetColor("_Color",c);properties.SetFloat("_Age",t+seed);surface.SetPropertyBlock(properties);
  }
 }
 public sealed partial class KryptaGame
 {
  Mesh softVfxQuad;Material softMistMaterial,softFlameMaterial;bool ownsSoftMist;
  void EnsureSoftVfxResources()
  {
   if(softVfxQuad)return;
   softVfxQuad=new Mesh{name="Shared Nocturnis soft volume quad"};
   softVfxQuad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
   softVfxQuad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
   softVfxQuad.colors=new[]{Color.white,Color.white,Color.white,Color.white};softVfxQuad.triangles=new[]{0,1,2,0,2,3};softVfxQuad.RecalculateBounds();
   var shader=Resources.Load<Shader>("Shaders/SoftMist");ownsSoftMist=shader!=null;
   softMistMaterial=shader ? new Material(shader){name="Shared Nocturnis smoke and void core"} : art.particleMaterial;
   if(shader){softFlameMaterial=new Material(shader){name="Shared Nocturnis tapered flame"};softFlameMaterial.SetFloat("_DstBlend",1);softFlameMaterial.SetFloat("_Shape",1);}
   else softFlameMaterial=art.particleMaterial;
  }
  void DisposeSoftVfxResources()
  {if(softVfxQuad)Destroy(softVfxQuad);if(ownsSoftMist){if(softMistMaterial)Destroy(softMistMaterial);if(softFlameMaterial)Destroy(softFlameMaterial);}softVfxQuad=null;softMistMaterial=softFlameMaterial=null;ownsSoftMist=false;}
  void SoftVolume(Transform root,string name,Vector3 local,Vector2 size,Color tint,bool smoke=false,bool floor=false,bool flame=false,Vector3 drift=default(Vector3),float seed=0)
  {
   EnsureSoftVfxResources();var o=new GameObject(name);o.transform.SetParent(root,false);o.transform.localPosition=local;o.transform.localScale=new Vector3(size.x,size.y,1);
   o.AddComponent<MeshFilter>().sharedMesh=softVfxQuad;var renderer=o.AddComponent<MeshRenderer>();renderer.sharedMaterial=smoke ? softMistMaterial : flame ? softFlameMaterial : art.particleMaterial;
   renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
   var sprite=o.AddComponent<NocturnisSoftVfxSprite>();sprite.Prepare(renderer,local,size,tint,drift,floor,flame,seed);sprite.Advance(View,.12f,ReducedFlashes ? .72f : 1,ReducedFlashes);
  }
  void SoftLineGlow(LineRenderer source,Color tint,float width)
  {
   var points=new Vector3[source.positionCount];source.GetPositions(points);
   var glow=VfxLine(source.transform,"Soft trailing light",points,tint,width,source.loop);glow.sharedMaterial=art.particleMaterial;glow.widthCurve=source.widthCurve;
  }
  void SoftCastVolume(Transform root,int hero,float radius,bool defense,bool ultimate)
  {
   float size=defense ? .8f : ultimate ? 1.35f : 1;float r=radius*.6f;
   if(hero==0)
   {
    SoftVolume(root,"Warm scorched ground",Vector3.up*.17f,new Vector2(radius*2.1f,radius*2.1f),new Color(.95f,.25f,.025f,.48f),false,true);
    int count=defense ? 4 : ultimate ? 8 : 6;
    for(int i=0;i<count;i++)
    {
     float a=i*Mathf.PI*2/count;Vector3 p=new Vector3(Mathf.Sin(a)*r,.75f*size,Mathf.Cos(a)*r);
     SoftVolume(root,"Smoldering plume",p+Vector3.up*.5f,new Vector2(1.15f,2.65f)*size,new Color(.24f,.12f,.065f,.5f),true,false,true,Vector3.up*.75f,i);
     SoftVolume(root,"Judgement flame",p,new Vector2(.95f,2.3f)*size,new Color(1,.29f,.025f,.9f),false,false,true,Vector3.up*.38f,i+.7f);
     SoftVolume(root,"Hot flame core",p-Vector3.up*.28f,new Vector2(.48f,1.35f)*size,new Color(1,.74f,.24f,.85f),false,false,true,Vector3.up*.18f,i+1.2f);
    }
   }
   else if(hero==1)
   {
    SoftVolume(root,"Sanguine shadow",Vector3.up*.15f,new Vector2(radius*1.9f,radius*1.9f),new Color(.20f,.012f,.023f,.65f),true,true);
    for(int i=0;i<6;i++)
    {
     float a=i*1.0472f;Vector3 p=new Vector3(Mathf.Sin(a)*r,.55f,Mathf.Cos(a)*r);
     SoftVolume(root,"Blood mist",p,new Vector2(1.65f,.95f)*size,new Color(.46f,.025f,.045f,.7f),true,false,false,Vector3.up*.35f,i);
     SoftVolume(root,"Crimson pulse",p+Vector3.up*.2f,new Vector2(.85f,.65f)*size,new Color(.82f,.09f,.13f,.6f),false,false,false,Vector3.up*.2f,i);
    }
   }
   else if(hero==2)
   {
    for(int i=0;i<5;i++)
    {
     float a=i*1.2566f;Vector3 p=new Vector3(Mathf.Sin(a)*r,.4f,Mathf.Cos(a)*r);
     SoftVolume(root,"Shadow dust",p,new Vector2(1.5f,1.1f)*size,new Color(.09f,.17f,.23f,.55f),true,false,false,Vector3.up*.3f,i);
     SoftVolume(root,"Moonlit feather trail",p+Vector3.up*.55f,new Vector2(.3f,1.5f)*size,new Color(.35f,.67f,.95f,.65f),false,false,true,Vector3.down*.4f,i);
    }
   }
   else if(hero==3)
   {
    float y=defense ? 1 : ultimate ? 2.25f : 1.4f;
    SoftVolume(root,"Void ground haze",Vector3.up*.16f,new Vector2(radius*1.8f,radius*1.8f),new Color(.29f,.12f,.62f,.42f),false,true);
    SoftVolume(root,"Soft singularity corona",Vector3.up*y,new Vector2(3.1f,3.1f)*size,new Color(.55f,.22f,.94f,.85f));
    SoftVolume(root,"Dark event core",Vector3.up*y-View.transform.forward*.03f,new Vector2(1.35f,1.35f)*size,new Color(.012f,.004f,.025f,1),true);
    for(int i=0;i<5;i++)
    {float a=i*1.2566f;Vector3 p=new Vector3(Mathf.Sin(a)*r,.45f+Mathf.Sin(a*2)*.3f,Mathf.Cos(a)*r);SoftVolume(root,"Infall nebula",p,new Vector2(1.45f,.85f)*size,new Color(.25f,.08f,.46f,.65f),true,false,false,-p*.25f,i);}
   }
   else
   {
    SoftVolume(root,"Disturbed earth",Vector3.up*.16f,new Vector2(radius*1.9f,radius*1.9f),new Color(.15f,.11f,.055f,.5f),true,true);
    for(int i=0;i<6;i++)
    {float a=i*1.0472f;Vector3 p=new Vector3(Mathf.Sin(a)*r,.45f,Mathf.Cos(a)*r);SoftVolume(root,"Root earth plume",p,new Vector2(1.65f,1.1f)*size,new Color(.28f,.22f,.11f,.65f),true,false,false,Vector3.up*.4f,i);SoftVolume(root,"Living sap glow",p+Vector3.up*.4f,new Vector2(.45f,1.5f)*size,new Color(.35f,.65f,.16f,.5f),false,false,true,Vector3.up*.25f,i);}
   }
  }
 }
}
