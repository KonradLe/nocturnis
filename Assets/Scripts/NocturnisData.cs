using UnityEngine;

namespace Krypta
{
    public sealed class HeroClass
    {
        public string name, person, role, resource, skill, defense, story;
        public float health, speed, range, damage, interval;
        public Color color;
        public HeroClass(string n,string p,string r,string res,string s,string d,string lore,
            float hp,float move,float reach,float hit,float rate,Color c)
        { name=n;person=p;role=r;resource=res;skill=s;defense=d;story=lore;health=hp;speed=move;range=reach;damage=hit;interval=rate;color=c; }
    }

    public static class NocturnisData
    {
        public static readonly HeroClass[] Heroes = {
            new HeroClass("Inkwizytor Popiołów","Seweryn Voss","OSTRZE • PANCERZ • OGIEŃ","Żar","Wyrok Niebios","Żelazna Pokuta",
                "Spalił zakon, który kazał mu strzec martwego boga. Teraz niesie ostatni żar jego serca.",150,4.8f,2.5f,29,.52f,new Color(1,.57f,.22f)),
            new HeroClass("Tkacz Krwi","Mira Szept","POSOKA • DRENAŻ • KLĄTWY","Posoka","Żniwo Znamion","Transfuzja",
                "Chirurżka bez katedry. Słyszy wspomnienia w krwi poległych i szuka imienia swojej siostry.",105,5,7,22,.58f,new Color(.94f,.19f,.32f)),
            new HeroClass("Łowca Zmierzchu","Kael Wrona","ŁUK • MOBILNOŚĆ • PRZEBICIE","Skupienie","Przeszywający Cień","Krok Cienia",
                "Przeprowadzał uchodźców przez las, aż jedna karawana wróciła bez żywych. Od tej nocy tropi jej przewodnika.",110,5.7f,9,24,.4f,new Color(.38f,.76f,.94f)),
            new HeroClass("Zwiastun Próżni","Ilyra Bezgwiezdna","ENTROPIA • OBSZAR • KONTROLA","Entropia","Osobliwość","Płaszcz Próżni",
                "Astronomka, która zobaczyła przestrzeń między Słońcami. Na jej mapach pojawia się ósmy cień.",100,5,8,28,.65f,new Color(.68f,.43f,1)),
            new HeroClass("Strażnik Korzeni","Brann Cierniowy","TOPÓR • KORZENIE • ODNOWA","Żywica","Gniew Korzeni","Kora Przodków",
                "Ostatni opiekun Dębu Pamięci. Pod jego korą rośnie Ichorit, a każda gałąź pamięta innego zmarłego.",140,5.1f,2.8f,33,.6f,new Color(.42f,.85f,.52f))
        };
        public static readonly string[] Zones=Campaign.Names;
        public static readonly string[] ZoneCopy=Campaign.Lore;
    }
}
