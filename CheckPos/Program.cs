using System.Drawing;
using System.Runtime.InteropServices;

class Program
{
    // 引入Win32 API
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    // POINT结构体，用于保存鼠标位置
    public struct POINT
    {
        public int X;
        public int Y;
    }

    const int HOTKEY_ID = 1;  // 热键ID
    const int VK_TILDE = 0xC0; // '~' 键的虚拟键码
    const int MOD_NOREPEAT = 0x4000; // 防止重复触发

    static void Main()
    {
        // 注册全局热键并启动消息循环
        Console.WriteLine("按下 ~ 键以获取鼠标在窗口内的相对位置和颜色");
        RegisterHotKey(IntPtr.Zero, HOTKEY_ID, MOD_NOREPEAT, VK_TILDE);

        // 消息循环
        while (true)
        {
            MSG msg = new MSG();
            if (GetMessage(ref msg, IntPtr.Zero, 0, 0))
            {
                if (msg.message == 0x0312 && msg.wParam.ToInt32() == HOTKEY_ID)
                {
                    ShowMouseInfo();
                }
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
    }

    // 显示鼠标位置和颜色
    private static void ShowMouseInfo()
    {
        POINT mousePos;
        GetCursorPos(out mousePos);

        IntPtr hWnd = WindowFromPoint(mousePos);  // 获取鼠标下的窗口句柄

        // 获取相对窗口的鼠标位置
        POINT clientPos = mousePos;
        ScreenToClient(hWnd, ref clientPos);  // 将屏幕坐标转换为窗口内坐标

        // 获取鼠标所在位置的颜色
        Color color = GetColorAt(mousePos.X, mousePos.Y);

        // 显示结果
        Console.WriteLine($"窗口句柄: {hWnd}");
        Console.WriteLine($"屏幕位置: ({mousePos.X}, {mousePos.Y})");
        Console.WriteLine($"窗口相对位置: ({clientPos.X}, {clientPos.Y})");
        Console.WriteLine($"颜色值: {color}");
    }

    // 获取指定位置的颜色
    private static Color GetColorAt(int x, int y)
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        uint pixel = GetPixel(hdc, x, y);
        ReleaseDC(IntPtr.Zero, hdc);

        int r = (int)(pixel & 0x000000FF);
        int g = (int)(pixel & 0x0000FF00) >> 8;
        int b = (int)(pixel & 0x00FF0000) >> 16;

        return Color.FromArgb(r, g, b);
    }

    // Win32 消息结构体
    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [DllImport("user32.dll")]
    private static extern bool GetMessage(ref MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);
}
