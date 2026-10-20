#nullable enable
using System.Drawing;
using System.Text.Json;

namespace RotationUS.Diagnostics;

internal static class HolyMasteryTests
{
    internal static int Run()
    {
        int count=0,revision=1;
        var f=new Dictionary<int,Color>();var bars=new Dictionary<int,Color>();
        var t=new HealingThresholds();var buffs=new HolyBuffs();
        bool raid=false;
        void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL Holy Mastery: "+why);count++;}
        void F(int id,bool value=true)=>f[id]=value?Color.White:Color.Black;
        void Hp(int id,double hp) {int n=(int)Math.Round(hp*255);f[id]=Color.FromArgb(n,n,n);}
        void Member(int slot,double hp)=>f[50+slot]=Color.FromArgb((int)Math.Round(hp*255),255,0);
        void Skill(int n,bool value=true) {F(20+n,value);F(30+n,value);}
        void Group(double hp,int? amount=null) {
            for(int slot=1;slot<=(raid?40:5);slot++) Member(slot,slot>=2&&slot<2+(amount??(raid?4:3))?hp:1);
            Hp(8,hp);
        }
        void Target(int slot) {
            for(int i=1;i<=5;i++) F(45+i,i==slot);
            for(int i=1;i<=40;i++) bars[i]=Color.FromArgb(bars[i].R,i==slot?255:0,i==1?255:0);
            F(3,slot>0);F(4,slot>0);F(5,slot>0);
            if(slot>0) Hp(8,TeamHealthReading.Read(f[50+slot]).Percent??1);
        }
        void Reset(bool mode=false,double hp=.4) {
            raid=mode;revision=1;t=new();buffs=new(){Infusion=false,HandOfDivinity=false,DivinePurpose=false};
            f=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
            foreach(int id in new[]{1,2,3,4,5,9,10,19,20,42,43,44,45,47,96,100}) F(id);
            f[93]=f[94]=Color.Orange;F(41,raid);Hp(6,1);Hp(7,1);Group(hp);
            bars=RaidTestFixture.Bars(f,sequence:revision);
            HOLY.Inst.Reset();HOLY.Inst.Thresholds=t;HOLY.Inst.Buffs=buffs;
        }
        HolyCooldownContext Observe(double now,bool pulse=true,bool newPacket=true) {
            if(newPacket) {revision++;bars[43]=bars[45]=Color.FromArgb(revision&255,(revision>>8)&255,0);}
            HOLY.Inst.ObserveHealing(f,t,now,executing:false,raidBars:bars,pulse:pulse);
            return raid?HOLY.Inst.Raid.CooldownContext:HOLY.Inst.CooldownContext;
        }
        HolyDecision Preview()=>HOLY.Decide(f,buffs,t,raid?HOLY.Inst.Raid.CooldownContext:HOLY.Inst.CooldownContext,
            raidRoster:HOLY.Inst.Raid.Roster,raidHealing:HOLY.Inst.Raid.Sample);
        int Act() {var keys=new Dictionary<int,bool>();HOLY.Inst.Process(f,bars,keys);Check(keys.Count<=1,"at most one request");return keys.Keys.SingleOrDefault();}

        foreach(bool mode in new[]{false,true}) {
            Reset(mode);Skill(8);Skill(2);
            Check(!Observe(0).Mastery!.SevereConfirmed&&Preview().Key==8,"first severe sample waits");
            Check(!Observe(0,newPacket:false).Mastery!.SevereConfirmed,"duplicate evaluation does not confirm");
            Check(!Observe(.1,newPacket:false).Mastery!.SevereConfirmed,"same pulse/revision does not confirm");
            Check(Observe(.3,false).Mastery!.SevereConfirmed&&Preview().Key==14,"independent confirmation enables default Mastery");
            Check(Preview().Key==14&&Preview().Key==14,"preview is pure");
            int previousRequest=(mode?HOLY.Inst.Raid.CooldownContext:HOLY.Inst.CooldownContext).RequestSequence;
            Check(Act()==14,"execution consumes preview observation");
            Check((mode?HOLY.Inst.Raid.CooldownContext:HOLY.Inst.CooldownContext).RequestSequence==previousRequest+1,"actual request recorded once");

            Reset(mode,.1);Skill(8);F(91);F(92);Observe(0);
            Check(Preview().Key==14,"critical needs no historical confirmation and allows overflow");
            Group(.1,mode?3:2);Observe(.1,false);Check(Preview().Key!=14,"fixed mode-specific minimum");
            Reset(mode,.55);Skill(8);Skill(2);Observe(0);Observe(.3,false);
            Check(Preview().Key==8,"stable Major alone does not release Mastery");

            Reset(mode);Skill(8);Skill(2);Observe(0);Observe(.3,false);
            Skill(4);Observe(.4,false);Check(Preview().Key==(mode?10:4),"ready Toll precedes ordinary Mastery");
            f[24]=Color.Orange;f[34]=Color.Orange;Observe(.5,true);
            Check(Preview().Key==8,"unknown Toll does not authorize severe upgrade");
            f[24]=Color.Black;Observe(.6,true);Check(Preview().Key==14,"confirmed unavailable Toll authorizes severe upgrade");
            F(91);Skill(3);Observe(.7,false);Check(Preview().Key==9,"usable spender before ordinary Mastery");
            Skill(3,false);Observe(.8,false);Check(Preview().Key==8,"unusable spender does not authorize nonurgent overflow");
            F(91,false);f[92]=Color.Orange;Observe(.9,true);Check(Preview().Key==8,"unknown power blocks nonurgent burst");
            F(92,false);Target(0);Observe(1,false);Check(Preview().Key==14,"ordinary Mastery needs no friendly target");

            Reset(mode,.7);Skill(2);Target(1);Observe(0);Check(Act()==(mode?202:102),"begin selection");
            Group(.1);Target(2);Skill(4);Skill(8);Observe(.1,false);
            Check(Act()==14,"ready Toll awaiting second target confirmation cannot block critical Mastery");
            Reset(mode,.7);Skill(2);Target(1);Observe(0);Act();
            Group(.4);Target(2);Skill(4);Skill(8);Observe(.1,false);
            Check(Act()==0,"ordinary pending Toll cannot by itself authorize Mastery");
            Observe(.3,true);Check(Act()==(mode?10:4),"ordinary second acknowledgement retains Toll priority");
            Group(.1);f[24]=f[34]=Color.Orange;Observe(.4,false);
            Check(Preview().Key==14,"critical injury independently permits Mastery with unknown Toll");
            F(20,false);Check(Preview().Key==0,"critical Mastery still respects GCD");
            F(20);F(9,false);Check(Preview().Key==0,"critical Mastery still respects active cast");
            Reset(mode,.1);Skill(4);Skill(8);Observe(0);Check(Act()==(mode?10:4),"confirmed target keeps Toll first");
            Group(1);Observe(.1,false);Check(Act()!=14,"recovery cancels additional cooldown");

            Reset(mode,.7);Skill(6);F(10,false);buffs.HandOfDivinity=true;Group(.7,1);Observe(0);
            Check(Preview().Key==12,"moving Hand fallback at ordinary health");
            Skill(2);Observe(.1,false);Check(Preview().Key==8,"single Shock still beats new fallback");
            Skill(2,false);Skill(3);F(91);Observe(.2,true);Check(Preview().Key==9,"ordinary spender beats new fallback");
            F(91,false);Skill(3,false);Skill(5);buffs.Infusion=true;Observe(.3,false);
            Check(Preview().Key==11,"infused Flash beats new fallback");
            buffs.Infusion=false;Skill(5,false);f[10]=Color.Orange;Observe(.4,true);
            Check(Preview().Key==0,"unknown movement does not enable new fallback");
            F(10);Observe(.5,false);Check(Preview().Key==0,"stationary ordinary Hand retains old policy");
            F(10,false);buffs.HandOfDivinity=null;Observe(.6,true);Check(Preview().Key==0,"unknown Hand cannot grant instant Light");
            buffs.HandOfDivinity=true;Skill(6,false);Observe(.7,false);Check(Preview().Key==0,"unusable Light cannot use proc");
            Skill(6);Skill(9);Group(.7);Observe(.8,true);Check(Preview().Key==15,"moving Hand authorizes useful Virtue followup");
        }

        // Raid target bindings authorize selection, not the self Mastery key.
        // Exercise real observations and Process with missing/unknown bindings,
        // both without a target and with a visible friendly target.
        foreach(var binding in new[]{Color.Black,Color.Orange})
        foreach(int target in new[]{0,2}) {
            Reset(true);Skill(8);Target(target);bars[49]=binding;
            Observe(0);Check(Act()==0,"missing bindings do not waive severe confirmation or permit selection");
            Observe(.3);
            Check(Preview().Key==14,"confirmed ordinary Mastery does not depend on raid target bindings");
            foreach(var guard in new[]{
                (9,Color.Black),(20,Color.Black),(28,Color.Black),(38,Color.Black),
                (91,Color.White),(92,Color.White),(91,Color.Orange),(92,Color.Orange)
            }) {
                var previous=f[guard.Item1];f[guard.Item1]=guard.Item2;
                Check(Preview().Key==0,"binding-independent Mastery retains cast/readiness/resource gate "+guard.Item1);
                f[guard.Item1]=previous;
            }
            buffs.DivinePurpose=true;
            Check(Preview().Key==0,"binding-independent ordinary Mastery retains free-spender priority");
            buffs.DivinePurpose=false;
            Check(HOLY.Decide(f,buffs,t with{RaidAutoAuraMastery=false},HOLY.Inst.Raid.CooldownContext,
                raidRoster:HOLY.Inst.Raid.Roster,raidHealing:HOLY.Inst.Raid.Sample).Key==0,
                "missing bindings cannot bypass raid manual reserve");
            Group(.4,3);Check(Preview().Key==0,"missing bindings cannot bypass four-member minimum");
            Group(.4);Target(target);
            Skill(4);Check(Preview().Key==0,"unavailable target path alone does not qualify ordinary Mastery");
            f[24]=f[34]=Color.Orange;Check(Preview().Key==0,"unknown Toll still cannot qualify severe Mastery");
            Skill(4,false);
            Check(Act()==14,"execution releases self Mastery without raid target bindings");
            Observe(.4);Check(Act()==0,"binding-independent Mastery retains request throttle");
        }

        Reset(true,.1);Skill(8);t=t with{RaidAutoAuraMastery=false};HOLY.Inst.Thresholds=t;Observe(0);
        Check(Preview().Key!=14,"raid reserve blocks emergency");
        F(41,false);raid=false;Observe(.1,false);Check(Preview().Key==14,"raid reserve does not disable party");

        // Pressure comparisons use only eligible injured actors, independent of
        // the pre-existing Wings trend, and requests consume old evidence.
        foreach(bool mode in new[]{false,true}) {
            Reset(mode,.59);var pressure=new HolyPressure();
            Dictionary<int,Color> Treatment()=>HolyTriage.HealingFrames(f,mode?RaidRosterSnapshot.Read(bars):null);
            pressure.Observe(Treatment(),t,0);Member(1,.9);pressure.Observe(Treatment(),t,1);
            var unchanged=pressure.Observe(Treatment(),t,2);
            Check(!unchanged.Mastery!.Worsening,"healthy actor loss does not prove low-health deterioration");
            Group(.48);pressure.Observe(Treatment(),t,3);var worsening=pressure.Observe(Treatment(),t,4);
            Check(worsening.Mastery!.Deteriorating,"low-health actors falling confirm new deterioration");
            pressure.Requested(13,Treatment(),t,4);
            Check(!pressure.Current.Mastery!.Worsening,"request consumes trend without claiming success");
            Check(!pressure.Observe(Treatment(),t,4.1).Mastery!.Worsening,"old drop cannot authorize another cooldown");
            Check(pressure.Current.Mastery!.SevereConfirmed,"independent severe injury remains confirmed");
            Group(.9);Check(!pressure.Observe(Treatment(),t,4.2).Mastery!.Worsening,"recovery invalidates evidence");
            Check(!pressure.Observe(Treatment(),t,6).Mastery!.SevereConfirmed,"sampling gap clears confirmation");
            Group(.4);pressure.Observe(Treatment(),t,7);pressure.Observe(Treatment(),t,7.1);
            Check(!pressure.Observe(Treatment(),t,6.5).Mastery!.SevereConfirmed,"clock rollback clears old confirmation");
        }

        foreach(bool mode in new[]{false,true})
        foreach(var boundary in new[]{("Major",153),("Severe",128),("Urgent",51)}) {
            Reset(mode);Group(boundary.Item2/255.0);
            t=boundary.Item1 switch{
                "Major"=>t with{Major=boundary.Item2/255.0},
                "Severe"=>t with{GroupCritical=boundary.Item2/255.0},
                _=>t with{Critical=boundary.Item2/255.0}
            };
            int Count(HolyMasterySample sample)=>boundary.Item1 switch{"Major"=>sample.Major,"Severe"=>sample.Severe,_=>sample.Urgent};
            var exact=Observe(0).Mastery!;
            Check(Count(exact)==0,"encoded exact threshold is excluded: "+boundary.Item1);
            Group((boundary.Item2-1)/255.0);
            Check(Count(Observe(.1,false).Mastery!)==(mode?4:3),"one encoded step below threshold is included: "+boundary.Item1);
        }

        Reset();Skill(8);Skill(2);Observe(0);Observe(.3,false);
        F(43,false);Observe(.4,true);Check(Preview().Key==8,"out-of-range party injury excluded");
        F(43);Observe(.5,false);Check(Preview().Key==8,"range change requires fresh confirmation");
        Observe(.8,true);Check(Preview().Key==14,"range cohort reconfirms");
        f[43]=Color.Orange;Observe(.9,false);Check(Preview().Key==8,"unknown range is not confirmed injury");
        F(43);Observe(1,true);t=t with{GroupCritical=.45};HOLY.Inst.Thresholds=t;Observe(1.1,false);
        Check(!HOLY.Inst.CooldownContext.Mastery!.SevereConfirmed,"threshold changes invalidate prior confirmation");

        Reset();Skill(8);F(29);Observe(0);F(29,false);Observe(.3,false);
        Check(HOLY.Inst.HealingSample.VirtueActive,"observed Virtue edge");
        F(43,false);Observe(.4,true);Check(HOLY.Inst.HealingSample.VirtueActive,"range change retains Virtue window");
        var limiter=new HolyPressure();var treatment=HolyTriage.HealingFrames(f);
        limiter.Observe(treatment,t,0);limiter.Requested(14,treatment,t,0);F(43);treatment=HolyTriage.HealingFrames(f);
        Check(!limiter.Observe(treatment,t,.1).Allows(14),"range change retains same-key request throttle");

        Reset();Skill(8);Observe(0);Observe(.3,false);
        var runner=new RotationExecution();runner.Start(ClassProfiles.Find(65)!);
        Check(HOLY.Inst.CooldownContext.Mastery!.SevereConfirmed,"starting execution retains fresh preview history");
        runner.Stop();Check(HOLY.Inst.CooldownContext.Mastery!.SevereConfirmed,"stopping execution retains observations");
        Observe(2,false);Check(!HOLY.Inst.CooldownContext.Mastery!.SevereConfirmed,"stale history cannot survive a sampling gap");

        // Configuration migration keeps calibration and all unrelated values.
        using var pattern=DiagnosticTests.Pattern(1);
        var layout=Calibration.Find(pattern,new Size(1600,900));
        layout.SpecId=65;
        layout.HolyThresholds=new(HealBelow:.88,RaidAutoAuraMastery:false);
        layout.SpecialPoints["holy_hand"]=new(){Enabled=true,Point=new(525,8),ReferenceColor=new(12,34,56)};
        string json=JsonSerializer.Serialize(layout).Replace("\"HolyThresholds\":{","\"HolyThresholds\":{\"AutoAuraMastery\":false,\"AuraMasteryHasToll\":false,");
        string path=Path.GetTempFileName();
        try {
            File.WriteAllText(path,json);var loaded=DiagnosticLayout.Load(path);loaded.Save(path);
            string saved=File.ReadAllText(path);
            Check(!saved.Contains("\"AutoAuraMastery\"")&&!saved.Contains("\"AuraMasteryHasToll\""),"legacy flags ignored and removed on save");
            Check(loaded.HolyThresholds.HealBelow==.88&&!loaded.HolyThresholds.RaidAutoAuraMastery,"unrelated parameters and raid reserve retained");
            Check(loaded.SpecialPoints["holy_hand"].Point==new SamplePoint(525,8)&&
                loaded.SpecialPoints["holy_hand"].ReferenceColor==new SampleColor(12,34,56),"calibration preserved");
        } finally {File.Delete(path);}
        var log=new HolyMasteryDiagnosticLog();Reset();Skill(8);var observed=Observe(0);
        Check(log.Observe(observed,f,t,false).Count==1&&log.Observe(observed,f,t,false).Count==0,"unchanged preview logs are silent");
        observed=Observe(.3,false);Check(log.Observe(observed,f,t,false).Count==1,"confirmation transition logged");
        Act();var lines=log.Observe(HOLY.Inst.CooldownContext,f,t,true);
        Check(lines.Any(x=>x.Message.Contains("已请求光环掌握")&&x.Message.Contains("不代表成功")),"request log does not invent success");
        HOLY.Inst.Reset();HOLY.Inst.Buffs=new();HOLY.Inst.Thresholds=new();
        return count;
    }
}
