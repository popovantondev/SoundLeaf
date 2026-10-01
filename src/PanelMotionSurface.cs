using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SoundLeaf
{
    // Fixed-size, click-through compositor surface. The real panel never resizes per frame.
    internal sealed class PanelMotionSurface : NativeWindow, IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct PointN { internal int X, Y; internal PointN(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] private struct SizeN { internal int Width, Height; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { internal byte Operation, Flags, Alpha, Format; }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { internal uint Size; internal int Width, Height; internal ushort Planes, Bits; internal uint Compression, ImageSize; internal int Xppm, Yppm; internal uint Used, Important; }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screen, ref PointN destination, ref SizeN size, IntPtr source, ref PointN origin, uint key, ref Blend blend, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr pixels, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] private static extern void CopyMemory(IntPtr destination, IntPtr source, UIntPtr length);
        private Bitmap snapshot, canvas;
        private Graphics graphics;
        private TextureBrush texture;
        private IntPtr dc, dib, oldBitmap, pixels;
        private readonly Rectangle bounds;
        internal RectangleF Destination { get; private set; }
        internal PanelMotionSurface(IntPtr owner, Rectangle bounds, Bitmap snapshot)
        {
            this.bounds = bounds; this.snapshot = snapshot;
            try
            {
                canvas = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
                graphics = Graphics.FromImage(canvas); graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                texture = new TextureBrush(snapshot, WrapMode.Clamp);
                var info = new BitmapInfo { Size = (uint)Marshal.SizeOf(typeof(BitmapInfo)), Width = bounds.Width, Height = -bounds.Height, Planes = 1, Bits = 32 };
                dc = CreateCompatibleDC(IntPtr.Zero); dib = CreateDIBSection(dc, ref info, 0, out pixels, IntPtr.Zero, 0);
                if (dc == IntPtr.Zero || dib == IntPtr.Zero || pixels == IntPtr.Zero) throw new Win32Exception();
                oldBitmap = SelectObject(dc, dib);
                CreateHandle(new CreateParams { Caption = "SoundLeaf panel animation", Parent = owner, X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height,
                    Style = unchecked((int)0x80000000), ExStyle = 0x080800A8 });
            }
            catch { Dispose(); throw; }
        }
        internal void Draw(RectangleF destination, float alpha, float radius)
        {
            Destination = destination;
            graphics.CompositingMode = CompositingMode.SourceCopy; graphics.Clear(Color.Transparent); graphics.CompositingMode = CompositingMode.SourceOver;
            using (var transform = new Matrix(destination.Width / snapshot.Width, 0, 0, destination.Height / snapshot.Height, destination.X, destination.Y)) texture.Transform = transform;
            using (var shape = LeafDrawing.Rounded(destination, Math.Min(radius, Math.Min(destination.Width, destination.Height) / 2))) graphics.FillPath(texture, shape);
            BitmapData data = canvas.LockBits(new Rectangle(Point.Empty, canvas.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try { CopyMemory(pixels, data.Scan0, new UIntPtr((uint)(bounds.Width * bounds.Height * 4))); } finally { canvas.UnlockBits(data); }
            var point = new PointN(bounds.X, bounds.Y); var size = new SizeN { Width = bounds.Width, Height = bounds.Height }; var origin = new PointN(0, 0);
            var blend = new Blend { Alpha = (byte)Math.Max(0, Math.Min(255, Math.Round(alpha * 255))), Format = 1 };
            IntPtr screen = GetDC(IntPtr.Zero);
            try { if (!UpdateLayeredWindow(Handle, screen, ref point, ref size, dc, ref origin, 0, ref blend, 2)) throw new Win32Exception(); }
            finally { ReleaseDC(IntPtr.Zero, screen); }
            ShowWindow(Handle, 4);
        }
        internal Bitmap Capture() { return (Bitmap)canvas.Clone(); }
        public void Dispose()
        {
            if (Handle != IntPtr.Zero) DestroyHandle();
            if (oldBitmap != IntPtr.Zero && dc != IntPtr.Zero) SelectObject(dc, oldBitmap);
            oldBitmap = IntPtr.Zero;
            if (dib != IntPtr.Zero) { DeleteObject(dib); dib = IntPtr.Zero; }
            if (dc != IntPtr.Zero) { DeleteDC(dc); dc = IntPtr.Zero; }
            if (texture != null) { texture.Dispose(); texture = null; }
            if (graphics != null) { graphics.Dispose(); graphics = null; }
            if (canvas != null) { canvas.Dispose(); canvas = null; }
            if (snapshot != null) { snapshot.Dispose(); snapshot = null; }
        }
    }
}
