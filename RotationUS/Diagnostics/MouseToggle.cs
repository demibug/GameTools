#nullable enable
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RotationUS.Diagnostics;

internal sealed class MouseToggleEdges
{
    private readonly HashSet<int> held = new();
    internal bool Observe(int message, uint data)
    {
        int button = message is 0x207 or 0x208 ? 0 : (int)(data >> 16);
        if (message == 0x207 || message == 0x20B && button is 1 or 2)
            return held.Add(button);
        if (message is 0x208 or 0x20C) held.Remove(button);
        return false;
    }
}

// The WinForms message loop delivers the hook. Queue work and immediately pass
// the mouse event on so the game keeps receiving its normal mouse controls.
internal sealed class MouseToggle : IDisposable
{
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseData
    {
        public int X, Y;
        public uint Data, Flags, Time;
        public UIntPtr ExtraInfo;
    }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int type, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? name);
    private readonly HookProc callback;
    private readonly MouseToggleEdges edges = new();
    private IntPtr hook;

    internal MouseToggle(Action requestToggle)
    {
        callback = (code, message, data) =>
        {
            int id = (int)message;
            if (code >= 0 && id is 0x207 or 0x208 or 0x20B or 0x20C)
            {
                var mouse = Marshal.PtrToStructure<MouseData>(data);
                if (edges.Observe(id, mouse.Data)) requestToggle();
            }
            return CallNextHookEx(hook, code, message, data);
        };
        hook = SetWindowsHookExW(14, callback, GetModuleHandleW(null), 0);
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法监听鼠标中键 / 侧键");
    }
    public void Dispose()
    {
        if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        GC.KeepAlive(callback);
    }
}
