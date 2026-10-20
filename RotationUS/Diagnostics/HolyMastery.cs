#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

// History is independent of the Wings trend. All values describe observations,
// never a successful cast or the number of remaining proc charges.
internal sealed record HolyMasterySample(bool Reliable=false,int Minimum=3,int Major=0,int Severe=0,int Urgent=0,
    bool SevereConfirmed=false,bool Worsening=false)
{
    internal bool Emergency=>Reliable&&Urgent>=Minimum;
    internal bool SevereNow=>Reliable&&Severe>=Minimum;
    internal bool Deteriorating=>Reliable&&Major>=Minimum&&Worsening;
    internal static HolyMasterySample Read(Dictionary<int,Color> f,HealingThresholds t)
    {
        bool raid=f.TryGetValue(41,out var c)&&FuryBuffs.Boolean(c)==true;
        var group=HolyTriage.Analyze(f,raid,t);
        int major=0;
        for(int slot=1;slot<=(raid?40:5);slot++)
            if(f.TryGetValue(50+slot,out var color)&&TeamHealthReading.Read(color).Percent is >0 and var hp&&hp<t.Major) major++;
        return new(group.Reliable&&(raid||group.Unknown==0),raid?4:3,major,group.Critical,group.Urgent);
    }
}

internal sealed class HolyMasteryWindow
{
    private double lastAt=double.NegativeInfinity,baselineAt,worseningUntil;
    private double[]? baseline;
    private string? population;
    private HealingThresholds? thresholds;
    private int consecutive;
    internal HolyMasterySample Current {get;private set;}=new();
    internal void Reset()
    {
        lastAt=double.NegativeInfinity;baselineAt=worseningUntil=0;
        baseline=null;population=null;thresholds=null;consecutive=0;Current=new();
    }
    internal HolyMasterySample Observe(Dictionary<int,Color> f,HealingThresholds t,double now,bool fresh)
    {
        if(now==lastAt) return Current;
        bool raid=f.TryGetValue(41,out var c)&&FuryBuffs.Boolean(c)==true;
        var health=Enumerable.Range(51,raid?40:5)
            .Select(i=>f.TryGetValue(i,out var color)?TeamHealthReading.Read(color):new(TeamHealthStatus.Unknown,null)).ToArray();
        string members=(raid?"raid:":"party:")+string.Join(",",health.Select(h=>h.Percent is >0?"alive":h.Status+":"+h.Percent));
        var reading=HolyMasterySample.Read(f,t);
        bool changed=population!=members||thresholds!=t||now<lastAt||now-lastAt>1.5;
        if(changed) {baseline=null;consecutive=0;worseningUntil=0;}
        lastAt=now;population=members;thresholds=t;
        // Recovery/unknown data invalidates evidence even between heartbeat edges.
        if(!reading.SevereNow) consecutive=0;
        else if(fresh) consecutive=Math.Min(2,consecutive+1);
        if(!reading.Reliable||reading.Major<reading.Minimum) worseningUntil=0;
        if(!reading.Reliable||health.Any(h=>h.Status==TeamHealthStatus.Unknown)) {
            baseline=null;worseningUntil=0;
        } else {
            var values=health.Select(h=>h.Percent??0).ToArray();
            if(baseline is null) {baseline=values;baselineAt=now;}
            else if(fresh&&now-baselineAt>=t.CooldownReviewSeconds) {
                var drops=values.Select((hp,i)=>(hp,drop:baseline[i]-hp))
                    .Where(x=>x.hp>0&&x.hp<t.Major).Select(x=>x.drop).ToArray();
                if(reading.Major>=reading.Minimum&&
                    (drops.Any(d=>d>=.08)||drops.Length>0&&drops.Average()>=.05))
                    worseningUntil=now+t.CooldownReviewSeconds;
                baseline=values;baselineAt=now;
            }
        }
        return Current=reading with{SevereConfirmed=consecutive>=2,Worsening=now<worseningUntil};
    }
    internal void Requested(double now)
    {
        // Consume only the old trend; independently severe/critical injury can
        // still justify the next cooldown. No success is inferred from a request.
        baseline=null;baselineAt=now;worseningUntil=0;
        Current=Current with{Worsening=false};
    }
}

