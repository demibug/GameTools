#nullable enable
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

internal static class PartyRunicPowerTests
{
    internal static PartyRosterSnapshot Example => new(123,456,1,new[] {
        new PartyMember(1,2,65,2,true),new PartyMember(2,6,250,1,true),new PartyMember(3,8,63,3,true),
        new PartyMember(4,1,0,3,true),new PartyMember(0,0,0,0,false)});
    internal static Dictionary<int,Color> Packet(PartyRosterSnapshot roster, Color power, int revision=7)
    {
        Color N(int n)=>Color.FromArgb(n&255,(n>>8)&255,(n>>16)&255);
        var p=PartyRosterTests.Packet(roster);
        p[73]=N(revision);
        p[77]=p[94]=Color.FromArgb(72,82,1);
        p[78]=N(roster.SessionA);p[79]=N(roster.SessionB);p[80]=p[93]=N(roster.Epoch);p[81]=p[92]=N(revision);
        for(int i=0;i<5;i++) {p[82+i*2]=N(roster.Members[i].Id);p[83+i*2]=Color.Orange;}
        p[85]=power;return p;
    }
    internal static int Run()
    {
        int count=0;void Check(bool ok,string why) {if(!ok) throw new Exception("FAIL PartyRunicPower: "+why);count++;}
        var roster=Example;var cache=new PartyRunicPowerCache();var pixels=Packet(roster,Color.White);
        PartyRunicPower? Read(double now=0,PartyRosterSnapshot? source=null)=>cache.Observe(i=>pixels[i],source??roster,now);
        var enough=Read();Check(enough?.At(roster,2)==true,"45-point signal accepted for confirmed blood tank");
        Check(enough?.At(roster,1) is null,"non-blood never acquires power signal");
        Check(Read(.49)?.At(roster,2)==true,"same frame before expiry");
        Check(Read(.51) is null,"frozen power expires before heartbeat");
        pixels[94]=Color.Black;Check(Read(.6) is null,"invalid header");
        pixels[94]=Color.FromArgb(72,82,1);Check(Read(.7) is null,"invalid interruption cannot refresh stale revision");
        pixels=Packet(roster,Color.Black,8);Check(Read(.8)?.At(roster,2)==false,"fresh revision restores below-threshold observation");
        pixels=Packet(roster,Color.Orange,9);Check(Read(.9)?.At(roster,2) is null,"unknown never means enough");
        foreach(int id in Enumerable.Range(77,18).Where(i=>i<83 || i>91 || i%2==0)) {
            pixels=Packet(roster,Color.White,10);var c=pixels[id];pixels[id]=Color.FromArgb(c.R^1,c.G,c.B);
            Check(Read(1) is null,"corrupt public header rejected at "+id);
        }
        pixels=Packet(roster,Color.White,11);pixels[73]=Color.Black;
        Check(Read(1.1) is null,"different metadata revision cannot share energy");
        pixels=Packet(roster,Color.White,12);
        Check(Read(1.2,roster with {Epoch=2}) is null,"new roster cannot inherit energy");
        Check(Read(1.2,roster with {SessionB=888}) is null,"reload cannot inherit energy");
        var replacement=roster with {Members=roster.Members.Select((m,i)=>i==1?m with {Id=20}:m).ToArray()};
        Check(Read(1.2,replacement) is null,"slot replacement cannot inherit energy");
        Check(enough?.At(replacement,2) is null,"snapshot itself is identity-bound");
        cache.Observe(i=>pixels[i],null,1.3);Check(cache.Current is null,"invalid roster clears energy");
        pixels=Packet(roster,Color.White,13);Read(2);Check(Read(1.9) is null,"backwards clock clears");
        cache.Reset();Check(cache.Current is null,"explicit game/window reset clears");
        string? fixture=Environment.GetEnvironmentVariable("HIJI_PARTY_POWER_FIXTURE");
        if(!string.IsNullOrEmpty(fixture)) {
            var data=JsonSerializer.Deserialize<Dictionary<string,int[][]>>(File.ReadAllText(fixture))!;
            Color Lua(int id) {var c=id<77?data["roster"][id-51]:data["power"][id-77];return Color.FromArgb(c[0],c[1],c[2]);}
            var decoded=PartyRosterProtocol.Decode(Lua);
            Check(decoded is {Mode:2},"actual Lua metadata decodes");
            Check(cache.Observe(Lua,decoded!.Value.Snapshot,3)?.At(decoded.Value.Snapshot,2)==true,"actual native-rendered Lua power crosses production C# decoder");
        }
        var t=new HealingThresholds();
        Check(PartyHealingPriority.Compare(.45,2,.48,3,t,roster,enough)>0,"blood 45%, DPS 48% prioritizes DPS");
        Check(PartyHealingPriority.Compare(.45,2,.48,3,t,roster)<0,"unknown energy restores normal tank bonus");
        Check(PartyHealingPriority.BloodReduced(.40,2,roster,enough),"exact 40% eligible");
        Check(!PartyHealingPriority.BloodReduced(.399,2,roster,enough),"below 40% protected");
        Check(PartyHealingPriority.Compare(.35,2,.38,3,t,roster,enough)<0,"35% blood tank retains tank priority");
        Check(PartyHealingPriority.Compare(.18,2,.15,3,t,roster,enough)>0,"critical rescue uses actual health");
        Check(PartyHealingPriority.Compare(.59,2,.60,3,t,roster,enough)<0,"discount cannot cross urgency tiers");
        foreach(var m in new[]{roster.Members[1] with {SpecId=0},roster.Members[1] with {ClassId=0},roster.Members[1] with {SpecId=251},roster.Members[1] with {Online=false}})
            Check(!m.IsBloodTank,"missing spec/class, other spec, offline never qualifies");

        Dictionary<int,Color> frames=new();
        void Hp(int slot,double hp)=>frames[50+slot]=Color.FromArgb((int)Math.Round(hp*255),255,0);
        void Setup() {
            frames=Enumerable.Range(1,100).ToDictionary(i=>i,_=>Color.Black);
            foreach(int id in new[]{1,2,3,4,5,9,10,19,20,42,43,44,45,96,100}) frames[id]=Color.White;
            frames[6]=frames[7]=Color.White;frames[8]=Color.FromArgb(204,204,204);
            for(int slot=1;slot<=5;slot++) Hp(slot,1);Hp(2,.45);Hp(3,.48);
        }
        var buffs=new HolyBuffs {Infusion=false,HandOfDivinity=false,DivinePurpose=false};
        Setup();Check(HOLY.Decide(frames,buffs,party:roster,power:enough).Key==103,"actual selection respects discount");
        Check(HOLY.Decide(frames,buffs,party:roster).Key==102,"actual selection fallback on missing energy");
        Hp(2,.35);Check(HOLY.Decide(frames,buffs,party:roster,power:enough).Key==102,"actual selection protects low tank");
        Setup();Hp(3,1);Check(HOLY.Decide(frames,buffs,party:roster,power:enough).Key==102,"only injured blood tank still healed");
        Setup();frames[41]=Color.White;
        Check(HOLY.Decide(frames,buffs,party:roster,power:enough)==HOLY.Decide(frames,buffs),"raid unchanged even with enough RP");
        Setup();HOLY.Inst.SetPartyRoster(roster);HOLY.Inst.SetRunicPower(enough);HOLY.Inst.Buffs=buffs;
        var states=new Dictionary<int,bool>();HOLY.Inst.Process(frames,new(),states);
        Check(states.ContainsKey(103),"live Process selects DPS with enough power");
        states.Clear();HOLY.Inst.SetRunicPower(null);HOLY.Inst.Process(frames,new(),states);
        Check(states.ContainsKey(102),"energy loss cancels pending obsolete selection");
        HOLY.Inst.SetRunicPower(enough);states.Clear();HOLY.Inst.Process(frames,new(),states);
        Hp(2,.35);states.Clear();HOLY.Inst.Process(frames,new(),states);
        Check(states.ContainsKey(102),"40% protection cancels pending DPS selection immediately");
        // RP changes, expiry, and protection-line crossings must not skip the
        // two-sample acknowledgement when the recipient stays the same.
        var lowPower=new PartyRunicPower(roster,new bool?[]{null,false,null,null,null});
        foreach(var scenario in new[]{"protected","only tank","expiry","protection crossing"}) {
            Setup();HOLY.Inst.Reset();
            Hp(2,scenario=="protected"?.35:.45);Hp(3,1);
            frames[22]=frames[32]=Color.White;
            frames[8]=Color.FromArgb(89,89,89);
            HOLY.Inst.SetRunicPower(scenario is "expiry" or "protection crossing"?enough:lowPower);
            states.Clear();HOLY.Inst.Process(frames,new(),states);
            Check(states.ContainsKey(102),scenario+": request tank selection");
            frames[47]=Color.White;
            if(scenario=="protection crossing") Hp(2,.35);
            HOLY.Inst.SetRunicPower(scenario=="expiry"?null:enough);
            states.Clear();HOLY.Inst.Process(frames,new(),states);
            Check(states.Count==0 && HOLY.Inst.LastDecision.Reason.Contains("第二次目标确认"),scenario+": first acknowledgement cannot heal");
            states.Clear();HOLY.Inst.Process(frames,new(),states);
            Check(states.ContainsKey(8),scenario+": second acknowledgement permits heal");
        }
        Setup();HOLY.Inst.Reset();HOLY.Inst.SetRunicPower(enough);
        states.Clear();HOLY.Inst.Process(frames,new(),states);
        Check(states.ContainsKey(103),"GCD case starts with DPS selection");
        frames[20]=Color.Black;HOLY.Inst.SetRunicPower(null);
        states.Clear();HOLY.Inst.Process(frames,new(),states);
        Check(states.Count==0,"energy loss during GCD cannot send a selection");
        frames[20]=Color.White;
        states.Clear();HOLY.Inst.Process(frames,new(),states);
        Check(states.ContainsKey(102),"priority re-evaluation survives GCD wait");
        frames[47]=Color.White;frames[22]=frames[32]=Color.White;
        states.Clear();HOLY.Inst.Process(frames,new(),states);
        Check(states.Count==0,"replacement recipient still requires two acknowledgements");
        states.Clear();HOLY.Inst.Process(frames,new(),states);
        Check(states.ContainsKey(8),"replacement recipient heals after second acknowledgement");
        // The preferred recipient can revert to the current target before the
        // old select key is reflected in pixels. Reselect, then acknowledge it.
        foreach(string scenario in new[]{"unknown", "below45", "protection", "critical", "GCD", "no heal ready", "late old acknowledgement", "emergency toll", "self shock", "self macro"}) {
            Setup();HOLY.Inst.Reset();HOLY.Inst.SetRunicPower(enough);
            frames[47]=Color.White;frames[8]=Color.FromArgb(115,115,115);
            bool self=scenario is "self shock" or "self macro";
            if(self) {frames[47]=Color.Black;frames[46]=Color.White;frames[8]=Color.White;}
            if(scenario!="no heal ready") frames[22]=frames[32]=Color.White;
            states.Clear();HOLY.Inst.Process(frames,new(),states);
            Check(states.Count==1 && states.ContainsKey(103),scenario+": request DPS while targeting blood tank");
            if(scenario is "protection" or "critical") {
                int hp=scenario=="critical"?46:89;
                frames[52]=Color.FromArgb(hp,255,0);frames[8]=Color.FromArgb(hp,hp,hp);
            } else HOLY.Inst.SetRunicPower(scenario=="below45"?lowPower:null);
            if(scenario=="emergency toll") {
                Hp(2,.18);Hp(3,.19);frames[8]=Color.FromArgb(46,46,46);
                frames[24]=frames[34]=Color.White;
            }
            if(self) {
                Hp(1,.15);frames[6]=frames[8]=Color.FromArgb(38,38,38);
                if(scenario=="self macro") {
                    frames[23]=frames[33]=frames[91]=frames[92]=Color.White;
                    states.Clear();HOLY.Inst.Process(frames,new(),states);
                    Check(states.Count==1 && states.ContainsKey(2),"self emergency macro still bypasses recipient acknowledgement");
                    frames[23]=frames[33]=frames[91]=frames[92]=Color.Black;
                }
            }
            if(scenario=="GCD") {
                frames[20]=Color.Black;
                states.Clear();HOLY.Inst.Process(frames,new(),states);
                Check(states.Count==0,"current-recipient reselection waits through GCD");
                frames[20]=Color.White;
            }
            states.Clear();HOLY.Inst.Process(frames,new(),states);
            Check(states.Count==1 && states.ContainsKey(self?101:102),scenario+": explicitly reselect current recipient without healing or waiting for old DPS");
            states.Clear();HOLY.Inst.Process(frames,new(),states);
            Check(states.Count==0 && HOLY.Inst.LastDecision.Reason.Contains("第二次目标确认"),scenario+": first tank acknowledgement waits");
            if(scenario=="late old acknowledgement") {
                frames[47]=Color.Black;frames[48]=Color.White;
                states.Clear();HOLY.Inst.Process(frames,new(),states);
                Check(states.Count==0 && HOLY.Inst.LastDecision.Reason=="等待目标切换确认","late obsolete target cannot release healing");
                frames[47]=Color.White;frames[48]=Color.Black;
                states.Clear();HOLY.Inst.Process(frames,new(),states);
                Check(states.Count==0,"late obsolete target breaks consecutive confirmations");
            }
            states.Clear();HOLY.Inst.Process(frames,new(),states);
            Check(scenario=="no heal ready"?states.Count==0:states.Count==1 && states.ContainsKey(scenario=="emergency toll"?4:8),scenario+": second confirmation respects heal readiness");
            if(scenario=="no heal ready") {
                frames[22]=frames[32]=Color.White;
                states.Clear();HOLY.Inst.Process(frames,new(),states);
                Check(states.ContainsKey(8),"confirmed current recipient heals when skill becomes ready");
            }
        }
        HOLY.Inst.SetPartyRoster(null);HOLY.Inst.Reset();
        using var panel=new PartyRosterPanel();
        panel.Present(roster,enough,slot=>frames[50+slot],true,"战斗中","实时");
        Check(panel.Members.Rows[1].Cells[1].Value?.ToString()=="死亡骑士" && panel.Members.Rows[1].Cells[4].Value?.ToString()=="T：专精确认","panel shows class and identification source");
        Check(panel.Members.Rows[1].Cells[7].Value?.ToString()=="≥45（实时）","panel shows threshold not invented exact energy");
        panel.Clear("换人");Check(panel.Members.Rows[1].Cells[1].Value?.ToString()=="—" && panel.Members.Rows[1].Cells[7].Value?.ToString()=="—","panel clears old identity and RP together");
        return count;
    }
    internal static void Render(string path)
    {
        using var pattern=DiagnosticTests.Pattern(1);var layout=Calibration.Find(pattern,new Size(1600,900));layout.SpecId=65;
        using var pixels=new Bitmap(800,20);pixels.SetPixel(layout.Markers[1].X,0,ClassProfiles.Encode(65,2));
        string directory=Path.Combine(Path.GetTempPath(),"hiji-party-ui-"+Guid.NewGuid().ToString("N"));
        var profiles=new ProfileLayouts(directory);profiles.Save(ClassProfiles.Find(65)!,layout);
        using var form=new DiagnosticForm(false,profileLayouts:profiles);
        form.Show();Application.DoEvents();form.FreezeForTesting();form.ShowSynthetic(layout,pixels);
        static IEnumerable<Control> All(Control p)=>p.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(All(c)));
        var tabs=All(form).OfType<TabControl>().Single(t=>t.Name=="diagnosticTabs");
        tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().Single(t=>t.Name=="partyRosterPage");
        var panel=All(form).OfType<PartyRosterPanel>().Single();
        var roster=Example;var power=new PartyRunicPower(roster,new bool?[]{null,true,null,null,null});
        Color Hp(int slot)=>Color.FromArgb(slot==2?115:slot==3?122:255,255,0);
        panel.Present(roster,power,Hp,true,"战斗中：沿用脱战确认资料","合成验证画面 · 实时能量已接收（45点门槛）");
        foreach(var size in new[]{new Size(1180,800),new Size(950,650)}) {
            form.ClientSize=size;form.Refresh();Application.DoEvents();
            var last=panel.Members.GetRowDisplayRectangle(4,false);
            if(!panel.Visible || last.Bottom>panel.Members.ClientSize.Height || last.Height==0)
                throw new Exception($"Party rows clipped at {size}: last={last}, grid={panel.Members.ClientSize}");
            if(panel.Members.Rows[1].Cells[8].Value?.ToString()!="血DK降权（排序+5）") throw new Exception("Discount explanation missing");
            using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
            image.Save(size.Width==1180?path:Path.ChangeExtension(path,"small.png"),System.Drawing.Imaging.ImageFormat.Png);
        }
        form.Close();
        foreach(string file in Directory.GetFiles(Path.Combine(directory,"profiles"))) File.Delete(file);
        Directory.Delete(Path.Combine(directory,"profiles"));Directory.Delete(directory);
        Console.WriteLine("PASS: party panel shows all five members at 1180x800 and 950x650; "+path);
    }
}
