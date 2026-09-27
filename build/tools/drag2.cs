using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class Drag2
{
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetProcessDpiAwarenessContext(IntPtr c);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    const uint WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
    const uint WM_NCHITTEST = 0x0084;
    static IntPtr pet = IntPtr.Zero;

    // the pet form is the visible top level window of the process that has a title
    static bool Find(IntPtr h, IntPtr p)
    {
        uint wpid; GetWindowThreadProcessId(h, out wpid);
        if (wpid != (uint)p.ToInt64() || !IsWindowVisible(h)) return true;
        StringBuilder t = new StringBuilder(256); GetWindowTextW(h, t, 256);
        if (t.Length > 0) { pet = h; return false; }
        return true;
    }

    [STAThread]
    static void Main(string[] args)
    {
        try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); } catch { }
        Process[] ps = Process.GetProcessesByName(args.Length > 0 ? args[0] : "黄色小宠物");
        if (ps.Length == 0) { Console.WriteLine("not running"); return; }
        EnumWindows(Find, new IntPtr(ps[0].Id));
        if (pet == IntPtr.Zero) { Console.WriteLine("pet window not found"); return; }

        RECT a, cr; GetWindowRect(pet, out a); GetClientRect(pet, out cr);
        int cx = (a.L + a.R) / 2, cy = (a.T + a.B) / 2;
        int ccx = cr.R / 2, ccy = cr.B / 2;
        POINT saved; GetCursorPos(out saved);
        Console.WriteLine("pet before   = (" + a.L + "," + a.T + ") " + (a.R - a.L) + "x" + (a.B - a.T) + "   centre " + cx + "," + cy);

        // hit test the middle of the creature (should be HTCLIENT = 1)
        IntPtr lp = new IntPtr((cy << 16) | (cx & 0xFFFF));
        IntPtr ht = SendMessage(pet, WM_NCHITTEST, IntPtr.Zero, lp);
        Console.WriteLine("WM_NCHITTEST at centre -> " + ht.ToInt32() + "  (1=client, -1=transparent)");

        // a corner of the window that is certainly transparent
        IntPtr lp2 = new IntPtr((a.T + 3) << 16 | ((a.L + 3) & 0xFFFF));
        Console.WriteLine("WM_NCHITTEST at corner -> " + SendMessage(pet, WM_NCHITTEST, IntPtr.Zero, lp2).ToInt32() + "  (should be -1)");

        SetCursorPos(cx, cy); Thread.Sleep(120);
        PostMessage(pet, WM_LBUTTONDOWN, new IntPtr(1), new IntPtr((ccy << 16) | (ccx & 0xFFFF)));
        Thread.Sleep(150);

        int dx = -320, dy = -180;
        for (int i = 1; i <= 8; i++)
        {
            SetCursorPos(cx + dx * i / 8, cy + dy * i / 8);
            Thread.Sleep(50);
            PostMessage(pet, WM_MOUSEMOVE, new IntPtr(1), new IntPtr((ccy << 16) | (ccx & 0xFFFF)));
            Thread.Sleep(30);
        }
        PostMessage(pet, WM_LBUTTONUP, IntPtr.Zero, new IntPtr((ccy << 16) | (ccx & 0xFFFF)));
        Thread.Sleep(300);

        RECT b; GetWindowRect(pet, out b);
        Console.WriteLine("pet after    = (" + b.L + "," + b.T + ")");
        Console.WriteLine("expected     = (" + (a.L + dx) + "," + (a.T + dy) + ")");
        Console.WriteLine("delta        = (" + (b.L - a.L) + "," + (b.T - a.T) + ")   " +
                          (((b.L - a.L) == dx && (b.T - a.T) == dy) ? "PASS" : "FAIL"));

        SetCursorPos(saved.X, saved.Y);
    }
}
