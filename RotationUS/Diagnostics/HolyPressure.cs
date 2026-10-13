#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

// This is an observation/rate-limit policy, never evidence that a key cast a
// spell, that Wings is active, or that any particular unit carries a Beacon.
internal sealed record HolyCooldownContext(bool Holding=false,int LastKey=0,bool Retry=false,
    bool Worsening=false,bool Sustained=false,IReadOnlySet<int>? ThrottledKeys=null,HolyHealingSample? Healing=null)
{
    // Holding is a repeat-request throttle, not a shared cooldown waiting period.
    internal bool Allows(int key,bool emergency=false)=>ThrottledKeys is not null?
        !ThrottledKeys.Contains(key):!Holding||key!=LastKey;
}

internal sealed class HolyPressure
{
    private sealed record Snapshot(bool Raid,string Roster,double[] Health,int Urgent,bool Aoe);
    private Snapshot? baseline,attempt;
    private double baselineAt,lastAt,lastAttempt=double.NegativeInfinity,worseningUntil;
    private int lastKey;
    private readonly Dictionary<int,double> requestedAt=new();

    internal void Reset()
    {
        baseline=attempt=null;baselineAt=lastAt=worseningUntil=0;
        lastAttempt=double.NegativeInfinity;lastKey=0;requestedAt.Clear();
    }

    private static Snapshot? Read(Dictionary<int,Color> frames,HealingThresholds t)
    {
        bool? F(int id)=>frames.TryGetValue(id,out var c)?FuryBuffs.Boolean(c):null;
        if(F(1)!=true||F(2)!=true||F(96)!=true||F(100)!=true||F(19)!=true||F(41) is null) return null;
        bool raid=F(41)==true;
        var group=HolyTriage.Analyze(frames,raid,t);
        if(!group.Reliable||group.Unknown!=0) return null;
        var values=new double[raid?40:5];var roster=new char[values.Length];
        for(int i=0;i<values.Length;i++) {
            var hp=TeamHealthReading.Read(frames[51+i]);
            roster[i]=hp.Percent is >0?'L':hp.Status==TeamHealthStatus.Empty?'E':hp.Status==TeamHealthStatus.Offline?'O':'D';
            values[i]=hp.Percent??0;
        }
        return new(raid,new string(roster),values,group.Urgent,group.Aoe);
    }

    private static bool Comparable(Snapshot a,Snapshot b)=>a.Raid==b.Raid&&a.Roster==b.Roster;
    private static bool Worse(Snapshot current,Snapshot before)=>Comparable(current,before)&&
        (current.Urgent>before.Urgent||current.Health.Where((hp,i)=>before.Health[i]-hp>=.08).Any()||
         before.Health.Sum()-current.Health.Sum()>=.05*current.Roster.Count(c=>c=='L'));

    internal HolyCooldownContext Observe(Dictionary<int,Color> frames,HealingThresholds t,double now)
    {
        HolyCooldownContext Context(bool worsening=false,bool sustained=false) {
            var blocked=requestedAt.Where(p=>now-p.Value<.5).Select(p=>p.Key).ToHashSet();
            return new(blocked.Count>0,lastKey,now-lastAttempt>=.5,worsening,sustained,blocked);
        }
        var current=Read(frames,t);
        if(current is null) {
            bool Confirmed(int id)=>frames.TryGetValue(id,out var c)&&FuryBuffs.Boolean(c)==true;
            if(!Confirmed(1)||!Confirmed(2)||!Confirmed(96)||!Confirmed(100)) {Reset();return new();}
            // Missing roster data cannot prove improvement/deterioration.
            // It does not erase the per-key request throttle.
            baseline=attempt=null;worseningUntil=0;lastAt=now;
            return Context();
        }
        if((baseline is not null&&!Comparable(current,baseline))||now<lastAt||now-lastAt>Math.Max(3,t.CooldownReviewSeconds*2)) {
            if(now<lastAt) Reset();
            baseline=current;attempt=null;worseningUntil=0;baselineAt=now;
        }
        if(baseline is null) {baseline=current;baselineAt=now;}
        lastAt=now;
        if(!current.Aoe) worseningUntil=0;
        bool worsening=attempt is not null&&Worse(current,attempt);
        if(now-baselineAt>=t.CooldownReviewSeconds) {
            if(current.Aoe&&baseline.Aoe&&Worse(current,baseline)) worseningUntil=now+t.CooldownReviewSeconds;
            baseline=current;baselineAt=now;
        }
        bool sustained=current.Aoe&&now<worseningUntil;
        // CooldownReviewSeconds remains a compatible damage-trend sample
        // window. It never delays Virtue or another available cooldown.
        return Context(worsening||sustained,sustained);
    }

    internal void Requested(int key,Dictionary<int,Color> frames,HealingThresholds t,double now)
    {
        if(key is not (4 or 10 or 13 or 14 or 15)) return;
        requestedAt[key]=now;
        // Virtue only limits repeated requests for itself. It neither starts a
        // large-cooldown review nor resets the observed pressure history.
        if(key==15) return;
        attempt=Read(frames,t);lastAttempt=now;lastKey=key;
        // A pre-existing worsening signal justified this request; it must not
        // also authorize the next cooldown without fresh deterioration.
        baseline=attempt;baselineAt=now;worseningUntil=0;
    }
}
