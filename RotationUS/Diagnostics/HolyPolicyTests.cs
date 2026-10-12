#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal static class HolyPolicyTests
{
    internal static int Run()
    {
        int count=0;
        void Check(bool ok,string message) {if(!ok) throw new Exception("FAIL Holy policy: "+message);count++;}
        var frames=new Dictionary<int,Color>();var buff=new HolyBuffs();
        var settings=new HealingThresholds();
        void F(int id,bool value=true)=>frames[id]=value?Color.White:Color.Black;
        void Hp(int id,double value) {int v=(int)Math.Round(value*255);frames[id]=Color.FromArgb(v,v,v);}
        void Member(int slot,double value)=>frames[50+slot]=Color.FromArgb((int)Math.Round(value*255),255,0);
        void Skill(int n,bool available=true) {F(20+n,available);F(30+n,available);}
        void Reset(bool raid=false) {
            frames=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
            foreach(int id in new[]{1,2,3,4,5,9,10,19,20,42,43,44,45,47,96,99,100}) F(id);
            F(41,raid);Hp(6,1);Hp(7,1);Hp(8,.7);
            for(int i=1;i<=(raid?40:5);i++) Member(i,1);
            Member(2,.7);buff=new(){Infusion=false,HandOfDivinity=false,DivinePurpose=false};
            frames[93]=frames[94]=Color.Orange;
            settings=new();HOLY.Inst.Reset();
        }
        void Want(int key,string message,HolyCooldownContext? context=null) {
            var d=HOLY.Decide(frames,buff,settings,context);
            Check(d.Key==key,message+" actual="+d.Key+" "+d.Reason);
        }
        void Group(double hp=.4) {Hp(8,hp);Member(2,hp);Member(3,hp);Member(4,hp);}
        int Tick(double time) {
            HOLY.Inst.Buffs=buff;HOLY.Inst.Thresholds=settings;
            var actions=new Dictionary<int,bool>();
            HOLY.Inst.Process(frames,new(),actions,time);
            Check(actions.Count<=1,"at most one emitted action per observation");
            return actions.Count==0?0:actions.Keys.Single();
        }

        // Reproductions from the review: ordinary self healing cannot consume
        // the only immediate heal while an accessible ally is dying.
        foreach(bool raid in new[]{false,true})
        foreach(int spell in new[]{3,5,6}) {
            Reset(raid);Hp(6,.8);Member(1,.8);Hp(8,.1);Member(2,.1);
            Skill(spell);F(91);buff.Infusion=spell==5;buff.HandOfDivinity=spell==6;
            Want(spell==3?9:spell==5?11:12,"critical ally precedes ordinary self macro");
        }
        Reset();Hp(6,.8);Member(1,.8);Member(3,.1);Skill(3);F(91);
        Want(103,"party selection of critical ally precedes self spender");
        Reset();Hp(6,.1);Member(1,.1);Member(2,.05);Hp(8,.05);Skill(3);F(91);
        Want(2,"self emergency remains available even when another ally is lower");
        Reset();Hp(6,.8);Member(1,.8);Member(3,.1);F(43,false);Hp(8,1);Member(2,1);Skill(3);F(91);
        Want(2,"out-of-range critical ally cannot suppress possible self healing");
        Reset(true);Hp(6,.8);Member(1,.8);Member(3,.1);Hp(8,1);Member(2,1);Skill(3);F(91);
        Want(2,"raid does not invent a cast route to an unselected injured member");

        foreach(int spell in new[]{2,3,5,6}) {
            Reset();Hp(8,.1);Member(2,.1);Member(3,.8);Skill(9);Skill(spell);F(91);
            buff.Infusion=spell==5;buff.HandOfDivinity=spell==6;
            Want(spell==2?8:spell==3?9:spell==5?11:12,"direct emergency heal precedes Virtue setup");
        }
        Reset();Hp(8,.3);Member(2,.3);Member(3,.8);Skill(9);Skill(3);F(91);
        settings=settings with{Critical=.35};Want(9,"configured Critical participates in decisions");

        foreach(bool raid in new[]{false,true})
        foreach(int target in new[]{0,1,2})
        foreach(int rescue in new[]{1,2,3,5,22,23}) {
            Reset(raid);Hp(6,.1);Member(1,.1);Hp(8,1);Member(2,1);
            if(target==0) F(3,false);
            if(target==1) frames[3]=Color.Orange;
            if(target==2) {F(4,false);F(12);}
            switch(rescue) {
                case 1: Skill(5);buff.Infusion=true;break;
                case 2: Skill(3);F(91);break;
                case 3: Skill(6);buff.HandOfDivinity=true;break;
                case 5: Skill(10);break;
                case 22: F(16);break;
                case 23: F(17);break;
            }
            Want(rescue,"self rescue independent of missing/dead/hostile target");
            F(2,false);Want(0,"self rescue still requires living unmounted player");
        }

        foreach(bool raid in new[]{false,true})
        foreach(bool capped in new[]{false,true}) {
            Reset(raid);Group();if(raid) for(int i=5;i<=12;i++) Member(i,.4);
            F(91);F(92,capped);Skill(3);Skill(4);
            Want(9,"spend 3-5 HP before non-emergency Toll");
            F(91,false);F(92,false);Want(raid?10:4,"resample low resource then Toll");
            frames[91]=Color.Orange;Want(0,"unknown HP cannot authorize non-emergency Toll");
        }
        Reset(true);Group();for(int i=5;i<=12;i++) Member(i,.4);Hp(8,1);F(91);F(92);Skill(4);Skill(3);
        Want(0,"full current raid target cannot bypass resource gate with group Toll");
        foreach(bool? virtue in new bool?[]{true,false,null}) {
            Reset();Group(.1);Skill(4);Skill(3);F(91);F(92);Skill(9);
            frames[29]=virtue is null?Color.Orange:virtue==true?Color.White:Color.Black;
            Want(4,"multiple critical allies permit emergency Toll overflow regardless of Virtue");
        }
        Reset();Group(.75);Skill(4);Want(4,"three below80 enable useful party Toll");
        Member(4,1);Want(0,"two below80 alone do not enable party Toll");
        Hp(8,.55);Member(2,.55);Member(3,.55);Want(4,"two below60 enable party Toll");
        frames[54]=frames[55]=Color.Orange;Want(0,"unreliable group data cannot authorize Toll");
        Reset();Group(.75);Skill(4);settings=settings with{TollLow=.7};Want(0,"Toll threshold is configurable");
        Reset(true);Group(.75);Skill(4);Want(0,"relaxed party threshold not applied to forty-person raid");
        Reset(true);Hp(8,.1);Member(2,.1);Member(3,.1);Skill(4);F(91);F(92);
        Want(10,"two imminent deaths in a large raid may bypass population and overflow gates");

        Reset();Hp(8,1);Member(2,1);F(4,false);F(12);F(47,false);F(91);F(92);F(95);F(97);F(98);Skill(1);
        Want(18,"safe capped Shield independent of Shock charges");
        F(97,false);Want(0,"no generator spam at cap when Shield unavailable");
        F(92,false);Want(7,"Judgment remains usable below cap");

        foreach(bool? infusion in new bool?[]{true,false,null})
        foreach(bool? tollReady in new bool?[]{true,false,null}) {
            Reset();Group();Skill(7);Skill(5);buff.Infusion=infusion;
            frames[24]=tollReady is null?Color.Orange:tollReady==true?Color.White:Color.Black;
            F(34,false);Want(13,"needed Wings independent of Infusion and Toll cooldown");
        }
        Reset();Hp(8,.1);Member(2,.1);Skill(7);Skill(5);
        Want(13,"critical ally with no instant heal can request Wings before a hard cast");
        Skill(2);Want(8,"available instant heal saves critical ally before Wings");
        Reset();Group();Skill(7);Skill(2);settings=settings with{AutoWings=false};Want(8,"manual Wings reserve");
        Reset(true);Group();for(int i=5;i<=12;i++) Member(i,.4);Skill(7);Skill(2);
        settings=settings with{RaidAutoWings=false};Want(8,"raid Wings reserve");
        Reset();Group();Skill(8);Skill(2);Want(8,"default Mastery is manual without confirmed talent configuration");
        settings=settings with{AutoAuraMastery=true};Want(8,"auto switch alone cannot infer Mastery Toll talent");
        settings=settings with{AuraMasteryHasToll=true};Want(14,"explicit talent enables healing Mastery");
        F(91);Skill(3);Want(9,"spend before Mastery-generated Toll");
        Reset(true);Group();for(int i=5;i<=12;i++) Member(i,.4);Skill(8);Skill(2);
        settings=settings with{AutoAuraMastery=true,AuraMasteryHasToll=true};Want(8,"raid Mastery separately reserved by default");
        settings=settings with{RaidAutoAuraMastery=true};Want(14,"raid explicit auto Mastery");

        Reset();Group(.75);Skill(9);Skill(5);F(10,false);Want(0,"moving without instant follow-up never opens empty Virtue");
        F(10);Want(15,"standing executable Flash supports Virtue without fixed combination");
        F(35,false);Want(0,"unusable follow-up cannot open Virtue");
        Skill(2);Want(15,"available Shock supports Virtue");
        F(92);Want(0,"blocked capped generator is not a valid Virtue follow-up");
        Skill(3);F(91);Want(15,"capped spender is a valid Virtue follow-up");

        // Exercise production Process across time and observed state changes.
        Reset();Group();Skill(4);Skill(7);Skill(2);
        Check(Tick(0)==4,"first group burst uses Toll");
        Skill(4,false);F(20,false);Check(Tick(.1)==0,"actual GCD still gates spells");F(20);
        Check(Tick(1)==8,"Toll request does not immediately chain Wings; direct healing continues");
        Group(.9);Check(Tick(2.1)==0,"recovered group retains Wings after cooldown review");
        Group();Check(Tick(2.2)==13,"persistent heavy damage after review permits Wings");
        Skill(7,false);Skill(8);settings=settings with{AutoAuraMastery=true,AuraMasteryHasToll=true};
        Check(Tick(2.3)==8,"Wings cooldown does not immediately chain Mastery");
        Group(.25);Check(Tick(3)==14,"observed deterioration permits stacking another cooldown");
        Reset();Group();Skill(4);Skill(7);Skill(2);
        Check(Tick(0)==4,"second burst setup");Skill(4,false);frames[55]=Color.Orange;
        Check(Tick(1)==8,"one unknown roster member cannot erase cooldown observation hold");Member(5,1);
        Check(Tick(1.1)==8,"restored roster does not erase cooldown hold");

        Reset();Group(.75);Skill(9);Skill(4);Skill(2);
        Check(Tick(0)==15,"party begins with Virtue");Skill(9,false);
        Check(Tick(1)==8,"party first uses regular healing after Virtue request");
        Check(Tick(2.1)==4,"party can escalate if injury remains after review");
        Reset(true);Group();for(int i=5;i<=12;i++) Member(i,.4);Skill(9);Skill(4);Skill(2);
        Check(Tick(0)==15,"raid begins with executable Virtue setup");Skill(9,false);
        Check(Tick(1)==10,"raid allows normal Toll follow-up without claiming a Beacon aura");

        Reset();Group(.83);Skill(7);Skill(4);Skill(2);
        Check(Tick(0)==8,"ordinary initial group damage uses regular healing");
        Group(.72);Check(Tick(2.1)==13,"sustained worsening chooses Wings even when Toll is ready");
        Skill(7,false);Skill(8);settings=settings with{AutoAuraMastery=true,AuraMasteryHasToll=true};
        Check(Tick(3)==8,"pre-Wings worsening is not fresh evidence to chain Mastery");
        Reset();Group(.83);Skill(7);Skill(2);Check(Tick(0)==8,"stable ordinary damage setup");
        Check(Tick(2.1)==8,"elapsed time alone does not turn ordinary damage into Wings demand");

        Reset();Group();Skill(4);Skill(7);Skill(2);
        Check(Tick(0)==4,"failed cast retry setup");
        Check(Tick(.1)==8,"same unchanged sample does not flood cooldown requests");
        Check(Tick(.6)==4,"still-ready cooldown can retry without assuming cast success");
        Skill(4,false);Check(Tick(2.1)==13,"retry does not extend review forever");
        F(1,false);Check(Tick(2.2)==0,"combat exit stops and clears pressure history");
        F(1);Skill(4);Check(Tick(2.3)==4,"new combat is not blocked by previous requests");

        Reset();F(47,false);F(46);Member(2,.4);
        Check(Tick(0)==102,"pending ordinary selection setup");
        Member(3,.1);Check(Tick(.1)==103,"new critical member supersedes stale selection wait");
        F(46,false);F(48);Hp(8,.1);Skill(2);
        Check(Tick(.2)==0,"critical replacement still needs first acknowledgement");
        Check(Tick(.3)==8,"second acknowledgement permits critical heal");

        foreach(double value in new[]{double.NaN,double.PositiveInfinity,.1,11}) {
            bool rejected=false;try{(settings with{CooldownReviewSeconds=value}).Validate();}catch(InvalidDataException){rejected=true;}
            Check(rejected,"invalid cooldown review rejected");
        }
        var legacy=System.Text.Json.JsonSerializer.Deserialize<HealingThresholds>("{}")!;
        Check(legacy.TollLow==.8&&legacy.TollCritical==.6&&legacy.CooldownReviewSeconds==2&&legacy.AutoWings&&
            !legacy.AutoAuraMastery&&!legacy.AuraMasteryHasToll&&!legacy.RaidAutoAuraMastery,"legacy JSON supplies conservative new defaults");
        var configured=legacy with{CooldownReviewSeconds=3,TollLow=.79,TollCritical=.59,AutoWings=false,
            RaidAutoWings=false,AutoAuraMastery=true,AuraMasteryHasToll=true,RaidAutoAuraMastery=true};
        var restored=System.Text.Json.JsonSerializer.Deserialize<HealingThresholds>(System.Text.Json.JsonSerializer.Serialize(configured));
        Check(configured==restored,"cooldown policy and reserve switches survive configuration round-trip");
        HOLY.Inst.Thresholds=new();HOLY.Inst.Reset();return count;
    }
}
