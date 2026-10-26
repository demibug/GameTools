#nullable enable
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;
internal static class HolyTests
{
    internal static int Run()
    {
        int count=0;
        void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL Holy: "+why);count++;}
        var f=new Dictionary<int,Color>();var b=new Dictionary<int,Color>();HolyBuffs buff=new();
        void F(int id,bool value=true)=>f[id]=value?Color.White:Color.Black;
        void Hp(int id,double value)=>f[id]=Color.FromArgb((int)Math.Round(value*255),(int)Math.Round(value*255),(int)Math.Round(value*255));
        void Member(int slot,double hp)=>f[50+slot]=Color.FromArgb((int)Math.Round(hp*255),255,0);
        void Skill(int n,bool value=true) {F(20+n,value);F(30+n,value);}
        void Reset(bool raid=false) {
            f=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
            foreach(int id in new[]{1,2,3,4,5,9,10,19,20,42,43,44,45,47,96,99,100}) F(id);
            F(41,raid);Hp(6,1);Hp(7,1);Hp(8,.70);
            foreach(int id in new[]{18,93,94,97,98}) f[id]=Color.Orange;
            for(int i=1;i<=5;i++) Member(i,i==2?.70:1);
            buff=new() {Infusion=false,HandOfDivinity=false,DivinePurpose=false};
            HOLY.Inst.Reset();
        }
        // Mastery is enabled by default; sustained/severe cases require observed evidence.
        var automatic=new HealingThresholds(RaidAutoAuraMastery:true);
        void Want(int key,string why,HolyCooldownContext? context=null) {var d=RaidTestFixture.Decide(f,buff,automatic,context);Check(d.Key==key,why+" actual="+d.Key+" "+d.Reason);}
        void ShieldSetup(bool raid=false) {
            Reset(raid);F(4,false);F(12);F(13);F(21);F(22);F(31);F(92);F(95);F(97);F(98);
            F(47,false);Hp(6,.85);
            for(int slot=1;slot<=(raid?40:5);slot++) Member(slot,.85);
        }
        foreach(bool raid in new[]{false,true}) {
            ShieldSetup(raid);Want(18,"safe Shield has highest priority ahead of ready Judgment, raid="+raid);
            foreach(int id in new[]{1,2,3,9,13,20,19,92,95,97,98}) {
                foreach(var color in new[]{Color.Black,Color.Orange}) {
                    f[id]=color;Check(RaidTestFixture.Decide(f,buff).Key!=18,"Shield requires confirmed gate"+id+", raid="+raid);
                }
                F(id);
            }
            foreach(int charges in new[]{0,1,2}) {
                F(22,charges>0);F(13,charges>=2);
                Check((RaidTestFixture.Decide(f,buff).Key==18)==(charges>=2),"Shield requires two Shock charges, charges="+charges+", raid="+raid);
            }
            f.Remove(13);Check(RaidTestFixture.Decide(f,buff).Key!=18,"missing Shock charge observation forbids Shield");F(13);
            foreach(var color in new[]{Color.White,Color.Black,Color.Orange}) {
                f[21]=color;Want(18,"Judgment cooldown no longer gates Shield, raid="+raid);
            }
            F(21);
            foreach(int id in new[]{4}) {
                foreach(var color in new[]{Color.White,Color.Orange}) {
                    f[id]=color;Check(RaidTestFixture.Decide(f,buff).Key!=18,"Shield requires confirmed enemy target"+id);
                }
                F(id,false);
            }
            foreach(var color in new[]{Color.White,Color.Black,Color.Orange}) {
                f[11]=color;Want(18,"food observation cannot gate safe Shield, raid="+raid);
            }
            f[41]=Color.Orange;Check(RaidTestFixture.Decide(f,buff).Key!=18,"unknown party/raid mode forbids Shield");F(41,raid);
            Hp(6,.84);Check(RaidTestFixture.Decide(f,buff).Key!=18,"stale healthy roster cannot hide injured player");Hp(6,.85);
            for(int slot=1;slot<=(raid?40:5);slot++) {
                Member(slot,.84);Check(RaidTestFixture.Decide(f,buff).Key!=18,"any injured living member forbids Shield, slot="+slot);Member(slot,.85);
            }
            f[55]=Color.Orange;
            Check(HolyTriage.Analyze(f,raid,new()).Reliable,"one unknown member still meets healing coverage");
            Check(RaidTestFixture.Decide(f,buff).Key!=18,"Shield demands complete health knowledge even when healing coverage is reliable");
            Member(5,0);Want(18,"confirmed dead member does not forbid Shield");
            f[55]=Color.Blue;Want(18,"offline member excluded from online living roster");
            f[55]=Color.Black;Want(18,"empty member slot excluded");Member(5,.85);
            Check(RaidTestFixture.Decide(f,buff,new(GroupLow:.95)).Key!=18,"major group injury forbids Shield even if configured healing line is met");
        }
        ShieldSetup();Hp(6,.49);Skill(10);Want(5,"unsafe Shield cannot override low-health Protection");
        Reset();F(46);F(47,false);Member(2,.4);var shieldStates=new Dictionary<int,bool>();HOLY.Inst.Buffs=buff;
        HOLY.Inst.Process(f,b,shieldStates);Check(shieldStates.ContainsKey(102),"Shield pending selection setup");
        // Keep the pending state while every member recovers and an enemy is selected.
        for(int slot=1;slot<=5;slot++) Member(slot,.85);
        F(4,false);F(12);F(13);F(21);F(22);F(92);F(95);F(97);F(98);shieldStates.Clear();
        HOLY.Inst.Process(f,b,shieldStates);Check(shieldStates.Count==1&&shieldStates.ContainsKey(18),"safe Shield replaces obsolete pending heal selection");
        // Full Shock charges outrank procs; ordinary Flash never preempts a
        // usable Shock, including the early self-macro path.
        foreach(bool raid in new[]{false,true})
        foreach(bool self in new[]{false,true})
        foreach(int charges in new[]{0,1,2})
        foreach(bool infusion in new[]{false,true})
        foreach(bool hand in new[]{false,true})
        foreach(double hp in new[]{.55,.70}) {
            Reset(raid);Skill(2);F(22,charges>0);F(13,charges>=2);Skill(5);Skill(6);
            buff.Infusion=infusion;buff.HandOfDivinity=hand;
            Hp(8,hp);Member(2,hp);
            if(self) {Hp(6,hp);Member(1,hp);F(46);F(47,false);}
            int expected=charges==2?8:infusion?(self?1:11):hand&&hp<.60?(self?3:12):charges==1?8:self?3:12;
            Want(expected,$"Shock priority raid={raid}, self={self}, charges={charges}, infusion={infusion}, hand={hand}");
        }
        Reset();Hp(6,.70);Member(1,.70);F(4,false);F(12);F(47,false);Skill(2);Skill(5);
        Want(101,"party selects injured self before using ready Shock instead of reading self Flash");
        var shockStates=new Dictionary<int,bool>();HOLY.Inst.Buffs=buff;
        HOLY.Inst.Process(f,b,shockStates);Check(shockStates.Count==1&&shockStates.ContainsKey(101),"Shock self selection starts separately");
        shockStates.Clear();F(4);F(46);Hp(8,.70);
        HOLY.Inst.Process(f,b,shockStates);Check(shockStates.Count==0,"ready Shock waits for first self target confirmation");
        HOLY.Inst.Process(f,b,shockStates);Check(shockStates.Count==1&&shockStates.ContainsKey(8),"second self confirmation releases Shock instead of self Flash");
        Reset(true);Hp(6,.70);Member(1,.70);Skill(2);Skill(5);Hp(8,1);Member(2,1);
        Want(201,"raid selects injured self for Shock instead of healing a full-health target");
        F(4,false);F(12);Want(201,"raid hostile target can select injured self for Shock");
        Reset();Hp(8,.80);Member(2,.80);Hp(6,.80);Member(1,.80);Skill(2);Skill(5);F(32,false);
        Want(1,"unusable Shock cannot suppress self Flash");
        f[32]=Color.Orange;Want(1,"unknown Shock usability cannot suppress self Flash");
        Reset();Hp(6,.80);Skill(2);Skill(5);Hp(8,1);Member(2,1);f[51]=Color.Orange;
        Want(1,"unknown self roster health cannot suppress self Flash without a real Shock target or selector");
        foreach(int charges in new[]{1,2}) {
            foreach(int id in new[]{19,41,4,5,8}) {
                Reset();Hp(8,.80);Member(2,.80);Hp(6,.80);Member(1,.80);F(46);F(47,false);Skill(2);Skill(5);F(13,charges==2);
                f[id]=Color.Orange;
                Want(1,$"unconfirmed actual Shock route must retain self Flash, point={id}, charges={charges}");
                if(charges==2) {
                    buff.Infusion=true;F(10,false);
                    Want(1,$"unconfirmed full-Shock route must retain instant self Infusion, point={id}");
                    buff.Infusion=false;Skill(6);buff.HandOfDivinity=true;
                    Hp(6,.55);Member(1,.55);
                    Want(3,$"unconfirmed full-Shock route must retain instant self Hand, point={id}");
                }
            }
        }
        Reset();Skill(2);Skill(5);F(13);buff.Infusion=true;F(32,false);
        Want(11,"two charges cannot bypass actual Shock usability");
        foreach(var color in new[]{Color.Black,Color.Orange}) {
            F(32);f[13]=color;Want(11,"one or unknown second charge retains infused Flash before Shock");
        }
        // Hard casts use the recipient's health band; actual usability, rather
        // than a mana percentage or unknown proc/cooldown, gates the chosen spell.
        foreach(bool self in new[]{false,true})
        foreach(bool raid in new[]{false,true})
        foreach(double hp in new[]{.15,.55,.60,.748,.752,.84})
        foreach(double? mana in new double?[]{.20,.40,null})
        foreach(bool stationary in new[]{false,true})
        foreach(bool? infusion in new bool?[]{false,true,null})
        foreach(bool? shock in new bool?[]{false,true,null}) {
            Reset(raid);Skill(5);Skill(6);F(10,stationary);buff.Infusion=infusion;
            f[22]=shock is null?Color.Orange:shock==true?Color.White:Color.Black;
            if(mana is {} value) Hp(7,value);else f[7]=Color.Orange;
            if(self) {Hp(6,hp);Member(1,hp);Hp(8,1);Member(2,1);F(46);F(47,false);}
            else {Hp(8,hp);Member(2,hp);}
            int expected=infusion==true?(self?1:11):!stationary?0:hp<.75?(self?3:12):(self?1:11);
            Want(expected,$"hard Light gates self={self}, raid={raid}, hp={hp}, mana={mana}, stationary={stationary}, infusion={infusion}, shock={shock}");
        }
        Reset(true);Hp(8,.55);Member(2,.55);Skill(6);Skill(5);Skill(2);
        Want(8,"ready and usable Shock precedes hard-cast Light");
        F(22,false);F(91);Skill(3);Want(9,"available holy power spender precedes target hard-cast Light");
        F(91,false);buff.DivinePurpose=true;Want(9,"free spender precedes target hard-cast Light");buff.DivinePurpose=false;
        Hp(7,.29);Want(12,"usable Light below30 mana remains the low-health cast");Hp(7,.31);Want(12,"usable Light above30 mana remains the low-health cast");
        foreach(int id in new[]{1,2,3,4,5,9,20}) {
            F(id,false);Want(id is 3 or 4?202:0,"hard Light never casts at invalid target; raid may select a valid ally");F(id);
        }
        foreach(int id in new[]{26,36}) {
            foreach(var color in new[]{Color.Black,Color.Orange}) {
                f[id]=color;Want(11,"unavailable or unknown Light falls back to confirmed low-health hard Flash");
            }
            F(id);
        }
        Hp(7,.4);Check(RaidTestFixture.Decide(f,buff,new(HolyLightMana:.50)).Key==12,"legacy mana floor no longer blocks usable low-health Light");
        F(30);F(40);Hp(6,.59);Want(5,"low health Protection uses existing minus action5");
        F(20,false);F(9,false);Want(5,"off-GCD Protection does not wait for GCD or current heal cast");
        Hp(6,.60);Want(0,"Protection excludes60 percent boundary");Hp(6,.61);Want(0,"Protection excludes health above60");Hp(6,.59);
        foreach(int id in new[]{1,2,30,40}) {
            foreach(var color in new[]{Color.Black,Color.Orange}) {
                f[id]=color;Want(0,"Protection needs confirmed combat, live player/target, ready and usable spell");
            }
            F(id);
        }
        Reset();F(46);F(47,false);Member(2,.4);var protectionStates=new Dictionary<int,bool>();HOLY.Inst.Buffs=buff;
        HOLY.Inst.Process(f,b,protectionStates);Check(protectionStates.ContainsKey(102),"Protection pending selection setup");
        protectionStates.Clear();Hp(6,.59);Skill(10);HOLY.Inst.Process(f,b,protectionStates);
        Check(protectionStates.Count==1&&protectionStates.ContainsKey(5),"Protection bypasses pending target acknowledgement");
        // Self macros share the configured target healing line, including the
        // stationary Flash fallback. Isolate self health from target/group injury.
        foreach(bool raid in new[]{false,true})
        foreach(double hp in new[]{.80,.84,.848,.85,.87})
        foreach(int mode in new[]{1,2,3,4,5}) {
            Reset(raid);Hp(8,1);Member(2,1);Hp(6,hp);Member(1,hp);F(46);F(47,false);
            if(mode is 1 or 4) Skill(5);
            if(mode==1) buff.Infusion=true;
            if(mode is 2 or 5) Skill(3);
            if(mode==2) F(91);
            if(mode==5) buff.DivinePurpose=true;
            if(mode==3) {Skill(6);buff.HandOfDivinity=true;}
            int key=mode switch {1 or 4=>1,2 or 5=>2,3=>3,_=>0};
            Want(hp<.85?key:0,$"self85 boundary mode={mode}, raid={raid}, hp={hp}");
            Check(RaidTestFixture.Decide(f,buff,new(HealBelow:.80)).Key==0,$"self respects configured80 line mode={mode}, raid={raid}, hp={hp}");
        }
        // Check every ordinary target-healing branch at the new line in both
        // group modes, independently of the more-specific emergency self macros.
        foreach(bool raid in new[]{false,true})
        foreach(double hp in new[]{.84,.85,.87})
        foreach(int spell in new[]{2,3,5,6}) {
            Reset(raid);Hp(8,hp);Member(2,hp);Skill(spell);
            if(spell==3) F(91);
            if(spell==6) buff.HandOfDivinity=true;
            int expected=hp>=.85?0:spell switch{2=>8,3=>9,5=>11,6=>12,_=>0};
            Want(expected,$"85 percent boundary for spell{spell}, raid={raid}, hp={hp}");
        }
        foreach(bool raid in new[]{false,true}) {
            Reset(raid);Skill(4);Skill(2);Want(8,"single injury never spends ready Toll");
            Hp(8,.15);Member(2,.15);Want(8,"single critical injury still does not spend group Toll");
            Skill(2,false);Skill(5);Skill(6);Want(12,"critical single injury with no instant heal uses Light instead of group Toll");
            Hp(8,.80);Member(2,.80);Member(3,.80);Skill(2);Want(8,"ordinary two-person aoe below85 does not spend Toll");
            Hp(8,.60);Member(2,.60);Member(3,.60);Member(4,.60);
            Check(RaidTestFixture.Decide(f,buff,automatic,new(Healing:new(new PartyTollReading(true),TollConfirmed:true))).Key==(raid?8:4),
                "confirmed party group injury or raid threshold spends mode-specific Toll");
            Member(4,1);Want(8,"two below70 do not meet major group threshold");
            Hp(8,.40);Member(2,.40);Member(3,.40);Want(8,"two below50 do not permit group Toll");
            Member(4,.40);Want(raid?8:4,"three below50 permit party Toll only");
            F(19,false);Want(8,"stale major group health without a group cannot spend Toll");F(19);
            f[54]=f[55]=Color.Orange;Want(8,"unreliable group health cannot spend Toll");Member(4,1);Member(5,1);
            F(24,false);Want(8,"group Toll on cooldown falls through to Shock");F(24);F(34,false);Want(8,"unusable group Toll falls through to Shock");
            F(34);f[24]=Color.Orange;Want(8,"unknown group Toll readiness falls through to Shock");
        }
        Reset(true);Skill(4);Skill(2);for(int slot=1;slot<=40;slot++) Member(slot,1);
        for(int slot=1;slot<=11;slot++) Member(slot,.55);Want(10,"40-person raid with at least4 severe injuries permits Toll");
        Member(12,.55);Want(10,"additional severe injuries retain raid Toll");
        // An unavailable or unknown Shock must not block confirmed Flash.
        foreach(bool raid in new[]{false,true})
        foreach(bool stationary in new[]{false,true})
        foreach(bool? ready in new bool?[]{false,true,null})
        foreach(bool? usable in new bool?[]{false,true,null})
        foreach(bool? hand in new bool?[]{false,null}) {
            Reset(raid);Hp(8,.84);Member(2,.84);Skill(5);Skill(6);F(10,stationary);
            f[22]=ready is null?Color.Orange:ready==true?Color.White:Color.Black;
            f[32]=usable is null?Color.Orange:usable==true?Color.White:Color.Black;
            buff.Infusion=null;buff.HandOfDivinity=hand;
            Want(ready==true&&usable==true?8:stationary?11:0,
                $"Shock/Flash fallback raid={raid}, stationary={stationary}, ready={ready}, usable={usable}, Hand={hand}");
        }
        // Native confirmed absence must override a stale external presence point.
        foreach(bool self in new[]{false,true})
        foreach(bool? native in new bool?[]{false,true,null}) {
            Reset();Skill(5);Skill(6);Hp(8,.55);Member(2,.55);
            buff.Infusion=null;F(10,false); // isolate instant Hand from hard casts
            if(self) Hp(6,.55);
            buff.HandOfDivinity=true;
            f[94]=native is null?Color.Orange:native==true?Color.White:Color.Black;
            Want(native==false?0:(self?3:12),$"Hand source precedence, self={self}, native={native}");
        }
        Reset();Skill(2);Want(8,"ordinary injury shock");
        F(100,false);F(94);Want(0,"old layer collector cannot masquerade as instant Holy Light");F(100);
        F(1,false);Want(0,"out of combat");F(1);F(2,false);Want(0,"dead player");F(2);F(3,false);F(11);Want(0,"dead target never cancels food marker");F(11,false);Want(0,"dead target no heal");
        Reset();Hp(8,.95);Member(2,.95);Skill(2);Skill(5);Want(0,"minor injury no healing");
        Reset();Hp(8,.848);Member(2,.848);Skill(2);Want(8,"Shock heals just below default85 percent");
        Hp(8,.85);Member(2,.85);Want(0,"Shock does not heal at default85 percent after quantization");
        Hp(8,.87);Member(2,.87);Want(0,"Shock does not heal between new85 and former90 threshold");
        Skill(2,false);Skill(5);Want(0,"Flash fallback also respects new85 threshold");
        Skill(3);F(91);buff.DivinePurpose=true;Want(0,"spender including free proc respects new85 threshold");
        Reset();Hp(8,.87);Member(2,.87);Member(3,.87);Want(0,"party members above85 do not trigger selection or ordinary aoe");
        Check(!HolyTriage.Analyze(f,false,new()).Aoe,"ordinary party aoe uses85 injury threshold");
        Member(3,.84);Want(103,"party member below85 is selected while87 target is ignored");
        Member(2,.84);Check(!HolyTriage.Analyze(f,false,new()).Aoe,"two below85 do not trigger ordinary party aoe");
        Member(4,.84);Check(HolyTriage.Analyze(f,false,new()).Aoe,"three below85 trigger ordinary party aoe");
        Reset();Skill(2);Check(RaidTestFixture.Decide(f,buff,new(HealBelow:.65)).Key==0,"Shock uses configured healing threshold instead of hardcoded85");
        F(5,false);Want(0,"Shock needs target healing range");Check(RaidTestFixture.Decide(f,buff).Reason.Contains("点5"),"out of range is not misreported as topped target");
        f[4]=Color.Orange;Want(0,"Shock waits for confirmed friendly target");
        Reset();Skill(4,false);Skill(2);Want(8,"unlearned toll skips to shock");
        Reset();Member(2,.6);Member(3,.6);Member(4,.6);Hp(8,.6);
        foreach(int n in new[]{4,5,7,8,9}) Skill(n,false);
        buff.Infusion=buff.HandOfDivinity=null;
        Skill(2);Want(8,"unlearned group spells and flash skip unknown dependent auras");
        Skill(2,false);Skill(3);F(91);Want(9,"unlearned shock falls through to holy power spender");
        Skill(3,false);Skill(6);Hp(8,.55);Want(12,"unknown infusion does not block stationary low-health Light");Skill(5);Want(12,"available Flash does not replace low-health Light");
        Reset();Hp(6,.19);Want(0,"manual Lay never casts automatically");
        F(18);Want(0,"legacy Forbearance present cannot change manual Lay behavior");
        F(18,false);Want(0,"legacy Forbearance absent cannot enable automatic Lay");
        F(16);Want(22,"critical self still uses healing stone instead of manual Lay");
        Reset();Hp(6,.4);F(16);F(17);Want(22,"stone first");F(16,false);Want(23,"potion second");
        F(1,false);Want(0,"items no combat");
        Reset();Hp(8,1);Member(2,1);Hp(6,.79);F(91);Skill(3);Want(2,"self wog >=3 uses action2 key8");F(91,false);buff.Infusion=true;Skill(5);Want(1,"self infused flash uses action1 key7");
        F(3,false);Want(1,"self healing does not require live target");
        Reset();Hp(8,1);Member(2,1);Hp(6,.55);Skill(6);buff.HandOfDivinity=true;F(10,false);Hp(7,.05);
        Want(3,"self Hand instant Holy Light uses action3 key9 while moving and low mana");
        buff.HandOfDivinity=false;Want(0,"self moving Holy Light stops when Hand is consumed");
        buff.HandOfDivinity=null;Want(0,"unknown Hand never enables moving self Holy Light");
        F(10);Hp(6,.55);Hp(7,.4);Want(3,"stationary major self injury with no infusion and Shock cooldown uses hard-cast Light");
        Skill(5);Want(3,"eligible hard-cast self Light precedes ordinary Flash fallback");
        Hp(7,.2);Want(3,"self Light uses actual usability instead of a mana percentage floor");F(36,false);Want(1,"unusable self Light falls back to low-health hard Flash");F(36);
        F(10,false);Want(0,"ordinary self Flash requires stationary");F(10);
        Hp(6,.84);Want(1,"self stationary Flash now covers ordinary injury below85");Hp(6,.55);
        Skill(5,false);Want(3,"unlearned Flash does not block usable low-health Light");Skill(5);F(9,false);Want(0,"self hard cast waits for active cast");F(9);F(20,false);Want(0,"self hard cast waits for gcd");
        Reset();F(11);F(4,false);F(12);F(13);F(14);F(15);F(20,false);Want(0,"Holy never interrupts or cancels food even with legacy ready observations");
        F(20);Want(0,"legacy food and cast observations cannot request an interrupt outside GCD either");
        Reset();Skill(2);
        foreach(int id in new[]{11,13,14,15}) {
            foreach(var color in new[]{Color.White,Color.Black,Color.Orange}) {
                f[id]=color;Want(8,"food and removed interrupt observations cannot block healing, point="+id);
            }
        }
        Reset();Member(3,.30);Want(103,"lowest party2 selected");F(43,false);Want(0,"out of range party2 excluded");
        F(43);f[53]=Color.Blue;Want(0,"offline excluded");f[53]=Color.Orange;Want(0,"unknown excluded");Member(3,0);Want(0,"dead excluded");
        Reset();Member(5,.2);Want(105,"party4 uses F5");F(11);Want(105,"food no longer blocks party4 selection");f[11]=Color.Orange;Want(105,"unknown food no longer blocks selection");
        Reset(true);Member(3,.2);Skill(2);Want(203,"raid selects lowest injured member");F(4,false);F(12);Skill(1);Want(203,"raid switches from hostile target to injured member");
        F(99,false);Want(203,"out of range Judgment does not block raid healing selection");f[99]=Color.Orange;Want(203,"unknown Judgment range does not block raid healing selection");
        Reset();Member(3,.30);F(19,false);Want(0,"no group excludes stale party candidates");
        Reset();F(47,false);F(46);Member(2,.4);Want(102,"select party1 separate from healing");
        var states=new Dictionary<int,bool>();HOLY.Inst.Buffs=buff;HOLY.Inst.Process(f,b,states);
        Check(states.Count==1&&states.ContainsKey(102),"only selector action");
        states.Clear();HOLY.Inst.Process(f,b,states);Check(states.Count==0,"wait old target");
        Hp(6,.19);Skill(3);F(91);HOLY.Inst.Process(f,b,states);Check(states.ContainsKey(2),"self wog key8 bypasses target acknowledgement");
        states.Clear();Hp(6,.19);Skill(3,false);F(91,false);Skill(5);HOLY.Inst.Buffs.Infusion=true;
        HOLY.Inst.Process(f,b,states);Check(states.Count==1&&states.ContainsKey(1),"self infused Flash key7 does not wait for another target acknowledgement");
        states.Clear();HOLY.Inst.Buffs.Infusion=false;Skill(5,false);Skill(6);HOLY.Inst.Buffs.HandOfDivinity=true;F(10,false);
        HOLY.Inst.Process(f,b,states);Check(states.Count==1&&states.ContainsKey(3),"self Hand Holy Light key9 does not wait for another target acknowledgement");
        states.Clear();Hp(6,1);Skill(6,false);HOLY.Inst.Buffs.HandOfDivinity=false;F(10);
        F(46,false);F(47);F(11);Skill(2);HOLY.Inst.Process(f,b,states);Check(states.Count==0,"food presence does not reset pending first target confirmation");
        F(47,false);HOLY.Inst.Process(f,b,states);Check(states.Count==0,"intervening target mismatch resets confirmation");
        F(47);f[11]=Color.Orange;HOLY.Inst.Process(f,b,states);Check(states.Count==0,"unknown food does not reset pending confirmation after mismatch");
        HOLY.Inst.Process(f,b,states);Check(states.ContainsKey(8),"second confirmation allows healing");
        Reset();Member(2,.6);Member(3,.6);Member(4,.6);Hp(8,.6);Skill(9);Skill(4);Skill(2);Want(15,"virtue precedes toll with an executable follow-up");
        f[97]=Color.Orange;Want(15,"ready Virtue ignores removed window slot");
        var confirmedGroup=new HolyCooldownContext(Healing:new(new PartyTollReading(true),TollConfirmed:true));
        F(29,false);F(97);Want(4,"party toll uses 0 self macro no target change",confirmedGroup);
        F(97,false);Want(4,"Virtue cooldown prevents repeat cast without a healing window",confirmedGroup);
        F(41);Member(5,.6);Want(10,"four confirmed raid injuries use semicolon on current target",confirmedGroup);Member(5,1);F(41,false);
        F(92);Skill(3);Want(9,"5 hp before toll");F(92,false);Skill(4,false);Skill(7);
        Member(2,.55);Member(3,.55);Member(4,.55);Hp(8,.55);Want(13,"three below60 -> wings");
        buff.Infusion=true;Skill(5);Want(13,"infusion does not indefinitely postpone needed wings");
        F(95);Want(13,"old unused layer point is ignored");F(95,false);
        buff.Infusion=false;
        Skill(7,false);Skill(8);Want(8,"three below60 alone do not authorize Mastery");
        F(98);Want(8,"legacy wings observation cannot create Mastery evidence");
        buff.Infusion=null;Want(8,"unknown infusion does not create Mastery evidence");
        Skill(2);Skill(7);Want(13,"unknown infusion does not block needed wings");
        f[24]=Color.Orange;Want(13,"unknown toll cooldown does not block needed wings");
        Reset();Skill(2);Skill(7);Skill(8);
        Want(8,"absent infusion with ordinary injury never opens wings or mastery");
        Member(2,.15);Hp(8,.15);
        Want(8,"one critically injured ally without infusion never opens group cooldowns");
        Member(2,.6);Member(3,.6);Hp(8,.6);
        Want(8,"ordinary group aoe with two below70 never opens major group cooldowns");
        Member(2,.4);Member(3,.4);Hp(8,.4);
        Want(8,"two below50 do not permit group wings");
        Member(4,.4);Want(13,"three below50 confirm major aoe and permit wings without infusion");
        foreach(var color in new[]{Color.White,Color.Black,Color.Orange}) {
            f[98]=color;Want(13,"removed wings aura does not affect ready group cooldown");
        }
        f[27]=Color.Orange;Want(14,"mastery demand is independent of wings cooldown",new(Mastery:new(SevereConfirmed:true)));
        F(27);F(37,false);Want(14,"unusable wings does not block configured healing mastery",new(Mastery:new(SevereConfirmed:true)));F(37);
        Skill(7,false);Want(14,"wings confirmed on cooldown permits ready mastery during major aoe",new(Mastery:new(SevereConfirmed:true)));
        F(38,false);Want(8,"unusable mastery skips to shock");F(38);
        F(19,false);Want(8,"no group cannot open wings from stale major group observations");
        Reset();buff.Infusion=true;Skill(5);Skill(2);Want(11,"consume infusion before shock");
        F(93,false);Want(8,"valid native absence overrides fixed point");
        f[93]=Color.Orange;buff.Infusion=null;Skill(5,false);Want(8,"unknown infusion cannot block unavailable flash");
        Skill(5);Want(8,"unknown infusion with available flash falls through to shock");
        F(93,false);F(95);Want(8,"unused old layer point cannot override absent infusion");F(95,false);f[93]=Color.Orange;
        Skill(2,false);F(10,false);Want(0,"unknown infusion while moving never assumes instant flash");
        Reset();Skill(3);F(91);Want(9,"3 holy power heal");
        Reset();Skill(3);Skill(2);Skill(4);Skill(5);buff.Infusion=true;buff.DivinePurpose=true;F(10,false);
        Want(9,"confirmed Divine Purpose at zero holy power spends before generators and Toll while moving");
        f[91]=Color.Orange;f[92]=Color.Orange;Want(9,"confirmed proc waives unknown resource gates");
        buff.DivinePurpose=false;Want(11,"consumed proc with unknown holy power re-evaluates instead of another free heal or single Toll");
        buff.DivinePurpose=null;Want(11,"unknown proc cannot waive resource gate");
        buff.DivinePurpose=true;F(33,false);Want(11,"proc cannot bypass unusable spender");
        F(33);F(23,false);Want(11,"proc cannot bypass unlearned or cooldown spender");F(23);
        F(3,false);Want(0,"free target heal requires live target");F(3);F(5,false);Want(0,"free target heal requires range");F(5);
        F(4,false);Want(0,"free target heal requires friendly target");F(4);
        F(9,false);Want(0,"free heal cannot interrupt active cast");F(9);F(20,false);Want(0,"free heal waits for gcd");F(20);
        F(1,false);Want(0,"free heal requires combat");F(1);F(2,false);Want(0,"free heal requires live player");F(2);
        Hp(8,.95);Member(2,.95);Want(0,"proc does not heal topped target");
        Reset();Hp(8,1);Member(2,1);Skill(3);buff.DivinePurpose=true;Hp(6,.79);F(10,false);Want(2,"self key8 accepts confirmed proc at zero holy power while moving");
        f[91]=Color.Orange;Want(2,"self proc bypasses unknown holy power");buff.DivinePurpose=null;Want(0,"unknown proc cannot grant self free heal");
        buff.DivinePurpose=true;F(3,false);Want(2,"self free heal does not require live target");
        Reset(true);Skill(3);buff.DivinePurpose=true;Member(3,.2);Want(203,"raid proc selects lower injured member");
        Reset();Member(5,.2);Skill(3);buff.DivinePurpose=true;Want(105,"external point105 does not collide with party4 selection action105");
        Reset();Hp(8,.55);Member(2,.55);Skill(6);Skill(5);Want(12,"major target gets hard Light with absent infusion and Shock cooldown");Hp(7,.2);Want(12,"low mana percentage still permits actually usable Light");
        foreach(bool? present in new bool?[]{true,false,null}) {
            buff.HandOfDivinity=present;Want(12,"stationary low-health Light does not require Hand");
        }
        F(10,false);Want(0,"moving uninfused no cast time spells");
        Reset();buff.Infusion=true;Skill(5);F(10,false);Want(11,"moving infused flash allowed");
        Reset();Hp(8,.80);Member(2,.80);Skill(2);F(22,false);Skill(5);Want(11,"stationary 80 percent ally gets uninfused Flash when Shock has no charges");
        foreach(var color in new[]{Color.White,Color.Black,Color.Orange}) {
            f[97]=color;Want(11,"removed window state cannot block Flash fallback during Shock cooldown");
        }
        buff.Infusion=null;Want(11,"unknown Infusion does not block ordinary stationary Flash during Shock cooldown");
        f[25]=Color.Orange;Want(0,"unknown Flash readiness cannot be assumed ready");Check(RaidTestFixture.Decide(f,buff).Reason.Contains("点25"),"blocked Flash identifies readiness point");
        F(25);F(35,false);Want(0,"unusable Flash cannot cast during Shock cooldown");Check(RaidTestFixture.Decide(f,buff).Reason.Contains("点35"),"blocked Flash identifies usability point");
        F(35);F(10,false);Want(0,"moving without confirmed Infusion waits instead of ordinary Flash");
        Check(RaidTestFixture.Decide(f,buff).Reason.Contains("点10"),"blocked Flash identifies movement point");
        foreach(int n in new[]{1,2,3,4,6,7,8,9}) {
            Reset();Hp(8,.80);Member(2,.80);Skill(5);F(20+n);F(30+n);f[20+n]=Color.Orange;
            Want(11,"unknown cooldown for skill"+n+" cannot block ordinary Flash fallback");
            F(20+n);f[30+n]=Color.Orange;
            Want(11,"unknown usability for skill"+n+" cannot block ordinary Flash fallback");
        }
        Reset(true);Hp(8,.80);Member(2,.80);Skill(5);Skill(2);F(22,false);Want(11,"raid selected 80 percent target receives ordinary Flash with Shock on cooldown");
        F(9,false);Want(0,"fallback never interrupts current cast");F(9);F(20,false);Want(0,"fallback waits for actual GCD");
        f[20]=Color.Orange;Want(0,"unknown GCD cannot enable fallback cast");
        Reset();Hp(8,.55);Member(2,.55);Skill(6);Skill(2);F(10,false);Hp(7,.05);buff.HandOfDivinity=true;
        Want(12,"confirmed Hand buff uses instant Holy Light while moving and below ordinary mana floor");
        Want(12,"Hand buff still present after first instant cast permits second");
        buff.HandOfDivinity=false;Want(8,"Hand buff consumed stops instant Holy Light after second cast");
        buff.HandOfDivinity=null;Want(8,"unknown Hand buff falls through to confirmed instant shock");
        Skill(2,false);Want(0,"unknown Hand never assumes moving Holy Light is instant");
        buff.HandOfDivinity=true;Skill(6,false);Want(0,"unlearned or unusable Holy Light skipped even with Hand buff");
        Skill(6);F(9,false);Want(0,"Hand waits for current cast");F(9);F(20,false);Want(0,"Hand waits for gcd");F(20);
        F(5,false);Want(0,"Hand needs healing range");F(5);F(4,false);Want(0,"Hand needs friendly target");F(4);
        Hp(8,.95);Member(2,.95);Want(0,"Hand does not overheal topped target");Hp(8,.55);Member(2,.55);
        buff.Infusion=true;Skill(5);Want(11,"existing infusion consumed before Hand instant Holy Light");
        F(93,false);Want(12,"native absent infusion and actual Hand buff choose Holy Light");
        F(94,false);Want(0,"native absent Hand overrides fixed point presence");
        Reset(true);Hp(8,.55);Member(2,.55);buff.HandOfDivinity=true;Skill(6);F(10,false);Member(3,.2);Want(203,"raid Hand selects lowest injured member first");
        Reset();Member(2,.55);Member(3,.55);Member(4,.55);Hp(8,.55);Skill(6);Skill(7);Skill(8);F(10,false);
        buff.HandOfDivinity=true;Want(13,"major pressure can activate wings even with a Hand proc");
        buff.HandOfDivinity=false;Want(13,"no infusion and no Hand uses ready wings");
        // Sending wings does not prove it granted instant casts.
        Skill(7,false);buff.HandOfDivinity=null;Want(0,"unknown Hand and stable below60 do not authorize instant Light or Mastery");
        buff.HandOfDivinity=true;Skill(8,false);Hp(8,.55);Member(2,.55);Want(12,"actual Hand acquisition and major injury after wings enable Holy Light");
        Reset();Member(2,.6);Member(3,.6);Member(4,.6);var t=new HealingThresholds();Check(HolyTriage.Analyze(f,false,t).Major,"party 3 below70 major");
        Check(HolyTriage.Single(.19,t)=="单体危急"&&HolyTriage.Single(.4,t)=="单体大掉血"&&HolyTriage.Single(.8,t)=="单体普通掉血"&&HolyTriage.Single(.95,t)=="轻微掉血，无需治疗","four single target classifications");
        Member(4,1);Check(!HolyTriage.Analyze(f,false,t).Major,"2 below70 not major");Member(2,.4);Member(3,.4);Check(!HolyTriage.Analyze(f,false,t).Major,"2 below50 not major");
        Member(4,.4);Check(HolyTriage.Analyze(f,false,t).Major,"3 below50 major");
        Reset(true);for(int i=1;i<=40;i++) Member(i,1);Member(1,.4);Member(2,.4);Check(!HolyTriage.Analyze(f,true,t).Major,"2 low of40 not raid major");
        for(int i=1;i<=12;i++) Member(i,.6);Check(HolyTriage.Analyze(f,true,t).Major,"30 percent of40 below70");
        for(int i=1;i<=40;i++) Member(i,1);for(int i=1;i<=8;i++) Member(i,.4);Check(HolyTriage.Analyze(f,true,t).Major,"20 percent of40 below50");
        for(int i=10;i<=40;i++) f[50+i]=Color.Orange;Check(!HolyTriage.Analyze(f,true,t).Reliable&&!HolyTriage.Analyze(f,true,t).Major,"unknown roster suppresses raid cooldowns");
        using var pattern=DiagnosticTests.Pattern(1);var layout=Calibration.Find(pattern,new Size(1600,900));layout.SpecId=65;
        // Exercise the actual execution entry without SendInput: a persisted
        // threshold must reach Process, rather than only the UI's Decide preview.
        Reset(true);Skill(2);Hp(8,.84);Member(2,.84);
        using(var pixels=new Bitmap(layout.ClientWidth,layout.ClientHeight)) {
            foreach(var pair in f) pixels.SetPixel(layout.Frames[pair.Key-1].X,layout.Frames[pair.Key-1].Y,pair.Value);
            foreach(var pair in RaidTestFixture.Bars(f)) pixels.SetPixel(layout.Bars[pair.Key-1].X,layout.Bars[pair.Key-1].Y,pair.Value);
            using var sample=new Captured((Bitmap)pixels.Clone(),new Rectangle(Point.Empty,pixels.Size));
            var sent=new Dictionary<int,bool>();var game=new GameWindow(1,(IntPtr)42,"Synthetic Holy");
            var runner=new RotationExecution(()=>game.Handle,()=>false,(frames,bars)=> {
                sent.Clear();HOLY.Inst.Process(frames,bars,sent);
            });
            var profile=ClassProfiles.Find(65)!;runner.Start(profile);
            layout.HolyThresholds=new(HealBelow:.80);runner.Tick(game,layout,sample,true,0,profile);
            Check(sent.Count==0,"execution uses saved80 line, not default85 preview threshold");
            foreach(var pair in RaidTestFixture.Bars(f)) pixels.SetPixel(layout.Bars[pair.Key-1].X,layout.Bars[pair.Key-1].Y,pair.Value);
            using var nextSample=new Captured((Bitmap)pixels.Clone(),new Rectangle(Point.Empty,pixels.Size));
            layout.HolyThresholds=new(HealBelow:.85);runner.Tick(game,layout,nextSample,true,.1,profile);
            Check(sent.Count==1&&sent.ContainsKey(8),"execution adopts saved85 threshold and emits Shock decision below line");
            runner.Stop();
        }
        foreach(var p in HolyBuffs.Points) {
            Check(HolyBuffs.Observe(layout,p.Key,_=>Color.Black) is null,"unconfigured "+p.Key);
            layout.SpecialPoints[p.Key]=new(){Enabled=true,Point=new(540,8),ReferenceColor=new(255,255,255),Comparison="expiring"};
            Check(HolyBuffs.Observe(layout,p.Key,_=>Color.Black)==false,"absent "+p.Key);
            Check(HolyBuffs.Observe(layout,p.Key,_=>Color.White)==true,"present "+p.Key);
            Check(p.Key=="holy_purpose"?HolyBuffs.Observe(layout,p.Key,_=>Color.Orange)==true:HolyBuffs.Observe(layout,p.Key,_=>Color.Orange) is null,"default color interpretation "+p.Key);
        }
        Check(ClassProfiles.Find(65)?.ClassId==2,"profile identity");
        Check(HolyBuffs.Fields().Where(field=>int.Parse(field.Id)>100).Select(field=>field.Id).SequenceEqual(new[]{"101","104","105"}),"remaining external point IDs remain stable after removing102 and103");
        Check(FuryBuffs.SpecialKey(layout,HolyBuffs.Fields().Single(field=>field.Id=="105"))=="holy_purpose"&&FuryBuffs.SpecialKey(layout,new("102","removed",ValueKind.Raw)) is null&&FuryBuffs.SpecialKey(layout,new("103","removed",ValueKind.Raw)) is null,"removed points never shift another buff into their slots");
        Check(new[]{0x10,0x11,0x30,0x33,0x34,0x35,0x37,0x38,0x39,0xBB,0xBD,0xDB,0xDD,0xDC,0xBA,0xDE,0xBE,0x70,0x71,0x72,0x73,0x74}.All(k=>HOLY.Inst.skipKeys.Contains(k)),"modifiers and all healing, Shield, item, marker and selection keys permit manual override");
        var directory=Path.Combine(Path.GetTempPath(),"hiji-holy-"+Guid.NewGuid().ToString("N"));
        try {
            var profiles=new ProfileLayouts(directory);var profile=ClassProfiles.Find(65)!;
            layout.HolyThresholds=new(HealBelow:.85);profiles.Save(profile,layout);
            var loaded=profiles.Load(profile,layout);
            Check(loaded.SpecialPoints.Count==3&&loaded.HolyThresholds.HealBelow==.85,"reload preserves three points and thresholds");
            var fourPoints=layout.Copy();fourPoints.SpecialPoints.Remove("holy_purpose");fourPoints.SpecialPoints["holy_virtue"]=new(){Enabled=true,Point=new(588,8),ReferenceColor=new(255,255,255)};profiles.Save(profile,fourPoints);
            var older=profiles.Load(profile,fourPoints);
            Check(older.SpecialPoints.Count==2&&!older.SpecialPoints.ContainsKey("holy_virtue")&&HolyBuffs.Read(older,_=>Color.White).DivinePurpose is null,"old config removes window without inferring a free proc");
            Check(HolyBuffs.Read(loaded,_=>Color.White).DivinePurpose==true,"saved Divine Purpose point feeds healing observation");
            var legacy=layout.Copy();legacy.SpecialPoints.Remove("holy_hand");
            foreach(string key in new[]{"holy_i2","holy_i3"}) legacy.SpecialPoints[key]=new(){Enabled=true,Point=new(560,8),ReferenceColor=new(255,255,255),Comparison="expiring"};
            legacy.SpecialPoints["holy_forbearance"]=new(){Enabled=true,Point=new(588,8),ReferenceColor=new(255,255,255),Comparison="expiring"};
            legacy.SpecialPoints["holy_wings"]=new(){Enabled=true,Point=new(590,8),ReferenceColor=new(255,255,255)};
            profiles.Save(profile,legacy);var migrated=profiles.Load(profile,legacy);
            Check(migrated.SpecialPoints.Count==2&&migrated.SpecialPoints.ContainsKey("holy_i1")&&!migrated.SpecialPoints.ContainsKey("holy_i2")&&!migrated.SpecialPoints.ContainsKey("holy_i3")&&!migrated.SpecialPoints.ContainsKey("holy_forbearance")&&!migrated.SpecialPoints.ContainsKey("holy_wings"),"old layer, Forbearance and wings settings removed while presence setting preserved");
            Check(migrated.SpecialPoints["holy_purpose"].Point==layout.SpecialPoints["holy_purpose"].Point&&migrated.SpecialPoints["holy_i1"].Point==layout.SpecialPoints["holy_i1"].Point,"migration preserves remaining point coordinates by key");
            Check(HolyBuffs.Read(migrated,_=>Color.White).HandOfDivinity is null,"migration never reuses an old layer point for Hand");
            profiles.Save(profile,layout);
            var other=layout.Copy();other.SpecId=71;other.SpecialPoints.Clear();other.SpecialPoints["smash"]=new();
            Check(profiles.Load(profile,other).SpecialPoints.Count==3,"other class does not replace holy points");
            using var settings=new HolyPointSettings();settings.LoadSettings(layout);
            settings.Stage("holy_i1",new(new(550,8),new(50,60,70)));
            settings.Stage("holy_hand",new(new(560,8),new(80,90,100)));
            var saved=settings.BuildLayout(layout,"holy_i1");settings.LoadSettings(saved,"holy_i1");
            Check(settings.Rows["holy_hand"].Pending is not null,"saving one row preserves Hand row pending");
            Check(saved.SpecialPoints["holy_i1"].ReferenceColor==new SampleColor(50,60,70),"reference saves correctly");
            settings.Stage("holy_purpose",new(new(580,8),new(255,255,255)));
            settings.LoadSettings(saved,"holy_i1");
            Check(settings.Rows["holy_purpose"].Pending is not null,"saving another row preserves pending Purpose reference");
            var purposeSaved=settings.BuildLayout(saved,"holy_purpose");profiles.Save(profile,purposeSaved);
            var purposeLoaded=profiles.Load(profile,saved);
            Check(HolyBuffs.Read(purposeLoaded,_=>Color.White).PurposeExpiringConfirmed,"Purpose expiry is automatic after profile save and reload");
            settings.LoadSettings(purposeLoaded,"holy_purpose");
            Check(settings.Rows["holy_purpose"].Rule.Text.Contains("指定色=临期"),"Purpose row always shows expiry rule");
            bool rejected=false;try { (t with{Major=.95}).Validate(); }catch(InvalidDataException) {rejected=true;}
            Check(rejected,"invalid threshold order rejected");
        } finally {
            string profilesDir=Path.Combine(directory,"profiles");
            if(Directory.Exists(profilesDir)) {foreach(string file in Directory.GetFiles(profilesDir)) File.Delete(file);Directory.Delete(profilesDir);}
            if(Directory.Exists(directory)) Directory.Delete(directory);
        }
        HOLY.Inst.Reset();return count;
    }
    internal static void Render(string path)
    {
        using var pattern=DiagnosticTests.Pattern(1);var layout=Calibration.Find(pattern,new Size(1600,900));layout.SpecId=65;
        using var pixels=new Bitmap(800,20);
        pixels.SetPixel(layout.Markers[0].X,0,Color.Lime);pixels.SetPixel(layout.Markers[1].X,0,ClassProfiles.Encode(65,2));pixels.SetPixel(layout.Markers[2].X,0,Color.Cyan);
        int x=540;
        foreach(var item in HolyBuffs.Points) {
            layout.SpecialPoints[item.Key]=new(){Enabled=true,Point=new(x,8),Comparison="expiring",ReferenceColor=new(255,255,255)};
            pixels.SetPixel(x,8,Color.White);x+=24;
        }
        var directory=Path.Combine(Path.GetTempPath(),"hiji-holy-ui-"+Guid.NewGuid().ToString("N"));
        var profiles=new ProfileLayouts(directory);profiles.Save(ClassProfiles.Find(65)!,layout);
        using var form=new DiagnosticForm(false,profileLayouts:profiles);
        form.Show();Application.DoEvents();form.FreezeForTesting();form.ShowSynthetic(layout,pixels);
        static IEnumerable<Control> All(Control p)=>p.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(All(c)));
        var settings=All(form).OfType<HolyPointSettings>().Single();
        var raidMastery=All(form).OfType<CheckBox>().Single(c=>c.Name=="holyRaidMastery");
        if(!raidMastery.Checked) throw new Exception("Raid automatic Mastery must default on");
        settings.Stage("holy_i1",new(new(550,8),new(255,255,255)));
        raidMastery.Checked=false;
        var saved=DiagnosticLayout.Load(profiles.PathFor(ClassProfiles.Find(65)!));
        if(saved.HolyThresholds.RaidAutoAuraMastery||settings.Rows["holy_i1"].Pending is null)
            throw new Exception("Raid manual switch must persist without disabling party automation or losing pending buff selection");
        // Recreate the form to verify that loading a saved false does not
        // accidentally save the checkbox's default true over the preference.
        using(var reloaded=new DiagnosticForm(false,profileLayouts:profiles)) {
            reloaded.Show();Application.DoEvents();reloaded.FreezeForTesting();reloaded.ShowSynthetic(layout,pixels);
            if(All(reloaded).OfType<CheckBox>().Single(c=>c.Name=="holyRaidMastery").Checked||
                DiagnosticLayout.Load(profiles.PathFor(ClassProfiles.Find(65)!)).HolyThresholds.RaidAutoAuraMastery)
                throw new Exception("Raid manual preference must survive restarting the form");
            reloaded.Close();
        }
        raidMastery.Checked=true;
        if(!DiagnosticLayout.Load(profiles.PathFor(ClassProfiles.Find(65)!)).HolyThresholds.RaidAutoAuraMastery)
            throw new Exception("Re-enabling raid automatic Mastery must persist");
        var purposeRow=settings.Rows["holy_purpose"];
        settings.Stage("holy_purpose",new(new(588,8),new(255,255,255)));
        // SaveSpecial's click handler checks a live game's size and identity;
        // synthetic UI tests exercise its persistence path without querying a game.
        form.SaveSpecialLayout(settings.BuildLayout(DiagnosticLayout.Load(profiles.PathFor(ClassProfiles.Find(65)!)),"holy_purpose"),"holy_purpose");
        Application.DoEvents();
        if(!HolyBuffs.Read(DiagnosticLayout.Load(profiles.PathFor(ClassProfiles.Find(65)!)),_=>Color.White).PurposeExpiringConfirmed)
            throw new Exception("Purpose expiry mode must persist and reload through the form save path");
        foreach(var size in new[]{new Size(1180,800),new Size(950,650)}) {
            form.ClientSize=size;form.Refresh();Application.DoEvents();
            var switchParent=raidMastery.Parent??throw new Exception("Raid Mastery switch has no parent");
            if(!raidMastery.Visible||!raidMastery.Enabled||raidMastery.Right>switchParent.ClientSize.Width||raidMastery.Bottom>switchParent.ClientSize.Height)
                throw new Exception($"Raid automatic Mastery switch clipped: size={size}, visible={raidMastery.Visible}, enabled={raidMastery.Enabled}, bounds={raidMastery.Bounds}, parent={switchParent.ClientSize}");
            foreach(var row in settings.Rows.Values)
                if(!row.Pick.Visible||!row.Disable.Enabled||row.Save.Right>row.Save.Parent!.ClientSize.Width||row.Pick.Bottom>row.Pick.Parent!.ClientSize.Height) throw new Exception("Holy settings clipped");
            if(All(settings).OfType<CheckBox>().Any()) throw new Exception("Holy buff settings must not contain an expiry mode switch");
        }
        form.ClientSize=new Size(1180,800);Application.DoEvents();
        using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(path,ImageFormat.Png);
        form.Close();
        foreach(string file in Directory.GetFiles(Path.Combine(directory,"profiles"))) File.Delete(file);
        Directory.Delete(Path.Combine(directory,"profiles"));Directory.Delete(directory);
        Console.WriteLine("PASS: Holy UI three rows and default-on raid Mastery switch at 1180x800 and 950x650; manual/auto persistence, restart and pending buff selection preserved; "+path);
    }
}
