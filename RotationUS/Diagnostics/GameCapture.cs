#nullable enable
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace RotationUS.Diagnostics;

internal sealed record GameWindow(int Pid, IntPtr Handle, string Title)
{
    public override string ToString() => $"Wow.exe · PID {Pid} · {Title}";
}
internal sealed class Captured : IDisposable
{
    public Bitmap Image { get; }
    public Rectangle Bounds { get; }
    public Captured(Bitmap image, Rectangle bounds) { Image = image; Bounds = bounds; }
    public Color At(SamplePoint point) => Image.GetPixel(point.X - Bounds.X, point.Y - Bounds.Y);
    public void Dispose() => Image.Dispose();
}

internal sealed class CaptureBuffer : IDisposable
{
    private Captured? frame;
    private Graphics? graphics;
    internal Captured Read(GameWindow window, Rectangle region, Rectangle diagnosticWindow, Size? knownSize = null)
    {
        var screen = GameCapture.ScreenRegion(window, region, diagnosticWindow, knownSize);
        if (frame is null || frame.Bounds != region)
        {
            Dispose();
            frame = new Captured(new Bitmap(region.Width, region.Height), region);
            graphics = Graphics.FromImage(frame.Image);
        }
        graphics!.CopyFromScreen(screen.Location, Point.Empty, region.Size);
        return frame; // Borrowed until the next Read; the buffer owns its lifetime.
    }
    public void Dispose()
    {
        graphics?.Dispose(); graphics = null;
        frame?.Dispose(); frame = null;
    }
}

internal static class GameCapture
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint point);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    internal static void Activate(GameWindow window)
    {
        ClientSize(window);
        if (!SetForegroundWindow(window.Handle)) throw new InvalidOperationException("无法切换到游戏，请将游戏置于前台后重试取点。");
    }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    public static GameWindow[] Windows()
    {
        var windows = new List<GameWindow>();
        foreach (var process in Process.GetProcessesByName("Wow"))
            using (process)
            {
                try { if (process.MainWindowHandle != IntPtr.Zero) windows.Add(new(process.Id, process.MainWindowHandle, process.MainWindowTitle)); }
                catch (InvalidOperationException) { }
            }
        return windows.ToArray();
    }
    public static Size ClientSize(GameWindow window)
    {
        GetWindowThreadProcessId(window.Handle, out uint pid);
        if (pid != window.Pid || IsIconic(window.Handle) || !GetClientRect(window.Handle, out var rect) || rect.Right <= 0 || rect.Bottom <= 0)
            throw new InvalidOperationException("游戏窗口已退出或最小化。请恢复窗口，必要时刷新游戏列表。");
        return new(rect.Right, rect.Bottom);
    }
    public static Captured Read(GameWindow window, Rectangle region, Rectangle diagnosticWindow)
    {
        var screen = ScreenRegion(window, region, diagnosticWindow);
        var image = new Bitmap(region.Width, region.Height);
        try
        {
            using var graphics = Graphics.FromImage(image);
            graphics.CopyFromScreen(screen.Location, Point.Empty, image.Size);
            return new(image, region);
        }
        catch { image.Dispose(); throw; }
    }
    internal static Rectangle ScreenRegion(GameWindow window, Rectangle region, Rectangle diagnosticWindow, Size? knownSize = null)
    {
        var size = knownSize ?? ClientSize(window);
        if (!new Rectangle(Point.Empty, size).Contains(region)) throw new InvalidOperationException("采样区域超出游戏客户区，请重新校准。");
        var origin = new NativePoint { X = region.Left, Y = region.Top };
        if (!ClientToScreen(window.Handle, ref origin)) throw new InvalidOperationException("无法定位游戏客户区。");
        var screen = new Rectangle(origin.X, origin.Y, region.Width, region.Height);
        if (screen.IntersectsWith(diagnosticWindow)) throw new InvalidOperationException("诊断窗口遮挡了采样区域，请移到游戏下方或第二块屏幕。");
        return screen;
    }
}
