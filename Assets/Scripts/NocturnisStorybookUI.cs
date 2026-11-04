using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaHUD
 {
  // Simple shapes keep the interface crisp at the same scale as the KayKit models.
  void StoryShape(Rect r,float radius,Color color)
  {
   radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
   if(radius<1){Fill(r,color);return;}
   // One rounded rectangle avoids alpha seams and repeated draw calls at the corners.
   Color previous=GUI.color;GUI.color=Color.white;
   GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);
   GUI.color=previous;
  }
  Texture2D storyPaperTexture,storyLeatherTexture;
  readonly Color StoryBrass=new Color(.73f,.52f,.25f),StoryPaleGold=new Color(1,.84f,.52f);
  void StoryTextures()
  {
   if(storyPaperTexture)return;
   storyPaperTexture=new Texture2D(128,128,TextureFormat.RGBA32,false);
   storyLeatherTexture=new Texture2D(128,128,TextureFormat.RGBA32,false);
   storyPaperTexture.wrapMode=storyLeatherTexture.wrapMode=TextureWrapMode.Repeat;
   storyPaperTexture.filterMode=storyLeatherTexture.filterMode=FilterMode.Bilinear;
   storyPaperTexture.hideFlags=storyLeatherTexture.hideFlags=HideFlags.HideAndDontSave;
   for(int y=0;y<128;y++)for(int x=0;x<128;x++)
   {
    int hash=unchecked((x*73856093)^(y*19349663));float grain=((hash&255)/255f-.5f)*.025f;
    float fibre=Mathf.Sin(x*.21f+y*.09f)*.008f+Mathf.Sin(y*.67f)*.004f;
    storyPaperTexture.SetPixel(x,y,new Color(.93f+grain+fibre,.855f+grain+fibre,.69f+grain+fibre,1));
    float wood=Mathf.Sin(y*.19f+Mathf.Sin(x*.035f)*.8f)*.013f+grain*.65f;
    storyLeatherTexture.SetPixel(x,y,new Color(.27f+wood,.165f+wood*.7f,.09f+wood*.5f,1));
   }
   storyPaperTexture.Apply();storyLeatherTexture.Apply();
  }
  void StoryPaper(Rect r,float strength=1)
  {
   StoryTextures();Color previous=GUI.color;GUI.color=new Color(1,1,1,strength);
   GUI.DrawTextureWithTexCoords(r,storyPaperTexture,new Rect(0,0,r.width/128,r.height/128),true);GUI.color=previous;
   Fill(new Rect(r.x,r.y,r.width,1),new Color(1,.96f,.79f,.6f*strength));
   Fill(new Rect(r.x,r.yMax-1,r.width,1),new Color(.4f,.24f,.1f,.25f*strength));
  }
  void StoryLeather(Rect r)
  {
   StoryTextures();Fill(new Rect(r.x+2,r.y+5,r.width,r.height),new Color(.035f,.025f,.015f,.7f));
   Fill(r,new Color(.16f,.095f,.045f));
   Rect inset=new Rect(r.x+2,r.y+2,r.width-4,r.height-4);Color previous=GUI.color;GUI.color=Color.white;
   GUI.DrawTextureWithTexCoords(inset,storyLeatherTexture,new Rect(0,0,r.width/128,r.height/128),true);GUI.color=previous;
   Fill(new Rect(r.x+3,r.y+3,r.width-6,1),new Color(.82f,.59f,.31f,.6f));
   Fill(new Rect(r.x+3,r.yMax-4,r.width-6,2),new Color(.11f,.055f,.02f,.8f));
  }
  // Compatibility entry point used by combat feedback and skill tooltips.
  void StoryPanel(Rect r,float corner=12){ClassicFrame(r,Mathf.Max(12,corner));}
  void StoryScroll(Rect r)
  {
   StoryPanel(r,0);
   StoryLeather(new Rect(r.x-7,r.y-4,r.width+14,12));
   StoryLeather(new Rect(r.x-7,r.yMax-7,r.width+14,12));
   for(int side=0;side<2;side++)
   {float x=side==0 ? r.x-6 : r.xMax+2;Fill(new Rect(x,r.y-5,4,14),StoryBrass);Fill(new Rect(x,r.yMax-8,4,14),StoryBrass);}
  }
  // A small moon-and-star shield, drawn from native shapes at the current UI scale.
  void StoryCrest(Rect r)
  {
   Vector2 m=r.center;float s=Mathf.Min(r.width,r.height),stroke=Mathf.Max(1.2f,s*.025f);
   Color face=new Color(.29f,.12f,.075f),edge=StoryPaleGold;
   Circle(m.x+1,m.y+3,s*.48f,new Color(.08f,.035f,.015f,.6f));
   Circle(m.x,m.y,s*.48f,new Color(.49f,.24f,.11f));
   Circle(m.x,m.y,s*.43f,face);
   Vector2[] shield={new Vector2(-.35f,-.37f),new Vector2(.35f,-.37f),new Vector2(.32f,.12f),new Vector2(.2f,.29f),new Vector2(0,.44f),new Vector2(-.2f,.29f),new Vector2(-.32f,.12f)};
   Circle(m.x,m.y-s*.04f,s*.28f,face);
   for(int i=0;i<shield.Length;i++)Line(m+shield[i]*s,m+shield[(i+1)%shield.Length]*s,stroke,edge);
   Circle(m.x-s*.04f,m.y-s*.075f,s*.19f,edge);
   Circle(m.x+s*.045f,m.y-s*.13f,s*.165f,face);
   StoryStar(m+new Vector2(.115f,.035f)*s,s*.105f,warmWhite,stroke*.8f);
   Line(m+new Vector2(-.11f,.29f)*s,m+new Vector2(.11f,.29f)*s,stroke*.65f,bronze);
  }
  void StoryClassGlyph(Rect r,int hero)
  {
   Vector2 m=r.center;float s=Mathf.Min(r.width,r.height)*.33f,w=Mathf.Max(1.4f,r.width*.055f);
   Color accent=StoryColors[Mathf.Clamp(hero,0,4)];
   Circle(m.x,m.y,r.width*.5f,StoryBrass);
   Circle(m.x,m.y,r.width*.5f-1.5f,new Color(.91f,.81f,.61f));
   if(hero==0)
   {
    Line(m+new Vector2(-s,s),m+new Vector2(s,-s),w*1.5f,accent);
    Line(m+new Vector2(-s,-s*.1f),m+new Vector2(s*.1f,s),w,accent);
   }
   else if(hero==1)
   {
    Circle(m.x,m.y+s*.36f,s*.65f,accent);
    Line(m+new Vector2(-s*.45f,s*.3f),m+new Vector2(0,-s),w*2,accent);
    Line(m+new Vector2(s*.45f,s*.3f),m+new Vector2(0,-s),w*2,accent);
    Circle(m.x-s*.18f,m.y+s*.35f,w*.6f,paper);
   }
   else if(hero==2)
   {
    Line(m+new Vector2(-s*.5f,-s),m+new Vector2(s*.35f,0),w,accent);
    Line(m+new Vector2(s*.35f,0),m+new Vector2(-s*.5f,s),w,accent);
    Line(m+new Vector2(-s*.5f,-s),m+new Vector2(-s*.5f,s),w*.6f,accent);
    Line(m-Vector2.right*s,m+Vector2.right*s,w,paper);
   }
   else if(hero==3)StoryStar(m,s,accent,w);
   else
   {
    Vector2[] leaf={new Vector2(0,-s),new Vector2(s*.7f,-s*.05f),new Vector2(s*.3f,s*.7f),new Vector2(-s*.3f,s*.7f),new Vector2(-s*.7f,-s*.05f)};
    for(int i=0;i<leaf.Length;i++)Line(m+leaf[i],m+leaf[(i+1)%leaf.Length],w,accent);
    Line(m-Vector2.up*s*.5f,m+Vector2.up*s,w,accent);
    Line(m,m+new Vector2(s*.45f,-s*.2f),w*.8f,accent);
   }
  }
  void StoryCheck(Vector2 p,float size,Color color)
  {
   Line(p+new Vector2(-size*.6f,0),p+new Vector2(-size*.1f,size*.5f),2,color);
   Line(p+new Vector2(-size*.1f,size*.5f),p+new Vector2(size*.8f,-size*.55f),2,color);
  }
  static readonly Color[] StoryColors={new Color(.6f,.27f,.08f),new Color(.61f,.15f,.2f),new Color(.12f,.38f,.45f),new Color(.39f,.24f,.58f),new Color(.25f,.43f,.16f)};
  Color StoryAccent
  {
   get{return StoryColors[Mathf.Clamp(game.SelectedHero,0,4)];}
  }
  void StorySkillIcon(Rect r,int skill)
  {
   Color c=StoryAccent;
   Fill(r,new Color(.88f,.76f,.54f));
   Color bright=c;
   Vector2 m=r.center;float s=Mathf.Min(r.width,r.height)*.30f;
   float stroke=Mathf.Clamp(r.width*.055f,1.7f,3.5f);
   if(skill==0)
   {
    if(game.SelectedHero==2)
    {
     Line(m+new Vector2(-s,-s),m+new Vector2(s*.2f,0),stroke,bright);
     Line(m+new Vector2(s*.2f,0),m+new Vector2(-s,s),stroke,bright);
     Line(m+new Vector2(-s,-s),m+new Vector2(-s,s),stroke*.5f,bright);
     Line(m+new Vector2(-s*.7f,0),m+new Vector2(s,0),stroke,bright);
     Line(m+new Vector2(s*.55f,-s*.4f),m+new Vector2(s,0),stroke,bright);
     Line(m+new Vector2(s*.55f,s*.4f),m+new Vector2(s,0),stroke,bright);
    }
    else
    {
     Line(m+new Vector2(-s,s),m+new Vector2(s,-s),stroke*1.7f,bright);
     Line(m+new Vector2(-s,-s*.1f),m+new Vector2(s*.1f,s),stroke,gold);
     Circle(m.x-s,m.y+s,stroke,gold);
    }
   }
   else if(skill==1)
   {
    if(game.SelectedHero==3){StoryStar(m,s,bright,stroke);Circle(m.x,m.y,s*.23f,paper);}
    else if(game.SelectedHero==4)
    {
     Line(m+Vector2.up*s,m-Vector2.up*s,stroke,bright);
     for(int i=-1;i<=1;i+=2){Line(m,m+new Vector2(i*s,-s*.6f),stroke,bright);Line(m+Vector2.up*s*.3f,m+new Vector2(i*s,s),stroke,bright);}
    }
    else
    {
     Circle(m.x,m.y+s*.25f,s*.66f,bright);
     Line(m+new Vector2(-s*.5f,s*.2f),m+new Vector2(s*.12f,-s),stroke*2,bright);
     Line(m+new Vector2(s*.5f,s*.2f),m+new Vector2(s*.12f,-s),stroke*2,bright);
     Circle(m.x,m.y+s*.35f,s*.26f,paper);
    }
   }
   else if(skill==2)
   {
    Vector2[] points={m+new Vector2(-s,-s*.7f),m+new Vector2(s,-s*.7f),m+new Vector2(s*.8f,s*.35f),m+new Vector2(0,s),m+new Vector2(-s*.8f,s*.35f)};
    for(int i=0;i<points.Length;i++)Line(points[i],points[(i+1)%points.Length],stroke,bright);
    Line(m-Vector2.up*s*.4f,m+Vector2.up*s*.4f,stroke,bright);Line(m-Vector2.right*s*.4f,m+Vector2.right*s*.4f,stroke,bright);
   }
   else if(skill==3)
   {
    Circle(m.x,m.y,s,bright);Circle(m.x,m.y,s-stroke,new Color(.88f,.76f,.54f));
    StoryStar(m,s*.55f,paper,stroke*.8f);
   }
   else if(skill==4)
   {
    for(int i=-1;i<=1;i++)
    {
     Vector2 p=m+new Vector2(i*s*.7f,-i*s*.7f);
     Circle(p.x,p.y,s*.47f,bright);Circle(p.x,p.y,s*.47f-stroke,new Color(.88f,.76f,.54f));
    }
   }
   else
   {
    StoryStar(m,s,bright,stroke*1.5f);
    Circle(m.x,m.y,s*.27f,paper);
    for(int i=0;i<4;i++){float a=(i*90+45)*Mathf.Deg2Rad;Circle(m.x+Mathf.Cos(a)*s,m.y+Mathf.Sin(a)*s,stroke*.8f,gold);}
   }
  }
  void StoryStar(Vector2 p,float size,Color color,float stroke)
  {
   Line(p-Vector2.right*size,p+Vector2.right*size,stroke,color);
   Line(p-Vector2.up*size,p+Vector2.up*size,stroke,color);
   Line(p+new Vector2(-size,-size)*.55f,p+new Vector2(size,size)*.55f,stroke*.7f,color);
   Line(p+new Vector2(-size,size)*.55f,p+new Vector2(size,-size)*.55f,stroke*.7f,color);
  }
  int storyTooltipSkill=-1,storyHoveredSkill=-1;
  bool storyTooltipPreview,storyHoveredPreview;
  float storyTooltipSince;
  Rect storyTooltipAnchor;
  GUIStyle storyTooltipTitle,storyTooltipBody,storyTooltipHint;
  void QueueStoryTooltip(Rect anchor,int skill,bool preview=false)
  {
   bool active=game.State==KryptaGame.Phase.Playing || (preview && game.State==KryptaGame.Phase.Title && menuConfirmation==MenuConfirmation.None && gateAt<0);
   if(!active || !anchor.Contains(Event.current.mousePosition))return;
   storyTooltipSkill=skill;storyTooltipAnchor=anchor;storyTooltipPreview=preview;
  }
  string StorySkillName(int skill)
  {return skill==0 ? "Atak podstawowy" : skill==1 ? game.Hero.skill : skill==2 ? game.Hero.defense : game.ExtraName(skill-3);}
  void DrawStoryTooltip()
  {
   bool visible=game.State==KryptaGame.Phase.Playing || (storyTooltipPreview && game.State==KryptaGame.Phase.Title && menuConfirmation==MenuConfirmation.None && gateAt<0);
   if(storyTooltipSkill<0 || !visible){storyHoveredSkill=-1;return;}
   if(storyHoveredSkill!=storyTooltipSkill || storyHoveredPreview!=storyTooltipPreview)
   {storyHoveredSkill=storyTooltipSkill;storyHoveredPreview=storyTooltipPreview;storyTooltipSince=Time.unscaledTime;}
   if(Time.unscaledTime-storyTooltipSince<.25f)return;
   if(storyTooltipTitle==null)
   {
    storyTooltipTitle=new GUIStyle(text){fontSize=17,fontStyle=FontStyle.Bold};
    storyTooltipBody=new GUIStyle(small){fontSize=13};
    storyTooltipHint=new GUIStyle(small){fontSize=12,fontStyle=FontStyle.Bold};
   }
   string copy=SkillDetail(storyTooltipSkill),name=StorySkillName(storyTooltipSkill);
   float active=storyTooltipPreview ? 0 : game.SkillActiveRemaining(storyTooltipSkill);
   if(active>0)copy+="\n\nAKTYWNE: "+active.ToString("0.0")+" s\n"+game.SkillActiveSummary(storyTooltipSkill);
   float bodyHeight=storyTooltipBody.CalcHeight(new GUIContent(copy),284),titleHeight=storyTooltipTitle.CalcHeight(new GUIContent(name),278);
   float height=titleHeight+bodyHeight+73;
   Rect r=new Rect(Mathf.Clamp(storyTooltipAnchor.center.x-159,16,946),Mathf.Max(18,storyTooltipAnchor.y-height-12),318,height);
   StoryPanel(r,12);
   Fill(new Rect(r.x+15,r.y+15,3,titleHeight),StoryAccent);
   Label(r.x+26,r.y+12,278,titleHeight+4,name,storyTooltipTitle,paper);
   Label(r.x+17,r.y+titleHeight+25,284,bodyHeight+3,copy,storyTooltipBody,paper);
   string reason=storyTooltipPreview ? "" : game.SkillBlockReason(storyTooltipSkill);
   string hint=storyTooltipSkill==5 && game.Level<4 ? "ODBLOKUJESZ NA POZIOMIE 4" : !string.IsNullOrEmpty(reason) ? reason : storyTooltipPreview ? "MOC TEGO BOHATERA" : "GOTOWE  •  "+NocturnisInput.Text("book")+": KSIĘGA MOCY";
   Label(r.x+17,r.yMax-32,284,25,hint,storyTooltipHint,!string.IsNullOrEmpty(reason) ? new Color(1,.57f,.40f) : gold);
  }
 }
}
