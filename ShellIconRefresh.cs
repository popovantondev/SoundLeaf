using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SoundLeaf
{
    // Installer-only shell adapter; no recorder startup or global cache reset.
    public sealed class ShellIconSnapshot
    {
        public string Path, Location;
        public int ResourceIndex, SystemIndex;
        public uint Flags;
    }
    public static class ShellIconRefresh
    {
        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            [PreserveSig] int BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result);
        }
        [ComImport, Guid("000214FA-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IExtractIcon
        {
            [PreserveSig] int GetIconLocation(uint flags, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder location, uint length, out int index, out uint attributes);
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct FileInfo
        {
            public IntPtr Icon; public int Index; public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string Type;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)] private static extern void SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern void SHUpdateImage(string path, int resourceIndex, uint flags, int systemIndex);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern void SHChangeNotify(uint change, uint flags, string path, IntPtr second);
        [DllImport("shell32.dll", EntryPoint = "SHChangeNotify")] private static extern void NotifyIndex(uint change, uint flags, IntPtr first, IntPtr second);
        [DllImport("shell32.dll", PreserveSig = false)] private static extern void SHGetImageList(int size, ref Guid iid, out IntPtr list);
        [DllImport("comctl32.dll")] private static extern IntPtr ImageList_GetIcon(IntPtr list, int index, uint flags);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
        public static ShellIconSnapshot Capture(string path)
        {
            path = System.IO.Path.GetFullPath(path);
            if (!File.Exists(path)) throw new FileNotFoundException("Icon target missing.", path);
            IShellItem item = null; object handlerObject = null;
            try
            {
                Guid itemId = typeof(IShellItem).GUID;
                SHCreateItemFromParsingName(path, IntPtr.Zero, ref itemId, out item);
                Guid handler = new Guid("3981E225-F559-11D3-8E3A-00C04F6837D5"), extractorId = typeof(IExtractIcon).GUID;
                Marshal.ThrowExceptionForHR(item.BindToHandler(IntPtr.Zero, ref handler, ref extractorId, out handlerObject));
                var location = new StringBuilder(32768); int index; uint flags;
                int result = ((IExtractIcon)handlerObject).GetIconLocation(2, location, (uint)location.Capacity, out index, out flags);
                if (result != 0) throw new InvalidOperationException("Shell did not return a concrete icon location: " + result);
                FileInfo info;
                if (SHGetFileInfo(path, 0, out info, (uint)Marshal.SizeOf(typeof(FileInfo)), 0x4000) == IntPtr.Zero) throw new Win32Exception();
                return new ShellIconSnapshot { Path = path, Location = location.ToString(), ResourceIndex = index, Flags = flags, SystemIndex = info.Index };
            }
            finally
            {
                if (handlerObject != null) Marshal.ReleaseComObject(handlerObject);
                if (item != null) Marshal.ReleaseComObject(item);
            }
        }
        public static void Refresh(ShellIconSnapshot previous)
        {
            // Use the OLD extractor location/index/flags captured before replacement.
            if (previous == null) throw new ArgumentNullException("previous");
            SHUpdateImage(previous.Location, previous.ResourceIndex, previous.Flags, previous.SystemIndex);
            NotifyIndex(0x8000, 0x1003, IntPtr.Zero, new IntPtr(previous.SystemIndex));
            SHChangeNotify(0x2000, 0x1005, previous.Path, IntPtr.Zero);
            SHChangeNotify(0x1000, 0x1005, System.IO.Path.GetDirectoryName(previous.Path), IntPtr.Zero);
        }
        public static void InvalidateShellArtwork()
        {
            // Notification only: Windows rebuilds its artwork cache. No file/registry deletion.
            NotifyIndex(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero);
        }
        public static Bitmap ReadCached(string path, int imageListSize)
        {
            if (imageListSize < 0 || imageListSize > 4) throw new ArgumentOutOfRangeException("imageListSize");
            FileInfo info;
            if (SHGetFileInfo(System.IO.Path.GetFullPath(path), 0, out info, (uint)Marshal.SizeOf(typeof(FileInfo)), 0x4000) == IntPtr.Zero) throw new Win32Exception();
            Guid iid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"); IntPtr list;
            SHGetImageList(imageListSize, ref iid, out list);
            try
            {
                IntPtr handle = ImageList_GetIcon(list, info.Index, 1);
                if (handle == IntPtr.Zero) throw new Win32Exception();
                try { using (var icon = Icon.FromHandle(handle)) return icon.ToBitmap(); }
                finally { DestroyIcon(handle); }
            }
            finally { Marshal.Release(list); }
        }
        public static bool SameArtwork(string first, string second, int size)
        {
            using (var a = ReadCached(first, size)) using (var b = ReadCached(second, size))
            {
                if (a.Size != b.Size) return false;
                for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++)
                {
                    var p = a.GetPixel(x,y); var q = b.GetPixel(x,y);
                    if (p.A == 0 && q.A == 0) continue;
                    if (p.ToArgb() != q.ToArgb()) return false;
                }
                return true;
            }
        }
    }
}
