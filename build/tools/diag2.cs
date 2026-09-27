using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

class D2
{
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetProcessDpiAwarenessContext(IntPtr c);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    [STAThread]
    static void Main(string[] args)
    {
        try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); } catch { }

        POINT c; GetCursorPos(out c);
        Console.WriteLine("cursor (physical) = " + c.X + "," + c.Y);

        string want = args.Length > 0 ? args[0] : "黄色小宠物";
        Process[] ps = Process.GetProcessesByName(want);
        if (ps.Length == 0) { Console.WriteLine("process '" + want + "' not running"); return; }
        uint pid = (uint)ps[0].Id;
        Console.WriteLine("pid = " + pid);
        EnumWindows(delegate(IntPtr h, IntPtr p)
        {
            uint wpid; GetWindowThreadProcessId(h, out wpid);
            if (wpid != pid) return true;
            RECT r; GetWindowRect(h, out r);
            StringBuilder t = new StringBuilder(256); GetWindowTextW(h, t, 256);
            StringBuilder cl = new StringBuilder(256); GetClassNameW(h, cl, 256);
            int ex = GetWindowLong(h, -20);
            Console.WriteLine(string.Format("hwnd=0x{0:X} vis={1} rect=({2},{3})-({4},{5}) size={6}x{7} ex=0x{8:X} title='{9}' class='{10}'",
                h.ToInt64(), IsWindowVisible(h), r.L, r.T, r.R, r.B, r.R - r.L, r.B - r.T, ex, t, cl));
            return true;
        }, IntPtr.Zero);
    }
}