internal static class HolyMasteryPolicy
{
    internal static bool TollUnavailable(Dictionary<int,Color> f)=>
        f.TryGetValue(24,out var ready)&&FuryBuffs.Boolean(ready)==false||
        f.TryGetValue(34,out var usable)&&FuryBuffs.Boolean(usable)==false;
    internal static bool Eligible(HolyMasterySample s,Dictionary<int,Color> f)=>
        s.Emergency||s.Deteriorating||(s.SevereNow&&s.SevereConfirmed&&TollUnavailable(f));
    internal static string Reason(HolyMasterySample s,Dictionary<int,Color> f,HealingThresholds t,bool raid)
    {
        if(raid&&!t.RaidAutoAuraMastery) return "团队自动光环掌握关闭";
        if(!s.Reliable) return "有效伤情不足／血量或距离未知";
        if(s.Emergency) return "群体危急，允许圣能溢出";
        if(s.Deteriorating) return "已确认低血伤员持续恶化";
        if(!s.SevereNow) return $"光环伤情人数不足（需要{s.Minimum}人）";
        if(!s.SevereConfirmed) return "严重群伤等待第二次独立采样";
        if(TollUnavailable(f)) return "严重群伤已确认，鸣钟明确不可用";
        bool? Flag(int id)=>f.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        return Flag(24)==true&&Flag(34)==true?"鸣钟可用，优先鸣钟":"鸣钟状态未知";
    }
}

internal sealed class HolyMasteryDiagnosticLog
{
    private readonly Dictionary<string,string> previous=new();
    private readonly Dictionary<string,int> requests=new();
    internal List<PartyLogChange> Observe(HolyCooldownContext context,Dictionary<int,Color> f,HealingThresholds t,bool executing)
    {
        bool? F(int id)=>f.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        bool raid=F(41)==true;
        string mode=raid?"团队":"小队";
        var sample=context.Mastery??new();
        string reason=HolyMasteryPolicy.Reason(sample,f,t,raid);
        string gate=F(1)!=true||F(2)!=true?"玩家状态未确认":
            F(9)!=true||F(20)!=true?"等待施法结束／GCD":
            F(28)!=true||F(38)!=true?"光环掌握未就绪／不可用／未知":
            !context.Allows(14)?"光环掌握请求限频":
            !sample.Emergency&&(F(91)!=false||F(92)!=false)?"非紧急：先处理圣能／圣能未知":
            "仍需按可执行技能优先级判断";
        string state=reason+";"+gate+";"+executing;
        var changes=new List<PartyLogChange>();
        if(!previous.TryGetValue(mode,out var old)||old!=state) {
            string recovery=old is not null&&(!sample.Reliable||sample.Major<sample.Minimum&&sample.Severe<sample.Minimum&&sample.Urgent<sample.Minimum)?
                "；伤情恢复或证据失效，取消旧追加冷却资格":"";
            changes.Add(new("mastery:"+mode,$"[{mode}光环掌握] {reason}；{gate}；低于大掉血线={sample.Major}，严重={sample.Severe}，危急={sample.Urgent}；执行={(executing?"开":"关（仅预览）")}{recovery}"));
            previous[mode]=state;
        }
        if(executing&&context.LastKey>0&&context.RequestSequence>0&&
            (!requests.TryGetValue(mode,out var sequence)||sequence!=context.RequestSequence)) {
            string name=context.LastKey switch{4 or 10=>"圣洁鸣钟",13=>"复仇之怒",14=>"光环掌握",15=>"美德道标",_=>"治疗动作"};
            changes.Add(new("mastery-request:"+mode,$"[{mode}治疗请求] 已请求{name}；不代表成功施放或已经治疗（请求{context.RequestSequence}）"));
            requests[mode]=context.RequestSequence;
        }
        return changes;
    }
}
