using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SoundLeaf
{
    // Separate hidden notification identity keeps both notification icons leafy,
    // without ever replacing the recording/pause/saving icon in the actual tray.
    internal sealed class BrandedNotifications : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Data
        {
            internal uint Size; internal IntPtr Window; internal uint Id, Flags, Callback;
            internal IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Tip;
            internal uint State, StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] internal string Info;
            internal uint Timeout;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] internal string Title;
            internal uint InfoFlags; internal Guid Guid; internal IntPtr BalloonIcon;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint operation, ref Data data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int metric, uint dpi);
        private readonly NotifyIcon icon = new NotifyIcon { Text = "SoundLeaf" };
        private Icon headerBrand, balloonBrand;
        private uint artworkDpi;
        internal int HeaderPixels { get { return headerBrand.Width; } }
        internal int BalloonPixels { get { return balloonBrand.Width; } }
        internal bool LastPublished { get; private set; }
        internal BrandedNotifications()
        {
            SetArtworkDpi(NotificationDpi());
            icon.BalloonTipClosed += delegate { icon.Visible = false; };
            icon.BalloonTipClicked += delegate { icon.Visible = false; };
        }
        internal void Show(int duration, string title, string message, ToolTipIcon severity)
        {
            SetArtworkDpi(NotificationDpi());
            icon.Visible = true;
            IntPtr window; uint id;
            if (!TrayAnchorResolver.TryGetIdentity(icon, out window, out id)) { icon.Visible = false; LastPublished = false; return; }
            var data = new Data { Size = (uint)Marshal.SizeOf(typeof(Data)), Window = window, Id = id,
                Flags = 0x08 | 0x10, State = 1, StateMask = 1, Tip = "", Info = Limit(message, 255), Title = Limit(title, 63),
                Timeout = (uint)duration, InfoFlags = 4 | 0x10 | 0x20 | 0x80, BalloonIcon = balloonBrand.Handle };
            LastPublished = Shell_NotifyIcon(1, ref data);
            if (!LastPublished) icon.Visible = false;
        }
        private static uint NotificationDpi()
        {
            // Native notification UI belongs to the main shell taskbar, not the cursor's monitor.
            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            uint dpi = tray != IntPtr.Zero ? GetDpiForWindow(tray) : GetDpiForSystem();
            return dpi == 0 ? 96u : dpi;
        }
        internal void SetArtworkDpi(uint dpi)
        {
            if (dpi == artworkDpi && headerBrand != null) return;
            int small = Math.Max(8, Math.Min(256, GetSystemMetricsForDpi(49, dpi)));
            int large = Math.Max(8, Math.Min(256, GetSystemMetricsForDpi(11, dpi)));
            Icon nextHeader = SoundLeafIcons.CreateBrand(small), nextBalloon = null;
            try
            {
                nextBalloon = SoundLeafIcons.CreateBrand(large);
                icon.Icon = nextHeader;
            }
            catch { nextHeader.Dispose(); if (nextBalloon != null) nextBalloon.Dispose(); throw; }
            Icon previousHeader = headerBrand, previousBalloon = balloonBrand;
            headerBrand = nextHeader; balloonBrand = nextBalloon; artworkDpi = dpi;
            if (previousHeader != null) previousHeader.Dispose(); if (previousBalloon != null) previousBalloon.Dispose();
        }
        private static string Limit(string text, int length) { text = text ?? ""; return text.Length <= length ? text : text.Substring(0, length - 1) + "…"; }
        public void Dispose() { icon.Visible = false; icon.Dispose(); if (headerBrand != null) headerBrand.Dispose(); if (balloonBrand != null) balloonBrand.Dispose(); }
    }
}
