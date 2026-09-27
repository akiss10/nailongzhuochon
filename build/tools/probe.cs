using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

class Probe
{
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetProcessDpiAwarenessContext(IntPtr c);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    [STAThread]
    static void Main(string[] args)
    {
        try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); } catch { }
        string outDir = args[0];
        Process[] ps = Process.GetProcessesByName("黄色小宠物");
        if (ps.Length == 0) { Console.WriteLine("not running"); return; }
        IntPtr h = ps[0].MainWindowHandle;
        RECT wr; GetWindowRect(h, out wr);
        Console.WriteLine("window " + wr.L + "," + wr.T + " " + (wr.R - wr.L) + "x" + (wr.B - wr.T));
        int[,] targets = new int[,] { { wr.L + 500, wr.T + 560 }, { wr.L + 250, wr.T + 560 }, { wr.L + 700, wr.T + 560 }, { wr.L + 480, wr.T + 700 } };
        for (int i = 0; i < targets.GetLength(0); i++)
        {
            SetCursorPos(targets[i, 0], targets[i, 1]);
            Thread.Sleep(160);
            POINT c; GetCursorPos(out c);
            int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77);
            int vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);
            using (Bitmap bmp = new Bitmap(vw, vh))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(vx, vy, 0, 0, new Size(vw, vh));
                    using (Pen p = new Pen(Color.Red, 2))
                    {
                        g.DrawLine(p, c.X - vx - 14, c.Y - vy, c.X - vx + 14, c.Y - vy);
                        g.DrawLine(p, c.X - vx, c.Y - vy - 14, c.X - vx, c.Y - vy + 14);
                    }
                }
                bmp.Save(outDir + "\\probe_" + i + ".png", ImageFormat.Png);
            }
            Console.WriteLine("shot " + i + " cursor " + c.X + "," + c.Y + " asked " + targets[i, 0] + "," + targets[i, 1]);
        }
    }
}
