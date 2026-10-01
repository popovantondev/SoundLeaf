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
        private readonly NotifyIcon icon = new NotifyIcon { Text = "SoundLeaf" };
        private readonly Icon brand = SoundLeafIcons.Create(5, 0);
        internal bool LastPublished { get; private set; }
        internal BrandedNotifications()
        {
            icon.Icon = brand;
            icon.BalloonTipClosed += delegate { icon.Visible = false; };
            icon.BalloonTipClicked += delegate { icon.Visible = false; };
        }
        internal void Show(int duration, string title, string message, ToolTipIcon severity)
        {
            icon.Visible = true;
            IntPtr window; uint id;
            if (!TrayAnchorResolver.TryGetIdentity(icon, out window, out id)) { icon.Visible = false; LastPublished = false; return; }
            var data = new Data { Size = (uint)Marshal.SizeOf(typeof(Data)), Window = window, Id = id,
                Flags = 0x08 | 0x10, State = 1, StateMask = 1, Tip = "", Info = Limit(message, 255), Title = Limit(title, 63),
                Timeout = (uint)duration, InfoFlags = 4 | 0x10 | 0x20 | 0x80, BalloonIcon = brand.Handle };
            LastPublished = Shell_NotifyIcon(1, ref data);
            if (!LastPublished) icon.Visible = false;
        }
        private static string Limit(string text, int length) { text = text ?? ""; return text.Length <= length ? text : text.Substring(0, length - 1) + "…"; }
        public void Dispose() { icon.Visible = false; icon.Dispose(); brand.Dispose(); }
    }
}
