#nullable enable
using System.Drawing;
using System.Text.Json;

namespace RotationUS.Diagnostics;

internal static class PartyRosterTests
{
    internal static Dictionary<int,Color> Packet(PartyRosterSnapshot roster, int mode=2)
    {
        Color N(int n) => Color.FromArgb(n&255,(n>>8)&255,(n>>16)&255);
        var data = new List<Color> {Color.FromArgb(72,80,1),N(roster.SessionA),N(roster.SessionB),N(roster.Epoch),N(7),N(mode)};
        foreach (var member in roster.Members)
        {
            data.Add(N(member.Id)); data.Add(Color.FromArgb(member.ClassId,member.AssignedRole,member.Online?1:0)); data.Add(N(member.SpecId));
        }
        int sum=0;
        for(int i=0;i<data.Count;i++) sum+=data[i].R*(i*3+4)+data[i].G*(i*3+5)+data[i].B*(i*3+6);
        data.Add(N(sum%16777216));data.Add(N(7));data.Add(N(roster.Epoch));data.Add(Color.FromArgb(72,80,1));data.Add(N(mode));
        return data.Select((c,i)=>(c,i)).ToDictionary(p=>p.i+51,p=>p.c);
    }
    internal static int Run()
    {
        int count=0;
        void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL PartyRoster: "+why);count++;}
        var empty=new PartyMember(0,0,0,0,false);
        var snapshot=new PartyRosterSnapshot(123,456,1,new[] {
            new PartyMember(1,2,65,2,true), new PartyMember(2,1,73,3,true),
            new PartyMember(3,8,63,3,true),empty,empty});
        var packet=Packet(snapshot);
        var decoded=PartyRosterProtocol.Decode(i=>packet[i]);
        Check(decoded is {Mode:2}&&decoded.Value.Snapshot.Matches(snapshot),"complete packet decodes");
        string? fixture=Environment.GetEnvironmentVariable("HIJI_PARTY_PACKET_FIXTURE");
        if(!string.IsNullOrEmpty(fixture)) {
            var rgb=JsonSerializer.Deserialize<int[][]>(File.ReadAllText(fixture))!;
            var lua=PartyRosterProtocol.Decode(i=>Color.FromArgb(rgb[i-51][0],rgb[i-51][1],rgb[i-51][2]));
            Check(lua is {Mode:2}&&lua.Value.Snapshot.SessionA==123&&lua.Value.Snapshot.SessionB==456,
                "actual Lua RGB packet header/checksum decoded by production C#");
            Check(lua!.Value.Snapshot.Members.Take(3).Select(m=>(m.ClassId,m.SpecId,m.Role))
                .SequenceEqual(new[]{(2,65,2),(1,73,1),(8,63,3)}),"actual Lua party data crosses pixel protocol intact");
        }
        Check(snapshot.Members[1].Role==1,"known tank specialization overrides damage assignment");
        Check((snapshot.Members[1] with {SpecId=72,AssignedRole=1}).Role==3,"damage specialization overrides tank assignment");
        Check((snapshot.Members[1] with {SpecId=0,AssignedRole=1}).Role==1,"assignment fallback when inspection unavailable");
        Check((snapshot.Members[1] with {ClassId=0,AssignedRole=1}).Role==0,"missing class has no role bonus");
        Check((snapshot.Members[1] with {ClassId=8,AssignedRole=1}).Role==0,"inconsistent class/spec rejected");
        Check((snapshot.Members[1] with {Online=false}).Role==0,"offline metadata has no weight");
        foreach(int id in Enumerable.Range(51,26)) {
            var damaged=new Dictionary<int,Color>(packet);var c=damaged[id];damaged[id]=Color.FromArgb(c.R^1,c.G,c.B);
            Check(PartyRosterProtocol.Decode(i=>damaged[i]) is null,"corrupt/torn packet rejected at "+id);
        }
        var duplicate=snapshot with {Members=new[]{snapshot.Members[0],snapshot.Members[0],snapshot.Members[2],empty,empty}};
        var duplicates=Packet(duplicate);Check(PartyRosterProtocol.Decode(i=>duplicates[i]) is null,"duplicate identity rejected");
        Check(PartyRosterProtocol.Decode(_=>Color.Black) is null,"old addon without metadata falls back");

        var cache=new PartyRosterCache();double now=0;
        PartyRosterSnapshot? Observe(int mode,bool combat=true,bool live=true,bool group=true,bool raid=false,PartyRosterSnapshot? source=null,double delta=.1) {
            now+=delta;var p=Packet(source??snapshot,mode);
            return cache.Observe(i=>p[i],live,group,raid,combat,now);
        }
        Check(Observe(1) is null,"tool opened mid-combat cannot adopt old snapshot");
        Check(Observe(2,false) is not null,"acquire outside combat");
        for(int i=0;i<180;i++) Check(Observe(1,delta:1) is not null,"long combat retains confirmed identity "+i);
        Check(Observe(1,source:snapshot with {Epoch=2}) is null,"combat roster change immediately invalidates");
        Check(Observe(1) is null,"cannot restore a previous generation in combat");
        Check(Observe(2,false) is not null,"reacquire after combat");
        Check(Observe(1,source:snapshot with {SessionB=999}) is null,"reload/session change invalidates");
        Observe(2,false);Check(Observe(1,live:false) is null,"heartbeat loss clears cache");
        Check(Observe(1) is null,"heartbeat return alone cannot revive cache in combat");
        Observe(2,false);Check(Observe(1,delta:2) is null,"capture gap clears before use");
        Observe(2,false);Check(Observe(0,group:false) is null,"leaving group clears");
        Observe(2,false);Check(Observe(0,raid:true) is null,"raid never receives party cache");
        Observe(2,false);Check(Observe(3) is null,"pending/invalid roster state clears");
        int generation=cache.Generation;
        Observe(3,source:snapshot with {Epoch=3});
        Check(cache.Generation!=generation,"roster invalidation signaled even without usable metadata");
        Observe(2,false);
        var swapped=snapshot with {Members=new[]{snapshot.Members[0],snapshot.Members[2],snapshot.Members[1],empty,empty}};
        Check(Observe(1,source:swapped) is null,"same slot with another member cannot inherit cached tank");
        Observe(2,false);cache.Reset();Check(Observe(1) is null,"explicit game/window reset drops all history");

        var t=new HealingThresholds();
        Check(PartyHealingPriority.Compare(.45,2,.42,3,t,snapshot)<0,"tank wins close health in same tier");
        Check(PartyHealingPriority.Compare(.70,2,.30,3,t,snapshot)>0,"major injury beats ordinary tank injury");
        Check(PartyHealingPriority.Compare(.35,2,.15,3,t,snapshot)>0,"critical damage dealer wins");
        Check(PartyHealingPriority.Compare(.18,2,.15,3,t,snapshot)>0,"critical tier uses real health only");
        Check(PartyHealingPriority.Compare(.45,1,.43,3,t,snapshot)<0,"healer receives modest three point bonus");

        Dictionary<int,Color> f=new();
        void Flag(int id,bool value=true)=>f[id]=value?Color.White:Color.Black;
        void Hp(int id,double hp) {int n=(int)Math.Round(hp*255);f[id]=Color.FromArgb(n,n,n);}
        void Member(int slot,double hp)=>f[50+slot]=Color.FromArgb((int)Math.Round(hp*255),255,0);
        void Setup() {
            f=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
            foreach(int id in new[]{1,2,3,4,5,9,10,19,20,42,43,44,45,96,100}) Flag(id);
            Hp(6,1);Hp(7,1);Hp(8,.8);
            for(int slot=1;slot<=5;slot++) Member(slot,1);
            Member(2,.45);Member(3,.42);
        }
        HolyBuffs buffs=new() {Infusion=false,HandOfDivinity=false,DivinePurpose=false};
        Setup();Check(HOLY.Decide(f,buffs,party:snapshot).Key==102,"weighted policy reaches actual selector");
        Check(HOLY.Decide(f,buffs).Key==103,"missing snapshot follows original lowest health");
        Member(3,.15);Check(HOLY.Decide(f,buffs,party:snapshot).Key==103,"urgent actual selector still rescues damage dealer");
        Setup();Flag(42,false);Check(HOLY.Decide(f,buffs,party:snapshot).Key==103,"out of range tank excluded");
        Setup();f[52]=Color.Blue;Check(HOLY.Decide(f,buffs,party:snapshot).Key==103,"offline tank excluded");
        Setup();Member(2,0);Check(HOLY.Decide(f,buffs,party:snapshot).Key==103,"dead tank excluded");
        Setup();Member(2,.85);Member(3,1);Hp(8,1);
        Check(HOLY.Decide(f,buffs,party:snapshot).Key==0,"bonus cannot cause healing above actual heal threshold");
        Setup();Member(1,.42);Hp(6,.42);Member(3,1);Flag(23);Flag(33);Flag(91);
        Check(HOLY.Decide(f,buffs,party:snapshot).Key==2,"healer self priority uses same weights as member selection");
        Member(1,.44);Hp(6,.44);
        Check(HOLY.Decide(f,buffs,party:snapshot).Key==102,"ordinary self heal cannot preempt weighted tank");

        // Exact baseline equivalence across different health states, including self macros and raids.
        var unknown=snapshot with {Members=snapshot.Members.Select(m=>m with {ClassId=0,SpecId=0,AssignedRole=0}).ToArray()};
        var random=new Random(731);
        for(int i=0;i<150;i++) {
            Setup();Flag(22);Flag(32);Flag(23);Flag(33);Flag(91);
            for(int slot=1;slot<=5;slot++) Member(slot,random.Next(1,256)/255.0);
            Hp(6,f[51].R/255.0);Hp(8,f[52].R/255.0);Flag(47);
            Check(HOLY.Decide(f,buffs,party:unknown)==HOLY.Decide(f,buffs),"all unknown preserves original decision "+i);
            Flag(41);
            Check(HOLY.Decide(f,buffs,party:snapshot)==HOLY.Decide(f,buffs),"raid decision completely unchanged "+i);
        }
        Setup();HOLY.Inst.Reset();HOLY.Inst.SetPartyRoster(snapshot);HOLY.Inst.Buffs=buffs;
        var states=new Dictionary<int,bool>();var bars=new Dictionary<int,Color>();
        HOLY.Inst.Process(f,bars,states);Check(states.ContainsKey(102),"weighted selection requested");
        states.Clear();HOLY.Inst.SetPartyRoster(null);HOLY.Inst.Process(f,bars,states);
        Check(states.ContainsKey(103),"invalidating metadata cancels pending old selection immediately");
        HOLY.Inst.SetPartyRoster(null);HOLY.Inst.Reset();
        return count;
    }
}
