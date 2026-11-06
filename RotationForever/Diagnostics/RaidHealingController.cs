#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal sealed class RaidHealingController
{
    private readonly RaidHealingWindow window=new();
    private readonly HolyPressure pressure=new();
    private string? identity;
    private int pending,confirmations,confirmedRevision,lastProcessedRevision,observedRevision;
    private double started,lastAt=double.NegativeInfinity;
    internal RaidRosterSnapshot? Roster {get;private set;}
    internal RaidHealingSample Sample=>window.Current;
    private Dictionary<int,Color>? pressureFrames;
    internal HolyCooldownContext CooldownContext=>pressure.Current;
    internal void CancelExecution() {pending=confirmations=0;pressure.ClearRequests();}
    internal void Reset()
    {
        window.Reset();pressure.Reset();Roster=null;identity=null;
        pending=confirmations=confirmedRevision=lastProcessedRevision=observedRevision=0;started=0;lastAt=double.NegativeInfinity;
        pressureFrames=null;
    }
    internal void Observe(Dictionary<int,Color> f,IReadOnlyDictionary<int,Color>? bars,HealingThresholds t,double now,bool live=true)
    {
        bool F(int id)=>f.TryGetValue(id,out var c)&&FuryBuffs.Boolean(c)==true;
        var roster=live&&F(41)&&F(19)?RaidRosterSnapshot.Read(bars):null;
        if(roster is null||!F(1)||!F(2)||!F(96)||!F(100)||!double.IsFinite(now)) {Reset();return;}
        if(identity!=roster.Identity||now<lastAt) Reset();
        if(observedRevision==roster.Revision&&now-lastAt>1.5) {
            window.Reset();pressure.Reset();pending=confirmations=0;Roster=null;return;
        }
        bool fresh=observedRevision!=roster.Revision;
        if(fresh) {
            if(now-lastAt>1.5) {window.Reset();pressure.Reset();pending=confirmations=0;}
            lastAt=now;observedRevision=roster.Revision;
        }
        Roster=roster;identity=roster.Identity;
        window.Observe(f,roster,t,now,live);
        if(fresh) {
            pressureFrames=HolyTriage.HealingFrames(f,roster);
            pressure.Observe(pressureFrames,t,now);
        }
    }
    internal HolyDecision Process(Dictionary<int,Color> f,IReadOnlyDictionary<int,Color> bars,HolyBuffs buffs,HealingThresholds t,
        HolySacrificeContext sacrifice,double now)
    {
        Observe(f,bars,t,now);
        var roster=Roster;
        if(roster is not null&&lastProcessedRevision==roster.Revision) return new(0,"等待新的团队采样");
        if(roster is not null) lastProcessedRevision=roster.Revision;
        // Only reachable members contribute to the raid's sustained-pressure
        // history. The unmodified raw forty-member HP remains the damage gate.
        var context=CooldownContext with{Sacrifice=sacrifice};
        bool targetConfirmed=pending==0||(roster?.Target(pending)==true&&confirmations>=1&&confirmedRevision!=roster.Revision);
        var decision=RaidHolyPolicy.Decide(f,buffs,t,context,roster,Sample,pending,targetConfirmed);
        bool ValidPending()=>roster is not null&&roster.BindingsReady&&roster.Reachable(pending)&&
            f.TryGetValue(50+pending,out var c)&&TeamHealthReading.Read(c).Percent is >0;
        if(pending>0&&(!ValidPending()||decision.Key==18)) pending=confirmations=0;
        if(pending>0&&RaidTargetKeys.IsSelection(decision.Key)&&decision.Key!=200+pending) pending=confirmations=0;
        // These actions do not heal an unconfirmed external target.
        bool independent=decision.Key is 1 or 2 or 3 or 5 or 14 or 22 or 23;
        if(pending>0&&!independent) {
            if(roster!.Target(pending)) {
                if(confirmedRevision!=roster.Revision) {confirmedRevision=roster.Revision;confirmations++;}
                if(confirmations<2) return new(0,"等待团队选人后的第二次目标确认");
                pending=confirmations=0;
                decision=RaidHolyPolicy.Decide(f,buffs,t,context,roster,Sample);
            } else {
                confirmations=0;
                if(now-started<2) return new(0,"等待团队目标切换确认");
                pending=0;
                return new(0,"团队选人两秒未确认，请检查插件快捷键；下一次采样重新评估");
            }
        }
        if(RaidTargetKeys.IsSelection(decision.Key)) {
            pending=decision.Key-200;confirmations=0;confirmedRevision=roster!.Revision;started=now;
        }
        if(decision.Key!=0) pressure.Requested(decision.Key,pressureFrames??f,t,now);
        return decision;
    }
}
