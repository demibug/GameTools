#nullable enable
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

internal static class FloatingWindowTests
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageW(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

    internal static int Run()
    {
        int count = 0;
        void Check(bool condition, string name)
        { if (!condition) throw new Exception("FAIL: " + name); count++; }
        var areas = new[] { new Rectangle(-1280, 0, 1280, 984), new Rectangle(0, 0, 1920, 1040) };
        var size = new Size(44, 44);
        var negative = FloatingPlacement.Place(new(-800, 200), size, areas);
        Check(negative == new Point(-800, 200), "floating button supports negative-coordinate monitors");
        var removed = FloatingPlacement.Place(new(3500, -500), size, areas);
        Check(areas.Any(a => a.Contains(new Rectangle(removed, size))), "removed monitor position returns fully on screen");
        var capture = new Rectangle(0, 0, 1920, 128);
        var avoided = FloatingPlacement.Place(new(1000, 20), size, areas, capture);
        Check(!capture.IntersectsWith(new Rectangle(avoided, size))
            && areas.Any(a => a.Contains(new Rectangle(avoided, size))), "floating button moves clear of full-width capture strip");
        var stable = FloatingPlacement.Place(avoided, size, areas, capture);
        Check(stable == avoided, "safe floating button does not drift on subsequent samples");
        var onlyArea = new[] { new Rectangle(0, 0, 44, 44) };
        Check(FloatingPlacement.Place(new(0, 0), size, onlyArea, onlyArea[0]) == Point.Empty,
            "no safe placement retains bounds for capture obstruction rejection");
        var execution = new RotationExecution();
        int notifications = 0;
        execution.EnabledChanged += () => notifications++;
        execution.Start(); execution.Start(); execution.Stop(); execution.Stop();
        Check(notifications == 2 && !execution.Enabled, "execution status changes notify once per transition");
        using var pattern = DiagnosticTests.Pattern(1);
        var layout = Calibration.Find(pattern, new Size(1600, 900));
        using var sample = new Captured((Bitmap)pattern.Clone(), new Rectangle(Point.Empty, pattern.Size));
        execution.Start(ClassProfiles.Protection);
        execution.Tick(new GameWindow(1, (IntPtr)42, "Synthetic"), layout, sample, true, 0, ClassProfiles.Find(103));
        Check(notifications == 4 && !execution.Enabled, "automatic specialization stop notifies floating status before dispatch");
        string directory = Path.Combine(Path.GetTempPath(), "RotationUS-floating-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new FloatingWindowSettings(directory);
            Check(settings.Position is null && settings.LoadError is null && !Directory.Exists(directory),
                "first launch reads floating settings without creating files");
            Check(settings.Save(negative) is null, "floating position saved");
            var restored = new FloatingWindowSettings(directory);
            Check(restored.Position == negative && restored.LoadError is null, "floating position survives restart");
            File.WriteAllText(Path.Combine(directory, "floating-window.json"), "invalid JSON");
            var corrupt = new FloatingWindowSettings(directory);
            Check(corrupt.Position is null && corrupt.LoadError is not null, "invalid floating settings allow fallback position");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
        return count;
    }

    internal static void Render(string path)
    {
        static void Check(bool condition, string name)
        { if (!condition) throw new Exception("FAIL: " + name); }
        static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>()
            .SelectMany(child => new[] { child }.Concat(Descendants(child)));
        static void Save(Control control, string file, Color? centerColor = null)
        {
            using var bitmap = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            if (centerColor is { } color)
                Check(bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).ToArgb() == color.ToArgb(), "rendered status lamp matches execution state");
            bitmap.Save(file, ImageFormat.Png);
        }
        string directory = Path.Combine(Path.GetTempPath(), "RotationUS-floating-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string output = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string prefix = Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output));
        int dispatched = 0;
        var runner = new RotationExecution(() => (IntPtr)42, () => false, (_, _) => dispatched++);
        try
        {
            using var form = new DiagnosticForm(listenForMouse: false, savedLogs: new SavedLogStore(directory),
                profileLayouts: new ProfileLayouts(directory), execution: runner);
            form.Show(); Application.DoEvents(); form.FreezeForTesting();
            using var pattern = DiagnosticTests.Pattern(1);
            var layout = Calibration.Find(pattern, new Size(1600, 900));
            form.ShowSynthetic(layout, pattern);
            var button = Descendants(form).OfType<LoopStatusButton>().Single();
            var floating = (FloatingStatusForm)typeof(DiagnosticForm).GetField("floatingWindow", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
            var timer = (System.Windows.Forms.Timer)typeof(DiagnosticForm).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
            Check(!button.LoopEnabled && !floating.StatusButton.LoopEnabled, "both indicators initially red");
            form.ClientSize = new Size(950, 650); form.PerformLayout(); Application.DoEvents();
            var toolbar = button.Parent!;
            Check(toolbar.Controls.Cast<Control>().All(c => c.Right <= toolbar.ClientSize.Width && c.Bottom <= toolbar.ClientSize.Height),
                "toolbar with loop button fits minimum window size");
            form.ClientSize = new Size(1180, 800); form.PerformLayout();
            var originalBounds = form.Bounds;
            runner.Start(ClassProfiles.Protection);
            Check(button.LoopEnabled && floating.StatusButton.LoopEnabled && timer.Interval == 30, "both indicators immediately turn green on start");
            button.PerformClick(); Application.DoEvents();
            Check(!form.Visible && floating.Visible && floating.TopMost && !floating.ShowInTaskbar
                && runner.Enabled && !timer.Enabled, "collapse preserves execution and timer state while only showing floating window");
            Check(global::Program.GetForegroundWindow() != floating.Handle, "floating window does not take foreground focus");
            Save(floating, prefix + "-green.png", floating.StatusButton.StatusColor);
            using var sample = new Captured((Bitmap)pattern.Clone(), new Rectangle(Point.Empty, pattern.Size));
            for (int i = 0; i < 10; i++) runner.Tick(new GameWindow(1, (IntPtr)42, "Synthetic"), layout, sample, true, i * .03);
            Check(dispatched == 10, "hidden UI preserves fresh execution samples without real input");
            runner.Stop();
            Check(!button.LoopEnabled && !floating.StatusButton.LoopEnabled && timer.Interval == 100,
                "hidden floating indicator immediately turns red on stop");
            Save(floating, prefix + "-red.png", floating.StatusButton.StatusColor);
            floating.StatusButton.PerformClick(); Application.DoEvents();
            Check(form.Visible && !floating.Visible && form.Bounds == originalBounds && button.Visible,
                "floating click restores original bounds and embedded status button");
            Save(form, output);
            runner.Start(ClassProfiles.Protection);
            SendMessageW(form.Handle, 0x0112, (IntPtr)0xF020, IntPtr.Zero); Application.DoEvents();
            Check(!form.Visible && floating.Visible && runner.Enabled, "native minimize enters floating mode without stopping loop");
            runner.Tick(new GameWindow(1, (IntPtr)42, "Synthetic"), layout, sample, true, 1, ClassProfiles.Find(103));
            Check(!runner.Enabled && !floating.StatusButton.LoopEnabled && !button.LoopEnabled,
                "automatic specialization stop updates both indicators while minimized");
            var blockedRegion = floating.Bounds;
            floating.AvoidCapture(blockedRegion);
            Check(!floating.Bounds.IntersectsWith(blockedRegion), "visible floating window avoids a newly overlapping capture");
            floating.StatusButton.PerformClick(); Application.DoEvents();
            Check(form.Bounds == originalBounds, "repeated minimize and restore retains normal bounds");
            form.WindowState = FormWindowState.Maximized; Application.DoEvents();
            SendMessageW(form.Handle, 0x0112, (IntPtr)0xF020, IntPtr.Zero);
            floating.StatusButton.PerformClick(); Application.DoEvents();
            Check(form.Visible && form.WindowState == FormWindowState.Maximized, "restore preserves maximized window state");
            form.WindowState = FormWindowState.Normal; Application.DoEvents();
            form.WindowState = FormWindowState.Minimized; Application.DoEvents();
            Check(!form.Visible && floating.Visible, "programmatic minimize also enters floating mode");
            floating.StatusButton.PerformClick(); Application.DoEvents();
            Check(form.Visible && form.WindowState == FormWindowState.Normal && form.Bounds == originalBounds,
                $"programmatic minimize restores original normal window (visible={form.Visible}, state={form.WindowState}, bounds={form.Bounds}, expected={originalBounds})");
            // Exercise the actual drag handlers without injecting OS input or toggling the mouse hook.
            button.PerformClick(); Application.DoEvents();
            var originalCursor = Cursor.Position;
            try
            {
                var down = typeof(Control).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var move = typeof(Control).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var up = typeof(Control).GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var click = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!;
                down.Invoke(floating.StatusButton, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 22, 22, 0) });
                var area = Screen.FromControl(floating).WorkingArea;
                Cursor.Position = new Point(Math.Clamp(originalCursor.X - 100, area.Left, area.Right - 1),
                    Math.Clamp(originalCursor.Y - 100, area.Top, area.Bottom - 1));
                move.Invoke(floating.StatusButton, new object[] { new MouseEventArgs(MouseButtons.Left, 0, 22, 22, 0) });
                up.Invoke(floating.StatusButton, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 22, 22, 0) });
                click.Invoke(floating.StatusButton, new object[] { EventArgs.Empty });
                Check(!form.Visible && floating.Visible, "releasing drag does not expand main window");
                Check(new FloatingWindowSettings(directory).Position == floating.Location, "drag completion persists floating position");
                down.Invoke(floating.StatusButton, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 22, 22, 0) });
                click.Invoke(floating.StatusButton, new object[] { EventArgs.Empty });
                Check(form.Visible && !floating.Visible, "next click after dragging can expand normally");
            }
            finally { Cursor.Position = originalCursor; }
            form.CollapseToFloating();
            form.Close();
            Check(form.IsDisposed && floating.IsDisposed && !runner.Enabled, "closing main form removes floating window and stops execution");
            Console.WriteLine("Floating UI checks passed: " + output);
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories)) File.Delete(file);
            foreach (string child in Directory.GetDirectories(directory)) Directory.Delete(child);
            Directory.Delete(directory);
        }
    }
}
