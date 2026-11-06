#nullable enable
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RotationUS.Diagnostics;

internal static class ExpandedProtocolTests
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL expanded layout: " + label); count++; }
        foreach (double scale in new[] { .8, 1, 1.142, 1.25, 1.333, 1.5, 2 })
        foreach (int offset in new[] { 0, 3, 17 })
        {
            using var image = DiagnosticTests.Pattern(scale, offset, 2, 2560);
            var layout = Calibration.Find(image, new Size(2560, 1440));
            Check(layout.CurrentProtocol && PositionVerification.Check(layout, p => image.GetPixel(p.X,p.Y)).All(c => c.Passed), "100+100 centers and fixed markers " + scale);
            Check(layout.Frames[99].X < offset + 530 * scale && Math.Abs(layout.Markers[0].X - (offset + 745 * scale)) <= 2,
                "100 cells fit before unchanged large region " + scale);
            var previous = layout.Copy(); previous.Version=1; previous.CellWidth=10;
            previous.Frames=Enumerable.Range(1,50).Select(i=>new SamplePoint(offset+
                ((int)Math.Round(i*10*scale)+(int)Math.Round((i+1)*10*scale)-1)/2,2)).ToArray();
            previous.Bars=previous.Frames.Select(p=>p with { Y=2+(int)Math.Round(scale) }).ToArray();
            int oldLeft=offset+(int)Math.Round(10*scale);
            double oldCell=((int)Math.Round(510*scale)-(int)Math.Round(10*scale))/50.0;
            previous.Markers=Enumerable.Range(0,3).Select(i=>new SamplePoint((int)Math.Floor(oldLeft+(73.5+i)*oldCell),2)).ToArray();
            Check(PositionVerification.CompatibleSpecialGeometry(previous,layout),"preserve fixed-area points across pixel rounding " + scale);
        }
        foreach (var profile in ClassProfiles.All)
        {
            for (int id = 51; id <= 90; id++)
                Check(profile.Fields.Single(f => f.Id == id.ToString()).Kind == ValueKind.TeamHealth, profile.Key + " shared HP " + id);
            for (int id = 91; id <= 100; id++)
                Check(profile.Fields.Single(f => f.Id == id.ToString()).Kind == (profile.SpecId==65?ValueKind.FuryBoolean:ValueKind.Raw), profile.Key + " tail " + id);
        }
        Check(TeamHealthReading.Read(Color.FromArgb(0,255,0)) == new TeamHealthReading(TeamHealthStatus.Available,0), "zero health differs from empty");
        Check(TeamHealthReading.Read(Color.FromArgb(128,255,0)).Percent == 128/255.0, "HP byte decodes without using green as percent");
        Check(TeamHealthReading.Read(Color.Black).Status == TeamHealthStatus.Empty, "empty slot");
        Check(TeamHealthReading.Read(Color.Blue).Status == TeamHealthStatus.Offline, "offline slot");
        Check(TeamHealthReading.Read(Color.Orange).Status == TeamHealthStatus.Unknown, "failed health never counts as alive/dead");
        Check(TeamHealthReading.Read(Color.White).Status == TeamHealthStatus.Unknown, "invalid HP marker rejected");

        using var pattern = DiagnosticTests.Pattern(1);
        var current = Calibration.Find(pattern, new Size(1600,900));
        var old = current.Copy(); old.Version = 1; old.CellWidth = 10;
        old.Frames = Enumerable.Range(1,50).Select(i => new SamplePoint(i*10+4,0)).ToArray();
        old.Bars = old.Frames.Select(p => p with { Y=1 }).ToArray();
        old.SpecialEnabled = true; old.Special = new(610,8); old.SpecialReferenceColor = new(144,92,18);
        old.Validate();
        Check(PositionVerification.CompatibleSpecialGeometry(old,current), "only cell shrink/count migration preserves fixed-area points");
        var shifted = current.Copy(); shifted.Markers = shifted.Markers.Select(p => p with { X=p.X+10 }).ToArray();
        Check(!PositionVerification.CompatibleSpecialGeometry(old,shifted), "large-area shift requires confirming special points");
        using var scaled = DiagnosticTests.Pattern(1.25);
        Check(!PositionVerification.CompatibleSpecialGeometry(old,Calibration.Find(scaled,new Size(1600,900))), "changed UI scale invalidates old special points");
        string directory = Path.Combine(Path.GetTempPath(),"hiji-expanded-"+Guid.NewGuid().ToString("N"));
        var profiles = new ProfileLayouts(directory);
        try
        {
            foreach (int spec in new[] { 71,72,73 })
            {
                var profile = ClassProfiles.Find(spec)!; var saved = old.Copy(); saved.SpecId = spec;
                saved.SpecialPoints[spec == 71 ? "rend10" : "enrage"] = new() { Enabled=true, Point=new(550,8), Comparison="expiring", ReferenceColor=new(144,92,18) };
                profiles.Save(profile,saved);
                var migrated = profiles.Load(profile,current);
                Check(migrated.CurrentProtocol && migrated.SpecialPoints.Values.All(p => p.Enabled && p.ReferenceColor is not null), "retains " + profile.Key + " special-point RGB during upgrade");
                if (spec == 73) Check(migrated.SpecialEnabled && migrated.Special == old.Special && migrated.SpecialReferenceColor == old.SpecialReferenceColor, "Ignore Pain moves logical id without moving pixel");
                profiles.Save(profile,migrated);
                Check(DiagnosticLayout.Load(profiles.PathFor(profile)).CurrentProtocol, "saves new layout version " + profile.Key);
            }
            var document = JsonNode.Parse(JsonSerializer.Serialize(old))!.AsObject(); document.Remove("CellWidth");
            string path = Path.Combine(directory,"legacy.json"); File.WriteAllText(path,document.ToJsonString());
            Check(DiagnosticLayout.Load(path).CellWidth == 10, "reads original v1 JSON without width metadata");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory,"*.json",SearchOption.AllDirectories)) File.Delete(file);
                foreach (var child in Directory.GetDirectories(directory)) Directory.Delete(child);
                Directory.Delete(directory);
            }
        }

        using var live = (Bitmap)pattern.Clone();
        for (int id=51;id<=90;id++) live.SetPixel(current.Frames[id-1].X,0,Color.FromArgb(id-51,255,0));
        current.SpecialEnabled=true; current.Special=new(610,8); live.SetPixel(610,8,Color.Black);
        Dictionary<int,Color>? observed=null;
        var runner = new RotationExecution(()=>(IntPtr)42,()=>false,(frames,_)=>observed=new(frames)); runner.Start();
        using var sample = new Captured((Bitmap)live.Clone(),new Rectangle(Point.Empty,live.Size));
        var game = new GameWindow(1,(IntPtr)42,"Synthetic"); runner.Tick(game,current,sample,true,0);
        Check(observed?.Count == 101 && observed[51].G == 255 && observed[101].ToArgb() == Color.Black.ToArgb(), "HP 51 and special 101 coexist in execution");
        current.SpecialEnabled=false; runner.Tick(game,current,sample,true,1);
        Check(observed?.Count == 100 && observed.ContainsKey(51) && !observed.ContainsKey(101), "disabling special leaves all 40 HP cells intact");
        runner.Tick(game,old,sample,true,2);
        Check(!runner.Enabled, "old 50-cell layout never executes under new protocol");
        foreach (int spec in new[] { 71,72 })
        {
            var layout=current.Copy(); layout.SpecId=spec;
            var points=spec==71?ArmsBuffs.Points:FuryBuffs.Points;
            for(int i=0;i<points.Length;i++)
            {
                var point=new SamplePoint(540+i*20,8);
                layout.SpecialPoints[points[i].Key]=new(){Enabled=true,Point=point};
                var field=ClassProfiles.Find(spec)!.Fields.Single(f=>f.Id==(101+i).ToString());
                Check(FuryBuffs.Point(layout,field)==point,"numeric special id maps to saved key " + spec + ":" + (101+i));
            }
            Check(FuryBuffs.Point(layout,ClassProfiles.Find(spec)!.Fields.Single(f=>f.Id=="51"))==layout.Frames[50], "warrior HP51 resolves ordinary cell " + spec);
        }
        return count;
    }
}
