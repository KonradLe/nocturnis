using System.Collections.Generic;

namespace Krypta
{
    // Explicit clock makes pause, refresh and stack expiry deterministic.
    public sealed class StatusBook
    {
        public sealed class Entry
        {
            public string id, label;
            public float until, started, duration;
            public int stacks;
            public bool harmful;
        }
        readonly List<Entry> entries=new List<Entry>();
        public IList<Entry> Entries { get { return entries.AsReadOnly(); } }
        public void Apply(string id,string label,float now,float duration,int limit=1,bool harmful=false)
        {
            if (duration <= 0 || limit < 1) return;
            Expire(now);
            Entry e=entries.Find(x=>x.id==id);
            if (e==null) { e=new Entry {id=id,label=label,harmful=harmful}; entries.Add(e); }
            e.label=label;e.harmful=harmful;e.started=now;e.duration=duration;
            e.stacks=System.Math.Min(limit,e.stacks+1); e.until=now+duration;
        }
        public int Count(string id,float now) { Entry e=entries.Find(x=>x.id==id && x.until>now); return e==null ? 0 : e.stacks; }
        public void Remove(string id) { entries.RemoveAll(x=>x.id==id); }
        public void Expire(float now) { entries.RemoveAll(x=>x.until<=now); }
        public void Clear() { entries.Clear(); }
    }
}
