using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SoundLeaf
{
    internal enum TrayEdge { Bottom, Top, Left, Right }
    internal sealed class TrayAnchor
    {
        internal Rectangle Icon, Work, Monitor;
        internal float Scale = 1;
        internal TrayEdge Edge;
        internal bool Native;
        internal Point Center { get { return new Point(Icon.Left + Icon.Width / 2, Icon.Top + Icon.Height / 2); } }
    }
    internal static class TrayAnchorResolver
    {
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; public Rectangle Rectangle { get { return Rectangle.FromLTRB(Left, Top, Right, Bottom); } } }
        [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X, Y; public PointNative(Point p) { X = p.X; Y = p.Y; } }
        [StructLayout(LayoutKind.Sequential)] private struct Identifier { public uint Size; public IntPtr Window; public uint Id; public Guid Guid; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
        [DllImport("shell32.dll")] private static extern int Shell_NotifyIconGetRect(ref Identifier identifier, out Rect rect);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(PointNative point, uint flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SystemParametersInfo(uint action, uint param, out bool result, uint flags);

        // Framework implementation details are contained here; lookup failure is a safe fallback.
        internal static bool TryGetIdentity(NotifyIcon icon, out IntPtr handle, out uint identifier)
        {
            handle = IntPtr.Zero; identifier = 0;
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Type type = typeof(NotifyIcon);
                FieldInfo id = type.GetField("id", flags) ?? type.GetField("_id", flags);
                FieldInfo window = type.GetField("window", flags) ?? type.GetField("_window", flags);
                if (id == null || window == null) return false;
                var native = window.GetValue(icon) as NativeWindow;
                if (native == null || native.Handle == IntPtr.Zero) return false;
                handle = native.Handle; identifier = Convert.ToUInt32(id.GetValue(icon)); return true;
            }
            catch (Exception) { return false; }
        }
        internal static bool TryGetRectangle(NotifyIcon icon, out Rectangle rectangle)
        {
            rectangle = Rectangle.Empty;
            try
            {
                IntPtr handle; uint id; if (!TryGetIdentity(icon, out handle, out id)) return false;
                var identifier = new Identifier { Size = (uint)Marshal.SizeOf(typeof(Identifier)), Window = handle, Id = id };
                Rect result;
                if (Shell_NotifyIconGetRect(ref identifier, out result) != 0 || result.Right <= result.Left || result.Bottom <= result.Top) return false;
                rectangle = result.Rectangle; return true;
            }
            catch (Exception) { return false; }
        }
        internal static TrayAnchor Resolve(NotifyIcon icon, Point click)
        {
            Rectangle rectangle = Rectangle.Empty; bool native = icon != null && TryGetRectangle(icon, out rectangle);
            if (!native) rectangle = new Rectangle(click.X, click.Y, 1, 1);
            Point center = new Point(rectangle.Left + rectangle.Width / 2, rectangle.Top + rectangle.Height / 2);
            IntPtr monitor = MonitorFromPoint(new PointNative(center), 2);
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf(typeof(MonitorInfo)) };
            Rectangle work, screen;
            if (GetMonitorInfo(monitor, ref info)) { work = info.Work.Rectangle; screen = info.Monitor.Rectangle; }
            else { var fallback = Screen.FromPoint(center); work = fallback.WorkingArea; screen = fallback.Bounds; }
            float scale = 1;
            // An invisible per-monitor window obtains the target DPI before positioning the panel.
            // GetDpiForMonitor is not supported on a per-monitor-aware calling thread.
            var probe = new NativeWindow();
            try
            {
                probe.CreateHandle(new CreateParams { X = center.X, Y = center.Y, Width = 1, Height = 1, Style = unchecked((int)0x80000000), ExStyle = 0x08000080 });
                uint dpi = GetDpiForWindow(probe.Handle);
                if (dpi >= 96 && dpi <= 768) scale = dpi / 96f;
            }
            finally { if (probe.Handle != IntPtr.Zero) probe.DestroyHandle(); }
            return new TrayAnchor { Icon = rectangle, Work = work, Monitor = screen, Scale = scale, Edge = DetectEdge(center, screen, work), Native = native };
        }
        internal static TrayEdge DetectEdge(Point point, Rectangle monitor, Rectangle work)
        {
            int[] insets = { monitor.Bottom - work.Bottom, work.Top - monitor.Top, work.Left - monitor.Left, monitor.Right - work.Right };
            int largest = 0;
            for (int i = 1; i < 4; i++) if (insets[i] > insets[largest]) largest = i;
            if (insets[largest] > 0) return (TrayEdge)largest;
            int[] distances = { Math.Abs(point.Y - work.Bottom), Math.Abs(point.Y - work.Top), Math.Abs(point.X - work.Left), Math.Abs(point.X - work.Right) };
            int nearest = 0;
            for (int i = 1; i < 4; i++) if (distances[i] < distances[nearest]) nearest = i;
            return (TrayEdge)nearest;
        }
        internal static Rectangle Place(TrayAnchor anchor, Size desired)
        {
            Rectangle work = anchor.Work; int gap = Math.Max(1, (int)Math.Round(8 * anchor.Scale));
            gap = Math.Min(gap, Math.Max(1, Math.Min(work.Width, work.Height) / 8));
            int width = Math.Max(1, Math.Min(desired.Width, work.Width - gap * 2));
            int height = Math.Max(1, Math.Min(desired.Height, work.Height - gap * 2));
            Point point = anchor.Center;
            int left = point.X - width / 2, top = work.Bottom - gap - height;
            if (anchor.Edge == TrayEdge.Top) top = work.Top + gap;
            if (anchor.Edge == TrayEdge.Left) { left = work.Left + gap; top = point.Y - height / 2; }
            if (anchor.Edge == TrayEdge.Right) { left = work.Right - gap - width; top = point.Y - height / 2; }
            return new Rectangle(Math.Max(work.Left + gap, Math.Min(left, work.Right - gap - width)), Math.Max(work.Top + gap, Math.Min(top, work.Bottom - gap - height)), width, height);
        }
        internal static bool AnimationsEnabled()
        {
            bool enabled; return SystemParametersInfo(0x1042, 0, out enabled, 0) && enabled;
        }
    }
}
