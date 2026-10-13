#nullable enable
using System.Drawing;
using System.Text.Json;

namespace RotationUS.Diagnostics;

internal static class HolyHealingWindowTests
{
    internal static int Run()
    {
        int count=0;
        void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL Holy healing window: "+why);count++;}
        var f=new Dictionary<int,Color>();var t=new HealingThresholds();var window=new HolyHealingWindow();
        void F(int id,bool v=true)=>f[id]=v?Color.White:Color.Black;
        void Member(int slot,double hp)=>f[50+slot]=Color.FromArgb((int)Math.Round(hp*255),255,0);
        void Target(double hp) {int n=(int)Math.Round(hp*255);f[8]=Color.FromArgb(n,n,n);Member(2,hp);}
        void Skill(int n,bool v=true) {F(20+n,v);F(30+n,v);}
        void Reset(double hp=.78) {
            f=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
            foreach(int id in new[]{1,2,3,4,5,6,7,9,10,19,20,42,43,44,45,47,96,100}) F(id);
            for(int slot=1;slot<=5;slot++) Member(slot,1);
            Target(hp);Member(3,hp);Skill(4);Skill(2);
            f[93]=f[94]=Color.Orange;window.Reset();HOLY.Inst.Reset();HOLY.Inst.Thresholds=t;HOLY.Inst.Buffs=new();
        }
        HolyHealingSample Observe(double now,bool live=true)=>window.Observe(f,t,now,live);
        int Decide(HolyHealingSample s,HolyBuffs? buff=null)=>HOLY.Decide(f,buff??new(),t,new(Healing:s)).Key;
        int Tick(double now) {var actions=new Dictionary<int,bool>();HOLY.Inst.Process(f,new(),actions,now);return actions.Keys.SingleOrDefault();}

        Reset();Check(Decide(Observe(0))==8,"first ordinary injury uses normal Shock");
        Check(Decide(Observe(0))==8,"evaluating the same capture twice cannot confirm Toll");
        Check(Decide(Observe(.1))==4,"78/78 enables Toll after second capture");
        Reset(.84);Observe(0);Check(Decide(Observe(.1))==8,"84/84 deficit too small");
        Reset(.8);Observe(0);Check(Decide(Observe(.1))==4,"exact 40 percentage-point deficit qualifies");
        Reset(.78);Member(3,1);Observe(0);Check(Decide(Observe(.1))==8,"one ordinary injury never qualifies");
        Reset(.55);Check(Decide(Observe(0))==4,"two below60 do not wait for confirmation");
        Reset(.1);F(91);F(92);Check(Decide(Observe(0))==4,"two critical bypass overflow guard");
        Reset();Observe(0);Target(1);Member(3,1);Observe(.1);Target(.78);Member(3,.78);
        Check(Decide(Observe(.2))==8,"recovery cancels pending ordinary burst");
        Check(Decide(Observe(.3))==4,"new ordinary injury reconfirms");
        Reset();Observe(0);Check(Decide(Observe(2))==8,"sampling gap clears confirmation");
        Check(Decide(Observe(1))==8,"backward clock clears confirmation");
        Reset();Observe(0);F(43,false);Check(!Observe(.1).TollConfirmed,"out-of-range injuries do not accumulate deficit");
        F(43);Check(Decide(Observe(.2))==8,"return to range starts fresh confirmation");
        foreach(int invalid in new[]{43,54}) {
            Reset();Observe(0);f[invalid]=Color.Orange;
            Check(!Observe(.1).Toll.Valid,"unknown range or member cancels pending evidence");
        }
        Reset();Observe(0);F(91);Skill(3);
        var confirmed=Observe(.1);Check(Decide(confirmed)==9,"actual3HP spends before Toll");
        Check(Decide(confirmed,new(){DivinePurpose=true})==9,"free spender does not prove reduced resources");
        Skill(3,false);Check(Decide(confirmed)==8,"unusable spender cannot grant overflowing Toll");
        F(91,false);f[92]=Color.Orange;Check(Decide(confirmed)==8,"unknown5HP flag blocks nonemergency Toll");
        F(92,false);Check(Decide(confirmed)==4,"resampled known low power releases Toll");
        Reset();Check(Tick(0)==8&&Tick(.1)==4,"production Process confirms ordinary Toll");
        Reset();HOLY.Inst.ObserveHealing(f,t,0);var output=new Dictionary<int,bool>();
        HOLY.Inst.Process(f,new(),output);Check(output.Keys.Single()==8,"capture preparation is not observed twice by execution");
        HOLY.Inst.ObserveHealing(f,t,.1);output.Clear();HOLY.Inst.Process(f,new(),output);
        Check(output.Keys.Single()==4,"execution consumes the same confirmed sample as diagnostics");
        HOLY.Inst.Reset();Check(!HOLY.Inst.HealingSample.TollConfirmed,"roster/session Reset clears evidence");

        Reset();F(29,false);Check(!Observe(0).VirtueActive,"starting mid cooldown does not invent window");
        F(29);Observe(.1);F(29,false);F(20,false);
        var active=Observe(.2);Check(active.VirtueActive&&active.VirtueRemaining==9,"ready to cooldown edge starts exactly9s even during GCD");
        for(int i=1;i<=8;i++) Check(Math.Abs(Observe(.2+i).VirtueRemaining-(9-i))<1e-9,"cooldown samples never extend timer");
        var ended=Observe(9.2);Check(!ended.VirtueActive&&ended.VirtueState.Contains("结束"),"9s window expires");
        Check(Observe(9.3).VirtueState==ended.VirtueState,"expired state remains stable for deduplicated logs");
        F(29);Observe(9.4);F(29,false);Check(Observe(9.5).VirtueActive,"subsequent observed cast starts new window");
        f[29]=Color.Orange;Check(!Observe(9.6).VirtueActive,"unknown cooldown clears window");
        F(29,false);Check(!Observe(9.7).VirtueActive,"unknown to cooldown is not a cast edge");
        foreach(string change in new[]{"gap","backward","raid","combat","live","member","reset"}) {
            Reset();F(29);Observe(1);F(29,false);Check(Observe(1.1).VirtueActive,"reset-case active setup");
            double now=1.2;bool live=true;
            switch(change) {
                case "gap":now=3;break;case "backward":now=.5;break;case "raid":F(41);break;
                case "combat":F(1,false);break;case "live":live=false;break;
                case "member":f[55]=Color.Black;break;case "reset":window.Reset();break;
            }
            Check(!Observe(now,live).VirtueActive,"window clears on "+change);
        }
        Reset();F(29);Check(!Observe(0).VirtueActive,"ready baseline alone is not cast");
        Observe(.1);Check(!window.Current.VirtueActive,"failed or repeated key without cooldown remains inactive");

        Reset();Skill(4,false);Skill(2,false);Skill(6);F(10,false);
        var hand=new HolyBuffs {HandOfDivinity=true,Infusion=false};
        Check(Decide(HolyHealingSample.Empty,hand)==0,"ordinary injury preserves Hand outside window");
        F(29);Observe(0);F(29,false);active=Observe(.1);
        Check(Decide(active,hand)==12,"inferred party Virtue allows useful instant Holy Light");
        hand.HandExpiring=true;Check(Decide(HolyHealingSample.Empty,hand)==12,"confirmed expiry allows ordinary injured target");
        F(94,false);Check(Decide(active,hand)==0,"native absence overrides external expiry and window");
        f[94]=Color.Orange;Target(1);Member(3,1);Check(Decide(active,hand)==0,"expiry/window never authorizes healing full HP");
        Reset();F(41);Skill(4,false);Skill(2,false);Skill(6);F(10,false);
        Check(Decide(active,new(){HandOfDivinity=true,Infusion=false})==0,"party window cannot change raid Hand rule");
        Skill(4);Skill(6,false);Skill(2);Check(Decide(confirmed)==8,"party ordinary Toll confirmation cannot relax raid thresholds");

        var layout=new DiagnosticLayout();layout.SpecialPoints["holy_hand"]=new(){Enabled=true,ReferenceColor=new(255,255,255),Comparison="expiring"};
        Check(HolyBuffs.Read(layout,_=>Color.White).HandExpiring is null,"legacy expiring comparison must not opt in");
        Check(HolyBuffs.Read(layout,_=>Color.Red).HandOfDivinity is null,"legacy mismatch stays unknown");
        layout.SpecialPoints["holy_hand"].HandExpiryMode=true;
        foreach(var color in new[]{Color.Black,Color.Red,Color.White}) {
            var read=HolyBuffs.Read(layout,_=>color);
            Check(read.HandOfDivinity==(color!=Color.Black)&&read.HandExpiring==(color==Color.White),"new three-state color meaning: "+color.Name);
        }
        var copy=JsonSerializer.Deserialize<DiagnosticLayout>(JsonSerializer.Serialize(layout.Copy(),new JsonSerializerOptions{IgnoreReadOnlyProperties=true}))!;
        Check(copy.SpecialPoints["holy_hand"].HandExpiryMode,"opt-in survives copy and profile serialization");
        copy.SpecialPoints["holy_hand"].Enabled=false;
        Check(HolyBuffs.Read(copy,_=>Color.White).HandExpiring is null,"disabled color cannot grant expiry");

        Reset();var log=new HolyHealingDiagnosticLog();var buff=new HolyBuffs();F(29);
        Check(log.Observe(Observe(0),f,buff,t,false).Count==3,"first diagnostics include Toll Virtue and Hand");
        var changes=log.Observe(Observe(.1),f,buff,t,false);Check(changes.Count==1&&changes[0].Key.EndsWith("toll"),"only ordinary confirmation changes");
        F(29,false);active=Observe(.2);changes=log.Observe(active,f,buff,t,false);
        Check(changes.Count==1&&changes[0].Key.EndsWith("virtue"),"one start event");
        Target(.77);Member(3,.77);
        Check(log.Observe(Observe(.3),f,buff,t,false).Count==0,"HP evidence and countdown alone never spam logs");
        F(91);changes=log.Observe(Observe(.4),f,buff,t,false);
        Check(changes.Count==1&&changes[0].Message.Contains("消耗实际圣能"),"resource blocker logged");
        F(91,false);Check(log.Observe(Observe(.5),f,buff,t,false).Count==1,"blocker recovery logged");
        window.Reset();Check(log.Observe(window.Current,new(),new(),t,false).Count>=2,"cleanup removes eligibility and window in logs");
        Check(log.Observe(window.Current,new(),new(),t,false).Count==0,"repeated cleanup remains silent");
        HOLY.Inst.Reset();HOLY.Inst.Buffs=new();HOLY.Inst.Thresholds=new();return count;
    }
}
