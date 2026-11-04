using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaHUD
 {
  int bookPage,alchemyPage,enchantSlot,enchantKind=1;
  float gateAt=-1,runeAt=-10;
  int previewGarb=-1;
  int equipmentSelection,groundSelection;
  bool inspectGround;
  void DetailedEquipment(){DrawRpgEquipment();}
  void LegendaryFrame(Rect r)
  {float t=game.ReducedFlashes ? 0 : Time.unscaledTime*.6f;for(int n=0;n<5;n++){float x=r.x+8+(r.width-16)*n/4;float y=r.y+2+Mathf.Sin(t+n)*2;Line(new Vector2(x-4,y+4),new Vector2(x,y-2),1.5f,gold);Line(new Vector2(x,y-2),new Vector2(x+5,y+3),1.5f,gold);}}
  void Line(Vector2 a,Vector2 b,float width,Color color)
  {
   // Axis-aligned strips retain the active GUI scale and scroll clipping.
   // Rotating GUI.matrix loses one of those transforms in nested groups.
   if(Event.current.type!=EventType.Repaint || width<=0)return;
   Vector2 delta=b-a;float dx=Mathf.Abs(delta.x),dy=Mathf.Abs(delta.y);
   if(dx<.001f){Fill(new Rect(a.x-width*.5f,Mathf.Min(a.y,b.y),width,Mathf.Max(dy,width)),color);return;}
   if(dy<.001f){Fill(new Rect(Mathf.Min(a.x,b.x),a.y-width*.5f,dx,width),color);return;}
   bool horizontal=dx>=dy;
   int steps=Mathf.CeilToInt(horizontal ? dx : dy);
   float along=(horizontal ? dx : dy)/steps;
   float across=width*delta.magnitude/(horizontal ? dx : dy);
   for(int i=0;i<steps;i++)
   {
    Vector2 center=Vector2.Lerp(a,b,(i+.5f)/steps);
    Fill(horizontal ? new Rect(center.x-along*.5f,center.y-across*.5f,along,across) :
     new Rect(center.x-across*.5f,center.y-along*.5f,across,along),color);
   }
  }
  void ResourceOrb(float x,float y)
  {
   if(game.SelectedHero!=2)Orb(x,y,70,game.Resource/100,game.Hero.color,Mathf.FloorToInt(game.Resource).ToString(),game.Hero.resource.ToUpper());
   else
   {
    Circle(x,y,70,new Color(.025f,.012f,.025f));
    HalfOrb(x,y,false,game.Resource/100,new Color(.9f,.62f,.12f));
    HalfOrb(x,y,true,game.Shadow/100,new Color(.32f,.2f,.48f));
    Line(new Vector2(x,y-66),new Vector2(x,y+65),2,gold);
    GUIStyle number=new GUIStyle(center){fontSize=Mathf.RoundToInt(12/BeltScale),wordWrap=false};
    GUIStyle caption=new GUIStyle(center){fontSize=Mathf.RoundToInt(10/BeltScale),wordWrap=false};
    Label(x-67,y-14,66,30,Mathf.FloorToInt(game.Resource).ToString(),number,paper);
    Label(x+2,y-14,66,30,Mathf.FloorToInt(game.Shadow).ToString(),number,paper);
    Label(x-66,y+14,65,20,"SKUP.",caption,gold);
    Label(x+2,y+14,64,20,"MROK",caption,paper);
   }
   // Particles stay inside the glass and above the current resource surface.
   for(int i=0;i<15;i++)
   {
    float t=Time.unscaledTime*(game.ReducedFlashes ? .35f : 1),a=i*2.399f+t*.4f;
    float r=15+(i*13)%45,px=Mathf.Cos(a)*r,py=Mathf.Sin(a)*r;
    float amount=game.SelectedHero==2 && px>0 ? game.Shadow : game.Resource;
    if(py<70-140*amount/100 || Mathf.Abs(py)<33)continue;
    Color c=game.SelectedHero==0 ? new Color(1,.6f,.1f,.65f) : game.SelectedHero==3 ? new Color(.8f,.65f,1,.75f) : new Color(1,1,1,.18f);
    Circle(x+px,y+py,game.SelectedHero==0 ? 3 : 1.5f,c);
   }
   if(game.SelectedHero==3 && (game.Resource>=95 || game.Status.Count("entropy",Time.time)>0))
   {
    float strength=game.ReducedFlashes ? .35f : .65f+.2f*Mathf.Sin(Time.unscaledTime*5);
    for(int i=0;i<4;i++){float a=i*Mathf.PI/2+.3f;Vector2 p=new Vector2(x,y)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*66;Vector2 q=p+new Vector2(Mathf.Cos(a+.4f),Mathf.Sin(a+.4f))*19;Line(p,q,2,new Color(.7f,.4f,1,strength));Line(q,q+new Vector2(Mathf.Cos(a-.4f),Mathf.Sin(a-.4f))*14,2,new Color(.7f,.4f,1,strength));}
   }
  }
  void HalfOrb(float x,float y,bool right,float amount,Color color)
  {
   float height=140*Mathf.Clamp01(amount);GUI.BeginGroup(new Rect(x+(right ? 0 : -70),y+70-height,70,height));
   Color old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(right ? -70 : 0,height-140,140,140),orbSurface);GUI.color=old;GUI.EndGroup();
  }
  void CrackedHealth()
  {
   if(game.Health/game.MaxHealth>=.25f)return;
   Color crack=new Color(.95f,.75f,.75f,.7f);
   Vector2 p=new Vector2(196,489),q=new Vector2(210,516),r=new Vector2(201,532);
   Line(p,q,1.5f,crack);Line(q,r,1.5f,crack);Line(q,new Vector2(230,510),1,crack);
   Line(new Vector2(273,592),new Vector2(264,569),1.5f,crack);Line(new Vector2(264,569),new Vector2(278,552),1.5f,crack);
  }
  void LowHealthVignette()
  {
   if(game.Health/game.MaxHealth>=.25f)return;
   float pulse=game.ReducedFlashes ? .25f : .24f+.1f*Mathf.Sin(Time.unscaledTime*3);
   for(int i=0;i<18;i++){Color c=new Color(.5f,.005f,.035f,pulse*(1-i/18f));float d=i*4;Fill(new Rect(d,d,4,720-2*d),c);Fill(new Rect(1276-d,d,4,720-2*d),c);Fill(new Rect(d,d,1280-2*d,4),c);Fill(new Rect(d,716-d,1280-2*d,4),c);}
  }
  void EnterWorld()
  {
   if(gateAt>=0)return;gateAt=Time.unscaledTime;game.UISound("gate");
  }
  void Gates()
  {
   if(gateAt<0)return;
   float u=Mathf.Clamp01((Time.unscaledTime-gateAt)/1.25f),d=Mathf.SmoothStep(0,1,u)*650;
   for(int side=0;side<2;side++)
   {float x=side==0 ? -d : 640+d;Frame(new Rect(x,0,640,720));for(int n=0;n<9;n++)Fill(new Rect(x+30+n*71,0,9,720),new Color(.13f,.12f,.14f));Fill(new Rect(x,345,640,20),gold*.6f);}
   Fill(new Rect(640-d,0,d*2,720),new Color(.43f,.15f,.65f,(game.ReducedFlashes ? .14f : .3f)*(1-u)));
  }
  // The painted atlas has an intentionally taller last row. Use its actual
  // pixel boundaries and a 3px inset, so adjacent classes never bleed into icons.
  static readonly int[] SkillAtlasColumns={0,229,458,687,918,1145,1374};
  static readonly int[] SkillAtlasRows={0,216,431,640,860,1145};
  void SkillIcon(Rect rect,int skill)
  {
   if(!skillArt)return;
   int col=Mathf.Clamp(skill,0,5),row=Mathf.Clamp(game.SelectedHero,0,4);
   float left=(SkillAtlasColumns[col]+3)/1374f,right=(SkillAtlasColumns[col+1]-3)/1374f;
   float top=(SkillAtlasRows[row]+3)/1145f,bottom=(SkillAtlasRows[row+1]-3)/1145f;
   GUI.DrawTextureWithTexCoords(rect,skillArt,new Rect(left,1-bottom,right-left,bottom-top),true);
  }
  string SkillDetail(int i)
  {
   int hero=game.SelectedHero;float power=48+game.Level*6+game.CharmRank*7;
   if(i==0)return "LPM • "+game.AttackPower.ToString("0")+" obrażeń bazowych • "+game.Hero.range.ToString("0.0")+" m\nTrafienie odnawia zasób. "+new[]{"Gotowa kontra wzmacnia następne trafienie o 75%.","Trafienie nakłada znamię krwi na 7 s, maks. 3. Detonuj je mocą główną.","Pojedynczy cel na linii strzału.","Trafienie dodaje 20 Entropii.","Cios zakorzenia zwykłego wroga na 0,65 s; wróg nadal może atakować."}[hero];
   if(i==1)
   {
    string[] effect={
     "Stożek 3,6 m: "+power+" obrażeń. "+(game.Rune==0 ? "Osłabienie pancerza na 5 s." : "Ogłuszenie 3 s; elity maks. 0,75 s, potem 5 s odporności."),
     "Detonuje znamiona w 4 m od celu (zasięg 8 m). Bez znamion 32% obrażeń; 1–3 znamiona: 115/160/205%. Leczy za zużyte znamiona.",
     "Strzała na 12 m przebija 3 cele; każdy kolejny otrzymuje 88% poprzednich obrażeń. "+(game.Rune==1 ? "Szerszy tor i spowolnienie 3 s." : "Trafia do 0,7 m od osi strzału."),
     "Osobliwość 4,2 m wybucha po 0,85 s. Przyciąga zwykłych wrogów i spowalnia na 2 s."+(game.Rune==1 ? " Osłabia też pancerz na 5 s." : ""),
     "Stożek 4,8 m: "+power+" obrażeń. Korzenie "+(game.Rune==1 ? "4 s." : "2 s; +5 HP za cel, maks. 20.")+" Blokują ruch, nie ataki. Elity tylko spowalnia."};
    return "PPM / "+NocturnisInput.Text("skill")+" • odnowienie 5 s\n"+(hero==3 ? "+25 Entropii." : "Koszt: 25 "+game.Hero.resource+".")+"\n"+effect[hero];
   }
   if(i==2)return "1 / "+NocturnisInput.Text("defense")+" • odnowienie 10 s\n"+new[]{
    "Pancerz −55% obrażeń przez 5 s. Pierwsze trafienie w oknie 1,25 s daje kontrę: następny cios lub moc główna +75% (do 4 s).",
    "Usuwa Ichorit, leczy 25 HP; drenaż 15% zadanych obrażeń przez 5 s.",
    "Koszt 20 Mroku. Cień: niewrażliwość i szybkość +35% na 2 s. Następna moc główna w 5 s: +25% obrażeń i 2 dodatkowe cele.",
    "Pancerz na 3 s. Magazynuje 45% pozostałych obrażeń, maks. 80. Po 3 s oddaje je w promieniu 3 m i dodaje 10 Entropii.",
    "Leczy 15 HP; pancerz −55% przez 6 s. Splot 2,5 m na 3 s zatrzymuje ruch zwykłych wrogów."}[hero];
   if(i==3)return NocturnisInput.Text("extra0")+" • odnowienie 14 s • "+(hero==3 ? "+20 Entropii" : "koszt 20")+"\n"+new[]{"Obrażenia otrzymywane −30% przez 7 s.","Drenaż 15% zadanych obrażeń przez 8 s.","Obrażenia +30% przez 7 s; +30 Mroku.","Obrażenia otrzymywane −30% przez 7 s.","Regeneracja +6 HP/s przez 7 s. Splot korzeni 3,5 m przez 4 s; nie wyłącza ataków wroga."}[hero];
   if(i==4)return NocturnisInput.Text("extra1")+" • odnowienie 9 s • "+(hero==3 ? "+20 Entropii" : "koszt 20")+"\n"+new[]{"Wyrok na linii przed bohaterem: uderza po 0,25 s w promieniu 2,2 m. Może zużyć gotową kontrę.","Krwawy splot nakłada 2 znamiona w promieniu 4 m i zadaje niewielkie obrażenia. Przygotowanie do detonacji.","Strzała pęt przebija do 4 celów na linii 10 m i spowalnia je na 3 s.","Osobliwość 3,2 m po 0,65 s. Przyciąga zwykłych wrogów, potem spowalnia ich.","Po 0,2 s wyrasta splot 2,7 m. Przez 2,5 s zatrzymuje zwykłych wrogów, którzy wejdą w obszar."}[hero];
   return NocturnisInput.Text("extra2")+" • odnowienie 24 s • "+(hero==3 ? "+50 Entropii" : "koszt 50")+"\nOd poziomu 4. "+new[]{"Trzy wyroki przed bohaterem po 0,25 / 0,5 / 0,75 s. Każdy zadaje 55% obrażeń bazowych. Kontra wzmacnia całą serię.","Detonacja znamion w promieniu 7 m. Oznaczone ofiary wyzwalają dodatkową falę 2,5 m; każdy wróg dostaje ją najwyżej raz.","Wachlarz trzech przebijających strzał na 12 m, do 5 celów na linię. Każda zadaje 48% obrażeń bazowych i spowalnia.","Martwa gwiazda o promieniu 5,5 m wybucha po 1,25 s: 135% obrażeń bazowych i przyciąganie zwykłych wrogów.","Trzy sploty na linii przed bohaterem; każdy działa 4 s. Blokują ruch zwykłych wrogów. Leczy 25 HP."}[hero];
  }
  void DetailedBook()
  {
   Fill(new Rect(-offsetX/scale,-offsetY/scale,Screen.width/scale,Screen.height/scale),new Color(.015f,.01f,.015f));
   if(bookArt)GUI.DrawTexture(new Rect(60,18,1160,684),bookArt,ScaleMode.StretchToFill,true);
   Label(174,94,410,44,"KSIĘGA MOCY",heading,parchment);Label(174,143,410,30,game.Hero.name,small,parchment);
   string[] tabs={"ORĘŻ I MOC","OCHRONA","KULMINACJA"};
   for(int i=0;i<3;i++)if(Button(new Rect(170+i*137,182,130,32),(bookPage==i ? "● " : "")+tabs[i],bookPage==i)){bookPage=i;game.UISound("triptych");}
   for(int row=0;row<2;row++)
   {
    int slot=bookPage*2+row;float y=237+row*170;SkillIcon(new Rect(173,y,65,65),slot);
    string name=slot==0 ? "Atak podstawowy" : slot==1 ? game.Hero.skill : slot==2 ? game.Hero.defense : game.ExtraName(slot-3);
    Label(253,y,335,35,name,text,parchment);Label(253,y+39,320,130,SkillDetail(slot),small,parchment);
   }
   Label(692,95,410,44,"GNIAZDO RUNY",heading,parchment);Label(692,147,390,30,game.Hero.skill,text,parchment);
   string[] variants={"Stalowy Gniew: ogłuszenie 3 s zamiast osłabienia pancerza.","Transfuzja: leczenie 9% zamiast 5% zdrowia za trafiony cel.","Sieć Cienia: pole dodatkowo spowalnia wrogów.","Rozpad: trafieni otrzymują +25% obrażeń przez 5 s.","Głębokie Korzenie: kontrola 4 s zamiast 2 s, bez leczenia przy trafieniu."};
   for(int i=0;i<2;i++)
   {
    float y=220+i*78;Frame(new Rect(690,y,385,64));Circle(722,y+32,21,new Color(.08f,.05f,.1f));
    if(game.Rune==i){float glow=game.ReducedFlashes ? .65f : .65f+.15f*Mathf.Sin(Time.unscaledTime*2);Circle(722,y+32,17,new Color(game.Hero.color.r,game.Hero.color.g,game.Hero.color.b,glow));Line(new Vector2(716,y+22),new Vector2(728,y+32),2,paper);Line(new Vector2(728,y+32),new Vector2(716,y+43),2,paper);}
    if(Button(new Rect(755,y+11,308,42),i==0 ? "RUNA PIERWOTNA" : "RUNA PRZEMIANY")){game.SetRune(i);runeAt=Time.unscaledTime;}
   }
   float f=Mathf.Clamp01(1-(Time.unscaledTime-runeAt)/.8f);if(f>0 && !game.ReducedFlashes)Circle(722,252+game.Rune*78,22+f*15,new Color(.75f,.4f,1,f*.2f));
   Label(694,396,383,105,variants[game.SelectedHero],text,parchment);
   Label(694,517,383,65,"Runa modyfikuje moc Q / PPM.\nWybór jest zapisywany automatycznie.\nPylon Rzezi znosi koszt mocy.",small,parchment);
   if(Button(new Rect(458,625,364,35),"ZAMKNIJ KSIĘGĘ [K]"))game.Resume();
  }
  void Alchemist()
  {
   string[] tabs={"MIKSTURY","KLEJNOTY","ZAKLINANIE"};for(int i=0;i<3;i++)if(Button(new Rect(260+i*253,280,243,32),tabs[i],alchemyPage==i))alchemyPage=i;
   Label(260,320,750,25,"Złoto: "+game.Coins+" • Mikstury "+game.Potions+"/5 • Receptura "+game.PotionRank+"/3 • Moc klejnotów +"+game.Gems*2+" ataku",small,gold);
   if(alchemyPage==0)
   {if(Button(new Rect(260,367,755,44),"KUP MIKSTURĘ • 8 ZŁ"))game.Alchemy(0);if(Button(new Rect(260,423,755,44),"WZMOCNIJ LECZENIE +20 • "+(40+game.PotionRank*30)+" ZŁ"))game.Alchemy(1);}
   else if(alchemyPage==1)
   {
    for(int i=0;i<3;i++)
    {int grade=i;float y=355+i*47;Label(263,y+5,230,30,KryptaGame.GemNames[i]+" ×"+game.GemBag[i]+" • +"+((i+1)*2)+" ataku",small,paper);
     GUI.enabled=game.GemBag[i]>0 && game.SocketedGems.Count<5;if(Button(new Rect(497,y,233,37),"OSADŹ"))game.SocketGem(grade);
     GUI.enabled=i<2 && game.GemBag[i]>=3 && game.Coins>=20;if(Button(new Rect(741,y,274,37),i<2 ? "POŁĄCZ 3 → 1 • 20 ZŁ" : "NAJWYŻSZA JAKOŚĆ"))game.CombineGem(grade);GUI.enabled=true;}
    if(Button(new Rect(260,505,370,37),"KUP ODŁAMEK • 30 ZŁ"))game.BuyGem();
    GUI.enabled=game.SocketedGems.Count>0 && game.Coins>=10;if(Button(new Rect(641,505,374,37),"WYJMIJ OSTATNI • 10 ZŁ"))game.ExtractGem();GUI.enabled=true;
    Label(263,550,750,30,"Gniazda pieczęci: "+game.SocketedGems.Count+"/5. Wyjęty klejnot wraca do sakwy w tej samej jakości.",small,muted);
   }
   else
   {
    for(int i=0;i<3;i++)if(Button(new Rect(260+i*253,362,243,38),(enchantSlot==i ? "● " : "")+new[]{"ORĘŻ","PANCERZ","PIECZĘĆ"}[i]))enchantSlot=i;
    for(int i=1;i<=3;i++)if(Button(new Rect(260+(i-1)*253,414,243,38),(enchantKind==i ? "● " : "")+KryptaGame.EnchantNames[i]))enchantKind=i;
    Label(263,465,750,36,game.EnchantLabel(game.GearEnchant(enchantSlot),game.GearRank(enchantSlot))+" → "+game.EnchantLabel(enchantKind,game.GearRank(enchantSlot)),text,gold);
    GUI.enabled=game.GearRank(enchantSlot)>0 && game.Coins>=30+game.GearRank(enchantSlot)*10 && game.GearEnchant(enchantSlot)!=enchantKind;
    if(Button(new Rect(260,517,755,40),"ZMIEŃ ZAKLĘCIE • "+(30+game.GearRank(enchantSlot)*10)+" ZŁ"))game.EnchantGear(enchantSlot,enchantKind);GUI.enabled=true;
   }
  }
 }
}
