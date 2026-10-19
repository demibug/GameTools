#nullable enable
using System.Drawing;
using System.Text.Json;

namespace RotationUS.Diagnostics;

internal static class RaidHealingTests
{
    internal static int Run()
    {
        int count=0;
        void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL raid healing: "+why);count++;}
        var f=new Dictionary<int,Color>();var b=new Dictionary<int,Color>();
        var t=new HealingThresholds(AuraMasteryHasToll:true);
        var buffs=new HolyBuffs();var window=new RaidHealingWindow();var control=new RaidHealingController();
        int seq=0,epoch=1;double now=0;
        void F(int id,bool v=true)=>f[id]=v?Color.White:Color.Black;
        void Hp(int id,double hp) {int n=(int)Math.Round(hp*255);f[id]=Color.FromArgb(n,n,n);}
        void Member(int slot,double hp)=>f[50+slot]=Color.FromArgb((int)Math.Round(hp*255),255,0);
        void Skill(int spell,bool v=true) {F(20+spell,v);F(30+spell,v);}
        void Target(int slot) {
            for(int i=1;i<=40;i++) b[i]=Color.FromArgb(b[i].R,i==slot?255:0,b[i].B);
            F(3,slot>0);F(4,slot>0);F(5,slot>0);F(46,slot==40);
            Hp(8,slot>0?TeamHealthReading.Read(f[50+slot]).Percent??1:1);
        }
        void Next(double step=.1) {
            seq++;now+=step;b[43]=b[45]=Color.FromArgb(seq&255,(seq>>8)&255,0);
        }
        RaidRosterSnapshot Roster()=>RaidRosterSnapshot.Read(b)!;
        void Reset(int target=1) {
            f=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
            foreach(int id in new[]{1,2,3,4,5,9,10,19,20,41,96,99,100}) F(id);
            Hp(6,1);Hp(7,1);Hp(8,.75);
            for(int i=1;i<=40;i++) Member(i,1);
            Member(1,.75);seq=1;epoch=1;now=0;
            b=RaidTestFixture.Bars(f,target,40,epoch,seq);
            Target(target);Skill(2);f[93]=f[94]=Color.Orange;
            buffs=new(){Infusion=false,HandOfDivinity=false,DivinePurpose=false};
            window.Reset();control.Reset();HOLY.Inst.Reset();HOLY.Inst.Thresholds=t;HOLY.Inst.Buffs=buffs;
        }
        void Group(int n,double hp) {for(int i=1;i<=n;i++) Member(i,hp);Target(1);}
        RaidHealingSample Observe()=>window.Observe(f,Roster(),t,now);
        HolyDecision Decide(HolyCooldownContext? context=null,RaidHealingSample? sample=null)=>
            HOLY.Decide(f,buffs,t,context,raidRoster:Roster(),raidHealing:sample);
        int Tick()=>control.Process(f,b,buffs,t,new(),now).Key;
        int ProductionTick() {
            HOLY.Inst.Buffs=buffs;
            HOLY.Inst.ObserveHealing(f,t,10+now,raidBars:b);
            var actions=new Dictionary<int,bool>();HOLY.Inst.Process(f,b,actions);
            Check(actions.Count<=1,"production emits at most one action per sample");
            return actions.Keys.SingleOrDefault();
        }

        byte[] expected={0x70,0x71,0x72,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7A,0x7B,
            0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x30,0xBD,0xBB,0xDB,0xDD,0xDC,0xBA,0xDE,0xBE};
        for(int slot=1;slot<=30;slot++) {
            var keys=RaidTargetKeys.Sequence(slot);byte modifier=slot<=12?(byte)0x10:(byte)0x11;
            Check(keys.SequenceEqual(new[]{(modifier,false),(expected[slot-1],false),(expected[slot-1],true),(modifier,true)}),"ordered balanced keys for raid"+slot);
            Reset(0);Member(1,1);Member(slot,.5);
            Check(Decide().Key==200+slot,"selection mapping for raid"+slot);
            Check(Tick()==200+slot,"controller sends selection alone for raid"+slot);
            Target(slot);Next();Check(Tick()==0,"first confirmation for raid"+slot);
            Check(Tick()==0,"same packet cannot confirm twice for raid"+slot);
            Next();Check(Tick()==8,"second new confirmation permits healing for raid"+slot);
        }
        // Exercise the real ObserveHealing -> Process entry points continuously,
        // including retargeting inside the inferred Virtue window, for every key.
        for(int slot=1;slot<=30;slot++) {
            Reset();Group(4,.75);Member(slot,.65);Target(0);Skill(9);Skill(6);buffs.HandOfDivinity=true;
            Check(ProductionTick()==200+slot,"chain selects raid"+slot+" from no target");
            Target(slot);Next();Check(ProductionTick()==0,"chain first acknowledgement holds");
            Check(ProductionTick()==0,"chain repeated capture cannot acknowledge twice");
            Next();Check(ProductionTick()==15,"chain second acknowledgement opens with Virtue");
            Check(!HOLY.Inst.Raid.Sample.VirtueActive,"chain key request alone does not prove Virtue cast");
            F(29,false);F(20,false);Skill(2,false);F(10,false);Next();
            Check(ProductionTick()==0&&HOLY.Inst.Raid.Sample.VirtueActive,"chain observes Virtue cooldown during GCD without casting");
            F(20);Next();Check(ProductionTick()==12,"chain moving Hand follows Virtue above60 percent");
            int other=slot%30+1;Member(slot,.8);Member(other,.70);Target(slot);Next();
            Check(ProductionTick()==200+other,"chain selects new lower-health recipient inside Virtue window");
            Target(other);Next();Check(ProductionTick()==0,"chain new recipient requires first acknowledgement");
            Next();Check(ProductionTick()==12&&HOLY.Inst.Raid.Sample.VirtueActive,"chain new recipient receives Hand after second acknowledgement");
            Skill(5);buffs.Infusion=true;Next();Check(ProductionTick()==11,"chain infusion Flash precedes Hand");
            Skill(2);F(13);Next();Check(ProductionTick()==8,"chain two-charge Shock precedes infusion");
            Skill(3);buffs.DivinePurpose=true;Next();Check(ProductionTick()==9,"chain free Glory precedes Shock");
            buffs.DivinePurpose=false;F(92);Next();Check(ProductionTick()==9,"chain capped Glory precedes Shock");
            F(92,false);F(13,false);Skill(2,false);buffs.Infusion=false;
            for(int i=0;i<20;i++) {Next(.5);HOLY.Inst.ObserveHealing(f,t,10+now,raidBars:b);}
            Check(ProductionTick()==0&&!HOLY.Inst.Raid.Sample.VirtueActive,"chain expired window stops moving Hand above60 percent");
            F(10);Next();Check(ProductionTick()==11,"chain standing Flash remains available after window expiry");
        }
        foreach(bool virtueReady in new[]{false,true}) {
            Reset();Group(virtueReady?3:4,.75);Target(0);Skill(9,virtueReady);
            Check(ProductionTick()==201,"no eligible Virtue still automatically selects");
            Target(1);Next();Check(ProductionTick()==0,"direct heal retains target acknowledgement");
            Next();Check(ProductionTick()==8,"three injured or unavailable Virtue proceeds directly to Shock");
        }
        Reset(1);Member(2,.75);Check(Decide().Key==8,"tie retains current target");
        Member(2,.1);Check(Decide().Key==202,"critical ally preempts ordinary current target");
        b[2]=Color.FromArgb(0,0,0);Check(Decide().Key==8,"out of range excluded");
        b[2]=Color.FromArgb(128,0,0);Check(Decide().Key==8,"unknown range excluded");
        b[2]=Color.FromArgb(255,0,0);
        foreach(var hp in new[]{Color.Black,Color.Blue,Color.Orange,Color.FromArgb(0,255,0)}) {
            f[52]=hp;Check(Decide().Key==8,"empty offline unknown and dead excluded "+hp);
        }
        Reset(0);Member(1,1);Member(31,.1);Check(!RaidTargetKeys.IsSelection(Decide().Key),"no automatic raid31 binding");
        Member(40,.1);Hp(6,.1);Skill(3);F(91);Check(Decide().Key==2,"self outside first30 retains self emergency macro");
        Reset();Group(3,.75);Member(40,.75);Hp(6,.75);Target(40);b[40]=Color.FromArgb(128,255,255);Skill(9);
        Check(Decide().Key==15,"confirmed self identity supplies own range and counts as fourth member");
        Skill(9,false);Check(Decide().Key==8,"confirmed self identity permits self Shock despite unknown range API");
        Reset();Member(1,1);foreach(int slot in new[]{31,32,33,40}) Member(slot,.75);Hp(6,.75);Target(1);Skill(9);
        Check(Decide().Key==0,"unselectable self Shock alone cannot promise a Virtue follow-up");
        Skill(5);Check(Decide().Key==15,"available self Flash provides executable Virtue follow-up outside first30");
        Reset();b[44]=Color.Red;Check(RaidRosterSnapshot.Read(b) is null,"torn epoch rejected");
        Reset();b[45]=Color.Red;Check(RaidRosterSnapshot.Read(b) is null,"torn revision rejected");
        Reset();b[46]=Color.Black;Check(RaidRosterSnapshot.Read(b) is null,"old collector rejected");
        Reset();b[2]=Color.White;Check(RaidRosterSnapshot.Read(b) is null,"contradictory target/self identities rejected");
        Reset();b[49]=Color.Black;Check(Decide().Key==0,"unprepared bindings block target healing");
        Check(HOLY.Decide(f,buffs,t).Key==0,"missing transport never trusts manual target");
        Hp(6,.1);F(16);Check(HOLY.Decide(f,buffs,t).Key==22,"missing packet does not block self item rescue");
        Reset(0);Member(1,1);F(3);F(4,false);F(12);F(13);F(92);F(95);F(97);F(98);
        Check(HOLY.Decide(f,buffs,t).Key==0,"missing raid transport does not authorize damage through old collector");

        foreach(double hp in new[]{.8,.55,.1})
        foreach(int injured in new[]{1,2,3,4}) {
            Reset();Group(injured,hp);foreach(int spell in new[]{4,7,8,9}) Skill(spell);
            Check(Decide(new(Sustained:true)).Key==(injured==4?15:8),$"Virtue four-person minimum n={injured} hp={hp}");
            Skill(9,false);var sample=new RaidHealingSample(RaidInjury.Read(f,Roster(),t),TollConfirmed:true);
            Check(Decide(sample:sample).Key==(injured==4?10:8),$"Toll four-person minimum n={injured} hp={hp}");
            Skill(4,false);
            Check(Decide().Key==(injured<4?8:hp<.2?14:hp<.6?13:8),$"Wings/Mastery minimum n={injured} hp={hp}");
        }
        foreach(double hp in new[]{.59,.60,.65,.70,.75})
        foreach(bool worse in new[]{false,true}) {
            Reset();Group(4,hp);Skill(7);
            double observedHp=TeamHealthReading.Read(f[51]).Percent!.Value;
            Check(Decide(new(Sustained:worse)).Key==(observedHp<.60||(observedHp<.70&&worse)?13:8),$"raid Wings mirrors party bloodline hp={hp} worse={worse}");
        }
        Reset();Group(4,.75);Skill(4);Check(Decide(sample:Observe()).Key==8,"ordinary Toll first sample holds");
        Check(Decide(sample:Observe()).Key==8,"same revision cannot confirm Toll");Next();Check(Decide(sample:Observe()).Key==10,"second fresh sample confirms ordinary Toll");
        Member(4,1);Next();Check(Decide(sample:Observe()).Key==8,"recovery to three clears group demand");
        Reset();Group(4,.55);Skill(4);b[4]=Color.Black;Check(Decide().Key==8,"out-of-range fourth does not count");
        b[4]=Color.FromArgb(128,0,0);Check(Decide().Key==8,"unknown fourth does not count");
        Reset();Group(4,.95);var high=t with{HealBelow=.99};
        Check(!RaidInjury.Read(f,Roster(),high).Ordinary,"four tiny deficits still need total40 percentage points");
        Group(4,.89);Check(RaidInjury.Read(f,Roster(),high).Ordinary,"four sufficient deficits cross total40 gate");
        Reset();Group(4,.55);Skill(4);F(91);Skill(3);Check(Decide().Key==9,"nonemergency Toll first spends three HP");
        F(91,false);f[92]=Color.Orange;Check(Decide().Key==8,"unknown cap does not authorize nonemergency Toll");
        Reset();Group(4,.1);Skill(4);F(91);F(92);Check(Decide().Key==10,"four critical permit emergency Toll at cap");
        Skill(9);Check(Decide().Key==15,"critical Virtue still precedes emergency Toll");
        Skill(2,false);F(10,false);Check(Decide().Key==10,"no executable follow-up skips Virtue immediately");
        Skill(4,false);Skill(8);Check(Decide().Key==14,"unavailable Toll permits emergency self Mastery");
        var reserved=t with{RaidAutoAuraMastery=false};Check(HOLY.Decide(f,buffs,reserved,raidRoster:Roster()).Key==0,"raid Mastery reserve retained");
        Reset();Group(4,.75);Skill(9);Skill(3);buffs.DivinePurpose=true;buffs.PurposeExpiring=true;
        Check(Decide().Key==9,"noncritical expiry precedes ordinary Virtue");Group(4,.1);Check(Decide().Key==15,"critical group Virtue precedes expiry");

        Reset();Group(4,.75);Skill(9);Check(Decide().Key==15,"Virtue opener");
        Check(!Observe().VirtueActive,"sending/selecting Virtue does not start window");
        F(29,false);Check(!Observe().VirtueActive,"same revision cannot invent cast edge");
        Next();var active=Observe();Check(active.VirtueActive&&active.VirtueRemaining==9,"new ready-to-cooldown opens nine seconds");
        Skill(2,false);Skill(6);F(10,false);buffs.HandOfDivinity=true;
        Check(Decide(sample:active).Key==12,"Virtue window releases Hand at75 while moving");
        Check(Decide().Key==0,"no raid window preserves ordinary Hand outside60");
        Skill(2);F(13);Check(Decide(sample:active).Key==8,"two-charge Shock precedes window Hand");
        F(13,false);Skill(5);buffs.Infusion=true;Check(Decide(sample:active).Key==11,"infusion precedes window Hand");
        Skill(5,false);buffs.Infusion=false;
        Next(.5);Check(Observe().VirtueRemaining<9,"cooldown frames never extend window");
        for(int i=0;i<18;i++) {Next(.5);Observe();}
        Check(!window.Current.VirtueActive,"window expires without cooldown recovery");
        Reset();F(29,false);Check(!Observe().VirtueActive,"first observation already cooling cannot infer cast");
        F(29);Next();Observe();F(29,false);Next();Check(Observe().VirtueActive,"manual cast also opens window");
        b[42]=b[44]=Color.FromArgb(2,0,0);Next();Check(!Observe().VirtueActive,"roster epoch clears window");
        F(29);Next();Observe();F(29,false);Next();Observe();Next(2);Check(!Observe().VirtueActive,"sampling gap clears window");

        Reset(0);Member(30,.1);Check(Tick()==230,"pending critical selection");
        Next();Member(30,0);Check(Tick()==201,"dead pending member replaced by next valid injury");
        Next();Target(1);Check(Tick()==0,"replacement first confirmation");Next();Check(Tick()==8,"replacement second confirmation");
        Reset(0);Member(1,1);Member(30,.5);Check(Tick()==230,"timeout selection begins");
        for(int i=0;i<4;i++) {Next(.5);Check(Tick()==0,"wait or timeout cannot cast old target");}
        Next();Check(Tick()==230,"timeout permits fresh selection next sample");
        Reset(0);Member(30,.1);Tick();Target(30);Next();Check(Tick()==0,"first confirmation before roster change");
        b[42]=b[44]=Color.FromArgb(2,0,0);Member(30,1);Member(29,.05);Target(30);Next();Check(Tick()==229,"new epoch cannot reuse old acknowledgement");
        Reset(0);Member(30,.1);Tick();Hp(6,.05);Skill(3);F(91);Next();Check(Tick()==2,"self emergency bypasses pending acknowledgement");
        Reset();F(29);control.Observe(f,b,t,now);F(29,false);Next();control.Observe(f,b,t,now);Check(control.Sample.VirtueActive,"controller observes cast");
        now+=2;control.Observe(f,b,t,now);Check(control.Roster is null&&!control.Sample.VirtueActive,"stalled packet expires even if overall heartbeat continues");
        now+=.1;control.Observe(f,b,t,now);Check(control.Roster is null,"same stale packet cannot reactivate");
        Next();control.Observe(f,b,t,now);Check(control.Roster is not null&&!control.Sample.VirtueActive,"fresh packet recovers without old window");

        // UI observation time and execution use different clock origins. Process
        // must consume the prepared timestamp, not reset the just-observed edge.
        Reset();Group(4,.75);Skill(9);HOLY.Inst.ObserveHealing(f,t,10,raidBars:b);
        var actions=new Dictionary<int,bool>();HOLY.Inst.Process(f,b,actions);Check(actions.ContainsKey(15),"production Virtue starts from prepared UI sample");
        F(29,false);Next();Skill(2,false);Skill(6);F(10,false);buffs.HandOfDivinity=true;HOLY.Inst.Buffs=buffs;
        HOLY.Inst.ObserveHealing(f,t,10.1,raidBars:b);actions.Clear();HOLY.Inst.Process(f,b,actions);
        Check(actions.ContainsKey(12)&&HOLY.Inst.Raid.Sample.VirtueActive,"production keeps UI-observed window for Hand follow-up");
        F(41,false);HOLY.Inst.ObserveHealing(f,t,10.2,raidBars:b);Check(HOLY.Inst.Raid.Roster is null&&!HOLY.Inst.Raid.Sample.VirtueActive,"party transition clears raid state");

        string? packetPath=Environment.GetEnvironmentVariable("HIJI_RAID_PACKET");
        if(!string.IsNullOrEmpty(packetPath)) {
            var raw=JsonSerializer.Deserialize<Dictionary<int,int[]>>(File.ReadAllText(packetPath))!;
            var packet=raw.ToDictionary(p=>p.Key,p=>Color.FromArgb(p.Value[0],p.Value[1],p.Value[2]));
            var decoded=RaidRosterSnapshot.Read(packet);
            Check(decoded is {BindingsReady:true,TargetSlot:30,SelfSlot:4}&&decoded.Reachable(1)&&!decoded.Reachable(2),"actual Lua packet interoperates with C# decoder");
        }
        HOLY.Inst.Reset();HOLY.Inst.Thresholds=new();HOLY.Inst.Buffs=new();
        return count;
    }
}
