using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Player
{
    internal interface IAudioSource : IDisposable
    {
        WaveFormat Format { get; }
        string DeviceId { get; }
        void Start();
        void Stop();
        void Reset();
        void Drain(Action<byte[], int, ulong, uint> consume);
        void CheckDevice();
    }
    // These COM objects and buffers are exclusively owned by one MTA worker.
    internal sealed class AudioCapture : IAudioSource
    {
        private IMMDeviceEnumerator enumerator;
        private IMMDevice device;
        private IAudioClient client;
        private IAudioCaptureClient capture;
        private bool running;
        public WaveFormat Format { get; private set; }
        public string DeviceId { get; private set; }

        internal AudioCapture()
        {
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                Check(enumerator.GetDefaultAudioEndpoint(0, 0, out device));
                string id;
                Check(device.GetId(out id));
                DeviceId = id;
                Guid iid = typeof(IAudioClient).GUID;
                object instance;
                Check(device.Activate(ref iid, 23, IntPtr.Zero, out instance));
                client = (IAudioClient)instance;
                IntPtr format;
                Check(client.GetMixFormat(out format));
                try
                {
                    int extra = (ushort)Marshal.ReadInt16(format, 16);
                    if (extra > 1024) throw new InvalidOperationException("Unexpected audio format length.");
                    byte[] bytes = new byte[18 + extra];
                    Marshal.Copy(format, bytes, 0, bytes.Length);
                    Format = new WaveFormat(bytes);
                    Check(client.Initialize(0, 0x00020000, 2000000, 0, format, IntPtr.Zero));
                }
                finally { Marshal.FreeCoTaskMem(format); }
                iid = typeof(IAudioCaptureClient).GUID;
                Check(client.GetService(ref iid, out instance));
                capture = (IAudioCaptureClient)instance;
            }
            catch { Dispose(); throw; }
        }
        internal static long Clock100ns
        {
            get { return (long)(Stopwatch.GetTimestamp() * (10000000.0 / Stopwatch.Frequency)); }
        }
        public void Start() { Check(client.Start()); running = true; }
        public void Stop() { if (running) { Check(client.Stop()); running = false; } }
        public void Reset() { Check(client.Reset()); }
        public void Drain(Action<byte[], int, ulong, uint> consume)
        {
            uint frames;
            Check(capture.GetNextPacketSize(out frames));
            int packets = 0;
            while (frames != 0)
            {
                if (++packets > 4096) throw new InvalidOperationException("Audio device buffer did not drain.");
                IntPtr pointer;
                uint flags;
                ulong position, qpc;
                Check(capture.GetBuffer(out pointer, out frames, out flags, out position, out qpc));
                try
                {
                    int count = checked((int)frames * Format.BlockAlign);
                    byte[] data = new byte[count];
                    if ((flags & 2) == 0 && count > 0) Marshal.Copy(pointer, data, 0, count);
                    long now = Clock100ns;
                    // Reject impossible device timestamps before they can create a huge silence gap.
                    if (qpc > (ulong)(now + 10000000) || qpc < (ulong)Math.Max(0, now - 20000000))
                        flags |= 4;
                    consume(data, (int)frames, qpc, flags);
                }
                finally { Check(capture.ReleaseBuffer(frames)); }
                Check(capture.GetNextPacketSize(out frames));
            }
        }
        public void CheckDevice()
        {
            uint state, padding;
            Check(device.GetState(out state));
            if (state != 1) throw new InvalidOperationException("Устройство вывода отключено.");
            Check(client.GetCurrentPadding(out padding));
            IMMDevice current = null;
            try
            {
                Check(enumerator.GetDefaultAudioEndpoint(0, 0, out current));
                string id;
                Check(current.GetId(out id));
                if (id != DeviceId) throw new InvalidOperationException("Устройство вывода сменилось. Начните новую запись для нового устройства.");
            }
            finally { Release(current); }
        }
        public void Dispose()
        {
            if (client != null && running) { client.Stop(); running = false; }
            Release(capture); capture = null;
            Release(client); client = null;
            Release(device); device = null;
            Release(enumerator); enumerator = null;
        }
        private static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
        private static void Release(object value) { if (value != null) Marshal.ReleaseComObject(value); }
    }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice endpoint);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }
    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint flags, long duration, long period, IntPtr format, IntPtr session);
        [PreserveSig] int GetBufferSize(out uint size);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long normal, out long minimum);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }
    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong position, out ulong qpc);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }
}
