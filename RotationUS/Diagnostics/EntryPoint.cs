#nullable enable
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RotationUS.Diagnostics;

internal static class EntryPoint
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint processId);
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int handle);

    private static void EnsureConsole()
    {
        // Keep redirected command output (including automated checks) intact.
        var output = GetStdHandle(-11);
        if (output != IntPtr.Zero && output != new IntPtr(-1)) return;
        if (!AttachConsole(uint.MaxValue)) AllocConsole();
    }

    [STAThread]
    private static int Main(string[] args)
    {
        bool consoleMode = args.Length > 0 && args[0] is "--self-test" or "--ui-smoke" or "--ui-position-smoke" or "--ui-picker-smoke" or "--ui-fury-smoke" or "--profile" or "--log-profile" or "--legacy";
        try
        {
            if (consoleMode) EnsureConsole();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.SequenceEqual(new[] { "--self-test" })) { DiagnosticTests.Run(); return 0; }
            if (args.Length == 2 && args[0] == "--profile") { PerformanceProbe.Run(args[1]); return 0; }
            if (args.SequenceEqual(new[] { "--log-profile" })) { PerformanceProbe.RunLogs(); return 0; }
            if (args.Length == 2 && args[0] == "--ui-smoke") { DiagnosticTests.Render(args[1]); return 0; }
            if (args.Length == 2 && args[0] == "--ui-position-smoke") { DiagnosticTests.Render(args[1], true); return 0; }
            if (args.Length == 2 && args[0] == "--ui-picker-smoke") { DiagnosticTests.RenderPicker(args[1]); return 0; }
            if (args.Length == 2 && args[0] == "--ui-fury-smoke") { FuryTests.Render(args[1]); return 0; }
            if (args.SequenceEqual(new[] { "--legacy" })) { global::Program.RunLegacy(); return 0; }
            if (args.Length != 0 && !args.SequenceEqual(new[] { "--diagnose" }))
                throw new ArgumentException("参数：--diagnose（默认）、--self-test、--profile <坐标 JSON>、--legacy");
            Application.Run(new DiagnosticForm());
            return 0;
        }
        catch (Exception e)
        {
            if (consoleMode) Console.Error.WriteLine(e);
            else MessageBox.Show("启动失败：" + e.Message, "RotationUS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
