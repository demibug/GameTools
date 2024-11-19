using System.Drawing;
using System.Runtime.InteropServices;

class Program
{
    // 引入Win32 API
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")]
    public static extern short GetKeyState(int vKey);
    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);



    // 键盘事件常量
    const byte VK_1 = 0x31;
    const byte VK_2 = 0x32;
    const byte VK_3 = 0x33;
    const byte VK_4 = 0x34;
    const byte VK_5 = 0x35;
    const byte VK_6 = 0x36;
    const byte VK_7 = 0x37;
    const byte VK_8 = 0x38;
    const byte VK_9 = 0x39;
    const byte VK_0 = 0x30;
    const byte VK_Jian = 0xBD;  // "-"
    const byte VK_Deng = 0xBB;  // "="
    const byte VK_FangkuohaoZuo = 0xDB; // "["
    const byte VK_FangkuohaoYou = 0xDD; // "]"
    const byte VK_Xiegang = 0xDC;   // "/"
    const byte VK_Fenhao = 0xBA;   // ";"
    const byte VK_Danyinhao = 0xDE;   // "'"
    const byte VK_Juhao = 0xBE; // "."
    const byte VK_SHIFT = 0x10; // Shift 键
    const byte VK_ALT = 0x12; // Alt 键

    const int VK_MENU = 0x12; // Alt 键的虚拟键码

    const uint KEYEVENTF_KEYDOWN = 0x0000;
    const uint KEYEVENTF_KEYUP = 0x0002;

    const int WM_HOTKEY = 0x0312;
    const int PM_REMOVE = 0x0001; // 消息从队列中移除


    // 需要跳过检测的键的虚拟键码
    private static int[] skipKeys = {
            0xC0, // ` 键
            0x31, 0x32, 0x33, 0x34, 0x35, 0x36, // 1 2 3 4 5 6
            0x51, 0x45, 0x52, 0x54, 0x46, 0x47, // Q E R T F G
            0x5A, 0x58, 0x43, 0x56, 0x42, // Z X C V B
            // 包含大写字母的组合键
            //0x31 + 0x20, 0x32 + 0x20, 0x33 + 0x20, 0x34 + 0x20, 0x35 + 0x20, 0x36 + 0x20, // Shift + 1 2 3 4 5 6
            //0x51 + 0x20, 0x45 + 0x20, 0x52 + 0x20, 0x54 + 0x20, 0x46 + 0x20, 0x47 + 0x20, // Shift + Q E R T F G
            //0x5A + 0x20, 0x58 + 0x20, 0x43 + 0x20, 0x56 + 0x20, 0x42 + 0x20 // Shift + Z X C V B
        };


    private static bool wasPressedMiddleButton;
    private static int sleepTime;
    private static Bitmap bitmap;


    private static Dictionary<int, bool> dictState = new Dictionary<int, bool>();
    // 将 dictPts 中的窗口相对坐标转换为屏幕坐标
    private static Dictionary<int, POINT> screenPts = new Dictionary<int, POINT>();

    private static Dictionary<int, int> dictSleepTime = new Dictionary<int, int>();

    // POINT结构体，用于保存坐标位置
    public struct POINT
    {
        public int X;
        public int Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    const int INPUT_KEYBOARD = 1;

    ~Program()
    {
        bitmap?.Dispose();
    }

    static void Main()
    {
        Dictionary<int, POINT> dictPts = new Dictionary<int, POINT>();
        int i = 1;
        // home
        //dictPts[i++] = new POINT { X = 15, Y = 1350 };
        //dictPts[i++] = new POINT { X = 28, Y = 1350 };
        //dictPts[i++] = new POINT { X = 38, Y = 1350 };
        //dictPts[i++] = new POINT { X = 49, Y = 1350 };
        //dictPts[i++] = new POINT { X = 61, Y = 1350 };
        //dictPts[i++] = new POINT { X = 74, Y = 1350 };
        //dictPts[i++] = new POINT { X = 83, Y = 1350 };
        //dictPts[i++] = new POINT { X = 96, Y = 1350 };
        //dictPts[i++] = new POINT { X = 107, Y = 1350 };
        //dictPts[i++] = new POINT { X = 117, Y = 1350 };
        //dictPts[i++] = new POINT { X = 129, Y = 1350 };
        //dictPts[i++] = new POINT { X = 140, Y = 1350 };
        //dictPts[i++] = new POINT { X = 152, Y = 1350 };
        //dictPts[i++] = new POINT { X = 164, Y = 1350 };
        //dictPts[i++] = new POINT { X = 175, Y = 1350 };
        //dictPts[i++] = new POINT { X = 185, Y = 1350 };
        //dictPts[i++] = new POINT { X = 196, Y = 1350 };
        //dictPts[i++] = new POINT { X = 209, Y = 1350 };
        //dictPts[i++] = new POINT { X = 222, Y = 1350 };
        //dictPts[i++] = new POINT { X = 233, Y = 1350 };
        //dictPts[i++] = new POINT { X = 243, Y = 1350 };
        //dictPts[i++] = new POINT { X = 254, Y = 1350 };
        //dictPts[i++] = new POINT { X = 264, Y = 1350 };
        //dictPts[i++] = new POINT { X = 279, Y = 1350 };

        // work
        dictPts[i++] = new POINT { X = 17, Y = 1339 };
        dictPts[i++] = new POINT { X = 28, Y = 1339 };
        dictPts[i++] = new POINT { X = 38, Y = 1339 };
        dictPts[i++] = new POINT { X = 49, Y = 1339 };
        dictPts[i++] = new POINT { X = 61, Y = 1339 };
        dictPts[i++] = new POINT { X = 74, Y = 1339 };
        dictPts[i++] = new POINT { X = 83, Y = 1339 };
        dictPts[i++] = new POINT { X = 96, Y = 1339 };
        dictPts[i++] = new POINT { X = 107, Y = 1339 };
        dictPts[i++] = new POINT { X = 117, Y = 1339 };
        dictPts[i++] = new POINT { X = 129, Y = 1339 };
        dictPts[i++] = new POINT { X = 140, Y = 1339 };
        dictPts[i++] = new POINT { X = 152, Y = 1339 };
        dictPts[i++] = new POINT { X = 164, Y = 1339 };
        dictPts[i++] = new POINT { X = 175, Y = 1339 };
        dictPts[i++] = new POINT { X = 185, Y = 1339 };
        dictPts[i++] = new POINT { X = 196, Y = 1339 };
        dictPts[i++] = new POINT { X = 208, Y = 1339 };
        dictPts[i++] = new POINT { X = 219, Y = 1339 };
        dictPts[i++] = new POINT { X = 231, Y = 1339 };
        dictPts[i++] = new POINT { X = 242, Y = 1339 };
        dictPts[i++] = new POINT { X = 253, Y = 1339 };
        dictPts[i++] = new POINT { X = 264, Y = 1339 };
        dictPts[i++] = new POINT { X = 275, Y = 1339 };
        bool isPaused = true; // 程序暂停状态

        InitSleepTime();

        while (true)
        {
            // 每隔 100 毫秒检测一次
            Thread.Sleep(sleepTime);

            // 检查是否按下鼠标中键，切换暂停状态
            if (IsMouseMiddleButtonPressed())
            {
                if (wasPressedMiddleButton == false)
                {
                    isPaused = !isPaused; // 切换暂停状态
                    Console.WriteLine(isPaused ? "=====================程序已暂停=====================" : "!!!!!程序已继续!!!!!");
                    if (isPaused)
                    {
                        OnDisable();
                    }
                    else
                    {
                        OnEnable();
                    }

                    wasPressedMiddleButton = true;
                }
            }
            else
            {
                wasPressedMiddleButton = false;
            }

            // 如果程序处于暂停状态，跳过后续处理
            if (isPaused)
            {
                continue;
            }

            // 检查是否按下特定键，如果按下则跳过检测
            if (IsSkipKeyPressed())
            {
                continue; // 跳过本次检测
            }

            // 获取当前窗口句柄
            IntPtr hWnd = GetForegroundWindow();

            GetColors(hWnd, dictPts);
            CheckState(dictState);

            // 挂机
            //SimulateKeyPress(0x41);
            //Thread.Sleep(10000);
            //SimulateKeyPress(0x44);
            //Thread.Sleep(20000);
        }
    }

    private static void InitSleepTime()
    {
        for (int i = 0; i < 24; i++)
        {
            int key = i + 1;
            int delayTime = new Random().Next(50, 150);
            dictSleepTime.Add(key, delayTime);

        }
    }

    private static void GetColors(IntPtr hWnd, Dictionary<int, POINT> dictPts)
    {
        screenPts.Clear();
        dictState.Clear();

        // 获取窗口的屏幕位置和大小
        if (!GetWindowRect(hWnd, out RECT windowRect))
        {
            return;
        }

        foreach (var kvp in dictPts)
        {
            POINT pt = kvp.Value;
            ClientToScreen(hWnd, ref pt); // 将窗口坐标转换为屏幕坐标
            screenPts[kvp.Key] = pt;

            // 检查点是否在窗口范围内
            if (pt.X < windowRect.Left || pt.X > windowRect.Right ||
                pt.Y < windowRect.Top || pt.Y > windowRect.Bottom)
            {

                return;
            }
        }


        // 计算拷贝区域的最小矩形
        Rectangle bounds = GetBounds(screenPts); // 确保 GetBounds 使用的是屏幕坐标

        // 检查是否需要重新创建 Bitmap
        if (bitmap == null || bitmap.Width != bounds.Width || bitmap.Height != bounds.Height)
        {
            bitmap?.Dispose(); // 释放之前的 Bitmap
            bitmap = new Bitmap(bounds.Width, bounds.Height);
        }

        using (Graphics g = Graphics.FromImage(bitmap))
        {
            // 拷贝屏幕
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }

        // 检查每个点的颜色
        foreach (var kvp in screenPts)
        {
            int key = kvp.Key;
            POINT pt = kvp.Value;

            // 获取颜色
            Color color = bitmap.GetPixel(pt.X - bounds.X, pt.Y - bounds.Y);
            bool isActive = color.R == 255 && color.G == 255 && color.B == 255;
            dictState[key] = isActive;
            if (isActive)
            {
                sleepTime = dictSleepTime[key];
                break; // 如果找到活跃点，提前结束
            }
        }
    }

    private static Rectangle GetBounds(Dictionary<int, POINT> dictPts)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        // 查找最小和最大坐标
        foreach (var kvp in dictPts)
        {
            POINT pt = kvp.Value;
            if (pt.X < minX) minX = pt.X;
            if (pt.Y < minY) minY = pt.Y;
            if (pt.X > maxX) maxX = pt.X;
            if (pt.Y > maxY) maxY = pt.Y;
        }

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static void CheckState(Dictionary<int, bool> dictState)
    {
        if (CheckPt(1))
        {
            SimulateKeyPress(VK_7);
        }
        else if (CheckPt(2))
        {
            SimulateKeyPress(VK_8);
        }
        else if (CheckPt(3))
        {
            SimulateKeyPress(VK_9);
        }
        else if (CheckPt(4))
        {
            SimulateKeyPress(VK_0);
        }
        else if (CheckPt(5))
        {
            SimulateKeyPress(VK_Jian);
        }
        else if (CheckPt(6))
        {
            SimulateKeyPress(VK_Deng);
        }
        else if (CheckPt(7))
        {
            SimulateKeyPress(VK_FangkuohaoZuo);
        }
        else if (CheckPt(8))
        {
            SimulateKeyPress(VK_FangkuohaoYou);
        }
        else if (CheckPt(9))
        {
            SimulateKeyPress(VK_Xiegang);
        }
        else if (CheckPt(10))
        {
            SimulateKeyPress(VK_Fenhao);
        }
        else if (CheckPt(11))
        {
            SimulateKeyPress(VK_Danyinhao);
        }
        else if (CheckPt(12))
        {
            SimulateKeyPress(VK_Juhao);
        }
        else if (CheckPt(13))
        {
            SimulateShiftKeyPress(VK_7);
        }
        else if (CheckPt(14))
        {
            SimulateShiftKeyPress(VK_8);
        }
        else if (CheckPt(15))
        {
            SimulateShiftKeyPress(VK_9);
        }
        else if (CheckPt(16))
        {
            SimulateShiftKeyPress(VK_0);
        }
        else if (CheckPt(17))
        {
            SimulateShiftKeyPress(VK_Jian);
        }
        else if (CheckPt(18))
        {
            SimulateShiftKeyPress(VK_Deng);
        }
        else if (CheckPt(19))
        {
            SimulateShiftKeyPress(VK_1);
        }
        else if (CheckPt(20))
        {
            SimulateShiftKeyPress(VK_2);
        }
        else if (CheckPt(21))
        {
            SimulateShiftKeyPress(VK_3);
        }
        else if (CheckPt(22))
        {
            SimulateShiftKeyPress(VK_4);
        }
        else if (CheckPt(23))
        {
            SimulateShiftKeyPress(VK_5);
        }
        else if (CheckPt(24))
        {
            SimulateShiftKeyPress(VK_6);
        }
    }

    private static bool CheckPt(int index)
    {
        if (dictState.ContainsKey(index) && dictState[index] == true)
        {
            return true;
        }
        return false;
    }

    // 使用 SendInput 模拟键盘按键的函数
    private static void SimulateKeyPress(byte keyCode)
    {
        INPUT[] inputs = new INPUT[2];

        // 按下按键
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = keyCode;
        inputs[0].u.ki.dwFlags = KEYEVENTF_KEYDOWN;

        // 松开按键
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = keyCode;
        inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void ReleaseShiftKey()
    {
        INPUT[] inputs = new INPUT[1];

        // 弹起 Shift 键
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = VK_SHIFT;
        inputs[0].u.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }


    private static void SimulateShiftKeyPress(byte keyCode)
    {
        INPUT[] inputs = new INPUT[4];

        // 按下 Shift 键
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = VK_SHIFT;
        inputs[0].u.ki.dwFlags = KEYEVENTF_KEYDOWN;

        // 按下指定按键
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = keyCode;
        inputs[1].u.ki.dwFlags = KEYEVENTF_KEYDOWN;

        // 松开指定按键
        inputs[2].type = INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = keyCode;
        inputs[2].u.ki.dwFlags = KEYEVENTF_KEYUP;

        // 松开 Shift 键
        inputs[3].type = INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = VK_SHIFT;
        inputs[3].u.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void SimulateAltKeyPress(byte keyCode)
    {
        INPUT[] inputs = new INPUT[4];

        // 按下 Alt 键
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = VK_ALT;
        inputs[0].u.ki.dwFlags = KEYEVENTF_KEYDOWN;

        // 按下指定按键
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = keyCode;
        inputs[1].u.ki.dwFlags = KEYEVENTF_KEYDOWN;

        // 松开指定按键
        inputs[2].type = INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = keyCode;
        inputs[2].u.ki.dwFlags = KEYEVENTF_KEYUP;

        // 松开 Alt 键
        inputs[3].type = INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = VK_ALT;
        inputs[3].u.ki.dwFlags = KEYEVENTF_KEYUP;

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    // 将客户端坐标转换为屏幕坐标
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    // 检查特定键是否被按下
    private static bool IsSkipKeyPressed()
    {
        bool isAltPressed = (IsKeyPressed(VK_MENU));
        if (isAltPressed)
        {
            return true;
        }

        foreach (var vk in skipKeys)
        {
            if (IsKeyPressed(vk))
            {
                // 每200ms发送一次
                Thread.Sleep(500);
                return true;
            }
        }

        return false; // 没有键被按下，返回 false

    }

    private static bool IsKeyPressed(int vk)
    {
        // 检查按键状态，GetAsyncKeyState的返回值最高位为1表示按下
        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    // 检查鼠标中键是否被按下
    private static bool IsMouseMiddleButtonPressed()
    {
        const int VK_MBUTTON = 0x04; // 鼠标中键的虚拟键码
        return (GetAsyncKeyState(VK_MBUTTON) < 0);
    }

    private static void OnEnable()
    {
    }

    private static void OnDisable()
    {
    }
}
