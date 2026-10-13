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
        Want(9,"lower critical reachable ally precedes higher-health self emergency");
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
        var confirmedToll=new HolyCooldownContext(Healing:new(new PartyTollReading(true),TollConfirmed:true));
        Reset();Group(.75);Skill(4);Want(0,"ordinary group injury needs consecutive evidence");
        Want(4,"confirmed ordinary group damage enables Toll",confirmedToll);
        Member(4,1);Want(4,"confirmed two below80 with sufficient deficit enable party Toll",confirmedToll);
        Hp(8,.55);Member(2,.55);Member(3,.55);Want(4,"two below60 enable party Toll");
        frames[54]=frames[55]=Color.Orange;Want(0,"unreliable group data cannot authorize Toll");
        Reset();Group(.75);Skill(4);settings=settings with{HealBelow=.7};Want(0,"ordinary Toll uses configured healing line",confirmedToll);
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
        settings=settings with{AuraMasteryHasToll=true};Want(14,"raid automatic Mastery defaults on after talent confirmation");
        settings=settings with{RaidAutoAuraMastery=false};Want(8,"raid switch reserves Mastery for manual use");
        F(41,false);Want(14,"raid manual switch does not disable party automatic Mastery");

        foreach(bool raid in new[]{false,true})
        foreach(int direct in new[]{2,3,5,6}) {
            Reset(raid);Group(.1);Skill(8);Skill(direct);F(91);F(92);
            settings=settings with{AuraMasteryHasToll=true};
            buff.Infusion=direct==5;buff.HandOfDivinity=direct==6;
            Want(14,"multiple critical members after unavailable Toll precede direct-heal starvation and HP cap");
            Skill(4);Want(raid?10:4,"ready emergency Toll remains the first group rescue");
        }
        Reset();Group(.1);Skill(8);Skill(2);Skill(4);settings=settings with{AuraMasteryHasToll=true};
        Check(Tick(0)==4,"emergency Toll opens the chain");Skill(4,false);F(91);F(92);
        Check(Tick(.2)==14,"different emergency cooldown can follow Toll without a shared observation delay");
        Check(Tick(.3)==8,"same Mastery request remains rate limited while direct healing continues");
        Check(Tick(.8)==14,"still-ready Mastery can retry after its own request throttle");
        Skill(8,false);Check(Tick(.9)==8,"Mastery on cooldown returns to direct healing");
        Reset();F(47,false);F(46);Member(2,.4);Check(Tick(0)==102,"selection pending before emergency group damage");
        Group(.1);Skill(8);settings=settings with{AuraMasteryHasToll=true};
        Check(Tick(.1)==14,"self cooldown emergency is not delayed by pending target acknowledgement");
        Check(Tick(.2)==0,"Mastery requested during target acknowledgement records the minimum cooldown spacing");
        Reset(true);Group(.1);Skill(8);Skill(2);settings=settings with{AuraMasteryHasToll=true,RaidAutoAuraMastery=false};
        Want(8,"raid manual override also applies to emergency overflow exception");
        settings=settings with{RaidAutoAuraMastery=true,AuraMasteryHasToll=false};Want(8,"automatic default never invents the confirmed talent");
        settings=settings with{AuraMasteryHasToll=true,AutoAuraMastery=false};Want(8,"advanced global manual override still suppresses emergency Mastery");

        Reset();Group(.75);Skill(9);Skill(5);F(10,false);Want(0,"moving without instant follow-up never opens empty Virtue");
        F(10);Want(15,"standing executable Flash supports Virtue without fixed combination");
        F(35,false);Want(0,"unusable follow-up cannot open Virtue");
        Skill(2);Want(15,"available Shock supports Virtue");
        F(92);Want(15,"capped available heal remains a valid Virtue follow-up");
        Skill(3);F(91);Want(15,"capped spender is a valid Virtue follow-up");
        Reset();Group(.75);Skill(9);Skill(6);buff.Infusion=false;
        Want(0,"ordinary Holy Light above Major threshold is not executable Virtue follow-up");
        Hp(8,.55);Member(2,.55);Want(15,"below Major threshold permits executable hard Light after Virtue");
        Hp(7,.2);Want(0,"low mana invalidates hard Light-only Virtue follow-up");

        // The requested Virtue-first party examples, through real Process as
        // well as the pure decision. Target confirmation remains mandatory.
        Reset();Hp(6,.6);Member(1,.6);Hp(8,.7);Member(2,.7);
        Skill(9);Skill(5);buff.Infusion=true;
        Want(15,"ordinary self60 ally70 injury prepares Virtue before self Flash");
        F(46);F(47,false);Hp(8,.6);
        Want(15,"Virtue can cast on self without switching to an ally");
        frames[5]=Color.Orange;Want(15,"confirmed self recipient does not depend on an external friendly range query");F(5);
        Hp(6,1);Member(1,1);Hp(8,1);Member(3,.75);
        Want(15,"healthy self can receive Virtue when injured allies provide executable follow-up healing");
        Hp(6,.6);Member(1,.6);Member(3,1);Hp(8,.6);
        F(3,false);F(4,false);F(5,false);F(46,false);
        Want(101,"missing treatment target selects injured self for Virtue by ordinary triage");
        Check(Tick(0)==101,"Virtue selection is its own action when a target is needed");
        F(3);F(4);F(5);F(46);Hp(8,.6);
        Check(Tick(.1)==0,"Virtue waits for first self acknowledgement without ordinary self macro preemption");
        Check(Tick(.2)==15,"second self acknowledgement releases Virtue");
        Skill(9,false);
        Check(Tick(.3)==1,"next ready GCD follows Virtue with useful infused self healing without two-second wait");
        Skill(9);Want(15,"eligible Virtue on cooldown recovery still precedes ordinary self healing");
        Skill(5,false);Want(0,"no available follow-up retains Virtue even on a valid self target");

        foreach(bool self in new[]{false,true}) {
            Reset();Skill(9);Skill(5);Skill(3);F(91);buff.Infusion=true;
            if(self) {Hp(6,.15);Member(1,.15);Hp(8,.7);Member(2,.7);}
            else {Hp(6,.7);Member(1,.7);Hp(8,.15);Member(2,.15);}
            Want(self?1:11,"self and ally15 use infused Flash rescue ahead of ordinary three-HP spender and Virtue");
            F(92);Want(self?2:9,"self and ally capped rescue first spends Holy Power");F(92,false);
            buff.DivinePurpose=true;Want(self?2:9,"confirmed free rescue uses the actual spender for self and ally");
            buff.DivinePurpose=false;
            if(self) {Hp(6,.6);Member(1,.6);}
            else {Hp(8,.6);Member(2,.6);}
            Want(15,"recovered emergency returns to group Virtue setup rather than perpetual direct rescue");
        }
        Reset();Group(.4);Skill(7);Skill(2);
        Check(Tick(0)==13,"severe group damage with no Virtue can request Wings");
        Skill(9);Check(Tick(.1)==15,"Wings request never imposes a shared wait on eligible Virtue");
        Skill(9,false);Skill(7,false);Check(Tick(.2)==8,"ordinary direct healing continues immediately after Virtue");
        var limiter=new HolyPressure();Reset();Group(.75);
        limiter.Observe(frames,settings,0);limiter.Requested(15,frames,settings,0);
        var limited=limiter.Observe(frames,settings,.1);
        Check(!limited.Allows(15)&&limited.Allows(4)&&limited.Allows(13)&&limited.Allows(14),"Virtue throttles only its own repeat request");
        limiter.Requested(13,frames,settings,.1);limited=limiter.Observe(frames,settings,.2);
        Check(!limited.Allows(15)&&!limited.Allows(13)&&limited.Allows(4),"alternating requests cannot bypass independent per-key throttles");
        limited=limiter.Observe(frames,settings,.51);
        Check(limited.Allows(15)&&!limited.Allows(13),"each request throttle expires independently");

        // Reproduce full Holy Power while the spender cannot actually be used.
        // The cap must retain healing, without granting damage overflow or
        // casting unknown procs while moving.
        foreach(bool raid in new[]{false,true})
        foreach(bool self in new[]{false,true})
        foreach(int spell in new[]{2,5,6}) {
            Reset(raid);F(91);F(92);Skill(3,false);Skill(spell);F(10,false);
            buff.Infusion=spell==5;buff.HandOfDivinity=spell==6;
            double hp=spell==6?.55:.7;Hp(8,hp);Member(2,hp);
            if(self) {Hp(6,hp);Member(1,hp);F(46);F(47,false);}
            int expected=spell==2?8:spell==5?(self?1:11):(self?3:12);
            Want(expected,"cap never blocks confirmed instant healing with unavailable spender");
            Check(Tick(0)==expected,"actual execution retains capped instant healing with unavailable spender");
            Skill(3);Want(self?2:9,"available capped spender still precedes generators");
        }
        Reset();F(91);F(92);Skill(5);buff.Infusion=null;buff.HandOfDivinity=null;
        Want(11,"capped injured target retains stationary Flash fallback");
        F(10,false);Want(0,"cap never assumes unknown procs are instant while moving");
        F(10);Skill(2);Want(8,"standing ready Shock precedes hard-cast fallback at cap");
        Hp(8,1);Member(2,1);F(4,false);F(12);frames[95]=Color.Orange;
        Want(0,"healthy party cannot spam capped damage when range unknown");
        Check(HOLY.Decide(frames,buff).Reason.Contains("点95"),"capped damage wait identifies missing melee range");

        {
            var layout=new DiagnosticLayout();frames[93]=frames[94]=Color.Orange;
            buff=new();
            string hint=HolyBuffs.InstantSourceHint(layout,frames,buff);
            Check(hint.Contains("灌注外部点未配置")&&hint.Contains("神性之手外部点未配置"),"unconfigured instant sources are visible");
            layout.SpecialPoints["holy_i1"]=new(){Enabled=true,ReferenceColor=new(247,251,222)};
            Check(HolyBuffs.InstantSourceHint(layout,frames,buff).Contains("颜色不匹配"),"live log color mismatch is explained");
            buff.Infusion=false;frames[94]=Color.Black;
            Check(HolyBuffs.InstantSourceHint(layout,frames,buff)=="","confirmed native/external absence is not misreported as a configuration failure");
        }

        // Exercise production Process across time and observed state changes.
        Reset();Group();Skill(4);Skill(7);Skill(2);
        Check(Tick(0)==4,"first group burst uses Toll");
        Skill(4,false);F(20,false);Check(Tick(.1)==0,"actual GCD still gates spells");F(20);
        Check(Tick(1)==13,"persistent severe bloodline after unavailable Toll permits Wings without fixed wait");
        Group(.9);Check(Tick(2.1)==0,"recovered group retains Wings");
        Group();Check(Tick(2.2)==13,"renewed severe bloodline permits a still-ready Wings request");
        Skill(7,false);Skill(8);settings=settings with{AutoAuraMastery=true,AuraMasteryHasToll=true};
        Check(Tick(2.3)==14,"continued severe damage with Wings unavailable can append Mastery");
        Group(.25);Check(Tick(3)==14,"still-ready Mastery can retry during severe damage");
        Reset();Group();Skill(4);Skill(7);Skill(2);
        Check(Tick(0)==4,"second burst setup");Skill(4,false);frames[55]=Color.Orange;
        Check(Tick(1)==13,"sufficient readable group coverage retains severe damage response");Member(5,1);
        Check(Tick(1.1)==8,"roster recovery cannot erase the same-key Wings throttle");

        Reset();Group(.75);Skill(9);Skill(4);Skill(2);
        Check(Tick(0)==15,"party begins with Virtue");Skill(9,false);
        Check(Tick(.1)==4,"eligible Toll can immediately follow party Virtue once GCD is ready");
        Skill(4,false);Check(Tick(.2)==8,"direct treatment immediately follows Virtue and Toll requests");
        Reset(true);Group();for(int i=5;i<=12;i++) Member(i,.4);Skill(9);Skill(4);Skill(2);
        Check(Tick(0)==15,"raid begins with executable Virtue setup");Skill(9,false);
        Check(Tick(1)==10,"raid allows normal Toll follow-up without claiming a Beacon aura");

        Reset();Group(.83);Skill(7);Skill(4);Skill(2);
        Check(Tick(0)==8,"ordinary initial group damage uses regular healing");
        Group(.72);Check(Tick(2.1)==13,"sustained worsening chooses Wings even when Toll is ready");
        Skill(7,false);Skill(8);settings=settings with{AutoAuraMastery=true,AuraMasteryHasToll=true};
        Check(Tick(3)==4,"ordinary residual injury uses eligible Toll without inheriting a Wings waiting period");
        Reset();Group(.83);Skill(7);Skill(2);Check(Tick(0)==8,"stable ordinary damage setup");
        Check(Tick(2.1)==8,"elapsed time alone does not turn ordinary damage into Wings demand");

        Reset();Group();Skill(4);Skill(7);Skill(2);
        Check(Tick(0)==4,"failed cast retry setup");
        Check(Tick(.1)==13,"same-key Toll throttle permits another eligible cooldown during severe damage");
        Check(Tick(.6)==4,"still-ready cooldown can retry without assuming cast success");
        Skill(4,false);Check(Tick(2.1)==13,"Toll retries do not block ready Wings during severe damage");
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
            legacy.AutoAuraMastery&&!legacy.AuraMasteryHasToll&&legacy.RaidAutoAuraMastery,"missing settings default automatic on while talent still needs confirmation");
        var configured=legacy with{CooldownReviewSeconds=3,TollLow=.79,TollCritical=.59,AutoWings=false,
            RaidAutoWings=false,AutoAuraMastery=true,AuraMasteryHasToll=true,RaidAutoAuraMastery=true};
        var restored=System.Text.Json.JsonSerializer.Deserialize<HealingThresholds>(System.Text.Json.JsonSerializer.Serialize(configured));
        Check(configured==restored,"cooldown policy and reserve switches survive configuration round-trip");
        HOLY.Inst.Thresholds=new();HOLY.Inst.Reset();return count;
    }
}
