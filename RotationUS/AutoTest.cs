using System;
using System.Drawing;
using static Program;

class AutoTest
{

    private Bitmap? testBitmap;
    private Dictionary<int, int> dictPts = new Dictionary<int, int>();
    #region Singleton
    private static AutoTest _inst;
    private AutoTest() { }

    public static AutoTest Inst
    {
        get
        {
            if (_inst == null)
            {
                _inst = new AutoTest();
            }
            return _inst;
        }
    }
    #endregion

    ~AutoTest()
    {
        testBitmap?.Dispose();
    }
    public void FindPos(int testY)
    {
        int currentIdx = 1;
        int currentColorMode = 1;
        int pixelStart = 0;
        int pixelEnd = 0;
        bool isStart = false;
        int testX = 0;
        int maxTestX = 600;
        dictPts.Clear();
        IntPtr hWnd = Program.GetForegroundWindow();

        for (int i = 0; i < maxTestX; i++)
        {
            testX = i;

            Color color = GetColorAt(hWnd, testX, testY);
            
            if (isStart == false)
            {
                if (IsColorMode(color, currentColorMode))
                {
                    isStart = true;
                }
                continue;
            }

            if (isStart)
            {
                currentColorMode = currentIdx % 3;
                if (IsColorMode(color, currentColorMode))
                {
                    if (pixelStart == 0)
                    {
                        pixelStart = i;
                    }
                }
                else
                {
                    if (IsColorMode(color, 0) || IsColorMode(color, 1) || IsColorMode(color, 2))
                    {
                        pixelEnd = i;
                        int middle = (pixelStart + pixelEnd) / 2;
                        dictPts.Add(currentIdx, middle);

                        pixelStart = 0;
                        currentIdx++;
                    }
                    else
                    {
                        pixelEnd = i;
                        int middle = (pixelStart + pixelEnd) / 2;
                        dictPts.Add(currentIdx, middle);

                        pixelStart = 0;
                        break;
                    }
                }
            }
        }

        OutputPts();

    }

    private POINT testPt;
    private Color GetColorAt(IntPtr hWnd, int x, int y)
    {
        // 获取窗口的屏幕位置和大小
        if (!GetWindowRect(hWnd, out RECT windowRect))
        {
            return Color.Black;
        }

        testPt.X = x;
        testPt.Y = y;
        ClientToScreen(hWnd, ref testPt);

        if (testBitmap == null)
        {
            testBitmap?.Dispose(); // 释放之前的 Bitmap
            testBitmap = new Bitmap(1, 1);
        }

        // 从屏幕截图中获取颜色
        using (Graphics g = Graphics.FromImage(testBitmap))
        {
            g.CopyFromScreen(testPt.X, testPt.Y, 0, 0, new Size(1, 1));
        }

        // 返回指定坐标的颜色
        return testBitmap.GetPixel(0, 0);
    }

    private bool IsColorMode(Color color, int colorMode)
    {
        if (colorMode == 0)
        {
            if (color.R == 255 && color.G == 0 && color.B == 0)
            {
                return true;
            }
        }
        else if (colorMode == 1)
        {
            if (color.R == 0 && color.G == 255 && color.B == 0)
            {
                return true;
            }
        }
        else if (colorMode == 2)
        {
            if (color.R == 0 && color.G == 0 && color.B == 255)
            {
                return true;
            }
        }

        return false;
    }

    private void OutputPts()
    {
        foreach(var kvp in dictPts)
        {
            Console.WriteLine($"dictFrames[{kvp.Key}] = new POINT {{ X = {kvp.Value}, Y = frameY }};");
        }
    }
}