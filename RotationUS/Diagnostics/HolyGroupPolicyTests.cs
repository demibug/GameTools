#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal static class HolyGroupPolicyTests
{
    internal static int Run()
    {
        int count=0;
        var f=new Dictionary<int,Color>();
        var t=new HealingThresholds(AuraMasteryHasToll:true);
        var buff=new HolyBuffs {Infusion=false,HandOfDivinity=false};
        var confirmed=new HolyCooldownContext(Healing:new(new PartyTollReading(true),TollConfirmed:true));
        void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL Holy group policy: "+why);count++;}
        void F(int id,bool value=true)=>f[id]=value?Color.White:Color.Black;
        void Hp(int id,double hp) {int n=(int)Math.Round(hp*255);f[id]=Color.FromArgb(n,n,n);}
        void Member(int slot,double hp)=>f[50+slot]=Color.FromArgb((int)Math.Round(hp*255),255,0);
        void Skill(int n,bool value=true) {F(20+n,value);F(30+n,value);}
        void Reset(bool raid,int injured,double hp,int population=5) {
            f=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
            foreach(int id in new[]{1,2,3,4,5,9,10,19,20,42,43,44,45,47,96,100}) F(id);
            F(41,raid);Hp(6,1);Hp(7,1);Hp(8,hp);
            for(int slot=1;slot<=population;slot++) Member(slot,slot>=2&&slot<2+injured?hp:1);
            foreach(int spell in new[]{2,4,7,8,9}) Skill(spell);
            HOLY.Inst.Reset();HOLY.Inst.Thresholds=t;HOLY.Inst.Buffs=buff;
        }
        int Decide(HolyCooldownContext? context=null)=>RaidTestFixture.Decide(f,buff,t,context??confirmed).Key;
        int Tick(double now) {
            var actions=new Dictionary<int,bool>();HOLY.Inst.Process(f,FuryBuffs.Boolean(f[41])==true?RaidTestFixture.Bars(f):new(),actions,now);
            Check(actions.Count<=1,"one action per capture");return actions.Keys.SingleOrDefault();
        }

        foreach(bool raid in new[]{false,true})
        foreach(double hp in new[]{.8,.55,.4,.1}) {
            Reset(raid,2,hp);
            Check(Decide()==8,$"two injuries only use direct healing: raid={raid}, hp={hp}");
            Check(Decide(confirmed with{Sustained=true})==8,"worsening cannot bypass three-member minimum");
            var group=HolyTriage.Analyze(f,raid,t);
            Check(!group.Aoe&&!group.Major&&!group.Toll,"two-member group flags stay false");
            Reset(raid,raid?4:3,hp);
            Check(Decide()==15,"mode-specific minimum opens Virtue including emergency damage");
        }

        foreach(bool raid in new[]{false,true}) {
            int toll=raid?10:4;
            // Each unavailable observation must fall through in the same decision.
            foreach(int id in new[]{29,39})
            foreach(var color in new[]{Color.Black,Color.Orange}) {
                Reset(raid,raid?4:3,.55);f[id]=color;
                Check(Decide()==toll,"unavailable or unknown Virtue never holds ready Toll");
                Skill(4,false);Check(Decide()==13,"unavailable Virtue never holds group Wings");
                Skill(7,false);Check(Decide()==14,"unavailable Virtue never holds group Mastery");
                Skill(8,false);Check(Decide()==8,"unavailable Virtue never holds direct healing");
            }
            Reset(raid,raid?4:3,.55);
            Check(Decide(confirmed with{ThrottledKeys=new HashSet<int>{15}})==toll,"Virtue repeat throttle does not hold other cooldowns");
            Skill(2,false);Check(Decide()==toll,"no executable direct follow-up skips Virtue without holding Toll");

            Reset(raid,raid?4:3,.55);Skill(4,false);
            Check(Decide()==15,"Virtue precedes group Wings");
            Skill(7,false);Check(Decide()==15,"Virtue precedes group Mastery");
            Reset(raid,raid?4:3,.8);
            Check(Decide(confirmed with{Sustained=true})==15,"Virtue precedes sustained-pressure Wings");

            Reset(raid,raid?4:3,.1);F(91);F(92);
            Check(Decide()==15,"mode-specific critical minimum opens Virtue even at power cap");
            Skill(4,false);Check(Decide()==15,"Virtue precedes emergency Mastery when Toll unavailable");
            Skill(9,false);Check(Decide()==14,"unavailable Virtue falls through to emergency Mastery");
            foreach(int id in new[]{29,39})
            foreach(var color in new[]{Color.Black,Color.Orange}) {
                Reset(raid,raid?4:3,.1);F(91);F(92);f[id]=color;
                Check(Decide()==toll,"unavailable or unknown Virtue immediately falls through to emergency Toll");
                Skill(4,false);Check(Decide()==14,"unavailable or unknown Virtue immediately falls through to emergency Mastery");
            }
            Reset(raid,raid?4:3,.1);Skill(2,false);
            Check(Decide()==toll,"no executable follow-up does not delay emergency Toll for Virtue");
            Reset(raid,raid?4:3,.1);
            Check(Decide(confirmed with{ThrottledKeys=new HashSet<int>{15}})==toll,"Virtue repeat throttle permits emergency Toll immediately");
            F(3,false);F(4,false);F(5,false);F(47,false);Skill(4,false);
            Check(Decide()==14,"no valid Virtue recipient immediately permits self emergency Mastery");
            Reset(raid,raid?4:3,.75);Hp(8,.1);Member(2,.1);
            Check(Decide()==15,"one critical and two ordinary injured members open Virtue");
            Hp(6,.1);Member(1,.1);Member(4,1);
            Check(Decide()==15,"critical self with three injured members opens Virtue before self rescue");
            Skill(9,false);Check(Decide()!=15,"unavailable Virtue continues direct rescue for mixed injuries");
            foreach(int injuries in new[]{1,2}) {
                Reset(raid,injuries,.1);Skill(4,false);Skill(2,false);Skill(5);
                Check(Decide()==11,"one or two critical members never trigger rescue Wings, even without instant heals");
                Skill(7,false);Check(Decide()==11,"one or two critical members cannot trigger Mastery through single rescue pressure");
            }

            Reset(raid,raid?4:3,.55);
            Check(Tick(0)==15,"production starts with Virtue");
            F(29,false);F(20,false);Check(Tick(.1)==0,"Virtue follow-up respects actual GCD");
            F(20);Check(Tick(.2)==toll,"next ready GCD follows Virtue with Toll without waiting");
            Reset(raid,raid?4:3,.55);Check(Tick(0)==15,"recovery sequence starts with Virtue");
            F(29,false);Hp(8,1);for(int slot=2;slot<=(raid?5:4);slot++) Member(slot,1);
            Check(Tick(.1)==0,"recovery after Virtue cancels obsolete group cooldown demand");
            Reset(raid,raid?4:3,.55);Check(Tick(0)==15,"two-injury recovery sequence starts with Virtue");
            F(29,false);Member(4,1);
            Check(Tick(.1)==8,"dropping to two injuries after Virtue continues direct healing");
            Reset(raid,raid?4:3,.1);F(91);F(92);
            Check(Tick(0)==15,"production emergency starts with Virtue");
            F(29,false);F(20,false);Check(Tick(.1)==0,"emergency follow-up respects actual GCD");
            F(20);Check(Tick(.2)==toll,"emergency Toll follows Virtue at next ready GCD");
            Skill(4,false);Check(Tick(.3)==14,"persistent emergency can follow with Mastery without waiting for Virtue");
        }

        // Large raids use the same fixed four-person minimum, including emergencies.
        Reset(true,3,.4,40);Check(Decide()==8,"three severe of40 do not meet raid thresholds");
        for(int slot=2;slot<=9;slot++) Member(slot,.4);
        Check(Decide()==15,"eight below50 in40 open Virtue");
        Skill(9,false);Check(Decide()==10,"eight below50 in40 permit raid Toll");
        Reset(true,3,.1,40);Check(Decide()==8,"three critical in40 stay single-target");Member(5,.1);Check(Decide()==15,"four critical in40 open Virtue");
        Skill(9,false);Check(Decide()==10,"unavailable Virtue preserves raid emergency Toll exception");
        Reset(false,3,.55);f[54]=f[55]=Color.Orange;
        Check(Decide()==8,"insufficient health coverage cannot open group healing");
        Reset(false,3,.55);Skill(9,false);Skill(7,false);Skill(8,false);F(44,false);
        Check(Decide()==8,"party Toll still needs three reachable injured members");
        foreach(bool raid in new[]{false,true}) {
            Reset(raid,raid?4:3,.1);Hp(6,.1);Member(1,.1);Member(4,1);Skill(3);F(91);
            Check(Decide()==15,"critical self with available spender still opens group Virtue");
            Skill(9,false);Check(Decide()==(raid?10:4),"emergency group Toll still precedes direct self spender");
            Skill(4,false);Check(Decide()==14,"emergency group Mastery still precedes direct self spender");
            Skill(8,false);Check(Decide()==2,"after group rescue options self spender takes direct-heal priority");
        }
        // Wings has its own party thresholds; other group spells keep theirs.
        foreach(double hp in new[]{.59,.60,.65,178.0/255,179.0/255,.75})
        foreach(bool worsening in new[]{false,true}) {
            Reset(false,3,hp);Skill(9,false);Skill(4,false);Skill(8,false);
            bool expected=hp<.60||(hp<.70&&worsening);
            Check(Decide(confirmed with{Sustained=worsening})==(expected?13:8),$"party Wings threshold hp={hp}, worsening={worsening}");
        }
        Reset(false,3,.59);Skill(9,false);Skill(4,false);Skill(8,false);Member(4,.65);
        Check(Decide()==8,"two below60 and a third below70 need confirmed worsening");
        Check(Decide(confirmed with{Sustained=true})==13,"three below70 with worsening permit Wings");
        Member(4,.75);Check(Decide(confirmed with{Sustained=true})==8,"two below70 cannot trigger Wings even with worsening");
        Reset(false,3,.65);Skill(9,false);Skill(4,false);
        Check(Decide()==14,"stable three below70 retain Mastery rules without opening Wings");
        Skill(4);Check(Decide()==4,"stable three below70 retain ordinary Toll rules");
        Check(Decide(confirmed with{Sustained=true})==13,"confirmed worsening below70 retains Wings before ordinary Toll");
        Skill(9);Check(Decide(confirmed with{Sustained=true})==15,"Virtue remains before worsening Wings");

        Reset(false,3,.8);Skill(9,false);Skill(4,false);Skill(8,false);
        Check(Tick(0)==8,"Wings trend baseline uses direct healing");
        Hp(8,.65);for(int slot=2;slot<=4;slot++) Member(slot,.65);
        Check(Tick(.1)==8,"falling below70 alone does not bypass trend sampling");
        Check(Tick(2.1)==13,"production trend confirms worsening and releases Wings below70");
        Hp(8,.75);for(int slot=2;slot<=4;slot++) Member(slot,.75);
        Check(Tick(2.7)==8,"recovery above70 cancels Wings even while previous trend is retained");
        Reset(false,3,.83);Skill(9,false);Skill(4,false);Skill(8,false);
        Check(Tick(0)==8,"above70 worsening baseline");
        Hp(8,.72);for(int slot=2;slot<=4;slot++) Member(slot,.72);
        Check(Tick(2.1)==8,"confirmed worsening above70 does not open party Wings");
        Reset(false,3,.65);Skill(9,false);Skill(4,false);Skill(8,false);
        Check(Tick(0)==8&&Tick(2.1)==8,"stable three below70 never become Wings demand through elapsed time alone");
        Reset(false,3,.8);Skill(9,false);Skill(4,false);Skill(8,false);
        Tick(0);Hp(8,.65);for(int slot=2;slot<=4;slot++) Member(slot,.65);f[55]=Color.Orange;
        Check(Tick(2.1)==8,"unknown member prevents trend confirmation for Wings below70");

        // Raid uses four-person gates, while single rescue is removed
        // in both modes, including self rescue with no instant spell available.
        Reset(true,3,.65);Skill(9,false);Skill(4,false);Skill(8,false);
        Check(Decide()==8,"three injured raid members do not trigger Wings");
        foreach(bool raid in new[]{false,true}) {
            Reset(raid,1,.1);Hp(6,.1);Member(1,.1);Member(2,1);F(46);F(47,false);
            Skill(9,false);Skill(4,false);Skill(2,false);Skill(5);
            Check(Decide()==1,"critical self without instant healing uses Flash instead of Wings");
        }
        HOLY.Inst.Reset();HOLY.Inst.Thresholds=new();HOLY.Inst.Buffs=new();
        return count;
    }
}
