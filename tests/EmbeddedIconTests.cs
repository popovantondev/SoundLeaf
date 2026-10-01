using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace SoundLeaf
{
    public static class EmbeddedIconTests
    {
        private delegate bool ResourceName(IntPtr module, IntPtr type, IntPtr name, IntPtr parameter);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool EnumResourceNames(IntPtr module, IntPtr type, ResourceName callback, IntPtr parameter);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
        [DllImport("kernel32.dll")] private static extern uint SizeofResource(IntPtr module, IntPtr resource);
        [DllImport("kernel32.dll")] private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
        [DllImport("kernel32.dll")] private static extern IntPtr LockResource(IntPtr resource);
        private static byte[] Read(IntPtr module, IntPtr name, int type)
        {
            IntPtr resource = FindResource(module, name, new IntPtr(type));
            if (resource == IntPtr.Zero) throw new Win32Exception();
            int size = checked((int)SizeofResource(module, resource)); if (size <= 0 || size > 1048576) throw new InvalidDataException("Invalid icon resource size.");
            IntPtr address = LockResource(LoadResource(module, resource)); if (address == IntPtr.Zero) throw new Win32Exception();
            byte[] bytes = new byte[size]; Marshal.Copy(address, bytes, 0, size); return bytes;
        }
        public static int Verify(string executable, string ico)
        {
            IntPtr module = LoadLibraryEx(Path.GetFullPath(executable), IntPtr.Zero, 0x22);
            if (module == IntPtr.Zero) throw new Win32Exception();
            try
            {
                byte[] group = null;
                ResourceName callback = delegate(IntPtr loaded, IntPtr type, IntPtr name, IntPtr unused) { group = Read(loaded, name, 14); return false; };
                EnumResourceNames(module, new IntPtr(14), callback, IntPtr.Zero); GC.KeepAlive(callback);
                if (group == null) throw new InvalidDataException("EXE has no icon group.");
                byte[] reference = File.ReadAllBytes(ico); int count = BitConverter.ToUInt16(reference, 4);
                if (count != 8 || BitConverter.ToUInt16(group, 4) != count) throw new InvalidDataException("Missing icon resolutions.");
                for (int i = 0; i < count; i++)
                {
                    int entry = 6 + i * 16, resourceEntry = 6 + i * 14;
                    int length = checked((int)BitConverter.ToUInt32(reference, entry + 8)); int offset = checked((int)BitConverter.ToUInt32(reference, entry + 12));
                    byte[] actual = Read(module, new IntPtr(BitConverter.ToUInt16(group, resourceEntry + 12)), 3);
                    if (actual.Length != length || offset < 0 || offset > reference.Length - length) throw new InvalidDataException("Icon size differs from brand resource.");
                    for (int pixel = 0; pixel < length; pixel++) if (reference[offset + pixel] != actual[pixel]) throw new InvalidDataException("EXE artwork differs from SoundLeaf.ico.");
                }
                return count;
            }
            finally { FreeLibrary(module); }
        }
    }
}
