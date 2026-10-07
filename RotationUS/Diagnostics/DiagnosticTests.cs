#nullable enable
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

internal static class DiagnosticTests
{
    internal static Bitmap Pattern(double scale, int left = 0, int top = 0, int width = 1600, string markerLayout = MarkerLayouts.Wide)
    {
        var image = new Bitmap(width, 96);
        using var g = Graphics.FromImage(image); g.Clear(Color.Black);
        Color[] row = { Color.Lime, Color.Blue, Color.Red };
        for (int i = 1; i <= 50; i++)
        {
            int x = left + (int)Math.Round(i * 10 * scale), right = left + (int)Math.Round((i + 1) * 10 * scale);
            int firstEnd = top + (int)Math.Round(scale), secondEnd = top + (int)Math.Round(scale * 2);
            using var brush = new SolidBrush(row[(i - 1) % 3]); g.FillRectangle(brush, x, top, right - x, firstEnd - top);
            using var lower = new SolidBrush(i % 2 == 1 ? Color.Magenta : Color.Cyan); g.FillRectangle(lower, x, firstEnd, right - x, secondEnd - firstEnd);
        }
        Color[] marker = { Color.Red, Color.Blue, Color.Magenta };
        for (int i = 0; i < 3; i++)
        {
            double origin = (MarkerLayouts.Offset(markerLayout) + .5) * 10;
            int x = left + (int)Math.Round((origin + i * 10) * scale), right = left + (int)Math.Round((origin + 10 + i * 10) * scale);
            using var brush = new SolidBrush(marker[i]); g.FillRectangle(brush, x, top, right - x, Math.Max(1, (int)Math.Round(3 * scale)));
        }
        return image;
    }
    public static void Run()
    {
        int count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); count++; }
        void Reject(Action action, string name)
        {
            try { action(); }
            catch (InvalidDataException) { count++; return; }
            throw new Exception("FAIL: accepted " + name);
        }
        foreach (double scale in new[] { .8, 1, 1.142, 1.25, 1.5, 2 })
        {
            using var image = Pattern(scale, 3, 2);
            var layout = Calibration.Find(image, new Size(1600, 900));
            Check(layout.Frames.Length == 50 && layout.Bars.Length == 50, "50+50 scaled points " + scale);
            Check(layout.Frames.Select((p, i) => Colors.Kind(image.GetPixel(p.X, p.Y)) == new[] { 2, 1, 4 }[i % 3]).All(v => v), "centers read upper pattern " + scale);
            Check(layout.Bars.Select((p, i) => Colors.Kind(image.GetPixel(p.X, p.Y)) == (i % 2 == 0 ? 5 : 3)).All(v => v), "second row centers " + scale);
            Check(Colors.Spec(image.GetPixel(layout.Markers[1].X, layout.Markers[1].Y)) == 73, "spec marker " + scale);
        }
        using (var blank = new Bitmap(1600, 96)) Reject(() => Calibration.Find(blank, new Size(1600, 900)), "blank screen");
        using (var incomplete = Pattern(1))
        {
            using var g = Graphics.FromImage(incomplete); g.FillRectangle(Brushes.Black, 500, 0, 10, 2);
            Reject(() => Calibration.Find(incomplete, new Size(1600, 900)), "missing final point");
        }
        using (var missingBars = Pattern(1))
        {
            using var g = Graphics.FromImage(missingBars); g.FillRectangle(Brushes.Black, 0, 1, 520, 1);
            Reject(() => Calibration.Find(missingBars, new Size(1600, 900)), "missing second row");
        }
        using (var live = Pattern(1))
        {
            using var g = Graphics.FromImage(live); g.FillRectangle(Brushes.Lime, 740, 0, 10, 3);
            Reject(() => Calibration.Find(live, new Size(1600, 900)), "live mode not calibration");
        }
        using var pattern = Pattern(1);
        Check(ClassProfiles.All.Length == 11 && ClassProfiles.All.Select(p => p.SpecId).Distinct().Count() == 11,
            "eleven distinct specialization implementations registered");
        foreach (var profile in ClassProfiles.All)
        {
            var identity = ClassProfiles.Identify(ClassProfiles.Encode(profile.SpecId, profile.ClassId));
            Check(identity.SpecId == profile.SpecId && identity.ClassId == profile.ClassId && identity.DisplayName == profile.DisplayName,
                "class/spec pixel round-trip " + profile.Key);
            Check(profile.Process.Target!.GetType().Name.Equals(profile.Key, StringComparison.OrdinalIgnoreCase),
                "dispatch selects matching existing class implementation " + profile.Key);
            Check(profile.Fields.Select(f => f.Id).Distinct().Count() == profile.Fields.Length,
                "no duplicate fields in " + profile.Key);
            var frames = Enumerable.Range(1, 51).ToDictionary(i => i, _ => Color.Black);
            var bars = Enumerable.Range(1, 50).ToDictionary(i => i, _ => Color.Black);
            profile.Process(frames, bars, new Dictionary<int, bool>());
            Check(true, "existing logic consumes matching protocol " + profile.Key);
        }
        Check(ClassProfiles.Find(103)!.Fields[6].Name == "能量比例" && ClassProfiles.Find(104)!.Fields[6].Name == "怒气比例"
            && ClassProfiles.Find(260)!.Fields[5].Name == "能量比例", "resources use class-specific indexes and names");
        Check(ClassProfiles.Find(62) is null && ClassProfiles.Identify(ClassProfiles.Encode(62, 8)).ClassId == 8,
            "unsupported class can be identified without an execution route");
        var example = Calibration.Find(pattern, new Size(1600, 900));
        example.SpecialEnabled = true; example.Special = new(900, 35);
        Check(example.CaptureBounds.Contains(new Point(900, 35)), "capture includes custom special point");
        example.Special = new(1600, 0); Reject(example.Validate, "out of bounds special point");
        example.SpecialEnabled = false; example.Special = new(610, 8); example.Validate();
        var normal = example.Frames[1]; example.Frames[1] = example.Frames[0]; Reject(example.Validate, "duplicate points"); example.Frames[1] = normal;
        var path = Path.GetTempFileName();
        try { example.Save(path); var restored = DiagnosticLayout.Load(path); Check(restored.Frames.SequenceEqual(example.Frames) && restored.Bars.SequenceEqual(example.Bars), "layout persistence"); }
        finally { File.Delete(path); }
        string configDirectory = Path.Combine(Path.GetTempPath(), "RotationUS-profiles-" + Guid.NewGuid().ToString("N"));
        var configs = new ProfileLayouts(configDirectory);
        try
        {
            var legacy = example.Copy(); legacy.SpecialEnabled = true; legacy.Special = new(610, 8); legacy.SpecialReferenceColor = new(68,43,7);
            Directory.CreateDirectory(configDirectory); legacy.Save(configs.DiscoveryPath);
            var firstCat = configs.Load(ClassProfiles.Find(103)!, legacy);
            configs.SaveGeometry(firstCat);
            var migrated = DiagnosticLayout.Load(configs.PathFor(ClassProfiles.Protection));
            Check(migrated.SpecialEnabled && migrated.SpecId == 73 && migrated.SpecialReferenceColor == legacy.SpecialReferenceColor,
                "first launch on another class preserves legacy protection point before common layout is overwritten");
            var fz = configs.Load(ClassProfiles.Protection, legacy);
            Check(fz.SpecialEnabled && fz.SpecId == 73 && fz.SpecialReferenceColor == legacy.SpecialReferenceColor,
                "old protection special point migrates intact");
            configs.Save(ClassProfiles.Protection, fz);
            var catProfile = ClassProfiles.Find(103)!;
            var cat = configs.Load(catProfile, fz);
            Check(cat.SpecId == 103 && !cat.SpecialEnabled && cat.SpecialReferenceColor is null, "cat does not inherit Ignore Pain config");
            configs.Save(catProfile, cat);
            var back = configs.Load(ClassProfiles.Protection, cat);
            Check(back.SpecialEnabled && back.SpecialReferenceColor == legacy.SpecialReferenceColor,
                "switching back restores protection-specific special point");
            Reject(() => configs.Save(catProfile, back), "cross-specialization config save");
            var shifted = cat.Copy(); shifted.Frames[0] = new(shifted.Frames[0].X + 1, shifted.Frames[0].Y);
            Check(!configs.Load(ClassProfiles.Protection, shifted).SpecialEnabled, "geometry change invalidates saved special point");
            configs.SaveGeometry(back);
            Check(!DiagnosticLayout.Load(configs.DiscoveryPath).SpecialEnabled, "shared discovery file excludes class-specific special point");
        }
        finally
        {
            if (Directory.Exists(configDirectory))
            {
                foreach (var file in Directory.GetFiles(configDirectory, "*.json", SearchOption.AllDirectories)) File.Delete(file);
                foreach (var directory in Directory.GetDirectories(configDirectory)) Directory.Delete(directory);
                Directory.Delete(configDirectory);
            }
        }
        var pulse = new Heartbeat();
        Check(!pulse.Observe(Color.Magenta, 0), "heartbeat needs a transition");
        Check(pulse.Observe(Color.Cyan, .25), "heartbeat transition");
        Check(!pulse.Observe(Color.Cyan, 1.8), "stale heartbeat");
        Check(!pulse.Observe(Color.Black, 2), "invalid heartbeat resets state");
        Check(!pulse.Observe(Color.Cyan, 2.1), "new heartbeat waits for transition");
        Check(pulse.Observe(Color.Magenta, 2.35), "heartbeat recovers");
        Check(ProtectionFields.All[0].Decode(Color.White) == "是", "legacy boolean true");
        Check(ProtectionFields.All[0].Decode(Color.FromArgb(254, 254, 254)) == "否", "legacy strict 255");
        Check(ProtectionFields.All[12].Decode(Color.White).Contains("旧规则就绪"), "cooldown readiness");
        Check(ProtectionFields.All[^1].Decode(Color.Black).EndsWith("是"), "special legacy black rule");
        Check(ProtectionFields.All[29].Kind == ValueKind.FuryBoolean && ProtectionFields.All[29].Name.Contains("≥80"), "point 30 now carries raw rage threshold rather than Ignore Pain duration");
        var referenceColor = new SampleColor(144, 92, 18);
        var picked = SpecialPointPicker.ReadPixel(pattern, new Point(14, 0));
        Check(picked.Point == new SamplePoint(14, 0) && picked.Color == SampleColor.From(Color.Lime), "picker records exact client pixel and RGB");
        Check(SpecialPointPicker.ReadPixel(pattern, new Point(0, 0)).Color == SampleColor.From(Color.Black)
            && SpecialPointPicker.ReadPixel(pattern, new Point(pattern.Width - 1, pattern.Height - 1)).Point.X == pattern.Width - 1,
            "picker reads first and last image pixels without scaling");
        var configured = example.Copy(); configured.SpecialEnabled = true; configured.Special = picked.Point; configured.SpecialReferenceColor = picked.Color;
        try
        {
            configured.Save(path); var restored = DiagnosticLayout.Load(path);
            Check(restored.Special == picked.Point && restored.SpecialEnabled && restored.SpecialReferenceColor == picked.Color,
                "picked position and RGB survive config reload");
        }
        finally { File.Delete(path); }
        Check(ProtectionFields.All[^1].Decode(referenceColor.ToColor(), referenceColor).EndsWith("是")
            && ProtectionFields.All[^1].Decode(Color.White, referenceColor).EndsWith("否"), "diagnostic decode matches picked-color rule");
        var fzFrames = Enumerable.Range(1, 50).ToDictionary(n => n, _ => Color.Black);
        var fzBars = new Dictionary<int, Color> { [1] = Color.Black, [2] = Color.Black };
        var fzStates = new Dictionary<int, bool>();
        fzFrames[4] = fzFrames[6] = Color.White;
        fzFrames[7] = Color.FromArgb(128, 128, 128);
        FZ.Inst.IgnorePainReferenceColor = referenceColor.ToColor();
        fzFrames[51] = Color.Black;
        FZ.Inst.Process(fzFrames, fzBars, fzStates);
        Check(fzStates.GetValueOrDefault(8), "Ignore Pain black special pixel triggers at sufficient rage");
        fzStates.Clear(); fzFrames[51] = referenceColor.ToColor();
        FZ.Inst.Process(fzFrames, fzBars, fzStates);
        Check(fzStates.GetValueOrDefault(8), "Ignore Pain exact picked RGB also triggers");
        fzStates.Clear(); fzFrames[51] = Color.FromArgb(144, 92, 19);
        FZ.Inst.Process(fzFrames, fzBars, fzStates);
        Check(!fzStates.GetValueOrDefault(8), "Ignore Pain does not match a different RGB");
        fzStates.Clear(); fzFrames.Remove(51);
        FZ.Inst.Process(fzFrames, fzBars, fzStates);
        Check(!fzStates.GetValueOrDefault(8), "unconfigured point is not a black reading");
        fzFrames[7] = Color.FromArgb(157, 157, 157); fzFrames[9] = Color.Black; fzFrames[30] = Color.White;
        FZ.Inst.Process(fzFrames, fzBars, fzStates);
        Check(fzStates.GetValueOrDefault(8), "raw rage 80 fallback works solo at 80/130 without special point");
        fzStates.Clear(); fzFrames[51] = referenceColor.ToColor(); fzFrames[7] = Color.Black; fzFrames[30] = Color.Black;
        FZ.Inst.Process(fzFrames, fzBars, fzStates);
        Check(!fzStates.GetValueOrDefault(8), "matching color still respects committed rage threshold");
        FZ.Inst.IgnorePainReferenceColor = null;
        fzFrames.Remove(51);fzFrames[3]=Color.White;fzFrames[7]=Color.FromArgb(157,157,157);
        fzFrames[30]=Color.White;fzFrames[15]=fzFrames[16]=fzFrames[21]=fzFrames[22]=fzFrames[23]=fzFrames[24]=Color.White;
        fzBars[2]=Color.White;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.Count==1&&fzStates.GetValueOrDefault(8),"rage 80 Ignore Pain precedes block, demo and all damage skills");
        foreach(int id in new[]{21,22,23,24,25}) fzFrames[id]=Color.Black;
        fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.GetValueOrDefault(8),"Ignore Pain high-rage rule works during GCD with no assisted recommendation");
        fzFrames[30]=Color.Orange;fzBars[2]=Color.Black;fzFrames[16]=Color.Black;
        fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(!fzStates.GetValueOrDefault(8),"unknown raw rage is not true even if red channel is 255");
        fzFrames[30]=Color.Black;fzFrames[7]=Color.White;fzFrames[9]=Color.White;
        fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(!fzStates.GetValueOrDefault(8),"high-rage rule uses actual threshold rather than percentage or group");
        fzFrames[16]=Color.White;fzFrames[21]=Color.White;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.GetValueOrDefault(10)&&!fzStates.GetValueOrDefault(9),"Demoralizing Shout enabled; Shield Charge does not fire without officer marker");
        fzFrames[16]=Color.Black;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.GetValueOrDefault(11),"Demo cooldown allows shield slam recommendation");
        fzFrames[30]=Color.White;fzFrames[4]=Color.Black;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(!fzStates.GetValueOrDefault(8),"Ignore Pain retains 10-yard combat conditions");
        Check(FuryBuffs.Decode(example,ProtectionFields.All[29],Color.Orange).StartsWith("未知"),"raw rage threshold diagnostics distinguish unknown");
        foreach(int id in Enumerable.Range(1,50)) fzFrames[id]=Color.Black;
        fzFrames[1]=fzFrames[3]=fzFrames[4]=fzFrames[5]=fzFrames[6]=fzFrames[8]=fzFrames[15]=fzFrames[18]=Color.White;
        fzBars[1]=fzBars[2]=Color.Black;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.Count==1&&fzStates.GetValueOrDefault(3),"officer requests Avatar before Shield Charge when both are ready");
        fzFrames[18]=Color.Black;fzFrames[32]=Color.White;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.GetValueOrDefault(9)&&!fzStates.GetValueOrDefault(28),"Avatar cooldown keeps officer marker for Shield Charge");
        fzFrames[15]=Color.Black;fzFrames[31]=Color.White;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.GetValueOrDefault(28),"both confirmed own cooldowns clear officer marker");
        fzFrames[31]=fzFrames[32]=Color.Black;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(!fzStates.GetValueOrDefault(28)&&fzStates.Count==0,"GCD-suppressed ready colors do not clear officer marker");
        fzFrames[31]=Color.Orange;fzFrames[32]=Color.White;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.Count==0,"unknown own cooldown does not confirm requests consumed");
        fzFrames[31]=Color.Black;fzFrames[15]=Color.White;fzFrames[4]=Color.Black;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.Count==0,"Shield Charge requires retained ten-yard conditions");
        fzFrames[4]=Color.White;fzFrames[8]=Color.Black;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.Count==0,"officer OFF never automatically Shield Charges");
        fzFrames[8]=Color.White;fzFrames[1]=Color.Black;fzFrames[4]=Color.Black;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.GetValueOrDefault(28),"out of combat clears shared officer marker");
        fzFrames[1]=fzFrames[4]=fzFrames[30]=Color.White;fzStates.Clear();FZ.Inst.Process(fzFrames,fzBars,fzStates);
        Check(fzStates.GetValueOrDefault(8),"rage 80 Ignore Pain still precedes shared officer burst requests");
        Check(Colors.Spec(Color.Cyan) == 71 && Colors.Spec(Color.Yellow) == 103 && Colors.Spec(Color.Magenta) == 104, "other specs identified");
        var all = PositionVerification.Check(example, p => pattern.GetPixel(p.X, p.Y));
        Check(all.Length == 103 && all.All(c => c.Passed), "all position verification points pass");
        using (var corrupt = (Bitmap)pattern.Clone())
        {
            var bad = example.Bars[26]; corrupt.SetPixel(bad.X, bad.Y, Color.Black);
            var report = PositionVerification.Check(example, p => corrupt.GetPixel(p.X, p.Y));
            Check(report.Count(c => !c.Passed) == 1 && report.Single(c => !c.Passed).Id == "B27", "reports exact wrong lower-row point");
        }
        var offCenter = example.Copy(); offCenter.Frames[0] = new(example.Frames[0].X + 1, example.Frames[0].Y);
        Check(PositionVerification.Check(offCenter, p => pattern.GetPixel(p.X, p.Y)).All(c => c.Passed) && !PositionVerification.SameGeometry(example, offCenter), "geometry detects offset within same-color block");
        var stable = new StableLocator();
        Check(!stable.Observe(example) && stable.Observe(example), "two consistent scans required");
        using (var resized = Pattern(1.333, width: 2560))
        {
            var next = Calibration.Find(resized, new Size(2560, 1440));
            Check(!PositionVerification.SameGeometry(example, next), "resolution and UI scale change invalidate old layout");
            Check(PositionVerification.Check(next, p => resized.GetPixel(p.X, p.Y)).All(c => c.Passed), "relocation finds all points after resolution change");
            Check(!stable.Observe(next) && stable.Observe(next), "new resolution needs stable coordinates again");
            stable.Reset(); Check(!stable.Observe(next), "failed scan resets stability");
            using var scan = new Captured((Bitmap)resized.Clone(), new Rectangle(Point.Empty, resized.Size));
            using var crop = PositionVerification.Crop(scan, next);
            Check(PositionVerification.Check(next, crop.At).All(c => c.Passed), "cropped preview retains all point coordinates");
        }
        using (var scaled = Pattern(1.25))
        {
            var sameSize = Calibration.Find(scaled, new Size(1600, 900));
            Check(!PositionVerification.SameGeometry(example, sameSize), "UI scale change detected with same resolution");
            Check(PositionVerification.Check(sameSize, p => scaled.GetPixel(p.X, p.Y)).All(c => c.Passed), "UI scale relocation without resize");
        }
        var edges = new MouseToggleEdges();
        Check(edges.Observe(0x207, 0) && !edges.Observe(0x207, 0), "middle press toggles once while held");
        Check(!edges.Observe(0x208, 0) && edges.Observe(0x207, 0), "middle release rearms toggle");
        Check(!edges.Observe(0x208, 0) && edges.Observe(0x207, 0), "rapid middle clicks remain distinct");
        Check(edges.Observe(0x20B, 1u << 16) && !edges.Observe(0x20B, 1u << 16), "side button one has independent edge");
        Check(edges.Observe(0x20B, 2u << 16), "side button two supported");
        Check(!edges.Observe(0x200, 0) && !edges.Observe(0x20A, 0), "movement and wheel do not toggle");
        Check(!edges.Observe(0x20B, 0), "invalid side button ignored");
        using (var hook = new MouseToggle(() => { }))
            Check(true, "Windows global mouse listener installs and disposes");

        IntPtr foreground = (IntPtr)42;
        bool manual = false;
        int executions = 0;
        Dictionary<int, Color>? executedFrames = null, executedBars = null;
        var runner = new RotationExecution(() => foreground, () => manual, (frames, bars) =>
        {
            executions++; executedFrames = frames; executedBars = bars;
        });
        var game = new GameWindow(1, (IntPtr)42, "Synthetic");
        using (var sample = new Captured((Bitmap)pattern.Clone(), new Rectangle(Point.Empty, pattern.Size)))
        {
            runner.Tick(game, example, sample, true, 0);
            Check(executions == 0, "execution starts disabled");
            runner.Start(); runner.Tick(game, example, sample, false, 0);
            Check(executions == 0, "unknown or stale data cannot send keys");
            foreground = (IntPtr)99; runner.Tick(game, example, sample, true, 0);
            Check(executions == 0, "background game cannot send keys");
            foreground = game.Handle; runner.Tick(game, example, sample, true, 0);
            Check(executions == 1 && executedFrames!.Count == 50 && executedBars!.Count == 50,
                "execution consumes calibrated standard rows");
            Check(executedFrames![30].ToArgb() == sample.At(example.Frames[29]).ToArgb()
                && executedBars![2].ToArgb() == sample.At(example.Bars[1]).ToArgb(), "execution reads calibrated coordinates including point 30");
            manual = true; runner.Tick(game, example, sample, true, .1);
            manual = false; runner.Tick(game, example, sample, true, .59);
            Check(executions == 1, "manual input gives a 500 ms pause");
            runner.Tick(game, example, sample, true, .61);
            Check(executions == 2, "execution resumes after manual pause");
            runner.Stop(); runner.Tick(game, example, sample, true, 1);
            Check(executions == 2, "middle toggle off blocks further execution");
        }
        int foregroundReads = 0;
        var focusChange = new RotationExecution(() => ++foregroundReads == 1 ? game.Handle : (IntPtr)99,
            () => false, (_, _) => executions++);
        focusChange.Start();
        using (var sample = new Captured((Bitmap)pattern.Clone(), new Rectangle(Point.Empty, pattern.Size)))
            focusChange.Tick(game, example, sample, true, 0);
        Check(executions == 2, "focus change during sample processing blocks input");

        var schedule = new PresentationSchedule();
        int displays = 0, beforeExecutions = executions;
        runner.Start();
        using (var sample = new Captured((Bitmap)pattern.Clone(), new Rectangle(Point.Empty, pattern.Size)))
        {
            for (int i = 0; i < 40; i++)
            {
                runner.Tick(game, example, sample, true, i * .03);
                if (schedule.Due(i * .03)) displays++;
            }
        }
        runner.Stop();
        Check(executions - beforeExecutions == 40 && displays == 5,
            "UI throttling preserves every fresh execution sample while reducing 40 displays to 5");
        Check(!schedule.Due(2, visible: false) && schedule.Due(2), "minimized UI skips presentation and restores immediately");
        schedule.Reset(); Check(schedule.Due(0), "layout or mode change forces an immediate display");

        int switchedExecutions = 0;
        var switchingRunner = new RotationExecution(() => game.Handle, () => false, (_, _) => switchedExecutions++);
        using (var sample = new Captured((Bitmap)pattern.Clone(), new Rectangle(Point.Empty, pattern.Size)))
        {
            switchingRunner.Start(ClassProfiles.Protection);
            switchingRunner.Tick(game, example, sample, true, 0, ClassProfiles.Find(103));
            Check(!switchingRunner.Enabled && switchedExecutions == 0, "spec change stops execution before any keys are sent");
            var cat = example.Copy(); cat.SpecId = 103;
            switchingRunner.Start(ClassProfiles.Find(103)); switchingRunner.Tick(game, cat, sample, true, 1, ClassProfiles.Find(103));
            Check(switchedExecutions == 1, "execution can start with matching new specialization config");
            cat.SpecId = 73; switchingRunner.Tick(game, cat, sample, true, 2, ClassProfiles.Find(103));
            Check(!switchingRunner.Enabled && switchedExecutions == 1, "wrong class config stops execution");
        }

        using (var sample = new Captured((Bitmap)pattern.Clone(), new Rectangle(Point.Empty, pattern.Size)))
        {
            var specialLayout = example.Copy(); specialLayout.SpecialEnabled = true;
            specialLayout.Special = new(14, 0); specialLayout.SpecialReferenceColor = referenceColor;
            runner.Start(); runner.Tick(game, specialLayout, sample, true, 3);
            Check(executedFrames!.ContainsKey(51) && executedFrames[51].ToArgb() == Color.Lime.ToArgb()
                && FZ.Inst.IgnorePainReferenceColor?.ToArgb() == referenceColor.ToColor().ToArgb(),
                "execution consumes configured special point and reference color");
            specialLayout.SpecialEnabled = false; runner.Tick(game, specialLayout, sample, true, 3.1);
            Check(!executedFrames.ContainsKey(51) && FZ.Inst.IgnorePainReferenceColor is null,
                "disabling special point removes its previous reading and reference");
            runner.Stop();
        }

        string logsDirectory = Path.Combine(Path.GetTempPath(), "RotationUS-logs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logsDirectory);
        string savedLog = Path.Combine(logsDirectory, "points-test.log.txt"), lockedLog = Path.Combine(logsDirectory, "points-locked.log.txt");
        string capturesDirectory = Path.Combine(logsDirectory, "captures"), customDirectory = Path.Combine(logsDirectory, "custom");
        string[] preserved = { "points-test.png", "points-test.csv", "points-test.layout.json", "diagnostic-layout.json", "notes.txt" };
        try
        {
            File.WriteAllText(savedLog, "old log"); File.WriteAllText(lockedLog, "locked log");
            foreach (string name in preserved) File.WriteAllText(Path.Combine(logsDirectory, name), "preserved");
            using (var locked = new FileStream(lockedLog, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var cleared = SavedLogs.ClearContents(logsDirectory);
                Check(cleared.Cleared == 1 && cleared.Errors.Length == 1, "locked log reports failure while other logs clear");
            }
            Check(File.Exists(savedLog) && new FileInfo(savedLog).Length == 0, "saved log cleared while file retained");
            Check(File.ReadAllText(lockedLog) == "locked log", "failed log remains intact");
            Check(preserved.All(name => File.ReadAllText(Path.Combine(logsDirectory, name)) == "preserved"), "PNG CSV layout and unrelated files preserved");
            var clearedAgain = SavedLogs.ClearContents(logsDirectory);
            Check(clearedAgain.Cleared == 2 && clearedAgain.Errors.Length == 0, "clearing logs is repeatable");
            var missingStore = new SavedLogStore(Path.Combine(logsDirectory, "missing"));
            Check(missingStore.ClearContents().Cleared == 0, "no saved logs requires no folder selection or directory creation");
            Directory.CreateDirectory(capturesDirectory); Directory.CreateDirectory(customDirectory);
            string defaultLog = Path.Combine(capturesDirectory, "points-old.log.txt");
            string customLog = Path.Combine(customDirectory, "custom-export.log.txt");
            string unrelatedLog = Path.Combine(customDirectory, "unrelated.log.txt");
            File.WriteAllText(defaultLog, "old default export");
            File.WriteAllText(customLog, "custom export"); File.WriteAllText(unrelatedLog, "other application");
            var store = new SavedLogStore(logsDirectory);
            store.Remember(customLog);
            var restoredStore = new SavedLogStore(logsDirectory);
            Check(restoredStore.LastDirectory == customDirectory && restoredStore.LoadError is null,
                "custom export location remembered across application restarts");
            var automaticallyCleared = restoredStore.ClearContents();
            Check(automaticallyCleared.Cleared == 2 && automaticallyCleared.Errors.Length == 0
                && new FileInfo(defaultLog).Length == 0 && new FileInfo(customLog).Length == 0,
                "one-click clearing finds old defaults and remembered custom exports");
            Check(File.ReadAllText(unrelatedLog) == "other application", "custom directory cleanup preserves untracked logs");
            restoredStore.Remember(defaultLog);
            Check(restoredStore.ClearContents().Cleared == 2, "remembered default log counted only once");
        }
        finally
        {
            foreach (string directory in new[] { capturesDirectory, customDirectory })
            {
                if (!Directory.Exists(directory)) continue;
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
            foreach (string file in Directory.GetFiles(logsDirectory)) File.Delete(file);
            Directory.Delete(logsDirectory);
        }
        count += FloatingWindowTests.Run();
        count += ArmsTests.Run();
        Console.WriteLine($"PASS: {count} diagnostic checks. Synthetic pixels only; no input sent.");
    }
    public static void Render(string path, bool position = false)
    {
        static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); }
        using var pattern = Pattern(1);
        var layout = Calibration.Find(pattern, new Size(1600, 900));
        layout.SpecialEnabled = true; layout.Special = new(610, 8); layout.SpecialReferenceColor = new(144, 92, 18);
        using var image = new Bitmap(800, 12);
        using (var g = Graphics.FromImage(image))
        {
            g.Clear(Color.Black);
            for (int i = 0; i < 50; i++)
            {
                var color = i == 5 ? Color.FromArgb(220, 220, 220) : i == 6 ? Color.FromArgb(160, 160, 160) : i % 4 == 0 ? Color.White : Color.Black;
                using var brush = new SolidBrush(color); g.FillRectangle(brush, 10 + i * 10, 0, 10, 1);
            }
            g.FillRectangle(Brushes.White, 10, 1, 20, 1);
            g.FillRectangle(Brushes.Lime, 740, 0, 10, 3); g.FillRectangle(Brushes.Blue, 750, 0, 10, 3); g.FillRectangle(Brushes.Cyan, 760, 0, 10, 3);
        }
        var isolatedLogs = new SavedLogStore(Path.Combine(Path.GetTempPath(), "RotationUS-ui-" + Guid.NewGuid().ToString("N")));
        using var form = new DiagnosticForm(listenForMouse: false, savedLogs: isolatedLogs,
            profileLayouts: new ProfileLayouts(Path.GetDirectoryName(isolatedLogs.DefaultDirectory)!));
        form.Show(); Application.DoEvents(); form.FreezeForTesting();
        if (position) form.ShowSyntheticPosition(layout, pattern); else form.ShowSynthetic(layout, image);
        form.Refresh(); Application.DoEvents();
        var displayGrid = Descendants(form).OfType<DataGridView>().Single();
        if (!position)
        {
            form.ProfilePresentation(layout, image);
            int repeatedChanges = 0;
            displayGrid.CellValueChanged += (_, _) => repeatedChanges++;
            form.ProfilePresentation(layout, image);
            Check(repeatedChanges == 0, "unchanged samples do not rewrite grid values");
        }
        static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>()
            .SelectMany(child => new[] { child }.Concat(Descendants(child)));
        var clear = Descendants(form).OfType<Button>().Single(b => b.Text == "清空窗口日志");
        var clearSaved = Descendants(form).OfType<Button>().Single(b => b.Text == "清空已保存日志");
        var logList = Descendants(form).OfType<ListBox>().Single();
        form.ProfileLogChurn(1000, reset: true);
        object expectedFirst = logList.Items[32];
        form.ProfileLogChurn(32);
        Check(logList.Items.Count == 1000 && Equals(logList.Items[0], expectedFirst),
            "full log keeps exactly the newest 1000 entries in order");
        Check(logList.HorizontalScrollbar && logList.HorizontalExtent > 1,
            "log retains horizontal scrolling without automatic width rescans");
        Check(clear.Visible && clearSaved.Visible && clearSaved.Bottom <= clearSaved.Parent!.ClientSize.Height,
            "log clearing buttons visible in diagnostic window");
        var pickButton = Descendants(form).OfType<Button>().Single(b => b.Text == "截图取点 / 取色");
        Check(pickButton.Visible, "special picker button visible");
        if (!position)
        {
            var saveSpecial = Descendants(form).OfType<Button>().Single(b => b.Text == "保存特殊点");
            Check(!saveSpecial.Enabled, "saved config does not unlock special-point save");
            form.StageSpecialPick(new(new(610, 8), new(68, 43, 7)));
            Check(saveSpecial.Enabled, "successful pixel selection unlocks save");
            var xControl = Descendants(form).OfType<NumericUpDown>().Single(n => n.Value == 610);
            xControl.Value = 611;
            Check(!saveSpecial.Enabled, "coordinate edit invalidates picked color and locks save");
            form.ShowSynthetic(layout, image);
            Check(!saveSpecial.Enabled, "applying saved layout locks save again");
            Check(!Descendants(form).OfType<Button>().Any(b => b.Text is "自动校准" or "校验位置"),
                "redundant calibration buttons removed");
            foreach (var profile in ClassProfiles.All)
            {
                var marker = layout.Markers[1]; image.SetPixel(marker.X, marker.Y, ClassProfiles.Encode(profile.SpecId, profile.ClassId));
                form.ShowSynthetic(layout, image);
                Check(displayGrid.Rows.Count == profile.Fields.Length
                    && Equals(displayGrid.Rows[6].Cells[1].Value, profile.Fields[6].Name)
                    && Descendants(form).OfType<Label>().Any(l => l.Text.Contains("当前职业：" + profile.DisplayName)),
                    "UI switches class label and data fields " + profile.Key);
                Check(pickButton.Enabled == profile.HasSpecial && !saveSpecial.Enabled,
                    "class switching resets special point controls " + profile.Key);
            }
            var m2 = layout.Markers[1]; image.SetPixel(m2.X, m2.Y, ClassProfiles.Encode(62, 8));
            form.ShowSynthetic(layout, image);
            Check(displayGrid.Rows.Count == 50 && !pickButton.Enabled, "unsupported class shows raw fields without protection controls");
            image.SetPixel(m2.X, m2.Y, ClassProfiles.Encode(73, 1)); form.ShowSynthetic(layout, image);
        }
        using var rendered = new Bitmap(form.Width, form.Height); form.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size));
        rendered.Save(Path.GetFullPath(path), ImageFormat.Png);
        clear.PerformClick();
        Check(Descendants(form).OfType<ListBox>().Single().Items.Count == 0, "clear window logs removes displayed history");
        Check(logList.HorizontalExtent == 1, "clearing log resets horizontal scroll width");
        clearSaved.PerformClick();
        Check(Descendants(form).OfType<ListBox>().Single().Items.Count == 1,
            "saved-log button completes without folder dialog and reports result");
        form.ClientSize = new Size(950, 650); form.PerformLayout(); Application.DoEvents();
        var toolbar = clear.Parent!;
        Check(toolbar.Controls.Cast<Control>().All(c => c.Right <= toolbar.ClientSize.Width && c.Bottom <= toolbar.ClientSize.Height),
            "toolbar fits minimum window size");
        Check(pickButton.Parent!.Controls.Cast<Control>().All(c => c.Right <= pickButton.Parent.ClientSize.Width && c.Bottom <= pickButton.Parent.ClientSize.Height),
            "special point controls fit minimum window size");
        form.Close(); Console.WriteLine("UI rendered: " + Path.GetFullPath(path));
    }

    public static void RenderPicker(string path)
    {
        using var image = new Bitmap(1280, 720);
        using (var g = Graphics.FromImage(image))
        {
            g.Clear(Color.DarkSlateGray);
            g.FillRectangle(Brushes.SandyBrown, 570, 340, 70, 70);
            g.DrawString("合成游戏截图 · 取点窗口检查", SystemFonts.DefaultFont, Brushes.White, 560, 300);
        }
        using var picker = new SpecialPointPicker(image, new Rectangle(50, 50, image.Width, image.Height));
        picker.Show(); Application.DoEvents(); picker.Refresh();
        using var rendered = new Bitmap(picker.Width, picker.Height);
        picker.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size));
        rendered.Save(Path.GetFullPath(path), ImageFormat.Png);
        picker.Close(); Console.WriteLine("Picker rendered: " + Path.GetFullPath(path));
    }
}
