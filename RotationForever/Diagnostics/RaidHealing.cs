#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed record RaidMemberSignal(bool? InRange,bool? Target,bool? Self);
internal sealed record RaidRosterSnapshot(int SessionA,int SessionB,int Epoch,int Revision,bool BindingsReady,RaidMemberSignal[] Members)
{
    internal string Identity=>$"{SessionA}:{SessionB}:{Epoch}";
    internal int TargetSlot=>Array.FindIndex(Members,m=>m.Target==true)+1;
    internal int SelfSlot=>Array.FindIndex(Members,m=>m.Self==true)+1;
    internal bool Reachable(int slot)=>slot is >=1 and <=40&&(Members[slot-1].Self==true||Members[slot-1].InRange==true);
    internal bool Target(int slot)=>slot is >=1 and <=40&&Members[slot-1].Target==true;
    internal static RaidRosterSnapshot? Read(IReadOnlyDictionary<int,Color>? bars)
    {
        if(bars is null||Enumerable.Range(1,49).Any(i=>!bars.ContainsKey(i))) return null;
        var magic=Color.FromArgb(72,84,1);
        if(bars[41].ToArgb()!=magic.ToArgb()||bars[46].ToArgb()!=magic.ToArgb()||
            bars[42].ToArgb()!=bars[44].ToArgb()||bars[43].ToArgb()!=bars[45].ToArgb()) return null;
        int Number(int i)=>bars[i].R|(bars[i].G<<8)|(bars[i].B<<16);
        bool? Bit(byte n)=>n==255?true:n==0?false:null;
        var members=Enumerable.Range(1,40).Select(i=>new RaidMemberSignal(Bit(bars[i].R),Bit(bars[i].G),Bit(bars[i].B))).ToArray();
        if(Number(42)==0||Number(43)==0||members.Count(m=>m.Target==true)>1||members.Count(m=>m.Self==true)>1) return null;
        return new(Number(47),Number(48),Number(42),Number(43),FuryBuffs.Boolean(bars[49])==true,members);
    }
}

internal sealed record RaidInjury(bool Reliable=false,bool Complete=false,int Eligible=0,int Injured=0,int Severe=0,int Urgent=0,
    double Deficit=0,int Low=0,int Critical=0,int WingsLow=0,int WingsSevere=0)
{
    internal const int Minimum=4;
    internal bool Aoe=>Reliable&&Injured>=Minimum;
    internal bool Major=>Reliable&&(Low>=Minimum||Critical>=Minimum);
    internal bool Ordinary=>Complete&&Injured>=Minimum&&Deficit+1e-9>=.40;
    internal bool Immediate=>Reliable&&Severe>=Minimum;
    internal bool Emergency=>Reliable&&Urgent>=Minimum;
    internal static RaidInjury Read(Dictionary<int,Color> frames,RaidRosterSnapshot? roster,HealingThresholds t)
    {
        if(roster is null) return new();
        int eligible=0,injured=0,severe=0,urgent=0,unknown=0,low=0,critical=0,wlow=0,wsevere=0;double deficit=0;
        for(int slot=1;slot<=40;slot++) {
            var hp=frames.TryGetValue(50+slot,out var c)?TeamHealthReading.Read(c):new(TeamHealthStatus.Unknown,null);
            if(hp.Status==TeamHealthStatus.Unknown) {unknown++;continue;}
            if(hp.Percent is not >0) continue;
            if(roster.Members[slot-1].InRange is null&&roster.Members[slot-1].Self!=true) {unknown++;continue;}
            if(!roster.Reachable(slot)) continue;
            eligible++;double p=hp.Percent.Value;
            if(p<t.HealBelow) {injured++;deficit+=1-p;}
            if(p<t.TollCritical) severe++;
            if(p<t.Critical) urgent++;
            if(p<t.GroupLow) low++;
            if(p<t.GroupCritical) critical++;
            if(p<.70) wlow++;
            if(p<.60) wsevere++;
        }
        bool reliable=eligible>0&&(double)eligible/(eligible+unknown)>=t.MinimumCoverage;
        return new(reliable,reliable&&unknown==0,eligible,injured,severe,urgent,deficit,low,critical,wlow,wsevere);
    }
}

