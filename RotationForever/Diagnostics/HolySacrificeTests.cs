#nullable enable
using System.Drawing;

namespace RotationUS.Diagnostics;

internal static class HolySacrificeTests
{
    internal static int Run()
    {
        int count=0;
        void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL Sacrifice Protection: "+why);count++;}
        var f=new Dictionary<int,Color>();
        void F(int id,bool v=true)=>f[id]=v?Color.White:Color.Black;
        void Hp(double value) {int n=(int)Math.Round(255*value);f[6]=Color.FromArgb(n,n,n);}
        void Reset(double hp=1,bool raid=false) {
            f=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
            foreach(int id in new[]{1,2,3,4,5,9,10,19,20,30,40,42,43,44,45,46,96,100}) F(id);
            for(int slot=1;slot<=5;slot++) f[50+slot]=Color.FromArgb(255,255,0);
            F(7);F(8);Hp(hp);f[11]=Color.Cyan;
            F(41,raid);
            HOLY.Inst.Reset();HOLY.Inst.Buffs=new();HOLY.Inst.Thresholds=new();HOLY.Inst.Sacrifice.DrainChanges();
        }
        int Tick(double now) {
            var output=new Dictionary<int,bool>();HOLY.Inst.Process(f,new(),output,now);
            Check(output.Count<=1,"one requested action per sample");return output.Keys.SingleOrDefault();
        }
        void Edge()=>f[11]=Color.FromArgb(128,0,255);
        foreach(bool raid in new[]{false,true})
        foreach(double hp in new[]{.1,.49,.5,.59,.6,.8,1}) {
            Reset(hp,raid);Tick(0);Edge();Check(Tick(.1)==5,"Sacrifice requests Protection at hp="+hp+", raid="+raid);
        }
        Reset();Tick(0);Edge();f[6]=Color.Orange;Check(Tick(.1)==5,"unknown HP cannot block confirmed Sacrifice link");
        foreach(double hp in new[]{.49,.5,.59,.6,.61,.8,1}) {
            Reset(hp);Check(Tick(0)==(hp<.6?5:0),"independent low HP rule at "+hp);
            F(41);Check(Tick(.1)==(hp<.6?5:0),"raid low HP rule at "+hp);
        }
        foreach(bool raid in new[]{false,true})
        foreach(int id in new[]{1,2,19,96,100,30,40}) foreach(Color c in new[]{Color.Black,Color.Orange}) {
            Reset(raid:raid);Tick(0);Edge();f[id]=c;
            Check(Tick(.1)!=5,"Sacrifice does not bypass gate "+id+"="+c.Name);
        }
        Reset();Tick(0);Edge();f[41]=Color.Orange;Check(Tick(.1)!=5,"unknown mode blocks link");
        foreach(int id in new[]{1,2,96,100,30,40}) foreach(Color c in new[]{Color.Black,Color.Orange}) {
            Reset(.59);f[id]=c;Check(Tick(0)!=5,"low HP also retains gate "+id);
        }
        Reset();Tick(0);Edge();foreach(int id in new[]{3,4,5,9,20}) F(id,false);
        Check(Tick(.1)==5,"link ignores target, cast and GCD gates without sending stopcast");
        Reset();Tick(0);Edge();F(4,false);F(12);F(95);F(97);F(98);F(92);F(13);
        Check(Tick(.1)==5,"link takes priority over otherwise eligible Shield damage");
        Reset();F(46,false);f[52]=Color.FromArgb(179,255,0);
        Check(Tick(0)==102,"target selection pending setup");Edge();F(9,false);F(20,false);
        Check(Tick(.1)==5,"self Protection bypasses pending recipient acknowledgement");

        Reset();Check(Tick(0)==0,"ready baseline alone does not trigger");Edge();
        Check(Tick(.1)==5&&Tick(.2)==0&&Tick(.6)==5&&Tick(.7)==0&&Tick(1.1)==5&&Tick(1.6)==0&&Tick(2.1)==0,
            "bounded three requests spaced at least0.5s, expire at2s");
        Check(!HOLY.Inst.Sacrifice.Current.Holding,"timeout discards pending request");
        Check(Tick(2.2)==0,"continued Sacrifice cooldown never recreates request");
        Reset();Tick(0);Edge();Tick(.1);F(30,false);
        Check(Tick(.2)==0&&HOLY.Inst.Sacrifice.Status.Contains("已观察到圣佑进入冷却"),"cooldown observation confirms completion");
        F(30);Check(Tick(.3)==0,"Protection recovery alone does not rearm Sacrifice");
        f[11]=Color.Cyan;Tick(.4);Edge();Check(Tick(.5)==5,"new Sacrifice edge rearms exactly once");
        Reset();Tick(0);Edge();F(40,false);Check(Tick(.1)==0,"unusable Protection waits briefly");
        F(40);Check(Tick(.5)==5,"usability recovers within request window");
        Reset();Tick(0);Edge();F(30,false);Tick(.1);Tick(1);Tick(2);Tick(2.2);F(30);
        Check(Tick(2.3)==0,"no delayed cast after cooldown recovers outside request window");
        foreach(string reset in new[]{"gap","backward","invalid","unknown","off","reset","roster"}) {
            Reset();Tick(0);Edge();Tick(.1);F(40,false);
            double probeAt=.3;
            switch(reset) {
                case "gap":Tick(2);probeAt=2.1;break;
                case "backward":Tick(.05);probeAt=.1;break;
                case "invalid":HOLY.Inst.ObserveHealing(f,new(),.2,false);break;
                case "unknown":f[11]=Color.Orange;Tick(.2);Edge();break;
                case "off":HOLY.Inst.ObserveHealing(f,new(),.2,true,false);break;
                case "reset":HOLY.Inst.Reset();break;
                case "roster":HOLY.Inst.SetPartyRoster(PartyRunicPowerTests.Example);break;
            }
            // Probe immediately, within the original request lifetime. Waiting
            // until 2.3 would pass even if cleanup failed, solely due to timeout.
            F(40);Check(Tick(probeAt)!=5&&!HOLY.Inst.Sacrifice.Current.Holding,"no stale link immediately after "+reset);
            HOLY.Inst.SetPartyRoster(null);
        }
        foreach(var old in new[]{Color.White,Color.Black,Color.Orange}) {
            Reset();f[11]=old;Tick(0);Edge();Check(Tick(.1)==0,"old palette/unknown cannot arm a new cooldown edge");
        }
        Reset();Edge();Check(Tick(0)==0,"first sample already cooling is not a release");
        Reset();HOLY.Inst.ObserveHealing(f,new(),0,true,false);Edge();HOLY.Inst.ObserveHealing(f,new(),.1,true,false);
        HOLY.Inst.Reset();Check(Tick(.2)==0,"execution restart never replays disabled observations");

        Reset();HOLY.Inst.ObserveHealing(f,new(),0);Edge();HOLY.Inst.ObserveHealing(f,new(),.1);
        // Simulate manual-input grace period: capture continues, Process is withheld.
        HOLY.Inst.ObserveHealing(f,new(),.3);HOLY.Inst.ObserveHealing(f,new(),.6);
        var output=new Dictionary<int,bool>();HOLY.Inst.Process(f,new(),output);
        Check(output.Keys.SingleOrDefault()==5,"manual grace preserves observed edge and uses capture clock for requests");
        HOLY.Inst.ObserveHealing(f,new(),.7);output.Clear();HOLY.Inst.Process(f,new(),output);
        Check(!output.ContainsKey(5),"prepared sample retries use same timebase as capture");
        HOLY.Inst.ObserveHealing(f,new(),.8,true,true,false);HOLY.Inst.Sacrifice.DrainChanges();
        HOLY.Inst.ObserveHealing(f,new(),.9,true,true,false);
        Check(HOLY.Inst.Sacrifice.DrainChanges().Count==0,"background cleanup is stable without log oscillation");
        HOLY.Inst.ObserveHealing(f,new(),1);output.Clear();HOLY.Inst.Process(f,new(),output);
        Check(!output.ContainsKey(5),"return to foreground never replays old edge");
        HOLY.Inst.ObserveHealing(f,new(),1.1,false,true,false);HOLY.Inst.Sacrifice.DrainChanges();
        HOLY.Inst.ObserveHealing(f,new(),1.2,false,true,false);HOLY.Inst.Sacrifice.Reset("采样失效：联动已清除");
        Check(HOLY.Inst.Sacrifice.DrainChanges().Count==0,"simultaneous heartbeat loss and background do not alternate log reasons");
        Reset(.49);F(19,false);
        Check(HOLY.Decide(f,new(),cooldowns:new(Sacrifice:new(false,true))).Key==5,"stale group context cannot suppress solo low HP Protection");

        foreach(bool raid in new[]{false,true}) {
            Reset(raid:raid);HOLY.Inst.ObserveHealing(f,new(),0);
            Edge();HOLY.Inst.ObserveHealing(f,new(),.1);
            // The player manually casts Protection during the input grace period;
            // Process has not emitted a request yet.
            F(30,false);HOLY.Inst.ObserveHealing(f,new(),.2);
            Check(!HOLY.Inst.Sacrifice.Current.Holding&&HOLY.Inst.Sacrifice.Status.Contains("已观察到圣佑进入冷却"),
                "manual Protection cooldown ends pending link before first automatic request");
            F(30);Check(Tick(.3)==0,"ready recovery after manual Protection cannot replay the completed link");
            Reset(raid:raid);Tick(0);Edge();Tick(.1);Tick(1.1);F(30,false);
            Check(Tick(2.1)==0&&HOLY.Inst.Sacrifice.Status.Contains("已观察到圣佑进入冷却"),
                "confirmation on deadline is not logged as a failed request");

            Reset(raid:raid);Tick(0);Edge();f[6]=Color.Orange;
            foreach(int id in new[]{3,4,5,9,20}) F(id,false);
            Check(Tick(.1)==5,"group link ignores HP/target/GCD/cast, raid="+raid);
            Check(Tick(.2)==0&&Tick(.6)==5&&Tick(1.1)==5&&Tick(1.6)==0&&Tick(2.1)==0,
                "both group modes retain spacing, three attempts and timeout");
            Reset(raid:raid);Tick(0);Edge();Tick(.1);F(30,false);
            Check(Tick(.2)==0&&HOLY.Inst.Sacrifice.Status.Contains("已观察到圣佑进入冷却"),"both modes confirm cooldown");
            Reset(raid:raid);Tick(0);Edge();F(41,!raid);
            Check(Tick(.1)==0,"mode switch does not fabricate a release");
            f[11]=Color.Cyan;Tick(.2);Edge();Check(Tick(.3)==5,"new release works after mode switch");
            F(41,raid);Check(Tick(.4)==0&&!HOLY.Inst.Sacrifice.Current.Holding,"mode switch clears existing pending request");
            Reset(raid:raid);Tick(0);Tick(2);F(41,!raid);Edge();
            Check(Tick(2.1)==0,"mode identity is retained after gap recovery");
        }

        Reset();Tick(0);HOLY.Inst.Sacrifice.DrainChanges();
        Check(Tick(.05)==0&&HOLY.Inst.Sacrifice.DrainChanges().Count==0,"unchanged ready state does not spam");
        Edge();Tick(.1);var changes=HOLY.Inst.Sacrifice.DrainChanges();
        Check(changes.Any(c=>c.Message.Contains("建立2秒"))&&changes.Any(c=>c.Message.Contains("已请求圣佑第1次")),"event and emitted request both retained");
        Tick(.2);Check(HOLY.Inst.Sacrifice.DrainChanges().Count==0,"waiting for confirmation does not spam");
        F(30,false);Tick(.3);Check(HOLY.Inst.Sacrifice.DrainChanges().Count==1,"completion logged once");
        Tick(.4);Check(HOLY.Inst.Sacrifice.DrainChanges().Count==0,"completed cooldown stays quiet");
        Check(HolySacrificeReading.Ready(Color.White) is null&&HolySacrificeReading.Ready(Color.Black) is null,"legacy food is never accepted");
        Check(HolyBuffs.Decode(new(),HolyBuffs.Fields().Single(v=>v.Id=="11"),Color.Cyan).Contains("牺牲就绪"),"UI decodes dedicated palette");

        // Exercise the real execution gate, with synthetic pixels and an injected
        // dispatcher. This does not send input or depend on a real game window.
        using var pattern=DiagnosticTests.Pattern(1);
        var layout=Calibration.Find(pattern,new Size(1600,900));layout.SpecId=65;
        using var capture=new Captured((Bitmap)pattern.Clone(),new Rectangle(Point.Empty,pattern.Size));
        var game=new GameWindow(1,(IntPtr)42,"Sacrifice synthetic");
        bool manual=false,foreground=true,loseFocusOnDispatch=false;
        int focusReads=0,emitted=0;
        var runner=new RotationExecution(
            ()=>foreground&&!(loseFocusOnDispatch&&++focusReads>=2)?game.Handle:(IntPtr)99,
            ()=>manual,(frames,bars)=>{
                var actions=new Dictionary<int,bool>();HOLY.Inst.Process(frames,bars,actions);
                if(actions.ContainsKey(5)) emitted++;
            });
        void CaptureTick(double now,bool live=true) {
            for(int i=1;i<=100;i++) {
                var point=layout.Frames[i-1];capture.Image.SetPixel(point.X,point.Y,f[i]);
            }
            focusReads=0;
            HOLY.Inst.ObserveHealing(f,new(),now,live,runner.Enabled,foreground);
            runner.Tick(game,layout,capture,live,now,ClassProfiles.Find(65));
        }
        foreach(bool raid in new[]{false,true}) {
            Reset(raid:raid);runner.Start(ClassProfiles.Find(65));emitted=0;
            CaptureTick(0);manual=true;Edge();CaptureTick(.1);
            F(30,false);CaptureTick(.2);manual=false;F(30);CaptureTick(.8);
            Check(emitted==0&&!HOLY.Inst.Sacrifice.Current.Holding,
                "real execution grace observes manual Protection and never sends a redundant key");

            f[11]=Color.Cyan;CaptureTick(1);manual=true;Edge();CaptureTick(1.1);
            manual=false;CaptureTick(1.2);Check(emitted==0,"manual grace blocks automatic dispatch");
            CaptureTick(1.7);Check(emitted==1,"unfulfilled link dispatches once after real manual grace");
            F(41,!raid);CaptureTick(1.8);Check(emitted==1&&!HOLY.Inst.Sacrifice.Current.Holding,
                "mode conversion during retry clears pending state through execution gate");

            f[11]=Color.Cyan;CaptureTick(2);Edge();CaptureTick(2.1);Check(emitted==2,"fresh release works in converted mode");
            runner.Stop();CaptureTick(2.2);runner.Start(ClassProfiles.Find(65));CaptureTick(2.3);
            Check(emitted==2&&!HOLY.Inst.Sacrifice.Current.Holding,"Stop/Start cannot replay the old captured cooldown");

            f[11]=Color.Cyan;CaptureTick(3);Edge();loseFocusOnDispatch=true;CaptureTick(3.1);
            Check(emitted==2&&!HOLY.Inst.Sacrifice.Current.Holding,"focus lost at final dispatch gate cancels pending link");
            loseFocusOnDispatch=false;CaptureTick(3.2);Check(emitted==2,"focus recovery does not reuse canceled observation");
            f[11]=Color.Cyan;CaptureTick(4);Edge();CaptureTick(4.1);Check(emitted==3,"subsequent genuine edge still works");
            CaptureTick(4.2,false);CaptureTick(4.3);Check(emitted==3&&!HOLY.Inst.Sacrifice.Current.Holding,
                "heartbeat failure clears request before next valid frame");
            runner.Stop();
        }
        HOLY.Inst.Reset();HOLY.Inst.Sacrifice.DrainChanges();return count;
    }
}
