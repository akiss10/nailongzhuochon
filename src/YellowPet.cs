// =====================================================================
//  黄色小宠物 (YellowPet) - a tiny Windows desktop pet
//
//  A per-pixel transparent, borderless, always-on-top layered window shows
//  the whole little scene (creature + desk + mouse pad + keyboard).  The
//  creature's left arm, paw and the computer mouse are a separate piece that
//  is warped per frame so the paw follows the real cursor -- the arm
//  stretches from the shoulder ("rubber arm"), and the paw never lets go of
//  the mouse.  Movement is limited to the area around the mouse pad.
//
//  Built with the .NET Framework csc.exe that ships with Windows, so the
//  resulting EXE needs no runtime install.
// =====================================================================
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace YellowPet
{
    internal static class Win32
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)]
        internal struct SIZE { public int CX, CY; public SIZE(int x, int y) { CX = x; CY = y; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth, biHeight;
            public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage;
            public int biXPelsPerMeter, biYPelsPerMeter;
            public uint biClrUsed, bmiClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; public uint bmiColors; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT { public int Left, Top, Right, Bottom; }

        internal const int ULW_ALPHA = 2;
        internal const byte AC_SRC_OVER = 0;
        internal const byte AC_SRC_ALPHA = 1;
        internal const int WS_EX_LAYERED = 0x00080000;
        internal const int WS_EX_TRANSPARENT = 0x00000020;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;
        internal const int WS_EX_NOACTIVATE = 0x08000000;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst,
            ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT p);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi,
            uint usage, out IntPtr bits, IntPtr section, uint offset);

        // ---- raw popup menu (works fine on a non-activating window) ----
        [DllImport("user32.dll")] internal static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll")] internal static extern bool DestroyMenu(IntPtr hMenu);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string lpNewItem);
        [DllImport("user32.dll")]
        internal static extern int TrackPopupMenuEx(IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);
        internal const uint MF_STRING = 0x0000;
        internal const uint MF_SEPARATOR = 0x0800;
        internal const uint MF_CHECKED = 0x0008;
        internal const uint MF_POPUP = 0x0010;
        internal const uint TPM_RIGHTBUTTON = 0x0002;
        internal const uint TPM_RETURNCMD = 0x0100;
    }

    /// <summary>A 32bpp premultiplied BGRA bitmap kept in managed memory.</summary>
    internal sealed class Surface
    {
        public readonly int W, H;
        public readonly byte[] Bgra;

        public Surface(int w, int h, byte[] bgra) { W = w; H = h; Bgra = bgra; }

        public static Surface Load(string resourceName)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(resourceName))
            {
                if (s == null) throw new InvalidOperationException("missing resource " + resourceName);
                using (Bitmap bmp = new Bitmap(s)) return FromBitmap(bmp);
            }
        }

        public static Surface LoadFileOrResource(string file, string resourceName)
        {
            // a file dropped next to the EXE overrides the embedded sprite
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string p = Path.Combine(dir, file);
                if (File.Exists(p)) { using (Bitmap bmp = new Bitmap(p)) return FromBitmap(bmp); }
            }
            catch { }
            return Load(resourceName);
        }

        public static Surface FromBitmap(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            byte[] dst = new byte[w * h * 4];
            using (Bitmap b32 = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b32)) g.DrawImageUnscaled(bmp, 0, 0);
                BitmapData d = b32.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] raw = new byte[w * h * 4];
                    Marshal.Copy(d.Scan0, raw, 0, raw.Length);
                    for (int i = 0; i < raw.Length; i += 4)
                    {
                        byte b = raw[i], gg = raw[i + 1], r = raw[i + 2], a = raw[i + 3];
                        dst[i] = (byte)(b * a / 255);
                        dst[i + 1] = (byte)(gg * a / 255);
                        dst[i + 2] = (byte)(r * a / 255);
                        dst[i + 3] = a;
                    }
                }
                finally { b32.UnlockBits(d); }
            }
            return new Surface(w, h, dst);
        }

        /// <summary>High quality rescale, done in premultiplied space.</summary>
        public Surface Scaled(double factor)
        {
            int nw = Math.Max(1, (int)Math.Round(W * factor));
            int nh = Math.Max(1, (int)Math.Round(H * factor));
            if (nw == W && nh == H) return this;
            using (Bitmap srcBmp = ToBitmap())
            using (Bitmap dstBmp = new Bitmap(nw, nh, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(dstBmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.DrawImage(srcBmp, new Rectangle(0, 0, nw, nh), 0, 0, W, H, GraphicsUnit.Pixel);
                }
                byte[] dst = new byte[nw * nh * 4];
                BitmapData d = dstBmp.LockBits(new Rectangle(0, 0, nw, nh), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try { Marshal.Copy(d.Scan0, dst, 0, dst.Length); }
                finally { dstBmp.UnlockBits(d); }
                for (int i = 0; i < dst.Length; i += 4)
                {
                    byte a = dst[i + 3];
                    if (dst[i] > a) dst[i] = a;
                    if (dst[i + 1] > a) dst[i + 1] = a;
                    if (dst[i + 2] > a) dst[i + 2] = a;
                }
                return new Surface(nw, nh, dst);
            }
        }

        public Bitmap ToBitmap()
        {
            Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(Bgra, 0, d.Scan0, Bgra.Length); }
            finally { bmp.UnlockBits(d); }
            return bmp;
        }
    }

    /// <summary>Scene + the movable arm piece + the warp weights.</summary>
    internal sealed class PetArt
    {
        public Surface Scene;      // the whole picture, arm removed
        public Surface Arm;        // arm + paw + mouse, at its home position
        public Surface Weight;     // 8bpp ramp: 0 at the shoulder, 255 at the paw
        public int ArmX, ArmY;     // where Arm sits inside Scene
        public double Scale = 1.0; // current display scale

        public const double HomeX = 235.0, HomeY = 1019.0;   // mouse centre (scene px)
        public const double PadL = 80.0, PadT = 900.0, PadR = 372.0, PadB = 1132.0;
        public const double AxisX = -0.792, AxisY = 0.611;   // shoulder -> paw
        public const double PerpX = -0.611, PerpY = -0.792;
        public double MinAxis = -35, MaxAxis = 128, MaxPerp = 132;

        public PetArt()
        {
            Scene = Surface.LoadFileOrResource("scene.png", "scene.png");
            Arm = Surface.LoadFileOrResource("arm.png", "arm.png");
            Weight = Surface.LoadFileOrResource("armw.png", "armw.png");
            ArmX = 156; ArmY = 794;
        }

        /// <summary>Cursor position (scene pixels) -> arm displacement (scene pixels).
        /// The mouse runs the *opposite* way to the cursor: cursor right -> the
        /// paw and the mouse slide left across the pad.</summary>
        public void DeltaFor(Point cursorScreen, Point windowOrigin, out double dx, out double dy)
        {
            double sx = (cursorScreen.X - windowOrigin.X) / Scale;
            double sy = (cursorScreen.Y - windowOrigin.Y) / Scale;
            if (sx < PadL) sx = PadL; else if (sx > PadR) sx = PadR;
            if (sy < PadT) sy = PadT; else if (sy > PadB) sy = PadB;
            double vx = HomeX - sx, vy = HomeY - sy;      // reversed
            double a = vx * AxisX + vy * AxisY;
            double p = vx * PerpX + vy * PerpY;
            if (a < MinAxis) a = MinAxis; else if (a > MaxAxis) a = MaxAxis;
            if (p < -MaxPerp) p = -MaxPerp; else if (p > MaxPerp) p = MaxPerp;
            dx = AxisX * a + PerpX * p;
            dy = AxisY * a + PerpY * p;
        }
    }

    /// <summary>Pushes a premultiplied surface into a WS_EX_LAYERED window.</summary>
    internal static class Layered
    {
        public static void Apply(IntPtr hwnd, byte[] bgra, int w, int h, int x, int y)
        {
            IntPtr screenDc = Win32.GetDC(IntPtr.Zero);
            IntPtr memDc = Win32.CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero, oldBmp = IntPtr.Zero, bits = IntPtr.Zero;
            try
            {
                Win32.BITMAPINFO bmi = new Win32.BITMAPINFO();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(Win32.BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = w;
                bmi.bmiHeader.biHeight = -h;
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0;
                hBmp = Win32.CreateDIBSection(screenDc, ref bmi, 0, out bits, IntPtr.Zero, 0);
                if (hBmp == IntPtr.Zero || bits == IntPtr.Zero) return;
                Marshal.Copy(bgra, 0, bits, bgra.Length);
                oldBmp = Win32.SelectObject(memDc, hBmp);
                Win32.POINT dst = new Win32.POINT(x, y);
                Win32.SIZE size = new Win32.SIZE(w, h);
                Win32.POINT src = new Win32.POINT(0, 0);
                Win32.BLENDFUNCTION blend = new Win32.BLENDFUNCTION();
                blend.BlendOp = Win32.AC_SRC_OVER;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = Win32.AC_SRC_ALPHA;
                Win32.UpdateLayeredWindow(hwnd, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Win32.ULW_ALPHA);
            }
            finally
            {
                if (oldBmp != IntPtr.Zero) Win32.SelectObject(memDc, oldBmp);
                if (hBmp != IntPtr.Zero) Win32.DeleteObject(hBmp);
                if (memDc != IntPtr.Zero) Win32.DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) Win32.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }

    internal sealed class PetForm : Form
    {
        private readonly PetArt _art;
        private byte[] _frame;          // premultiplied BGRA of the whole window
        private byte[] _base;           // the scene at the current scale
        private byte[] _arm, _wm;
        private int _aw, _ah, _ax, _ay, _W, _H;
        private int _dirtyX0, _dirtyY0, _dirtyX1, _dirtyY1;   // last arm destination box
        private bool _dragging;
        private Point _dragCursor, _dragOrigin;
        private double _lastDx = double.NaN, _lastDy = double.NaN;
        private bool _placed;

        public PetForm(PetArt art)
        {
            _art = art;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Text = "黄色小宠物";
            SetStyle(ControlStyles.Opaque, true);
            BackColor = Color.Black;
            ApplyScale(art.Scale);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        public int SpriteW { get { return _W; } }
        public int SpriteH { get { return _H; } }

        public void ApplyScale(double scale)
        {
            _art.Scale = scale;
            Surface sc = _art.Scene.Scaled(scale);
            Surface ar = _art.Arm.Scaled(scale);
            Surface wm = _art.Weight.Scaled(scale);
            _base = sc.Bgra;
            _arm = ar.Bgra;
            _wm = wm.Bgra;
            _aw = ar.W; _ah = ar.H; _W = sc.W; _H = sc.H;
            _ax = (int)Math.Round(_art.ArmX * scale);
            _ay = (int)Math.Round(_art.ArmY * scale);
            _frame = new byte[sc.Bgra.Length];
            Buffer.BlockCopy(_base, 0, _frame, 0, _base.Length);
            Size = new Size(sc.W, sc.H);
            _lastDx = double.NaN;
            _dirtyX0 = _dirtyY0 = _dirtyX1 = _dirtyY1 = 0;
        }

        public void PlaceInitial(Rectangle work)
        {
            Location = new Point(work.Right - SpriteW - 20, work.Bottom - SpriteH + 18);
            _placed = true;
        }

        public void KeepInside(Rectangle work)
        {
            int x = Left, y = Top;
            if (x + SpriteW < work.Left + 120) x = work.Left + 120 - SpriteW;
            if (x > work.Right - 120) x = work.Right - 120;
            if (y > work.Bottom - 60) y = work.Bottom - 60;
            if (y + SpriteH < work.Top + 120) y = work.Top + 120 - SpriteH;
            Location = new Point(x, y);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Push();
        }

        private void Push()
        {
            if (!IsHandleCreated) return;
            Layered.Apply(Handle, _frame, SpriteW, SpriteH, Left, Top);
        }

        /// <summary>Rebuild the frame: scene + the warped arm piece, then push it.</summary>
        public void UpdateArm()
        {
            if (!IsHandleCreated) return;
            Win32.POINT cur;
            Win32.GetCursorPos(out cur);
            double sdx, sdy;
            _art.DeltaFor(new Point(cur.X, cur.Y), Location, out sdx, out sdy);
            if (sdx == _lastDx && sdy == _lastDy) return;
            _lastDx = sdx; _lastDy = sdy;
            Render(sdx * _art.Scale, sdy * _art.Scale);
            Push();
        }

        public void ForceUpdate() { _lastDx = double.NaN; UpdateArm(); }

        private unsafe void Render(double dx, double dy)
        {
            int W = _W, H = _H;
            int fdx = (int)Math.Round(dx), fdy = (int)Math.Round(dy);

            // restore the region the arm covered last time
            if (_dirtyX1 > _dirtyX0 && _dirtyY1 > _dirtyY0)
                RestoreScene(_dirtyX0, _dirtyY0, _dirtyX1, _dirtyY1);

            int x0 = _ax + Math.Min(0, fdx) - 2, x1 = _ax + _aw + Math.Max(0, fdx) + 2;
            int y0 = _ay + Math.Min(0, fdy) - 2, y1 = _ay + _ah + Math.Max(0, fdy) + 2;
            if (x0 < 0) x0 = 0; if (y0 < 0) y0 = 0;
            if (x1 > W) x1 = W; if (y1 > H) y1 = H;
            if (x1 <= x0 || y1 <= y0) { _dirtyX1 = _dirtyX0; _dirtyY1 = _dirtyY0; return; }

            int aw = _aw, ah = _ah, ax = _ax, ay = _ay;
            fixed (byte* pFrame = _frame, pArm = _arm, pWm = _wm)
            {
                for (int y = y0; y < y1; y++)
                {
                    byte* row = pFrame + (long)y * W * 4;
                    for (int x = x0; x < x1; x++)
                    {
                        double sx = x - dx, sy = y - dy;
                        for (int it = 0; it < 2; it++)
                        {
                            int wx = (int)(sx - ax); if (wx < 0) wx = 0; else if (wx >= aw) wx = aw - 1;
                            int wy = (int)(sy - ay); if (wy < 0) wy = 0; else if (wy >= ah) wy = ah - 1;
                            double w = pWm[((long)wy * aw + wx) * 4] * (1.0 / 255.0);
                            sx = x - dx * w;
                            sy = y - dy * w;
                        }
                        double gx = sx - ax, gy = sy - ay;
                        if (gx < -0.5 || gy < -0.5 || gx > aw - 0.5 || gy > ah - 0.5) continue;
                        int ix = (int)Math.Floor(gx), iy = (int)Math.Floor(gy);
                        if (ix < 0) ix = 0; else if (ix > aw - 2) ix = aw - 2;
                        if (iy < 0) iy = 0; else if (iy > ah - 2) iy = ah - 2;
                        double fx = gx - ix, fy = gy - iy;
                        if (fx < 0) fx = 0; else if (fx > 1) fx = 1;
                        if (fy < 0) fy = 0; else if (fy > 1) fy = 1;
                        double w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
                        byte* p00 = pArm + ((long)iy * aw + ix) * 4;
                        byte* p10 = p00 + 4;
                        byte* p01 = p00 + (long)aw * 4;
                        byte* p11 = p01 + 4;
                        double bb = p00[0] * w00 + p10[0] * w10 + p01[0] * w01 + p11[0] * w11;
                        double gg = p00[1] * w00 + p10[1] * w10 + p01[1] * w01 + p11[1] * w11;
                        double rr = p00[2] * w00 + p10[2] * w10 + p01[2] * w01 + p11[2] * w11;
                        double aa = p00[3] * w00 + p10[3] * w10 + p01[3] * w01 + p11[3] * w11;
                        if (aa <= 0.5) continue;
                        double ia = 1.0 - aa / 255.0;
                        byte* d = row + (long)x * 4;
                        d[0] = (byte)(bb + d[0] * ia);
                        d[1] = (byte)(gg + d[1] * ia);
                        d[2] = (byte)(rr + d[2] * ia);
                        d[3] = (byte)(aa + d[3] * ia);
                    }
                }
            }
            _dirtyX0 = x0; _dirtyY0 = y0; _dirtyX1 = x1; _dirtyY1 = y1;
        }

        private void RestoreScene(int x0, int y0, int x1, int y1)
        {
            int W = _W;
            int len = (x1 - x0) * 4;
            for (int y = y0; y < y1; y++)
                Buffer.BlockCopy(_base, (y * W + x0) * 4, _frame, (y * W + x0) * 4, len);
        }

        public byte AlphaAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _W || y >= _H) return 0;
            return _frame[(y * _W + x) * 4 + 3];
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            if (m.Msg == WM_NCHITTEST && !_dragging)
            {
                int lp = (int)m.LParam.ToInt64();
                int sx = unchecked((short)(lp & 0xFFFF));
                int sy = unchecked((short)((lp >> 16) & 0xFFFF));
                Point client = PointToClient(new Point(sx, sy));
                m.Result = new IntPtr(AlphaAt(client.X, client.Y) < 24 ? -1 : 1);   // HTTRANSPARENT / HTCLIENT
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                _dragCursor = Cursor.Position;
                _dragOrigin = Location;
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                Point now = Cursor.Position;
                Location = new Point(_dragOrigin.X + (now.X - _dragCursor.X),
                                     _dragOrigin.Y + (now.Y - _dragCursor.Y));
                ForceUpdate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
                Capture = false;
            }
        }
    }

    internal static class Program
    {
        private static PetForm _pet;
        private static NotifyIcon _tray;
        private static Timer _timer;
        private static PetArt _art;
        private static double _dpiScale = 1.0;
        private static double _scale = 0.42;
        private static int _savedX = int.MinValue, _savedY = int.MinValue;
        private static IntPtr _iconHandle = IntPtr.Zero;
        private const int ID_SMALL = 101, ID_MEDIUM = 102, ID_LARGE = 103, ID_HUGE = 104, ID_QUIT = 199;

        [STAThread]
        private static void Main()
        {
            try { Run(); }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "yellowpet_error.txt"), ex.ToString()); }
                catch { }
                throw;
            }
        }

        private static void Run()
        {
            bool ok = false;
            try { ok = Win32.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { ok = false; }
            if (!ok) { try { Win32.SetProcessDPIAware(); } catch { } }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) _dpiScale = g.DpiX / 96.0; }
            catch { _dpiScale = 1.0; }

            _art = new PetArt();
            LoadSettings();

            _pet = new PetForm(_art);
            _pet.ApplyScale(_scale * _dpiScale);
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            if (_savedX != int.MinValue && OnScreen(_savedX, _savedY)) _pet.Location = new Point(_savedX, _savedY);
            else _pet.PlaceInitial(work);
            _pet.Show();
            _pet.ForceUpdate();

            BuildTray(_art);

            _timer = new Timer();
            _timer.Interval = 16;
            _timer.Tick += delegate { _pet.UpdateArm(); };
            _timer.Start();

            _pet.MouseUp += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right) ShowMenu();
            };

            Application.ApplicationExit += delegate { SaveSettings(); DisposeTray(); };
            Application.Run(_pet);
        }

        private static bool OnScreen(int x, int y)
        {
            foreach (Screen sc in Screen.AllScreens)
            {
                Rectangle r = sc.WorkingArea;
                if (x > r.Left - 400 && x < r.Right - 80 && y > r.Top - 100 && y < r.Bottom - 60) return true;
            }
            return false;
        }

        private static void ShowMenu()
        {
            IntPtr menu = Win32.CreatePopupMenu();
            IntPtr sub = Win32.CreatePopupMenu();
            if (menu == IntPtr.Zero || sub == IntPtr.Zero) return;
            try
            {
                Win32.AppendMenuW(sub, Win32.MF_STRING | (_scale < 0.34 ? Win32.MF_CHECKED : 0), new IntPtr(ID_SMALL), "小");
                Win32.AppendMenuW(sub, Win32.MF_STRING | (_scale >= 0.34 && _scale < 0.50 ? Win32.MF_CHECKED : 0), new IntPtr(ID_MEDIUM), "中");
                Win32.AppendMenuW(sub, Win32.MF_STRING | (_scale >= 0.50 && _scale < 0.68 ? Win32.MF_CHECKED : 0), new IntPtr(ID_LARGE), "大");
                Win32.AppendMenuW(sub, Win32.MF_STRING | (_scale >= 0.68 ? Win32.MF_CHECKED : 0), new IntPtr(ID_HUGE), "特大");
                Win32.AppendMenuW(menu, Win32.MF_POPUP, sub, "大小");
                Win32.AppendMenuW(menu, Win32.MF_SEPARATOR, IntPtr.Zero, null);
                Win32.AppendMenuW(menu, Win32.MF_STRING, new IntPtr(ID_QUIT), "退出");

                Point p = Cursor.Position;
                Win32.SetForegroundWindow(_pet.Handle);
                int cmd = Win32.TrackPopupMenuEx(menu, Win32.TPM_RIGHTBUTTON | Win32.TPM_RETURNCMD,
                                                 p.X, p.Y, _pet.Handle, IntPtr.Zero);
                if (cmd != 0) HandleCommand(cmd);
            }
            finally
            {
                Win32.DestroyMenu(sub);
                Win32.DestroyMenu(menu);
            }
        }

        public static void HandleCommand(int cmd)
        {
            switch (cmd)
            {
                case ID_SMALL: SetScale(0.28); break;
                case ID_MEDIUM: SetScale(0.42); break;
                case ID_LARGE: SetScale(0.58); break;
                case ID_HUGE: SetScale(0.80); break;
                case ID_QUIT: Application.Exit(); break;
            }
        }

        private static void SetScale(double s)
        {
            _scale = s;
            Point old = _pet.Location;
            int oldW = _pet.SpriteW, oldH = _pet.SpriteH;
            _pet.ApplyScale(_scale * _dpiScale);
            _pet.Location = new Point(old.X + (oldW - _pet.SpriteW) / 2, old.Y + (oldH - _pet.SpriteH));
            _pet.KeepInside(Screen.FromPoint(_pet.Location).WorkingArea);
            _pet.ForceUpdate();
            SaveSettings();
        }

        private static void BuildTray(PetArt art)
        {
            try
            {
                _tray = new NotifyIcon();
                try { _tray.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); }
                catch { }
                _tray.Text = "黄色小宠物（左键拖动 / 右键菜单）";
                _tray.Visible = true;
                _tray.MouseUp += delegate(object s, MouseEventArgs e)
                {
                    if (e.Button == MouseButtons.Right) ShowMenu();
                };
            }
            catch { _tray = null; }
        }

        private static void DisposeTray()
        {
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            if (_iconHandle != IntPtr.Zero) { Win32.DestroyIcon(_iconHandle); _iconHandle = IntPtr.Zero; }
        }

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YellowPet"), "settings.ini");
            }
        }

        private static void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (string line in File.ReadAllLines(SettingsPath))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim();
                    double d;
                    if (!double.TryParse(line.Substring(i + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) continue;
                    if (k == "scale" && d >= 0.15 && d <= 1.6) _scale = d;
                    else if (k == "x") _savedX = (int)Math.Round(d);
                    else if (k == "y") _savedY = (int)Math.Round(d);
                }
            }
            catch { }
        }

        private static void SaveSettings()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("scale=" + _scale.ToString("0.###", CultureInfo.InvariantCulture));
                if (_pet != null && _pet.IsHandleCreated)
                {
                    sb.AppendLine("x=" + _pet.Left.ToString(CultureInfo.InvariantCulture));
                    sb.AppendLine("y=" + _pet.Top.ToString(CultureInfo.InvariantCulture));
                }
                File.WriteAllText(SettingsPath, sb.ToString());
            }
            catch { }
        }
    }
}
