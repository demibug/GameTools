#nullable enable
using System.Drawing;
using System.Text.Json;

namespace RotationUS.Diagnostics;

internal static class HolyPurposeTests
{
    internal static int Run()
    {
        int count=0;
        void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL Holy Purpose: "+why);count++;}
        var f=new Dictionary<int,Color>();HolyBuffs buff=new();
        void F(int id,bool value=true)=>f[id]=value?Color.White:Color.Black;
        void Hp(int id,double value) {int n=(int)Math.Round(value*255);f[id]=Color.FromArgb(n,n,n);}
        void Member(int slot,double hp)=>f[50+slot]=Color.FromArgb((int)Math.Round(hp*255),255,0);
        void Skill(int n,bool value=true) {F(20+n,value);F(30+n,value);}
        void Reset(bool raid=false) {
            f=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
            foreach(int id in new[]{1,2,3,9,12,19,20,42,43,44,45,95,96,97,98,100}) F(id);
            F(41,raid);Hp(6,1);Hp(7,1);Hp(8,1);
            for(int slot=1;slot<=(raid?40:5);slot++) Member(slot,1);
            buff=new(){DivinePurpose=true,PurposeExpiring=true};
        }
        int Decide()=>HOLY.Decide(f,buff).Key;
        foreach(bool raid in new[]{false,true}) {
            Reset(raid);Check(Decide()==18,"zero power and zero Shock charges permit expiring shield");
            f[91]=f[92]=f[13]=Color.Orange;
            Check(Decide()==18,"confirmed expiry bypasses unknown resources and Shock charges");
            Check(HolyShieldLog.Describe(f,new(),true,buff).StartsWith("全部盾击条件满足"),"diagnostics match expiry resource exception");
            foreach(int id in new[]{1,2,3,9,12,19,20,95,96,97,98,100}) {
                foreach(var color in new[]{Color.Black,Color.Orange}) {
                    f[id]=color;Check(Decide()!=18,"expiry preserves safety gate "+id);
                }
                F(id);
            }
            f[41]=Color.Orange;Check(Decide()!=18,"unknown group mode blocks expiry shield");F(41,raid);
            Member(3,.84);Check(Decide()!=18,"injured ally blocks expiry shield");
            F(43,false);Check(Decide()!=18,"unreachable injured ally still blocks expiry shield");F(43);
            f[53]=Color.Orange;Check(Decide()!=18,"unknown HP blocks expiry shield");Member(3,1);
            Hp(6,.84);Check(Decide()!=18,"injured self blocks despite healthy roster");Hp(6,1);
            foreach(bool? expiry in new bool?[]{false,null}) {
                buff.PurposeExpiring=expiry;Check(Decide()!=18,"presence alone cannot waive shield gates");
            }
            buff.PurposeExpiring=true;
            foreach(bool? presence in new bool?[]{false,null}) {
                buff.DivinePurpose=presence;Check(Decide()!=18,"expiry never overrides missing/unknown presence");
            }
            buff.DivinePurpose=true;buff.PurposeExpiring=false;F(92);F(13);
            Check(Decide()==18,"ordinary five-power two-charge shield unchanged");

            Reset(raid);F(4);F(5);F(12,false);F(47);Hp(8,.7);Member(2,.7);Member(3,.7);Member(4,.7);
            if(raid) for(int slot=4;slot<=9;slot++) Member(slot,.7);
            Skill(2);Skill(3);Skill(4);Skill(5);Skill(9);F(13);F(10,false);
            Check(Decide()==9,"expiry heal precedes Virtue and generators while moving at zero power");
            buff.PurposeExpiring=false;Check(Decide()==15,"unexpired proc preserves Virtue setup");
            buff.PurposeExpiring=true;Skill(3,false);
            Check(Decide()!=9,"unusable spender falls through instead of repeated cast");Skill(3);
            F(9,false);Check(Decide()==0,"expiry does not interrupt cast");F(9);
            F(20,false);Check(Decide()==0,"expiry respects GCD");F(20);
            Hp(6,.5);Member(1,.5);Check(Decide()==2,"injured self receives free self macro");
            Skill(10);Check(Decide()==5,"defensive retains priority");
            Hp(6,.3);F(16);Check(Decide()==22,"healing stone retains priority");
        }
        Reset();F(4);F(5);F(47);Hp(8,.1);Member(2,.1);Member(3,.1);Member(4,.1);Skill(3);Skill(4);
        Check(Decide()==4,"multiple critical allies retain emergency Toll");
        Reset();Member(3,.7);Skill(3);
        Check(Decide()==103,"expiry selects reachable injured party member");
        HOLY.Inst.Reset();HOLY.Inst.Thresholds=new();HOLY.Inst.Buffs=buff;
        int Tick() {var states=new Dictionary<int,bool>();HOLY.Inst.Process(f,new(),states);return states.Keys.SingleOrDefault();}
        Check(Tick()==103,"production requests selection first");
        F(4);F(5);F(12,false);F(48);Hp(8,.7);
        Check(Tick()==0&&Tick()==9,"expiry healing retains two target confirmations");
        buff.DivinePurpose=false;buff.PurposeExpiring=false;Hp(8,1);Member(3,1);
        Check(Tick()==0,"fresh sample cancels consumed proc instead of replaying spend");
        Reset(true);Member(3,.7);Skill(3);Check(Decide()!=103&&Decide()!=18,"raid never auto-selects nor shields through injury");

        var layout=new DiagnosticLayout();
        layout.SpecialPoints["holy_purpose"]=new(){Enabled=true,Comparison="expiring",ReferenceColor=new(255,255,255)};
        Check(HolyBuffs.Read(layout,_=>Color.White).PurposeExpiringConfirmed,"existing reference defaults to expiry without a setting");
        Check(HolyBuffs.Read(layout,_=>Color.Red).DivinePurpose==true&&HolyBuffs.Read(layout,_=>Color.Red).PurposeExpiring==false,"other nonblack color means present but not expiring");
        foreach(var color in new[]{Color.Black,Color.Red,Color.White}) {
            int reads=0;var observed=HolyBuffs.Read(layout,_=>{reads++;return color;});
            Check(reads==1,"presence and expiry use one observation");
            Check(observed.DivinePurpose==(color!=Color.Black)&&observed.PurposeExpiring==(color==Color.White),"three-state purpose pixel "+color.Name);
            Check(HolyBuffs.Decode(layout,HolyBuffs.Fields().Single(x=>x.Id=="105"),color)==observed.PurposeState,"UI matches observed state");
        }
        var copy=JsonSerializer.Deserialize<DiagnosticLayout>(JsonSerializer.Serialize(layout.Copy(),new JsonSerializerOptions{IgnoreReadOnlyProperties=true}))!;
        Check(HolyBuffs.Read(copy,_=>Color.White).PurposeExpiringConfirmed,"copy and JSON preserve default expiry behavior");
        foreach(string oldFlag in new[]{"false","true"}) {
            var oldSetting=JsonSerializer.Deserialize<SpecialPointSetting>("{\"Enabled\":true,\"ReferenceColor\":{\"R\":255,\"G\":255,\"B\":255},\"PurposeExpiryMode\":"+oldFlag+"}")!;
            copy.SpecialPoints["holy_purpose"]=oldSetting;
            Check(HolyBuffs.Read(copy,_=>Color.White).PurposeExpiringConfirmed,"obsolete flag is ignored: "+oldFlag);
        }
        copy.SpecialPoints["holy_purpose"].Enabled=false;
        Check(HolyBuffs.Read(copy,_=>Color.White).PurposeExpiring is null,"disabled source cannot grant expiry");
        copy.SpecialPoints["holy_purpose"].Enabled=true;copy.SpecialPoints["holy_purpose"].ReferenceColor=new(0,0,0);
        Check(HolyBuffs.Read(copy,_=>Color.White).DivinePurpose is null,"invalid black reference fails closed");
        layout.SpecialPoints["holy_purpose"].HandExpiryMode=true;
        Check(HolyBuffs.Read(layout,_=>Color.White).PurposeExpiringConfirmed,"Purpose always uses expiry independently of Hand setting");
        Reset();var log=new HolyHealingDiagnosticLog();
        log.Observe(HolyHealingSample.Empty,f,buff,new(),false);
        Check(log.Observe(HolyHealingSample.Empty,f,buff,new(),false).Count==0,"unchanged expiry log deduplicates");
        buff.PurposeExpiring=false;
        Check(log.Observe(HolyHealingSample.Empty,f,buff,new(),false).Single().Key.EndsWith("purpose"),"expiry transition logged independently");
        buff.PurposeExpiring=true;
        Check(log.Observe(HolyHealingSample.Empty,f,buff,new(),false).Single().Key.EndsWith("purpose"),"expiry recovery logged");
        HOLY.Inst.Reset();HOLY.Inst.Buffs=new();HOLY.Inst.Thresholds=new();
        return count;
    }
}
