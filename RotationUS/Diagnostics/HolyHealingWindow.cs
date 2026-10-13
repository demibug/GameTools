#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed record PartyTollReading(bool Valid,int Eligible=0,int Injured=0,int Severe=0,int Urgent=0,double Deficit=0)
{
    internal bool Ordinary=>Valid&&Injured>=2&&Deficit+1e-9>=.40;
    internal bool Emergency=>Valid&&Urgent>=2;
    internal bool Immediate=>Valid&&Severe>=2;
    internal static PartyTollReading Read(Dictionary<int,Color> frames,HealingThresholds t)
    {
        bool? F(int id)=>frames.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        if(F(19)!=true||F(41)!=false) return new(false);
        int eligible=0,injured=0,severe=0,urgent=0;double deficit=0;
        for(int slot=1;slot<=5;slot++) {
            // Unknown membership/health/range cannot confirm a consecutive sample.
            var hp=frames.TryGetValue(50+slot,out var c)?TeamHealthReading.Read(c):new(TeamHealthStatus.Unknown,null);
            if(hp.Status==TeamHealthStatus.Unknown) return new(false);
            if(hp.Percent is not >0) continue;
            if(slot>1) {if(F(40+slot) is null) return new(false);if(F(40+slot)!=true) continue;}
            eligible++;
            if(hp.Percent<t.HealBelow) {injured++;deficit+=1-hp.Percent.Value;}
            if(hp.Percent<t.TollCritical) severe++;
            if(hp.Percent<t.Critical) urgent++;
        }
        return new(eligible>0,eligible,injured,severe,urgent,deficit);
    }
}

internal sealed record HolyHealingSample(PartyTollReading Toll,bool TollConfirmed=false,bool VirtueActive=false,
    double VirtueRemaining=0,string VirtueState="未确认施放",int VirtueSequence=0)
{
    internal static HolyHealingSample Empty {get;}=new(new PartyTollReading(false));
}

// Observed once per capture, including while execution is disabled. A key request
// never starts the timer. No member's Beacon ownership is inferred here.
internal sealed class HolyHealingWindow
{
    private bool? previousReady;
    private double lastAt=double.NegativeInfinity,virtueUntil;
    private int consecutive,sequence;
    private string? population;
    private HealingThresholds? thresholds;
    internal HolyHealingSample Current {get;private set;}=HolyHealingSample.Empty;
    internal void Reset(string reason="资料已清除")
    {
        previousReady=null;lastAt=double.NegativeInfinity;virtueUntil=0;consecutive=0;population=null;thresholds=null;
        Current=new(new PartyTollReading(false),VirtueState:reason,VirtueSequence:sequence);
    }
    internal HolyHealingSample Observe(Dictionary<int,Color> frames,HealingThresholds t,double now,bool live=true)
    {
        bool? F(int id)=>frames.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        if(!live||!double.IsFinite(now)||F(1)!=true||F(2)!=true||F(96)!=true||F(100)!=true||F(19)!=true||F(41)!=false) {
            Reset(F(41)==true?"团队模式：小队窗口停用":"采样失效／脱战／离队：窗口已清除");return Current;
        }
        string members=string.Join(",",Enumerable.Range(51,5).Select(id=>frames.TryGetValue(id,out var c)?TeamHealthReading.Read(c).Status:TeamHealthStatus.Unknown));
        if(now<lastAt||now-lastAt>1.5||population!=members) Reset("采样中断／队伍状态变化：窗口已清除");
        if(thresholds is {} before&&(before.HealBelow!=t.HealBelow||before.TollCritical!=t.TollCritical||before.Critical!=t.Critical)) consecutive=0;
        // Do not let a repeated evaluation of one capture count as a second sample.
        if(now==lastAt) return Current;
        lastAt=now;population=members;thresholds=t;
        var toll=PartyTollReading.Read(frames,t);
        consecutive=toll.Ordinary?Math.Min(2,consecutive+1):0;
        bool? ready=F(29);
        string state=Current.VirtueState;
        if(ready is null) {virtueUntil=0;state="美德冷却未知：推定窗口已清除";}
        else if(ready==false&&previousReady==true) {virtueUntil=now+9;sequence++;state="就绪转冷却：9秒推定窗口生效";}
        else if(ready==true) {virtueUntil=0;state="美德就绪：无窗口";}
        else if(virtueUntil>0&&now>=virtueUntil) {virtueUntil=0;state="9秒推定窗口结束";}
        else if(virtueUntil==0&&previousReady is null) state="美德冷却中：未确认9秒窗口";
        previousReady=ready;
        Current=new(toll,consecutive>=2,virtueUntil>now,Math.Max(0,virtueUntil-now),state,sequence);
        return Current;
    }
}

internal sealed class HolyHealingDiagnosticLog
{
    private readonly Dictionary<string,string> previous=new();
    internal bool Observed=>previous.Count>0;
    internal string Snapshot {get;private set;}="小队治疗窗口尚未采样";
    internal List<PartyLogChange> Observe(HolyHealingSample sample,Dictionary<int,Color> frames,HolyBuffs buff,HealingThresholds t,bool executing)
    {
        bool? F(int id)=>frames.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        var r=sample.Toll;
        string reason=!r.Valid?"小队有效伤情不足／非小队，沿用普通治疗":r.Emergency?"至少2人危急，允许圣能溢出":r.Immediate?"至少2人大掉血":
            !r.Ordinary?"人数或总缺血不足，普通治疗":!sample.TollConfirmed?"普通群伤：等待第二次有效采样":"普通群伤：连续两次确认";
        bool eligible=r.Emergency||r.Immediate||(r.Ordinary&&sample.TollConfirmed);
        string gate=!eligible?"未触发":F(24)!=true||F(34)!=true?"鸣钟冷却／不可用／未知":
            F(3)!=true||F(4)!=true||F(5)!=true?"等待有效友方目标":F(9)!=true||F(20)!=true?"等待施法结束／GCD":
            !r.Emergency&&(F(91)==true||F(92)==true)?"先消耗实际圣能，再采样":
            !r.Emergency&&(F(91)!=false||F(92)!=false)?"圣能未知，禁止非紧急鸣钟":"鸣钟条件满足（仍按技能优先级）";
        bool? hand=F(94)??buff.HandOfDivinity;
        string handState=hand!=true?hand==false?"无Buff":"Buff未知":buff.HandExpiring==true?"Buff存在且临期":buff.HandExpiring==false?"Buff存在，未临期":"Buff存在，临期状态未配置／未知";
        var entries=new[] {
            ("toll",reason+gate+executing,$"[小队鸣钟] {reason}；{gate}；可治疗={r.Eligible}，低于{t.HealBelow:P0}={r.Injured}，低于{t.TollCritical:P0}={r.Severe}，危急={r.Urgent}，伤员总缺血={r.Deficit*100:F1}个百分点；执行={(executing?"开":"关（仅诊断）")}"),
            ("virtue",sample.VirtueState+sample.VirtueSequence,$"[小队美德] {sample.VirtueState}；剩余={sample.VirtueRemaining:F1}秒；仅冷却推定，不代表道标归属"),
            ("hand",handState,"[神性之手] "+handState)
        };
        var changes=new List<PartyLogChange>();
        foreach(var (key,state,text) in entries) {
            if(!previous.TryGetValue(key,out var old)||old!=state) changes.Add(new("healing:"+key,text));
            previous[key]=state;
        }
        Snapshot=string.Join(Environment.NewLine,entries.Select(e=>e.Item3));return changes;
    }
}
