using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class Drag
{
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetProcessDpiAwarenessContext(IntPtr c);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr extra; }
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    const uint INPUT_MOUSE = 0, MOUSEEVENTF_MOVE = 1, MOUSEEVENTF_ABSOLUTE = 0x8000,
               MOUSEEVENTF_LEFTDOWN = 2, MOUSEEVENTF_LEFTUP = 4;

    static IntPtr petHwnd = IntPtr.Zero;

    static void Move(int x, int y)
    {
        int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77);
        int vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);
        INPUT[] inp = new INPUT[1];
        inp[0].type = INPUT_MOUSE;
        inp[0].mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;
        inp[0].mi.dx = (int)Math.Round((x - vx) * 65535.0 / (vw - 1));
        inp[0].mi.dy = (int)Math.Round((y - vy) * 65535.0 / (vh - 1));
        SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
    }

    static void Button(uint flag)
    {
        INPUT[] inp = new INPUT[1];
        inp[0].type = INPUT_MOUSE;
        inp[0].mi.dwFlags = flag;
        SendInput(1, inp, Marshal.SizeOf(typeof(INPUT)));
    }

    [STAThread]
    static void Main(string[] args)
    {
        try { if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); } catch { }
        Process[] ps = Process.GetProcessesByName(args.Length > 0 ? args[0] : "黄色小宠物");
        if (ps.Length == 0) { Console.WriteLine("not running"); return; }
        uint pid = (uint)ps[0].Id;
        EnumWindows(delegate(IntPtr h, IntPtr p)
        {
            uint wpid; GetWindowThreadProcessId(h, out wpid);
            if (wpid != pid || !IsWindowVisible(h)) return true;
            StringBuilder t = new StringBuilder(256); GetWindowTextW(h, t, 256);
            if (t.Length > 0) { petHwnd = h; return false; }
            return true;
        }, IntPtr.Zero);
        if (petHwnd == IntPtr.Zero) { Console.WriteLine("pet window not found"); return; }

        RECT a; GetWindowRect(petHwnd, out a);
        Console.WriteLine("pet before = (" + a.L + "," + a.T + ") " + (a.R - a.L) + "x" + (a.B - a.T));
        int cx = (a.L + a.R) / 2, cy = (a.T + a.B) / 2;

        POINT before; GetCursorPos(out before);
        Console.WriteLine("cursor before = " + before.X + "," + before.Y);

        Move(cx, cy); Thread.Sleep(200);
        POINT at; GetCursorPos(out at);
        IntPtr under = WindowFromPoint(at);
        Console.WriteLine("moved to " + at.X + "," + at.Y + "  window under cursor = 0x" + under.ToInt64().ToString("X") + " (pet 0x" + petHwnd.ToInt64().ToString("X") + ")");

        Button(MOUSEEVENTF_LEFTDOWN); Thread.Sleep(150);
        for (int i = 1; i <= 10; i++) { Move(cx - 40 * i, cy - 20 * i); Thread.Sleep(40); }
        Thread.Sleep(150);
        Button(MOUSEEVENTF_LEFTUP); Thread.Sleep(250);

        RECT b; GetWindowRect(petHwnd, out b);
        Console.WriteLine("pet after  = (" + b.L + "," + b.T + ")");
        Console.WriteLine("expected   = (" + (a.L - 400) + "," + (a.T - 200) + ")");
        Console.WriteLine("delta      = (" + (b.L - a.L) + "," + (b.T - a.T) + ")");

        Move(before.X, before.Y);
        Thread.Sleep(200);
        GetCursorPos(out at);
        Console.WriteLine("cursor restored -> " + at.X + "," + at.Y);
    }
}
