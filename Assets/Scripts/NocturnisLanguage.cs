using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace Krypta {
 // Translate only rendered text. Save IDs, gameplay data and quest state remain Polish/stable.
 public static class NocturnisLanguage {
  [Serializable] public sealed class Entry {public string pl,en;}
  [Serializable] public sealed class Catalog {public Entry[] entries;}
  sealed class Node {public readonly Dictionary<char,Node> next=new Dictionary<char,Node>();public Entry entry;}
  static readonly Dictionary<string,string> exact=new Dictionary<string,string>(StringComparer.Ordinal);
  static readonly Dictionary<string,string> cache=new Dictionary<string,string>(StringComparer.Ordinal);
  static Node root;static string loadedKey,current="pl";static bool choice;
  public static int Revision {get;private set;}
  public static string PreferenceKey=>KryptaGame.Testing ? "Nocturnis.QA.Language" : "Nocturnis.Language";
  public static string Current {get{Initialize();return current;}}
  public static bool English=>Current=="en";
  public static bool NeedsChoice {get{Initialize();return choice;}}
  public static int CatalogCount {get{Load();return exact.Count;}}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void Reset(){loadedKey=null;root=null;exact.Clear();cache.Clear();Revision=0;current="pl";choice=false;}
  public static void Initialize() {
   string key=PreferenceKey;if(loadedKey==key)return;loadedKey=key;
   string saved=PlayerPrefs.GetString(key,"");bool valid=saved=="pl"||saved=="en";
   current=valid?saved:"pl";
   if(KryptaGame.Testing && Array.IndexOf(Environment.GetCommandLineArgs(),"-language-en")>=0)current="en";
   choice=!valid && !Application.isBatchMode && !KryptaGame.Testing;
   if(KryptaGame.Testing && Array.IndexOf(Environment.GetCommandLineArgs(),"-language-choice-review")>=0)choice=true;
   cache.Clear();Revision++;
  }
  public static void Select(string language) {
   if(language!="pl"&&language!="en")throw new ArgumentException("Unsupported language");
   Initialize();current=language;choice=false;cache.Clear();Revision++;
   PlayerPrefs.SetString(PreferenceKey,language);PlayerPrefs.Save();NocturnisInput.ReloadBindings();
  }
  public static void Reload(){loadedKey=null;Initialize();}
  static void Load() {
   if(root!=null)return;root=new Node();
   var asset=Resources.Load<TextAsset>("Localization/strings");
   if(!asset)throw new InvalidOperationException("Localization catalog is missing");
   var data=JsonUtility.FromJson<Catalog>(asset.text);
   if(data==null||data.entries==null)throw new InvalidOperationException("Invalid localization catalog");
   foreach(var row in data.entries) {
    if(string.IsNullOrEmpty(row.pl)||string.IsNullOrEmpty(row.en))throw new InvalidOperationException("Empty translation");
    if(exact.ContainsKey(row.pl))throw new InvalidOperationException("Duplicate translation: "+row.pl);
    exact.Add(row.pl,row.en);
    var node=root;foreach(char c in row.pl){char k=char.ToUpperInvariant(c);Node next;if(!node.next.TryGetValue(k,out next)){next=new Node();node.next.Add(k,next);}node=next;}
    if(node.entry==null)node.entry=row;
   }
  }
  // Numeric presentation follows the displayed language without changing global
  // culture (which could affect gameplay serialization and existing saves).
  static string EnglishNumbers(string value) {
   if(value.IndexOf(',')<0)return value;
   char[] copy=null;
   for(int i=1;i<value.Length-1;i++)if(value[i]==','&&char.IsDigit(value[i-1])&&char.IsDigit(value[i+1])) {
    if(copy==null)copy=value.ToCharArray();copy[i]='.';
   }
   return copy==null?value:new string(copy);
  }
  static bool Letter(char c)=>char.IsLetter(c)||c=='_';
  static string CaseLike(string original,string translated) {
   bool letter=false,upper=true,lower=true;
   foreach(char c in original)if(char.IsLetter(c)){letter=true;upper&=char.IsUpper(c);lower&=char.IsLower(c);}
   if(letter&&upper)return translated.ToUpperInvariant();
   if(letter&&lower)return translated.ToLowerInvariant();
   return translated;
  }
  public static string Text(string value) {
   if(string.IsNullOrEmpty(value)||!English)return value;
   Load();string found;if(exact.TryGetValue(value,out found))return EnglishNumbers(found);
   if(cache.TryGetValue(value,out found))return found;
   var result=new StringBuilder(value.Length+24);
   for(int i=0;i<value.Length;) {
    // Never translate formatting tokens, rich-text tags or identifiers inside them.
    if(value[i]=='{'||value[i]=='<') {
     char end=value[i]=='{'?'}':'>';int close=value.IndexOf(end,i+1);
     if(close>=0){result.Append(value,i,close-i+1);i=close+1;continue;}
    }
    var node=root;Entry match=null;int length=0;
    for(int j=i;j<value.Length;j++) {
     Node next;if(!node.next.TryGetValue(char.ToUpperInvariant(value[j]),out next))break;node=next;
     if(node.entry==null)continue;
     bool left=!Letter(value[i])||i==0||!Letter(value[i-1]);
     bool right=!Letter(value[j])||j+1==value.Length||!Letter(value[j+1]);
     if(left&&right){match=node.entry;length=j-i+1;}
    }
    if(match!=null){result.Append(CaseLike(value.Substring(i,length),match.en));i+=length;}
    else result.Append(value[i++]);
   }
   found=EnglishNumbers(result.ToString());
   if(value.Length<=8192){if(cache.Count>=512)cache.Clear();cache[value]=found;}
   return found;
  }
 }
 public sealed partial class KryptaHUD {
  void LanguageChoice() {
   MenuStyles();Fill(new Rect(0,0,1280,720),Color.black);
   if(menuArt)GUI.DrawTexture(new Rect(0,0,1280,720),menuArt,ScaleMode.ScaleAndCrop);
   Fill(new Rect(0,0,1280,720),new Color(.015f,.01f,.02f,.55f));
   ClassicFrame(new Rect(255,125,770,475));
   Label(300,153,680,92,"NOCTURNIS",new GUIStyle(menuTitle){alignment=TextAnchor.MiddleCenter},warmWhite);
   Label(310,266,660,50,"Wybierz język / Choose your language",menuCentered,paper);
   Divider(345,335,590);
   if(Button(new Rect(330,378,290,65),"POLSKI",true))ChooseStartupLanguage("pl");
   if(Button(new Rect(660,378,290,65),"ENGLISH",true))ChooseStartupLanguage("en");
   Label(325,481,630,60,"Język możesz zmienić w ustawieniach.\nYou can change the language in Settings.",center,muted);
  }
  void ChooseStartupLanguage(string language) {
   NocturnisLanguage.Select(language);CancelPortraitDrag();GUI.FocusControl(null);
   NocturnisCinematic.TryPlay(game,null);
  }
  void LanguageSettings() {
   Label(282,245,715,40,"JĘZYK GRY",menuCentered,warmWhite);
   if(Button(new Rect(295,315,330,52),"POLSKI",!NocturnisLanguage.English))NocturnisLanguage.Select("pl");
   if(Button(new Rect(655,315,330,52),"ENGLISH",NocturnisLanguage.English))NocturnisLanguage.Select("en");
   Label(310,398,660,75,"Zmiana języka działa od razu i nie zmienia zapisów postaci.\nWybór jest zapamiętywany przy kolejnych uruchomieniach.",menuText,paper);
   Label(310,497,660,55,"Lektor filmowy: angielski.\nNapisy i plansze intro są zgodne z wybranym językiem.",center,muted);
  }
 }
}

namespace Krypta {
 public static class NocturnisLocalizedGUI {
  public static void Label(UnityEngine.Rect rect,string value,UnityEngine.GUIStyle style) {
   string translated=NocturnisInput.Format(value);
   if(KryptaGame.Testing && NocturnisLanguage.English)NocturnisLanguageReview.Record(value,translated);
   UnityEngine.GUI.Label(rect,translated,style);
  }
 }
}