internal sealed record RaidHealingSample(RaidInjury Injury,bool TollConfirmed=false,bool VirtueActive=false,
    double VirtueRemaining=0,string VirtueState="未确认施放")
{
    internal static RaidHealingSample Empty {get;}=new(new RaidInjury());
}

internal sealed class RaidHealingWindow
{
    private string? identity,population;
    private int revision,consecutive;
    private bool? previousReady;
    private double lastAt=double.NegativeInfinity,virtueUntil;
    private HealingThresholds? thresholds;
    internal RaidHealingSample Current {get;private set;}=RaidHealingSample.Empty;
    internal void Reset()
    {
        identity=population=null;revision=consecutive=0;previousReady=null;
        lastAt=double.NegativeInfinity;virtueUntil=0;thresholds=null;Current=RaidHealingSample.Empty;
    }
    internal RaidHealingSample Observe(Dictionary<int,Color> f,RaidRosterSnapshot? roster,HealingThresholds t,double now,bool live=true)
    {
        bool F(int id)=>f.TryGetValue(id,out var c)&&FuryBuffs.Boolean(c)==true;
        if(!live||!double.IsFinite(now)||roster is null||!F(1)||!F(2)||!F(19)||!F(41)||!F(96)||!F(100)) {Reset();return Current;}
        string members=string.Join(",",Enumerable.Range(51,40).Select(i=>f.TryGetValue(i,out var c)?TeamHealthReading.Read(c).Status:TeamHealthStatus.Unknown));
        if(identity!=roster.Identity||population!=members||now<lastAt||now-lastAt>1.5) Reset();
        if(thresholds is {} before&&(before.HealBelow!=t.HealBelow||before.TollCritical!=t.TollCritical||before.Critical!=t.Critical)) consecutive=0;
        if(identity==roster.Identity&&revision==roster.Revision) return Current;
        identity=roster.Identity;population=members;revision=roster.Revision;lastAt=now;thresholds=t;
        var injury=RaidInjury.Read(f,roster,t);
        consecutive=injury.Ordinary?Math.Min(2,consecutive+1):0;
        bool? ready=f.TryGetValue(29,out var color)?FuryBuffs.Boolean(color):null;
        string state=Current.VirtueState;
        if(ready is null) {virtueUntil=0;state="团队美德冷却未知：窗口清除";}
        else if(ready==false&&previousReady==true) {virtueUntil=now+9;state="团队美德就绪转冷却：9秒推定窗口";}
        else if(ready==true) {virtueUntil=0;state="团队美德就绪：无窗口";}
        else if(virtueUntil>0&&now>=virtueUntil) {virtueUntil=0;state="团队美德推定窗口结束";}
        else if(virtueUntil==0&&previousReady is null) state="团队美德冷却中：未确认施放";
        previousReady=ready;
        return Current=new(injury,consecutive>=2,virtueUntil>now,Math.Max(0,virtueUntil-now),state);
    }
}

internal static class RaidTargetKeys
{
    internal const int ActionStart=200,Count=30;
    internal static bool IsSelection(int key)=>key>ActionStart&&key<=ActionStart+Count;
    internal static (byte Modifier,byte Key) ForSlot(int slot)
    {
        if(slot is <1 or >30) throw new ArgumentOutOfRangeException(nameof(slot));
        if(slot<=12) return (0x10,(byte)(0x70+slot-1));
        byte[] keys={0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x30,0xBD,0xBB,0xDB,0xDD,0xDC,0xBA,0xDE,0xBE};
        return (0x11,keys[slot-13]);
    }
    internal static (byte Key,bool Up)[] Sequence(int slot)
    {
        var (modifier,key)=ForSlot(slot);
        return new[]{(modifier,false),(key,false),(key,true),(modifier,true)};
    }
}
