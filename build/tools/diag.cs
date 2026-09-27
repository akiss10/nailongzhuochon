using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class D
{
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetProcessDpiAwarenessContext(IntPtr c);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("shcore.dll")] static extern int GetProcessDpiAwareness(IntPtr h, out int v);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    [STAThread]
    static void Main()
    {
        bool ok = false;
        try { ok = SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch (Exception e) { Console.WriteLine("ctx failed " + e.GetType().Name); }
        if (!ok) { try { ok = SetProcessDPIAware(); } catch { } }
        int aw = -1;
        try { GetProcessDpiAwareness(IntPtr.Zero, out aw); } catch { }
        POINT p; GetCursorPos(out p);
        Console.WriteLine("SetAware=" + ok + "  GetProcessDpiAwareness=" + aw + " (0=unaware,1=system,2=permonitor)");
        Console.WriteLine("SM_CXSCREEN=" + GetSystemMetrics(0) + " SM_CYSCREEN=" + GetSystemMetrics(1));
        Console.WriteLine("raw GetCursorPos       = " + p.X + "," + p.Y);
        Console.WriteLine("WinForms Cursor.Position = " + Cursor.Position.X + "," + Cursor.Position.Y);
        Console.WriteLine("WinForms Screen.Bounds = " + Screen.PrimaryScreen.Bounds);
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Console.WriteLine("Graphics dpi = " + g.DpiX + " x " + g.DpiY);
    }
}
