#nullable enable
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using RotationUS.Diagnostics;

namespace RotationForever;

internal static class PaladinTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new Exception("Forever: " + name);
            checks++;
        }
        foreach (var (action, key, shift) in new[]
        {
            (PaladinAction.Seal, (ushort)0x31, true),
            (PaladinAction.CrusaderSeal, (ushort)0x32, true),
            (PaladinAction.Judgement, (ushort)0x37, false),
            (PaladinAction.HolyStrike, (ushort)0x38, false)
        })
        {
            GameKeys.KeyStroke[] expected = shift
                ? [new(0xA0, false), new(key, false), new(key, true), new(0xA0, true)]
                : [new(key, false), new(key, true)];
            var calls = new List<GameKeys.KeyStroke[]>();
            Check(GameKeys.Press(action, strokes => { calls.Add(strokes); return (uint)strokes.Length; }) &&
                calls.Count == 1 && calls[0].SequenceEqual(expected), "key binding and complete chord " + action);
            for (uint accepted = 0; accepted < expected.Length; accepted++)
            {
                calls.Clear();
                var held = new HashSet<ushort>();
                Check(!GameKeys.Press(action, strokes =>
                {
                    uint count = calls.Count == 0 ? accepted : (uint)strokes.Length;
                    calls.Add(strokes);
                    foreach (var stroke in strokes.Take((int)count))
                        if (stroke.Up) held.Remove(stroke.Key); else held.Add(stroke.Key);
                    return count;
                }) && held.Count == 0, $"partial send releases keys {action}/{accepted}");
            }
        }
        var frames = Enumerable.Range(1, 100).ToDictionary(id => id, _ => Color.Black);
        var bars = new Dictionary<int, Color>();
        void Flag(int id, bool value = true) => frames[id] = value ? Color.White : Color.Black;
        foreach (int id in new[] { 1, 2, 3, 4, 6, 7, 8, 16, 17, 18, 26, 27, 28, 50 }) Flag(id);
        PaladinDecision Decide() => ForeverPaladin.Decide(id => frames[id]);
        Check(Decide().Action == PaladinAction.Seal, "seal before attacks");
        Flag(5);
        Check(Decide().Action == PaladinAction.Judgement, "judgement during seal");
        Flag(7, false);
        Check(Decide().Action == PaladinAction.HolyStrike, "strike while judgement cooling");
        Flag(4, false);
        Check(Decide().Action == PaladinAction.None, "one range gate blocks attacks");
        Flag(5, false);
        Check(Decide().Action == PaladinAction.Seal, "self buff only needs combat");
        Flag(5); Flag(4); Flag(7);
        foreach (int gate in new[] { 1, 2, 3, 4, 50 })
        {
            Flag(gate, false);
            Check(Decide().Action == PaladinAction.None, "closed gate " + gate);
            Flag(gate);
        }
        frames[7] = Color.Orange;
        Check(Decide().Action == PaladinAction.None, "unknown readiness");
        Flag(7);
        Flag(27, false); Flag(28, false);
        Check(Decide().Action == PaladinAction.None, "resource flags block otherwise ready attacks");
        Flag(27); Flag(28);
        Flag(17, false); Flag(18, false);
        Check(Decide().Action == PaladinAction.None, "usability flags block otherwise ready attacks");
        Flag(17); Flag(18);
        Check(ForeverPaladin.Decode(ForeverPaladin.Fields()[8], Color.White) == "30.0 秒", "timer decoding");
        Check(ClassProfiles.Find(ForeverPaladin.ProfileId) == ClassProfiles.Forever, "profile registration");
        var identity = new ClassIdentity(ForeverPaladin.ProfileId, 2);
        Check(ClassProfiles.Identify(ClassProfiles.Encode(identity.SpecId, identity.ClassId)) == identity, "identity encoding");
        var gateCheck = new ClassIdentityGate();
        Check(!gateCheck.Observe(default, identity, true, 0), "identity first sample");
        Check(!gateCheck.Observe(default, identity, true, .1), "identity second sample");
        Check(gateCheck.Observe(default, identity, true, .31), "identity confirmation");
        Check(!gateCheck.Observe(identity, new(identity.SpecId, 1), true, .4), "wrong class");

        var rotation = new ForeverPaladin();
        var states = new Dictionary<int, bool>();
        int sent = 0;
        bool Send(PaladinAction action) { sent++; return true; }
        rotation.Process(frames, bars, states);
        rotation.Dispatch(0, Send);
        rotation.Dispatch(.99, Send);
        Check(sent == 1, "same action retry delay");
        rotation.Dispatch(1, Send);
        Check(sent == 2, "retry after one second");
        Flag(7, false);
        rotation.Process(frames, bars, states);
        rotation.Dispatch(1.1, Send);
        Check(sent == 2, "cross-action key interval");
        rotation.Dispatch(1.25, Send);
        Check(sent == 3, "next action after key interval");
        rotation.Reset();
        rotation.Process(frames, bars, states);
        rotation.Dispatch(0, Send);
        Check(sent == 4, "restart clears pending retry");
        try
        {
            global::Program.ExecuteClassSample(ClassProfiles.Find(70)!, frames, bars);
            throw new Exception("Retail input route accepted");
        }
        catch (InvalidOperationException) { checks++; }

        using var pattern = DiagnosticTests.Pattern(1);
        var layout = Calibration.Find(pattern, new Size(1600, 900));
        layout.SpecId = ForeverPaladin.ProfileId;
        using var image = LiveImage(layout);
        using var captured = new Captured(image, new Rectangle(Point.Empty, image.Size));
        int dispatched = 0;
        IntPtr foreground = (IntPtr)42;
        bool manual = false;
        var game = new GameWindow(1, (IntPtr)42, "Forever fixture");
        var execution = new RotationExecution(() => foreground, () => manual, (f, b) =>
        {
            Check(ForeverPaladin.Decide(id => f[id]).Action == PaladinAction.Judgement, "calibrated sample decisions");
            dispatched++;
        });
        execution.Start(ClassProfiles.Forever);
        execution.Tick(game, layout, captured, false, 0, ClassProfiles.Forever);
        Check(dispatched == 0, "stale heartbeat blocks execution");
        foreground = (IntPtr)99;
        execution.Tick(game, layout, captured, true, .1, ClassProfiles.Forever);
        Check(dispatched == 0, "background game blocks execution");
        foreground = game.Handle;
        manual = true;
        execution.Tick(game, layout, captured, true, .2, ClassProfiles.Forever);
        manual = false;
        execution.Tick(game, layout, captured, true, .69, ClassProfiles.Forever);
        Check(dispatched == 0, "manual pause is retained");
        execution.Tick(game, layout, captured, true, .71, ClassProfiles.Forever);
        Check(dispatched == 1, "normal entry sends calibrated data");
        execution.Stop();
        execution.Tick(game, layout, captured, true, 2, ClassProfiles.Forever);
        Check(dispatched == 1, "disabled execution");
        checks += TraceTests();
        return checks;
    }

    private static int TraceTests()
    {
        int checks = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new Exception("Forever trace: " + name);
            checks++;
        }
        var frames = Enumerable.Range(1, 100).ToDictionary(id => id, _ => Color.Black);
        foreach (int id in new[] { 1, 2, 3, 4, 6, 7, 8, 10, 11, 12, 26, 27, 28, 50 }) frames[id] = Color.White;
        var trace = new PaladinTrace();
        var lines = new List<string>();
        bool Write(string line) { lines.Add(line); return true; }
        void Record(double now, bool live = true, bool enabled = false) =>
            trace.Record(id => frames[id], live, enabled, true, "执行已关闭", now, Write);
        Record(0);
        Check(lines.Count == 1 && lines[0].Contains("正义圣印可用[16]=否 RGB(0,0,0)") &&
            lines[0].Contains("已学习正义圣印[10]=是") && lines[0].Contains("执行关闭"), "disabled capture includes exact seal blockers");
        Record(.1);
        frames[9] = Color.FromArgb(200, 200, 200);
        Record(.2);
        Check(lines.Count == 1, "unchanged state and countdown do not flood logs");
        Record(5);
        Check(lines.Count == 2, "persistent blocker written every five seconds");
        frames[16] = Color.White;
        Record(5.01, enabled: true);
        Check(lines.Count == 3 && lines[^1].Contains("圣印条件=已满足"), "usability transition recorded immediately");
        frames[26] = Color.Orange;
        Record(5.02, enabled: true);
        Check(lines[^1].Contains("正义圣印资源充足[26]=未知 RGB(255,165,0)"), "invalid color is unknown");
        Record(5.03, live: false);
        Check(lines[^1].Contains("采样无效，不推断技能状态") && !lines[^1].Contains("圣印条件=已满足"), "invalid sample never asserts readiness");
        var retry = new PaladinTrace();
        int attempts = 0;
        bool Failed(string line) { attempts++; return false; }
        retry.Record(id => frames[id], true, false, true, "关闭", 0, Failed);
        retry.Record(id => frames[id], true, false, true, "关闭", .1, Failed);
        Check(attempts == 2, "failed writes remain retryable");
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "forever-trace-" + Guid.NewGuid().ToString("N"));
        try
        {
            var log = new HolyShieldLog(new SavedLogStore(directory), "forever-capture");
            Check(log.RecordForever(lines[0]) && log.RecordForever(lines[0]), "real file accepts recurring snapshots");
            string content = System.IO.File.ReadAllText(log.Path);
            Check(content.StartsWith("永恒自动诊断") && content.Split("[永恒战斗诊断]").Length == 3,
                "correct header and snapshots persisted");
        }
        finally
        {
            if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true);
        }
        return checks;
    }

    private static Bitmap LiveImage(DiagnosticLayout layout)
    {
        var image = new Bitmap(800, 20);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(Color.Black);
        for (int id = 1; id <= 100; id++)
        {
            Color color = id is >= 1 and <= 12 or >= 16 and <= 18 or >= 26 and <= 28 or 50 ? Color.White : Color.Black;
            if (id == 9) color = Color.FromArgb(153, 153, 153);
            if (id is 14 or 15) color = Color.FromArgb(204, 204, 204);
            if (id == 51) color = Color.FromArgb(204, 255, 0);
            using var brush = new SolidBrush(color);
            graphics.FillRectangle(brush, id * 5, 0, 5, 1);
        }
        graphics.FillRectangle(Brushes.Lime, 740, 0, 10, 3);
        using var identity = new SolidBrush(ClassProfiles.Encode(ForeverPaladin.ProfileId, 2));
        graphics.FillRectangle(identity, 750, 0, 10, 3);
        graphics.FillRectangle(Brushes.Cyan, 760, 0, 10, 3);
        return image;
    }

    internal static void Render(string path)
    {
        using var pattern = DiagnosticTests.Pattern(1);
        using (var graphics = Graphics.FromImage(pattern))
        using (var marker = new SolidBrush(ClassProfiles.Encode(ForeverPaladin.ProfileId, 2)))
            graphics.FillRectangle(marker, 750, 0, 10, 3);
        var layout = Calibration.Find(pattern, new Size(1600, 900));
        layout.SpecId = ForeverPaladin.ProfileId;
        using var image = LiveImage(layout);
        string directory = Path.Combine(Path.GetTempPath(), "hiji-forever-ui-" + Guid.NewGuid().ToString("N"));
        var profiles = new ProfileLayouts(directory);
        using var form = new DiagnosticForm(false, new SavedLogStore(directory), profiles);
        form.Show();
        Application.DoEvents();
        form.FreezeForTesting();
        form.ShowSynthetic(layout, image);
        form.Refresh();
        Application.DoEvents();
        static IEnumerable<Control> All(Control parent) => parent.Controls.Cast<Control>()
            .SelectMany(child => new[] { child }.Concat(All(child)));
        var controls = All(form).ToArray();
        var grid = controls.OfType<DataGridView>().Single(g => g.Name == "pointGrid");
        if (grid.Rows.Count != 101 || grid.Rows[8].Cells[5].Value?.ToString() != "18.0 秒" ||
            !grid.Rows[5].Cells[1].Value!.ToString()!.Contains("Shift+1"))
            throw new Exception("Forever fields or timer display failed");
        foreach (string name in new[] { "定位测试", "暂停查看", "截图取点 / 取色", "清空窗口日志", "清空已保存日志" })
            if (!controls.OfType<Button>().Any(button => button.Text == name && button.Visible))
                throw new Exception("Missing diagnostic control " + name);
        if (!controls.OfType<Button>().Single(b => b.Text == "截图取点 / 取色").Enabled)
            throw new Exception("Diagnostic pixel picker disabled");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var rendered = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size));
        rendered.Save(path, ImageFormat.Png);
        var picked = new PickedPixel(new(610, 8), new(20, 30, 40));
        form.StageSpecialPick(picked);
        form.SaveSpecialLayout(form.BuildSpecialLayout(picked));
        var saved = DiagnosticLayout.Load(profiles.PathFor(ClassProfiles.Forever));
        if (!saved.SpecialEnabled || saved.Special != picked.Point || saved.SpecId != ForeverPaladin.ProfileId)
            throw new Exception("Forever observation point persistence failed");
        string export = Path.Combine(directory, "forever-export");
        form.ExportSnapshot(export);
        if (!File.ReadAllText(export + ".csv").Contains("正义圣印") ||
            !File.ReadAllText(export + ".log.txt").Contains("最后采样建议") ||
            !File.Exists(export + ".png") || !File.Exists(export + ".layout.json"))
            throw new Exception("Forever snapshot export failed");
        form.ShowSyntheticPosition(layout, pattern);
        if (grid.Rows.Count != 203)
            throw new Exception("Forever position diagnostic rows missing");
        using var located = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(located, new Rectangle(Point.Empty, located.Size));
        located.Save(Path.ChangeExtension(path, ".position.png"), ImageFormat.Png);
        form.Close();
        Console.WriteLine("Forever diagnostic UI passed: " + path);
    }
}
