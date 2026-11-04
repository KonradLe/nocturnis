using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaHUD
 {
  bool talentRespecConfirm;
  int talentUiHero=-1,talentSelection;
  int talentHovered=-1;
  GUIStyle talentNodeCaption,talentDetailTitle;
  static readonly Color[] TalentBranchColors={
   new Color(.92f,.54f,.32f),new Color(.48f,.72f,.83f),new Color(.68f,.58f,.86f)
  };
  static readonly int[] TalentGeneralIcons={15,16,17,18,19,20};
  static readonly int[] TalentClassSkills={0,5,1};

  // Publicly stable layout in the 1280 x 720 UI coordinate space, also used
  // by visual QA. Bottom nodes are the entry points; progression goes up.
  static Rect TalentNodeRect(int node)
  {
   int branch=node/3,tier=node%3;
   float x=196+branch*252+(tier==1 ? (branch==1 ? 0 : branch==0 ? 23 : -23) : 0);
   float y=495-tier*131.5f,size=tier==2 ? 96 : 84;
   return new Rect(x-size*.5f,y-size*.5f,size,size);
  }
  Color TalentTint(int branch)
  {return Color.Lerp(game.Hero.color,TalentBranchColors[branch],branch==0 ? .2f : .6f);}
  void TalentPanel()
  {
   if(talentUiHero!=game.SelectedHero)
   {
    talentUiHero=game.SelectedHero;talentRespecConfirm=false;talentSelection=0;
    for(int i=0;i<9;i++)if(game.TalentBlocked(i)==""){talentSelection=i;break;}
   }
   if(talentNodeCaption==null)
   {
    talentNodeCaption=new GUIStyle(center){fontSize=14,fontStyle=FontStyle.Bold};
    talentDetailTitle=new GUIStyle(heading){fontSize=23,alignment=TextAnchor.MiddleCenter,wordWrap=true};
   }
   bool panelEnabled=GUI.enabled;GUI.enabled=panelEnabled && !talentRespecConfirm;
   Fill(new Rect(0,0,1280,720),new Color(.008f,.006f,.012f,.96f));
   RpgSurface(new Rect(44,27,1192,664));Frame(new Rect(44,27,1192,664));
   Label(76,43,790,47,"ŚCIEŻKI UPADŁYCH GWIAZD",heading,gold);
   Label(79,97,740,27,game.Hero.person+" • "+game.Hero.name+" • poziom "+game.Level,small,paper);
   RpgBadge(new Rect(891,51,303,40),"PUNKTY TALENTÓW: "+game.TalentPoints,game.TalentPoints>0 ? gold : muted);
   Label(890,99,304,23,game.TalentSpent+" rozdanych  /  "+NocturnisTalentRules.Earned(game.Level)+" zdobytych",center,muted);

   // The open stone board keeps every path visible, with no text cards
   // obscuring links. The selected node alone owns the detail/learn button.
   Fill(new Rect(73,139,752,477),new Color(.025f,.025f,.033f,.68f));
   Outline(new Rect(73,139,752,477),new Color(.32f,.27f,.20f,.8f));
   RpgAccentLine(new Vector2(843,143),new Vector2(843,612),new Color(.43f,.32f,.19f),2);
   for(int branch=0;branch<3;branch++)
   {
    Color tint=TalentTint(branch);float x=196+branch*252;
    Label(x-112,146,224,29,game.TalentBranchName(branch),button,tint);
    RpgAccentLine(new Vector2(x-62,178),new Vector2(x+62,178),new Color(tint.r,tint.g,tint.b,.45f),1);
    for(int tier=1;tier<3;tier++)
    {
     int node=branch*3+tier;Rect lower=TalentNodeRect(node-1),upper=TalentNodeRect(node);
     Vector2 a=new Vector2(lower.center.x,lower.y-3),b=new Vector2(upper.center.x,upper.yMax+13);
     bool open=game.TalentRank(node-1)>=2 && game.Level>=NocturnisTalentRules.LevelGate(node);
     RpgAccentLine(a,b,new Color(.012f,.01f,.008f),7);
     RpgAccentLine(a,b,open ? gold : new Color(.27f,.23f,.19f),open ? 3.5f : 2);
     Vector2 middle=Vector2.Lerp(a,b,.5f);
     Diamond(middle.x,middle.y,open ? 5 : 3,open ? new Color(1,.81f,.43f) : new Color(.38f,.32f,.24f));
    }
   }
   talentHovered=-1;
   for(int node=0;node<9;node++)DrawTalentTreeNode(TalentNodeRect(node),node);
   int hint=talentHovered>=0 ? talentHovered : talentSelection;
   string hintLine=talentHovered>=0 ? game.TalentName(hint)+" • "+(game.TalentBlocked(hint)=="" ? "możesz rozwinąć" : game.TalentBlocked(hint)) : "Wybierz ikonę. Połączone węzły rozwijasz od dołu ku górze.";
   Label(95,574,710,33,hintLine,center,paper);
   TalentDetailCard();

   bool enabled=GUI.enabled;GUI.enabled=enabled && game.TalentRespecBlocked()=="";
   string cost=game.TalentRespecCost==0 ? "ZMIEŃ TALENTY • BEZ OPŁATY" : "ZMIEŃ TALENTY • "+game.TalentRespecCost+" ZŁOTA";
   if(Button(new Rect(77,637,350,34),cost))talentRespecConfirm=true;
   GUI.enabled=enabled;
   Label(438,632,381,43,game.Zone==0 ? "1 punkt za awans • następny węzeł wymaga 2 rang poprzedniego" : "W Oakhaven możesz zmienić wybrane talenty.",center,muted);
   if(Button(new Rect(834,637,365,34),"ZAMKNIJ • WRÓĆ DO WYPRAWY")){talentRespecConfirm=false;game.Resume();}
   GUI.enabled=panelEnabled;
   if(talentRespecConfirm)TalentRespecConfirmation();
  }
  void TalentVisualIcon(Rect rect,int node)
  {
   if(node<3)
   {
    int skill=TalentClassSkills[node];
    if(skillArt)SkillIcon(rect,skill);else StorySkillIcon(rect,skill);
   }
   else RpgAtlasIcon(rect,TalentGeneralIcons[node-3]);
  }
  void TalentStoneNode(Rect rect,Color edge,bool selected,bool capstone)
  {
   Fill(new Rect(rect.x+4,rect.y+5,rect.width,rect.height),new Color(0,0,0,.65f));
   Fill(rect,new Color(.12f,.115f,.12f));
   Outline(rect,new Color(.30f,.27f,.23f));
   Outline(new Rect(rect.x+2,rect.y+2,rect.width-4,rect.height-4),edge);
   Outline(new Rect(rect.x+6,rect.y+6,rect.width-12,rect.height-12),new Color(.045f,.037f,.027f));
   float corner=capstone ? 14 : 10;
   foreach(Vector2 c in new[]{new Vector2(rect.x,rect.y),new Vector2(rect.xMax,rect.y),new Vector2(rect.x,rect.yMax),new Vector2(rect.xMax,rect.yMax)})
   {
    float sx=c.x==rect.x ? 1 : -1,sy=c.y==rect.y ? 1 : -1;
    RpgAccentLine(c+new Vector2(2*sx,corner*sy),c+new Vector2(corner*sx,2*sy),edge,selected ? 3 : 2);
   }
   if(selected)
   {
    Outline(new Rect(rect.x-5,rect.y-5,rect.width+10,rect.height+10),gold);
    Diamond(rect.center.x,rect.y-10,5,gold);
   }
   if(capstone)
   {
    Diamond(rect.x-5,rect.center.y,5,edge);Diamond(rect.xMax+5,rect.center.y,5,edge);
   }
  }
  void DrawTalentTreeNode(Rect rect,int node)
  {
   int rank=game.TalentRank(node),maximum=NocturnisTalentRules.Maximum(node);
   bool selected=talentSelection==node,hover=GUI.enabled && rect.Contains(Event.current.mousePosition);
   bool locked=game.Level<NocturnisTalentRules.LevelGate(node) || (node%3>0 && game.TalentRank(node-1)<2);
   bool ready=game.TalentBlocked(node)=="";
   Color tint=TalentTint(node/3),edge=selected || ready ? gold : rank>0 ? tint : new Color(.38f,.34f,.29f);
   if(hover){talentHovered=node;edge=warmWhite;}
   TalentStoneNode(rect,edge,selected,node%3==2);
   Rect imageRect=new Rect(rect.x+9,rect.y+9,rect.width-18,rect.height-18);
   TalentVisualIcon(imageRect,node);
   if(locked)Fill(imageRect,new Color(.015f,.015f,.02f,.57f));
   else if(rank==0 && !ready)Fill(imageRect,new Color(.015f,.015f,.02f,.24f));
   if(locked)
   {
    Rect seal=new Rect(rect.xMax-23,rect.yMax-25,29,29);
    Fill(seal,new Color(.055f,.045f,.035f,.96f));RpgAtlasIcon(seal,21);Outline(seal,new Color(.48f,.41f,.31f));
   }
   // Explicit pips and a numeric rank make states readable without relying
   // on color, including in reduced-flash mode and at 960 x 540.
   RpgBadge(new Rect(rect.center.x-28,rect.yMax-1,56,24),rank+" / "+maximum,rank>0 ? gold : muted);
   for(int i=0;i<maximum;i++)
    Diamond(rect.center.x+(i-(maximum-1)*.5f)*13,rect.yMax+30,3.5f,i<rank ? new Color(1,.79f,.39f) : new Color(.32f,.29f,.25f));
   if(GUI.Button(rect,GUIContent.none,GUIStyle.none))SelectTalentNode(node);
  }
  void SelectTalentNode(int node)
  {
   if(node<0 || node>=NocturnisTalentRules.Count)return;
   talentSelection=node;game.UISound("ui-click");
  }
  string TalentRankValue(int node,int rank)
  {
   if(node==2)return rank>0 ? "+18%" : "nieaktywny";
   if(node==3)return "+"+(rank*12)+" HP";
   float value=node==0 ? rank*5 : node==1 ? rank*8 : node==4 ? rank*5 : node==5 ? rank*35 : node==6 ? rank*3 : node==7 ? rank*10 : rank*12;
   return (node==4 || node==8 ? "-" : "+")+value.ToString("0")+"%";
  }
  void TalentDetailCard()
  {
   int node=Mathf.Clamp(talentSelection,0,8),rank=game.TalentRank(node),maximum=NocturnisTalentRules.Maximum(node);
   Color tint=TalentTint(node/3);
   RpgSurface(new Rect(854,143,352,473));Frame(new Rect(854,143,352,473));
   Label(876,161,308,28,game.TalentBranchName(node/3),button,tint);
   Rect icon=new Rect(974,199,112,112);TalentStoneNode(icon,gold,true,node%3==2);
   TalentVisualIcon(new Rect(icon.x+10,icon.y+10,92,92),node);
   RpgBadge(new Rect(1002,302,56,25),rank+" / "+maximum,gold);
   Label(874,334,312,48,game.TalentName(node),talentDetailTitle,paper);
   string preview=rank<maximum ? TalentRankValue(node,rank)+"  →  "+TalentRankValue(node,rank+1) : TalentRankValue(node,rank)+" • PEŁNA RANGA";
   RpgBadge(new Rect(879,390,302,39),preview,rank<maximum ? gold : tint);
   Label(877,440,306,69,game.TalentEffect(node),text,paper);
   string reason=game.TalentBlocked(node);
   string requirement=reason=="" ? "Koszt: 1 punkt • dostępne: "+game.TalentPoints : reason;
   Label(877,514,306,39,requirement,small,reason=="" ? gold : muted);
   bool enabled=GUI.enabled;GUI.enabled=enabled && reason=="";
   if(Button(new Rect(876,565,308,36),rank==maximum ? "TALENT ROZWINIĘTY" : "ROZWIŃ • 1 PUNKT",true))game.SpendTalent(node);
   GUI.enabled=enabled;
  }
  void TalentRespecConfirmation()
  {
   Fill(new Rect(0,0,1280,720),new Color(0,0,0,.78f));
   RpgSurface(new Rect(365,225,550,265));Frame(new Rect(365,225,550,265));
   Label(395,249,490,40,"ODNOWIENIE PRZYSIĘGI",heading,gold);
   Label(395,302,490,83,"Odzyskasz wszystkie "+game.TalentSpent+" wydanych punktów. "+
    (game.TalentRespecCost==0 ? "Pierwsza zmiana jest bezpłatna." : "Koszt: "+game.TalentRespecCost+" złota.")+
    "\nZdrowie i zasób nie zostaną odnowione.",text,paper);
   if(Button(new Rect(395,413,234,40),"ZACHOWAJ TALENTY"))talentRespecConfirm=false;
   bool enabled=GUI.enabled;GUI.enabled=enabled && game.TalentRespecBlocked()=="";
   if(Button(new Rect(650,413,234,40),"ZWROT PUNKTÓW",true)){game.RespecTalents();talentRespecConfirm=false;}
   GUI.enabled=enabled;
  }
 }
}
