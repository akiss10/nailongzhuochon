using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

class Shot
{
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetProcessDpiAwarenessContext(IntPtr c);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    [STAThread]
    static void Main(string[] args)
    {
        try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); } catch { }
        int x = GetSystemMetrics(76), y = GetSystemMetrics(77);
        int w = GetSystemMetrics(78), h = GetSystemMetrics(79);
        POINT c; GetCursorPos(out c);
        using (Bitmap bmp = new Bitmap(w, h))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(x, y, 0, 0, new Size(w, h));
                using (Pen p = new Pen(Color.Red, 2))
                {
                    g.DrawLine(p, c.X - x - 16, c.Y - y, c.X - x + 16, c.Y - y);
                    g.DrawLine(p, c.X - x, c.Y - y - 16, c.X - x, c.Y - y + 16);
                }
            }
            bmp.Save(args[0], ImageFormat.Png);
        }
        Console.WriteLine("saved " + args[0] + "  screen=" + w + "x" + h + " origin=" + x + "," + y + " cursor=" + c.X + "," + c.Y);
    }
}
