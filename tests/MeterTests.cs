using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SoundLeaf
{
    internal static class MeterTests
    {
        private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        internal static void Run(Action<string, Action> test, string root)
        {
            test("Presentation threshold rejects noise and isolated peaks without changing samples", delegate
            {
                var gate = new AudioPresenceGate();
                for (int i = 0; i < 100; i++) Assert(!gate.Observe(i % 2 == 0 ? .001f : .0035f, true), "Background toggled sound on.");
                Assert(gate.Observe(.01f, true), "Playing sound not detected.");
                for (int i = 0; i < 100; i++) Assert(gate.Observe(i % 2 == 0 ? .003f : .0041f, true), "Near-threshold sound flickered.");
                Assert(!gate.Observe(0, true) && !gate.Observe(.003f, true), "Silence did not reset hysteresis.");
                Assert(!gate.Observe(float.NaN, true) && !gate.Observe(float.PositiveInfinity, true), "Nonfinite signal accepted.");
                Assert(!gate.Observe(1, false), "Unavailable device shown as sound.");
            });
            foreach (int bits in new[] { 16, 24, 32 })
            {
                int depth = bits;
                test("PCM " + depth + " meter signed full scale and unmodified input", delegate
                {
                    var format = Tests.Format(depth, false, false); var meter = new AudioMeter(format);
                    byte[] packet = new byte[format.BlockAlign]; int stride = depth / 8;
                    packet[stride - 1] = 0x80; for (int i = stride; i < stride * 2; i++) packet[i] = 0;
                    byte[] original = (byte[])packet.Clone(); meter.Observe(packet, 1, 0, 100);
                    Assert(meter.Peak(100) == 1 && Math.Abs(meter.Rms(100) - Math.Sqrt(.5)) < .00001, "Signed meter scaling incorrect.");
                    for (int i = 0; i < packet.Length; i++) Assert(packet[i] == original[i], "Meter mutated PCM.");
                });
            }
            test("Float meter ignores nonfinite values and never mutates PCM", delegate
            {
                var format = Tests.Format(32, true, false); var meter = new AudioMeter(format); var packet = new byte[16];
                Buffer.BlockCopy(BitConverter.GetBytes(float.NaN), 0, packet, 0, 4); Buffer.BlockCopy(BitConverter.GetBytes(float.PositiveInfinity), 0, packet, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(.5f), 0, packet, 8, 4); Buffer.BlockCopy(BitConverter.GetBytes(-.5f), 0, packet, 12, 4);
                var original = (byte[])packet.Clone(); meter.Observe(packet, 2, 0, 100);
                Assert(meter.Peak(100) == .5f && Math.Abs(meter.Rms(100) - Math.Sqrt(.125)) < .00001, "Float meter incorrect.");
                for (int i = 0; i < packet.Length; i++) Assert(packet[i] == original[i], "Float PCM changed.");
            });
            test("Silence flag and no-packet expiry reset current level", delegate
            {
                Assert(AudioMeter.Freshness100ns == 1500000, "No-packet expiry exceeds 150 ms.");
                var format = Tests.Format(16, false, false); var meter = new AudioMeter(format); byte[] data = { 0, 64, 0, 64 };
                meter.Observe(data, 1, 0, 100); Assert(meter.Peak(100) == .5f, "Signal not detected.");
                Assert(meter.Peak(100 + AudioMeter.Freshness100ns + 1) == 0, "Stale signal shown as live.");
                meter.Observe(data, 1, 2, 100); Assert(meter.Peak(100) == 0 && meter.Rms(100) == 0, "Silent packet shown as sound.");
                meter.Observe(data, 1, 0, 100); meter.Reset(); Assert(meter.Peak(100) == 0, "Pause reset failed.");
            });
            test("Meter filters tiny floating noise and limits observation to valid frames", delegate
            {
                var format = Tests.Format(32, true, false); var meter = new AudioMeter(format); byte[] data = new byte[16];
                Buffer.BlockCopy(BitConverter.GetBytes(.000001f), 0, data, 0, 4); Buffer.BlockCopy(BitConverter.GetBytes(1f), 0, data, 8, 4);
                meter.Observe(data, 1, 0, 100); Assert(meter.Peak(100) == 0, "Unused buffer tail or noise counted.");
            });
            test("Telemetry carries stable session identity and retains detected signal through silence", delegate
            {
                var updates = new List<SessionUpdate>(); var gate = new object(); var finished = new ManualResetEvent(false);
                string folder = Path.Combine(root, "meter-session"); Directory.CreateDirectory(folder);
                var session = new RecordingSession(folder, "", new AppLog(folder), delegate(SessionUpdate value)
                { lock (gate) updates.Add(value); if (value.State == RecordState.Stopped || value.State == RecordState.Faulted) finished.Set(); },
                    delegate { return new MeterSource(); }, RecordingMode.WavOnly, folder, 256 * 1024, null);
                session.Start(); Thread.Sleep(700); session.Stop(); Assert(finished.WaitOne(5000), "Meter session stalled.");
                lock (gate)
                {
                    var signal = updates.Find(delegate(SessionUpdate value) { return value.Telemetry && value.Peak > 0; });
                    var quiet = updates.Find(delegate(SessionUpdate value) { return value.Telemetry && value.HeardSignal && value.Peak == 0; });
                    Assert(signal != null && quiet != null && signal.DeviceAvailable && quiet.DeviceAvailable, "Signal or silent/device telemetry missing.");
                    Assert(signal.SessionId == quiet.SessionId && signal.Profile.Label == quiet.Profile.Label && quiet.CapturedBytes > 0, "Session snapshot changed.");
                    Assert(updates[updates.Count - 1].State == RecordState.Stopped, "WAV telemetry changed completion.");
                }
                finished.Dispose();
            });
        }
        private sealed class MeterSource : IAudioSource
        {
            private bool emitted;
            public WaveFormat Format { get { return Tests.Format(16, false, false); } }
            public string DeviceId { get { return "synthetic-meter"; } }
            public void Start() { } public void Stop() { } public void Reset() { } public void CheckDevice() { } public void Dispose() { }
            public void Drain(Action<byte[], int, ulong, uint> consume)
            {
                if (emitted) return; emitted = true; byte[] bytes = new byte[4800 * Format.BlockAlign];
                for (int i = 0; i < bytes.Length; i += 2) bytes[i + 1] = 32;
                consume(bytes, 4800, (ulong)AudioCapture.Clock100ns, 0);
            }
        }
    }
}
