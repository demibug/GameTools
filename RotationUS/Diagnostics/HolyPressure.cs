#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

// This is an observation/rate-limit policy, never evidence that a key cast a
// spell, that Wings is active, or that any particular unit carries a Beacon.
internal sealed record HolyCooldownContext(bool Holding=false,int LastKey=0,bool Retry=false,
    bool Worsening=false,bool Sustained=false)
{
    internal bool Allows(int key,bool emergency=false)=>!Holding||
        (Retry&&(key==LastKey||Worsening||emergency));
}

internal sealed class HolyPressure
{
    private sealed record Snapshot(bool Raid,string Roster,double[] Health,int Urgent,bool Aoe);
    private Snapshot? baseline,attempt;
    private double baselineAt,lastAt,lastAttempt=double.NegativeInfinity,worseningUntil;
    private int lastKey;

    internal void Reset()
    {
        baseline=attempt=null;baselineAt=lastAt=worseningUntil=0;
        lastAttempt=double.NegativeInfinity;lastKey=0;
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
        var current=Read(frames,t);
        if(current is null) {
            bool Confirmed(int id)=>frames.TryGetValue(id,out var c)&&FuryBuffs.Boolean(c)==true;
            if(!Confirmed(1)||!Confirmed(2)||!Confirmed(96)||!Confirmed(100)) {Reset();return new();}
            // Missing roster data cannot prove recovery or deterioration, and
            // must not cancel the short cooldown observation interval.
            baseline=attempt=null;worseningUntil=0;lastAt=now;
            double elapsed=now-lastAttempt;
            return new(elapsed<t.CooldownReviewSeconds,lastKey,elapsed>=.5);
        }
        if((baseline is not null&&!Comparable(current,baseline))||now<lastAt||now-lastAt>Math.Max(3,t.CooldownReviewSeconds*2)) {
            Reset();baseline=current;baselineAt=now;
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
        double age=now-lastAttempt;
        // Party Virtue gets a chance to work before another cooldown. Raid
        // follow-up may proceed, but this does not claim Virtue is active.
        bool holding=age<.5||(age<t.CooldownReviewSeconds&&(lastKey!=15||!current.Raid));
        return new(holding,lastKey,age>=.5,worsening||sustained,sustained);
    }

    internal void Requested(int key,Dictionary<int,Color> frames,HealingThresholds t,double now)
    {
        if(key is not (4 or 10 or 13 or 14 or 15)) return;
        // Retrying the same unacknowledged request must not extend the review
        // interval forever. No cooldown/buff state is inferred here.
        if(key==lastKey&&now-lastAttempt<t.CooldownReviewSeconds) return;
        attempt=Read(frames,t);lastAttempt=now;lastKey=key;
        // A pre-existing worsening signal justified this request; it must not
        // also authorize the next cooldown without fresh deterioration.
        baseline=attempt;baselineAt=now;worseningUntil=0;
    }
}
