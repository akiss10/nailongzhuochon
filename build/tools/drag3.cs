using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class Drag3
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
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    const uint WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
    static IntPtr pet = IntPtr.Zero;

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
        int ccx = cr.R / 2, ccy = cr.B / 2;
        int startX = (a.L + a.R) / 2, startY = (a.T + a.B) / 2;
        POINT saved; GetCursorPos(out saved);

        SetCursorPos(startX, startY); Thread.Sleep(120);
        POINT c0; GetCursorPos(out c0);
        PostMessage(pet, WM_LBUTTONDOWN, new IntPtr(1), new IntPtr((ccy << 16) | (ccx & 0xFFFF)));
        Thread.Sleep(120);

        int dx = -300, dy = -160;
        for (int i = 1; i <= 10; i++)
        {
            SetCursorPos(startX + dx * i / 10, startY + dy * i / 10);
            Thread.Sleep(60);
            PostMessage(pet, WM_MOUSEMOVE, new IntPtr(1), new IntPtr((ccy << 16) | (ccx & 0xFFFF)));
            Thread.Sleep(40);
        }
        POINT c1; GetCursorPos(out c1);
        PostMessage(pet, WM_LBUTTONUP, IntPtr.Zero, new IntPtr((ccy << 16) | (ccx & 0xFFFF)));
        Thread.Sleep(300);
        RECT b; GetWindowRect(pet, out b);

        int cursorDX = c1.X - c0.X, cursorDY = c1.Y - c0.Y;
        int petDX = b.L - a.L, petDY = b.T - a.T;
        Console.WriteLine("drag start cursor  = " + c0.X + "," + c0.Y + "   (asked for " + startX + "," + startY + ")");
        Console.WriteLine("drag end   cursor  = " + c1.X + "," + c1.Y);
        Console.WriteLine("cursor delta       = " + cursorDX + "," + cursorDY);
        Console.WriteLine("pet    delta       = " + petDX + "," + petDY);
        Console.WriteLine("match              = " + ((Math.Abs(cursorDX - petDX) <= 2 && Math.Abs(cursorDY - petDY) <= 2) ? "PASS" : "FAIL"));

        // also confirm the drag really ends: move the cursor after releasing
        SetCursorPos(c1.X + 120, c1.Y + 120);
        Thread.Sleep(300);
        RECT c; GetWindowRect(pet, out c);
        Console.WriteLine("after release, moving the cursor changed the pet by " + (c.L - b.L) + "," + (c.T - b.T) + " (should be 0,0)");

        SetCursorPos(saved.X, saved.Y);
    }
}
