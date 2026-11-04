using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaHUD
 {
  Texture2D rpgItemAtlas;
  string rpgHoverTitle,rpgHoverHint;
  Color rpgHoverColor;
  void RpgAssets(){if(!rpgItemAtlas)rpgItemAtlas=Resources.Load<Texture2D>("UI/RpgItemAtlas");}
  void RpgAtlasIcon(Rect r,int index)
  {
   RpgAssets();if(index<0 || index>=25 || !rpgItemAtlas)return;
   GUI.DrawTextureWithTexCoords(r,rpgItemAtlas,new Rect((index%5)/5f,1-(index/5+1)/5f,1/5f,1/5f),true);
  }
  Color RpgQualityColor(KryptaGame.Relic item)
  {
   if(item==null)return new Color(.28f,.25f,.24f);
   if(!item.identified)return new Color(.69f,.58f,.82f);
   if(item.legend>0)return new Color(1,.62f,.22f);
   return item.rank>=4 ? new Color(.73f,.43f,.98f) : item.rank>=2 ? new Color(.34f,.68f,1) : new Color(.72f,.73f,.69f);
  }
  string RpgQualityName(KryptaGame.Relic item)
  {return item==null ? "PUSTY SLOT" : !item.identified ? "NIEROZPOZNANY" : item.legend>0 ? "LEGENDARNY" : item.rank>=4 ? "EPICKI" : item.rank>=2 ? "RZADKI" : item.rank==0 ? "PODSTAWOWY" : "ZWYKŁY";}
  int RpgRelicAtlasIndex(KryptaGame.Relic item)
  {
   if(item==null)return 21;
   int hero=Mathf.Clamp(game.SelectedHero,0,4);
   return item.slot==0 ? hero : item.slot==1 ? (hero==3 ? 7 : hero==2 || hero==4 ? 6 : 5) : hero==3 ? 9 : hero==4 ? 10 : 8;
  }
  void RpgRelicIcon(Rect r,KryptaGame.Relic item)
  {
   if(item==null)return;
   Color before=GUI.color;if(!item.identified)GUI.color=new Color(before.r*.45f,before.g*.38f,before.b*.6f,before.a);
   RpgAtlasIcon(r,RpgRelicAtlasIndex(item));GUI.color=before;
   if(!item.identified)Label(r.x,r.y,r.width,r.height,"?",new GUIStyle(heading){alignment=TextAnchor.MiddleCenter,fontSize=32},paper);
  }
  void RpgAccentLine(Vector2 a,Vector2 b,Color color,float width=2)
  {Line(a,b,width,color);}
  void RpgSurface(Rect r)
  {
   Frame(r);Rect inner=new Rect(r.x+9,r.y+9,r.width-18,r.height-18);
   Fill(inner,new Color(.018f,.021f,.031f,.97f));
   if(menuArt){Color old=GUI.color;GUI.color=new Color(.45f,.38f,.56f,.19f);GUI.DrawTexture(inner,menuArt,ScaleMode.ScaleAndCrop,true);GUI.color=old;}
   Fill(new Rect(inner.x,inner.y,inner.width,Mathf.Min(63,inner.height)),new Color(.015f,.018f,.025f,.6f));
  }
  void RpgBadge(Rect r,string caption,Color color)
  {
   Fill(r,new Color(.025f,.025f,.034f,.94f));Outline(r,new Color(color.r*.6f,color.g*.6f,color.b*.6f,.85f));
   Label(r.x+5,r.y+1,r.width-10,r.height-2,caption,center,color);
  }
  bool RpgItemTile(Rect r,KryptaGame.Relic item,bool selected,bool equipped=false,bool allowTooltip=true)
  {
   Color quality=RpgQualityColor(item);
   Fill(new Rect(r.x+2,r.y+4,r.width,r.height),new Color(0,0,0,.48f));
   Fill(r,selected ? new Color(.13f,.105f,.07f,.98f) : new Color(.028f,.032f,.043f,.98f));
   Outline(r,selected ? gold : new Color(.22f,.23f,.25f));
   Rect inlay=new Rect(r.x+3,r.y+3,r.width-6,r.height-6);Outline(inlay,item==null ? new Color(.12f,.14f,.17f) : new Color(quality.r*.6f,quality.g*.6f,quality.b*.6f,.85f));
   if(item!=null)
   {
    float pad=Mathf.Max(6,r.width*.1f);RpgRelicIcon(new Rect(r.x+pad,r.y+pad,r.width-pad*2,r.height-pad*2-5),item);
    Fill(new Rect(r.x+5,r.yMax-6,r.width-10,2),quality);
    Label(r.xMax-30,r.yMax-26,26,18,"+"+item.rank,center,quality);
    if(item.protectedItem)RpgAtlasIcon(new Rect(r.x+2,r.y+2,23,23),21);
    if(equipped)
    {
     Rect equippedBadge=new Rect(r.x+5,r.y+5,19,18);
     Fill(equippedBadge,new Color(.025f,.025f,.034f,.96f));Outline(equippedBadge,gold);
     Label(equippedBadge.x,equippedBadge.y,equippedBadge.width,equippedBadge.height,"✓",center,gold);
    }
    if(item.legend>0)
    {
     // Axis-aligned ornament stays inside the current scroll-view clip.
     Color edge=new Color(1,.66f,.25f);
     Outline(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),edge);
     Outline(new Rect(r.x+5,r.y+5,r.width-10,r.height-10),new Color(.58f,.34f,.10f));
     float gem=Mathf.Clamp(r.width*.065f,4,7);
     Fill(new Rect(r.x+1,r.y+1,gem,gem),edge);
     Fill(new Rect(r.xMax-gem-1,r.y+1,gem,gem),edge);
     Fill(new Rect(r.x+1,r.yMax-gem-1,gem,gem),edge);
     Fill(new Rect(r.xMax-gem-1,r.yMax-gem-1,gem,gem),edge);
    }
    if(allowTooltip && GUI.enabled && r.Contains(Event.current.mousePosition)){rpgHoverTitle=game.RelicName(item);rpgHoverHint=RpgQualityName(item)+(item.protectedItem ? " • CHRONIONY" : "");rpgHoverColor=quality;}
   }
   else
   {
    Vector2 p=r.center;Color emptyMark=new Color(.19f,.19f,.21f);
    Fill(new Rect(p.x-6,p.y,12,1),emptyMark);
    Fill(new Rect(p.x,p.y-6,1,12),emptyMark);
   }
   if(selected){Outline(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),gold);Fill(new Rect(r.center.x-4,r.y,8,3),gold);}
   return GUI.Button(r,GUIContent.none,GUIStyle.none);
  }
  void RpgHoverReset(){rpgHoverTitle=null;rpgHoverHint=null;}
  void RpgHoverDraw()
  {
   if(string.IsNullOrEmpty(rpgHoverTitle) || Event.current.type!=EventType.Repaint)return;
   float w=285,h=Mathf.Max(66,small.CalcHeight(new GUIContent(NocturnisInput.Format(rpgHoverTitle)),w-28)+37);
   Vector2 mouse=Event.current.mousePosition;Rect r=new Rect(Mathf.Clamp(mouse.x+18,12,1268-w),Mathf.Clamp(mouse.y+22,12,708-h),w,h);
   Fill(new Rect(r.x+3,r.y+5,r.width,r.height),new Color(0,0,0,.65f));Frame(r);
   Label(r.x+14,r.y+10,w-28,h-35,rpgHoverTitle,small,rpgHoverColor);
   Label(r.x+14,r.yMax-25,w-28,19,rpgHoverHint,small,muted);
  }
 }
}
