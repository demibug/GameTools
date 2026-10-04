#nullable enable
using System.Diagnostics;
using System.Drawing;

namespace RotationUS.Diagnostics;

internal static class PerformanceProbe
{
    internal static void RunLogs()
    {
        var isolatedLogs = new SavedLogStore(Path.Combine(Path.GetTempPath(), "RotationUS-log-profile-" + Guid.NewGuid().ToString("N")));
        using var form = new DiagnosticForm(listenForMouse: false, savedLogs: isolatedLogs,
            profileLayouts: new ProfileLayouts(Path.GetDirectoryName(isolatedLogs.DefaultDirectory)!));
        Measure("Logs: 32 entries with an empty list", () => form.ProfileLogChurn(32, reset: true));
        form.ProfileLogChurn(1000, reset: true);
        Measure("Logs: 32 entries with a full 1000-line list", () => form.ProfileLogChurn(32));
    }
    internal static void Run(string layoutPath)
    {
        var layout = DiagnosticLayout.Load(layoutPath);
        var games = GameCapture.Windows();
        if (games.Length != 1) throw new InvalidOperationException("性能检查需要一个游戏窗口。");
        var game = games[0];
        if (GameCapture.ClientSize(game) != new Size(layout.ClientWidth, layout.ClientHeight))
            throw new InvalidOperationException("性能检查坐标与游戏尺寸不一致。");
        var region = layout.CaptureBounds;
        Console.WriteLine($"Capture region: {region.Width}x{region.Height}; iterations: 40; no input sent.");
        Measure("Capture: allocate bitmap + CopyFromScreen", () =>
        {
            using var frame = GameCapture.Read(game, region, Rectangle.Empty);
        });
        using var buffer = new CaptureBuffer();
        Measure("Capture: reuse bitmap + graphics", () => buffer.Read(game, region, Rectangle.Empty));
        var sample = buffer.Read(game, region, Rectangle.Empty);
        if (!ReferenceEquals(sample, buffer.Read(game, region, Rectangle.Empty)))
            throw new InvalidOperationException("截图缓冲区没有复用。");
        // Exercise the live path: read pixels and clone while the graphics is retained.
        _ = sample.At(layout.Frames[0]);
        using (var snapshot = (Bitmap)sample.Image.Clone())
            if (snapshot.Size != sample.Image.Size) throw new InvalidOperationException("采样快照尺寸不正确。");
        // Build a full coordinate image from the same captured pixels; no extra screen read for UI.
        using var image = new Bitmap(region.Right, region.Bottom);
        using (var graphics = Graphics.FromImage(image)) graphics.DrawImageUnscaled(sample.Image, region.Location);
        var isolatedLogs = new SavedLogStore(Path.Combine(Path.GetTempPath(), "RotationUS-profile-" + Guid.NewGuid().ToString("N")));
        using var form = new DiagnosticForm(listenForMouse: false, savedLogs: isolatedLogs);
        form.CreateControl(); form.PerformLayout();
        Measure("UI: sample copy + 53 row updates (offscreen)", () => form.ProfilePresentation(layout, image));
        using var rendered = new Bitmap(form.Width, form.Height);
        Measure("UI: row updates + full window paint (offscreen)", () =>
        {
            form.ProfilePresentation(layout, image);
            form.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size));
        });
        var runner = new RotationExecution(() => game.Handle, () => false, (_, _) => { });
        runner.Start();
        Measure("Execution: decode calibrated points (input disabled)", () => runner.Tick(game, layout, sample, true, 0));
    }
    private static void Measure(string name, Action action)
    {
        for (int i = 0; i < 5; i++) action();
        long allocations = GC.GetAllocatedBytesForCurrentThread();
        var timings = new double[40];
        for (int i = 0; i < timings.Length; i++)
        {
            long start = Stopwatch.GetTimestamp(); action();
            timings[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocations;
        Array.Sort(timings);
        Console.WriteLine($"{name}: mean={timings.Average():F3} ms; p95={timings[37]:F3} ms; allocated={bytes / timings.Length} bytes/frame");
    }
}
