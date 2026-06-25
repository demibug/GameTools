using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

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

    private static readonly PlayerClass ClassType = PlayerClass.BEAR;

    enum PlayerClass
    {
        FZ,
        UDK,
        WQZ,
        JX,
        TF,
        KTZ,
        FQ,
        BEAR,
        CAT,
        DHT,
    }

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
    public static int[] skipKeys = {
        0xC0, // ` 键
        0x31, // 1
        0x32, // 2
        0x33, // 3
        0x34, // 4
        0x35, // 5
        0x36, // 6
        0x51, // Q
        0x45, // E
        0x52, // R
        0x54, // T
        0x46, // F
        0x47, // G
        0x5A, // Z
        0x58, // X
        0x43, // C
        0x56, // V
        0x42, // B
        // 包含大写字母的组合键
        //0x31 + 0x20, 0x32 + 0x20, 0x33 + 0x20, 0x34 + 0x20, 0x35 + 0x20, 0x36 + 0x20, // Shift + 1 2 3 4 5 6
        //0x51 + 0x20, 0x45 + 0x20, 0x52 + 0x20, 0x54 + 0x20, 0x46 + 0x20, 0x47 + 0x20, // Shift + Q E R T F G
        //0x5A + 0x20, 0x58 + 0x20, 0x43 + 0x20, 0x56 + 0x20, 0x42 + 0x20 // Shift + Z X C V B
    };


    private static bool wasPressedMiddleButton;
    private static int sleepTime;
    private static Bitmap? frameBitmap;
    private static Bitmap? barBitmap;

    private static Dictionary<int, Color> dictFrameColors = new Dictionary<int, Color>();
    private static Dictionary<int, Color> dictBarColors = new Dictionary<int, Color>();
    private static Random random = new Random();

    private static Dictionary<int, bool> dictState = new Dictionary<int, bool>();

    // 将 dictPts 中的窗口相对坐标转换为屏幕坐标
    private static Dictionary<int, POINT> frameScreenPts = new Dictionary<int, POINT>();
    private static Dictionary<int, POINT> barScreenPts = new Dictionary<int, POINT>();

    //private static Dictionary<int, int> dictSleepTime = new Dictionary<int, int>();

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
        frameBitmap?.Dispose();
        barBitmap?.Dispose();
    }

    static void Main()
    {
        Dictionary<int, POINT> dictFrames = new Dictionary<int, POINT>();
        int frameY = 0;
        // home
        //dictFrames[1] = new POINT { X = 17, Y = frameY };
        //dictFrames[2] = new POINT { X = 29, Y = frameY };
        //dictFrames[3] = new POINT { X = 40, Y = frameY };
        //dictFrames[4] = new POINT { X = 52, Y = frameY };
        //dictFrames[5] = new POINT { X = 63, Y = frameY };
        //dictFrames[6] = new POINT { X = 74, Y = frameY };
        //dictFrames[7] = new POINT { X = 86, Y = frameY };
        //dictFrames[8] = new POINT { X = 97, Y = frameY };
        //dictFrames[9] = new POINT { X = 109, Y = frameY };
        //dictFrames[10] = new POINT { X = 120, Y = frameY };
        //dictFrames[11] = new POINT { X = 131, Y = frameY };
        //dictFrames[12] = new POINT { X = 143, Y = frameY };
        //dictFrames[13] = new POINT { X = 154, Y = frameY };
        //dictFrames[14] = new POINT { X = 166, Y = frameY };
        //dictFrames[15] = new POINT { X = 177, Y = frameY };
        //dictFrames[16] = new POINT { X = 189, Y = frameY };
        //dictFrames[17] = new POINT { X = 200, Y = frameY };
        //dictFrames[18] = new POINT { X = 211, Y = frameY };
        //dictFrames[19] = new POINT { X = 223, Y = frameY };
        //dictFrames[20] = new POINT { X = 234, Y = frameY };
        //dictFrames[21] = new POINT { X = 246, Y = frameY };
        //dictFrames[22] = new POINT { X = 257, Y = frameY };
        //dictFrames[23] = new POINT { X = 268, Y = frameY };
        //dictFrames[24] = new POINT { X = 280, Y = frameY };
        //dictFrames[25] = new POINT { X = 291, Y = frameY };
        //dictFrames[26] = new POINT { X = 303, Y = frameY };
        //dictFrames[27] = new POINT { X = 314, Y = frameY };
        //dictFrames[28] = new POINT { X = 325, Y = frameY };
        //dictFrames[29] = new POINT { X = 337, Y = frameY };
        //dictFrames[30] = new POINT { X = 348, Y = frameY };
        //dictFrames[31] = new POINT { X = 360, Y = frameY };
        //dictFrames[32] = new POINT { X = 371, Y = frameY };
        //dictFrames[33] = new POINT { X = 382, Y = frameY };
        //dictFrames[34] = new POINT { X = 394, Y = frameY };
        //dictFrames[35] = new POINT { X = 405, Y = frameY };
        //dictFrames[36] = new POINT { X = 417, Y = frameY };
        //dictFrames[37] = new POINT { X = 428, Y = frameY };
        //dictFrames[38] = new POINT { X = 440, Y = frameY };
        //dictFrames[39] = new POINT { X = 451, Y = frameY };
        //dictFrames[40] = new POINT { X = 462, Y = frameY };
        //dictFrames[41] = new POINT { X = 474, Y = frameY };
        //dictFrames[42] = new POINT { X = 485, Y = frameY };
        //dictFrames[43] = new POINT { X = 497, Y = frameY };
        //dictFrames[44] = new POINT { X = 508, Y = frameY };
        //dictFrames[45] = new POINT { X = 519, Y = frameY };
        //dictFrames[46] = new POINT { X = 531, Y = frameY };
        //dictFrames[47] = new POINT { X = 542, Y = frameY };
        //dictFrames[48] = new POINT { X = 554, Y = frameY };
        //dictFrames[49] = new POINT { X = 565, Y = frameY };
        //dictFrames[50] = new POINT { X = 576, Y = frameY };

        // laptop
        dictFrames[1] = new POINT { X = 17, Y = frameY };
        dictFrames[2] = new POINT { X = 29, Y = frameY };
        dictFrames[3] = new POINT { X = 40, Y = frameY };
        dictFrames[4] = new POINT { X = 51, Y = frameY };
        dictFrames[5] = new POINT { X = 63, Y = frameY };
        dictFrames[6] = new POINT { X = 74, Y = frameY };
        dictFrames[7] = new POINT { X = 85, Y = frameY };
        dictFrames[8] = new POINT { X = 97, Y = frameY };
        dictFrames[9] = new POINT { X = 108, Y = frameY };
        dictFrames[10] = new POINT { X = 119, Y = frameY };
        dictFrames[11] = new POINT { X = 130, Y = frameY };
        dictFrames[12] = new POINT { X = 142, Y = frameY };
        dictFrames[13] = new POINT { X = 153, Y = frameY };
        dictFrames[14] = new POINT { X = 164, Y = frameY };
        dictFrames[15] = new POINT { X = 176, Y = frameY };
        dictFrames[16] = new POINT { X = 187, Y = frameY };
        dictFrames[17] = new POINT { X = 198, Y = frameY };
        dictFrames[18] = new POINT { X = 210, Y = frameY };
        dictFrames[19] = new POINT { X = 221, Y = frameY };
        dictFrames[20] = new POINT { X = 232, Y = frameY };
        dictFrames[21] = new POINT { X = 244, Y = frameY };
        dictFrames[22] = new POINT { X = 255, Y = frameY };
        dictFrames[23] = new POINT { X = 266, Y = frameY };
        dictFrames[24] = new POINT { X = 278, Y = frameY };
        dictFrames[25] = new POINT { X = 289, Y = frameY };
        dictFrames[26] = new POINT { X = 300, Y = frameY };
        dictFrames[27] = new POINT { X = 312, Y = frameY };
        dictFrames[28] = new POINT { X = 323, Y = frameY };
        dictFrames[29] = new POINT { X = 334, Y = frameY };
        dictFrames[30] = new POINT { X = 346, Y = frameY };
        dictFrames[31] = new POINT { X = 357, Y = frameY };
        dictFrames[32] = new POINT { X = 368, Y = frameY };
        dictFrames[33] = new POINT { X = 379, Y = frameY };
        dictFrames[34] = new POINT { X = 391, Y = frameY };
        dictFrames[35] = new POINT { X = 402, Y = frameY };
        dictFrames[36] = new POINT { X = 413, Y = frameY };
        dictFrames[37] = new POINT { X = 425, Y = frameY };
        dictFrames[38] = new POINT { X = 436, Y = frameY };
        dictFrames[39] = new POINT { X = 447, Y = frameY };
        dictFrames[40] = new POINT { X = 459, Y = frameY };
        dictFrames[41] = new POINT { X = 470, Y = frameY };
        dictFrames[42] = new POINT { X = 481, Y = frameY };
        dictFrames[43] = new POINT { X = 493, Y = frameY };
        dictFrames[44] = new POINT { X = 504, Y = frameY };
        dictFrames[45] = new POINT { X = 515, Y = frameY };
        dictFrames[46] = new POINT { X = 527, Y = frameY };
        dictFrames[47] = new POINT { X = 538, Y = frameY };
        dictFrames[48] = new POINT { X = 549, Y = frameY };
        dictFrames[49] = new POINT { X = 561, Y = frameY };
        dictFrames[50] = new POINT { X = 572, Y = frameY };

        // 特殊点
        dictFrames[51] = new POINT { X = 615, Y = 3 };


        Dictionary<int, POINT> dictBars = new Dictionary<int, POINT>();

        int barY = 1;
        // home
        //dictBars[1] = new POINT { X = 17, Y = barY };
        //dictBars[2] = new POINT { X = 29, Y = barY };
        //dictBars[3] = new POINT { X = 40, Y = barY };
        //dictBars[4] = new POINT { X = 52, Y = barY };
        //dictBars[5] = new POINT { X = 63, Y = barY };
        //dictBars[6] = new POINT { X = 74, Y = barY };
        //dictBars[7] = new POINT { X = 86, Y = barY };
        //dictBars[8] = new POINT { X = 97, Y = barY };
        //dictBars[9] = new POINT { X = 109, Y = barY };
        //dictBars[10] = new POINT { X = 120, Y = barY };
        //dictBars[11] = new POINT { X = 131, Y = barY };
        //dictBars[12] = new POINT { X = 143, Y = barY };
        //dictBars[13] = new POINT { X = 154, Y = barY };
        //dictBars[14] = new POINT { X = 166, Y = barY };
        //dictBars[15] = new POINT { X = 177, Y = barY };
        //dictBars[16] = new POINT { X = 189, Y = barY };
        //dictBars[17] = new POINT { X = 200, Y = barY };
        //dictBars[18] = new POINT { X = 211, Y = barY };
        //dictBars[19] = new POINT { X = 223, Y = barY };
        //dictBars[20] = new POINT { X = 234, Y = barY };
        //dictBars[21] = new POINT { X = 246, Y = barY };
        //dictBars[22] = new POINT { X = 257, Y = barY };
        //dictBars[23] = new POINT { X = 268, Y = barY };
        //dictBars[24] = new POINT { X = 280, Y = barY };
        //dictBars[25] = new POINT { X = 291, Y = barY };
        //dictBars[26] = new POINT { X = 303, Y = barY };
        //dictBars[27] = new POINT { X = 314, Y = barY };
        //dictBars[28] = new POINT { X = 325, Y = barY };
        //dictBars[29] = new POINT { X = 337, Y = barY };
        //dictBars[30] = new POINT { X = 348, Y = barY };
        //dictBars[31] = new POINT { X = 360, Y = barY };
        //dictBars[32] = new POINT { X = 371, Y = barY };
        //dictBars[33] = new POINT { X = 382, Y = barY };
        //dictBars[34] = new POINT { X = 394, Y = barY };
        //dictBars[35] = new POINT { X = 405, Y = barY };
        //dictBars[36] = new POINT { X = 417, Y = barY };
        //dictBars[37] = new POINT { X = 428, Y = barY };
        //dictBars[38] = new POINT { X = 440, Y = barY };
        //dictBars[39] = new POINT { X = 451, Y = barY };
        //dictBars[40] = new POINT { X = 462, Y = barY };
        //dictBars[41] = new POINT { X = 474, Y = barY };
        //dictBars[42] = new POINT { X = 485, Y = barY };
        //dictBars[43] = new POINT { X = 497, Y = barY };
        //dictBars[44] = new POINT { X = 508, Y = barY };
        //dictBars[45] = new POINT { X = 519, Y = barY };
        //dictBars[46] = new POINT { X = 531, Y = barY };
        //dictBars[47] = new POINT { X = 542, Y = barY };
        //dictBars[48] = new POINT { X = 554, Y = barY };
        //dictBars[49] = new POINT { X = 565, Y = barY };
        //dictBars[50] = new POINT { X = 576, Y = barY };

        // laptop
        dictBars[1] = new POINT { X = 17, Y = barY };
        dictBars[2] = new POINT { X = 29, Y = barY };
        dictBars[3] = new POINT { X = 40, Y = barY };
        dictBars[4] = new POINT { X = 51, Y = barY };
        dictBars[5] = new POINT { X = 63, Y = barY };
        dictBars[6] = new POINT { X = 74, Y = barY };
        dictBars[7] = new POINT { X = 85, Y = barY };
        dictBars[8] = new POINT { X = 97, Y = barY };
        dictBars[9] = new POINT { X = 108, Y = barY };
        dictBars[10] = new POINT { X = 119, Y = barY };
        dictBars[11] = new POINT { X = 130, Y = barY };
        dictBars[12] = new POINT { X = 142, Y = barY };
        dictBars[13] = new POINT { X = 153, Y = barY };
        dictBars[14] = new POINT { X = 164, Y = barY };
        dictBars[15] = new POINT { X = 176, Y = barY };
        dictBars[16] = new POINT { X = 187, Y = barY };
        dictBars[17] = new POINT { X = 198, Y = barY };
        dictBars[18] = new POINT { X = 210, Y = barY };
        dictBars[19] = new POINT { X = 221, Y = barY };
        dictBars[20] = new POINT { X = 232, Y = barY };
        dictBars[21] = new POINT { X = 244, Y = barY };
        dictBars[22] = new POINT { X = 255, Y = barY };
        dictBars[23] = new POINT { X = 266, Y = barY };
        dictBars[24] = new POINT { X = 278, Y = barY };
        dictBars[25] = new POINT { X = 289, Y = barY };
        dictBars[26] = new POINT { X = 300, Y = barY };
        dictBars[27] = new POINT { X = 312, Y = barY };
        dictBars[28] = new POINT { X = 323, Y = barY };
        dictBars[29] = new POINT { X = 334, Y = barY };
        dictBars[30] = new POINT { X = 346, Y = barY };
        dictBars[31] = new POINT { X = 357, Y = barY };
        dictBars[32] = new POINT { X = 368, Y = barY };
        dictBars[33] = new POINT { X = 379, Y = barY };
        dictBars[34] = new POINT { X = 391, Y = barY };
        dictBars[35] = new POINT { X = 402, Y = barY };
        dictBars[36] = new POINT { X = 413, Y = barY };
        dictBars[37] = new POINT { X = 425, Y = barY };
        dictBars[38] = new POINT { X = 436, Y = barY };
        dictBars[39] = new POINT { X = 447, Y = barY };
        dictBars[40] = new POINT { X = 459, Y = barY };
        dictBars[41] = new POINT { X = 470, Y = barY };
        dictBars[42] = new POINT { X = 481, Y = barY };
        dictBars[43] = new POINT { X = 493, Y = barY };
        dictBars[44] = new POINT { X = 504, Y = barY };
        dictBars[45] = new POINT { X = 515, Y = barY };
        dictBars[46] = new POINT { X = 527, Y = barY };
        dictBars[47] = new POINT { X = 538, Y = barY };
        dictBars[48] = new POINT { X = 549, Y = barY };
        dictBars[49] = new POINT { X = 561, Y = barY };
        dictBars[50] = new POINT { X = 572, Y = barY };


        bool isPaused = true; // 程序暂停状态


        //InitSleepTime(dictPts.Count);

        while (true)
        {
            // 每隔 100 毫秒检测一次
            Thread.Sleep(sleepTime);

            // 检查是否按下鼠标中键，切换暂停状态
            if (IsMouseSideButtonPressed() || IsMouseMiddleButtonPressed())
            //if (IsMouseMiddleButtonPressed())
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


            // 获取当前窗口句柄
            IntPtr hWnd = GetForegroundWindow();

            GetColors(hWnd, dictFrames, dictBars);
            int[] skipKeys = null;

            if (dictFrameColors.Count > 0)
            {
                switch(ClassType)
                {
                    case PlayerClass.FZ:
                        FZ.Inst.Process(dictFrameColors, dictBarColors, dictState);
                        skipKeys = FZ.Inst.skipKeys;
                        break;
                    case PlayerClass.UDK:
                        UDK.Inst.Process(dictFrameColors, dictBarColors, dictState);
                        skipKeys = UDK.Inst.skipKeys;
                        break;
                    case PlayerClass.WQZ:
                        WQZ.Inst.Process(dictFrameColors, dictBarColors, dictState);
                        skipKeys = WQZ.Inst.skipKeys;
                        break;
                    case PlayerClass.JX:
                        JX.Inst.Process(dictFrameColors, dictBarColors, dictState);
                        skipKeys = JX.Inst.skipKeys;
                        break;
                    case PlayerClass.TF:
                        TF.Inst.Process(dictFrameColors, dictBarColors, dictState);
                        skipKeys = TF.Inst.skipKeys;
                        break;
                    case PlayerClass.KTZ:
                        KTZ.Inst.Process(dictFrameColors, dictBarColors, dictState);
                        skipKeys = KTZ.Inst.skipKeys;
                        break;
                    case PlayerClass.FQ:
                        FQ.Inst.Process(dictFrameColors, dictBarColors, dictState);
                        skipKeys = FQ.Inst.skipKeys;
                        break;
                    case PlayerClass.BEAR:
                        BEAR.Inst.Process(dictFrameColors, dictBarColors, dictState);
                        skipKeys = BEAR.Inst.skipKeys;
                        break;
                    case PlayerClass.CAT:
                        CAT.Inst.Process(dictFrameColors, dictBarColors, dictState);
                        skipKeys= CAT.Inst.skipKeys;
                        break;
                    case PlayerClass.DHT:
                        DHT.Inst.Process(dictFrameColors, dictBarColors, dictState);
                        skipKeys = DHT.Inst.skipKeys;
                        break;
                }
            }

            // 检查是否按下特定键，如果按下则跳过检测
            if (IsSkipKeyPressed(skipKeys))
            {
                dictState.Clear();
                sleepTime = 500;
            }
            CheckState(dictState);

            // 挂机
            //SimulateKeyPress(0x41);
            //Thread.Sleep(10000);
            //SimulateKeyPress(0x44);
            //Thread.Sleep(20000);
        }
    }

    private static void GetColors(IntPtr hWnd, Dictionary<int, POINT> framePts, Dictionary<int, POINT> barPts)
    {
        dictState.Clear();
        frameScreenPts.Clear();
        barScreenPts.Clear();
        dictFrameColors.Clear();
        dictBarColors.Clear();

        // 获取窗口的屏幕位置和大小
        if (!GetWindowRect(hWnd, out RECT windowRect))
        {
            return;
        }

        foreach (var kvp in framePts)
        {
            POINT pt = kvp.Value;
            ClientToScreen(hWnd, ref pt); // 将窗口坐标转换为屏幕坐标
            frameScreenPts[kvp.Key] = pt;

            // 检查点是否在窗口范围内
            if (pt.X < windowRect.Left || pt.X > windowRect.Right ||
                pt.Y < windowRect.Top || pt.Y > windowRect.Bottom)
            {

                return;
            }
        }

        foreach(var kvp in barPts)
        {
            POINT pt = kvp.Value;
            ClientToScreen(hWnd, ref pt); // 将窗口坐标转换为屏幕坐标
            barScreenPts[kvp.Key] = pt;
            // 检查点是否在窗口范围内
            if (pt.X < windowRect.Left || pt.X > windowRect.Right ||
                pt.Y < windowRect.Top || pt.Y > windowRect.Bottom)
            {
                return;
            }
        }


        // 计算拷贝区域的最小矩形
        Rectangle frameBounds = GetBounds(frameScreenPts); // 确保 GetBounds 使用的是屏幕坐标

        // 检查是否需要重新创建 Bitmap
        if (frameBitmap == null || frameBitmap.Width != frameBounds.Width || frameBitmap.Height != frameBounds.Height)
        {
            frameBitmap?.Dispose(); // 释放之前的 Bitmap
            frameBitmap = new Bitmap(frameBounds.Width, frameBounds.Height);
        }

        using (Graphics g = Graphics.FromImage(frameBitmap))
        {
            // 拷贝屏幕
            g.CopyFromScreen(frameBounds.Location, Point.Empty, frameBounds.Size);
        }

        // 检查每个点的颜色
        foreach (var kvp in frameScreenPts)
        {
            int key = kvp.Key;
            POINT pt = kvp.Value;

            // 获取颜色
            Color color = Color.Black;
            int x = pt.X - frameBounds.X;
            int y = pt.Y - frameBounds.Y;
            if (x >= 0 && x <= frameBitmap.Width && y >= 0 && y <= frameBitmap.Height)
            {
                color = frameBitmap.GetPixel(x, y);
            }

                
            dictFrameColors[key] = color;
        }

        //Rectangle barBounds = GetBounds(barScreenPts); // 确保 GetBounds 使用的是屏幕坐标

        //if (barBitmap == null || barBitmap.Width != barBounds.Width || barBitmap.Height != barBounds.Height)
        //{
        //    barBitmap?.Dispose(); // 释放之前的 Bitmap
        //    barBitmap = new Bitmap(barBounds.Width, barBounds.Height);
        //}

        //using (Graphics g = Graphics.FromImage(barBitmap))
        //{
        //    // 拷贝屏幕
        //    g.CopyFromScreen(barBounds.Location, Point.Empty, barBounds.Size);
        //}

        // 检查每个点的颜色
        foreach (var kvp in barScreenPts)
        {
            int key = kvp.Key;
            POINT pt = kvp.Value;

            // 获取颜色
            Color color = Color.Black;
            int x = pt.X - frameBounds.X;
            int y = pt.Y - frameBounds.Y;
            if (x >= 0 && x <= frameBitmap.Width && y >= 0 && y <= frameBitmap.Height)
            {
                color = frameBitmap.GetPixel(x, y);
            }
            dictBarColors[key] = color;
        }

        // test print
        //{
        //    StringBuilder sb = new System.Text.StringBuilder();
        //    foreach (var kvp in dictFrameColors)
        //    {
        //        sb.Append($"[{kvp.Key}: {kvp.Value.R}]");
        //    }

        //    Console.WriteLine(sb.ToString().Trim());

        //    StringBuilder sb1 = new System.Text.StringBuilder();
        //    foreach (var kvp in dictBarColors)
        //    {
        //        sb1.Append($"[{kvp.Key}: {kvp.Value.R}]");
        //    }

        //    Console.WriteLine(sb1.ToString().Trim());
        //}

        sleepTime = random.Next(10, 50);
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
            SimulateShiftKeyPress(VK_FangkuohaoZuo);
        }
        else if (CheckPt(14))
        {
            SimulateShiftKeyPress(VK_FangkuohaoYou);
        }
        else if (CheckPt(15))
        {
            SimulateShiftKeyPress(VK_Xiegang);
        }
        else if (CheckPt(16))
        {
            SimulateShiftKeyPress(VK_Fenhao);
        }
        else if (CheckPt(17))
        {
            SimulateShiftKeyPress(VK_Danyinhao);
        }
        else if (CheckPt(18))
        {
            SimulateShiftKeyPress(VK_Juhao);
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
        else if (CheckPt(25))
        {
            SimulateShiftKeyPress(VK_7);
        }
        else if (CheckPt(26))
        {
            SimulateShiftKeyPress(VK_8);
        }
        else if (CheckPt(27))
        {
            SimulateShiftKeyPress(VK_9);
        }
        else if (CheckPt(28))
        {
            SimulateShiftKeyPress(VK_0);
        }
        else if (CheckPt(29))
        {
            SimulateShiftKeyPress(VK_Jian);
        }
        else if (CheckPt(30))
        {
            SimulateShiftKeyPress(VK_Deng);
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
    private static bool IsSkipKeyPressed(int[] _skipKeys)
    {
        bool isAltPressed = (IsKeyPressed(VK_MENU));
        if (isAltPressed)
        {
            return true;
        }

        int[] skips = null;
        if (_skipKeys != null)
        {
            skips = _skipKeys;
        }
        else
        {
            skips = skipKeys;
        }
        foreach (var vk in skips)
        {
            if (IsKeyPressed(vk))
            {
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

    private static bool IsMouseSideButtonPressed()
    {
        const int VK_XBUTTON1 =  0x05;
        const int VK_XBUTTON2 = 0x06;
        return (GetAsyncKeyState(VK_XBUTTON1) < 0 || GetAsyncKeyState(VK_XBUTTON2) < 0);
    }

    private static void OnEnable()
    {
        //AutoTest.Inst.FindPos(0);
    }

    private static void OnDisable()
    {
    }
}
