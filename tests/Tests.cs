using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SoundLeaf
{
    internal static class Tests
    {
        private static string root, ffmpeg;
        private static int passed;
        private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Throws(Action action)
        {
            bool threw = false;
            try { action(); } catch { threw = true; }
            Assert(threw, "Expected an exception.");
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var f = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(f));
        }
        internal static WaveFormat Format(int bits, bool floating, bool extensible)
        {
            byte[] data = new byte[extensible ? 40 : 16];
            Action<int, byte[]> put = delegate(int offset, byte[] bytes) { Buffer.BlockCopy(bytes, 0, data, offset, bytes.Length); };
            put(0, BitConverter.GetBytes((ushort)(extensible ? 65534 : floating ? 3 : 1)));
            put(2, BitConverter.GetBytes((ushort)2));
            put(4, BitConverter.GetBytes(48000));
            put(8, BitConverter.GetBytes(48000 * 2 * bits / 8));
            put(12, BitConverter.GetBytes((ushort)(2 * bits / 8)));
            put(14, BitConverter.GetBytes((ushort)bits));
            if (extensible)
            {
                put(16, BitConverter.GetBytes((ushort)22));
                put(18, BitConverter.GetBytes((ushort)bits));
                put(20, BitConverter.GetBytes(3));
                put(24, new Guid(floating ? "00000003-0000-0010-8000-00aa00389b71" : "00000001-0000-0010-8000-00aa00389b71").ToByteArray());
            }
            return new WaveFormat(data);
        }
        private static byte[] Tone(WaveFormat format, double duration)
        {
            byte[] data = new byte[(int)(format.SampleRate * duration) * format.BlockAlign];
            for (int frame = 0; frame < data.Length / format.BlockAlign; frame++)
            {
                double value = 0.03 * Math.Sin(frame * 2 * Math.PI * 440 / format.SampleRate);
                byte[] sample = format.Float ? BitConverter.GetBytes((float)value) :
                    format.Bits == 16 ? BitConverter.GetBytes((short)(value * short.MaxValue)) : BitConverter.GetBytes((int)(value * int.MaxValue));
                for (int channel = 0; channel < format.Channels; channel++)
                    Buffer.BlockCopy(sample, 0, data, frame * format.BlockAlign + channel * (format.Bits / 8), sample.Length);
            }
            return data;
        }
        private static string WriteWave(string name, WaveFormat format, byte[] data)
        {
            using (var wave = new DurableWave(Path.Combine(root, name + ".partial.wav"), format))
            { wave.Write(data, 0, data.Length); return wave.Complete(); }
        }
        private static void Case(string name, Action action)
        {
            action(); passed++; Console.WriteLine("PASS " + name);
        }
        private static int Main(string[] args)
        {
            if (Process.GetCurrentProcess().ProcessName == "FakeFfmpegHang") { Thread.Sleep(60000); return 0; }
            if (args.Length == 2 && args[0] == "--crash-completed-wav")
            { ReadinessTests.WriteCompletedWav(args[1]); Environment.Exit(42); return 42; }
            if (args.Length == 3 && args[0] == "--recover-existing")
            {
                string folder = Path.GetFullPath(args[1]);
                string installedRoot = Directory.GetParent(Directory.GetParent(folder).FullName).FullName;
                if (!string.Equals(installedRoot, @"C:\Users\Public\Player", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Unexpected installation folder.");
                var pending = SessionRecovery.Find(folder);
                foreach (var item in pending)
                    if (item.Stem == args[2])
                    {
                        Console.WriteLine("RECOVERED " + SessionRecovery.Recover(item, installedRoot,
                            Path.Combine(installedRoot, "tools", "ffmpeg.exe"), Console.WriteLine));
                        return 0;
                    }
                throw new InvalidDataException("Requested session not found.");
            }
            if (args.Length == 3 && args[0] == "--merge-existing")
            {
                string folder = Path.GetFullPath(args[1]);
                string stem = args[2];
                if (stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || stem.IndexOfAny(new[] { '*', '?' }) >= 0)
                    throw new ArgumentException("Invalid recording stem.");
                var wavs = new System.Collections.Generic.List<string>();
                for (int i = 1; i <= 999; i++)
                {
                    string wav = Path.Combine(folder, stem + "-" + i.ToString("D3") + ".wav");
                    if (!File.Exists(wav)) break;
                    wavs.Add(wav);
                }
                if (wavs.Count < 2) throw new InvalidDataException("Recording parts missing.");
                string installedRoot = Directory.GetParent(Directory.GetParent(folder).FullName).FullName;
                if (!string.Equals(installedRoot, @"C:\Users\Public\Player", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Unexpected Player installation folder.");
                string output = MediaExport.ConcatParts(wavs, Path.Combine(installedRoot, "tools", "ffmpeg.exe"));
                string archive = MediaExport.ArchiveParts(wavs, installedRoot, true);
                Console.WriteLine("MERGED " + output + "; original parts archived at " + archive);
                return 0;
            }
            if (args.Length > 0 && args[0] == "--hang") { Thread.Sleep(60000); return 0; }
            if (args.Length == 2 && args[0] == "--crash")
            {
                var format = Format(16, false, false);
                var writer = new DurableWave(args[1], format);
                byte[] bytes = Tone(format, 2);
                writer.Write(bytes, 0, bytes.Length);
                writer.Checkpoint();
                writer.Write(bytes, 0, bytes.Length);
                Environment.Exit(42);
            }
            root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Verification",
                "unit-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(root);
            ffmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ffmpeg.exe");
            try
            {
                Case("format PCM16/PCM32/float32 and malformed extensible", delegate
                {
                    Assert(!Format(32, false, true).Float, "PCM32 must not be guessed as float.");
                    Assert(Format(32, true, true).Float, "IEEE float subformat");
                    byte[] malformed = Format(32, true, true).Bytes;
                    Array.Resize(ref malformed, 16);
                    Throws(delegate { new WaveFormat(malformed); });
                    byte[] invalid = Format(16, false, false).Bytes;
                    invalid[12] = 7;
                    Throws(delegate { new WaveFormat(invalid); });
                });
                Case("old recording-part folders are cleaned as units", delegate
                {
                    string playerRoot = Path.Combine(root, "cleanup-root");
                    string archive = Path.Combine(playerRoot, "Backups", "RecordingParts");
                    string oldFolder = Path.Combine(archive, "old-session");
                    string freshFolder = Path.Combine(archive, "fresh-session");
                    string activeFolder = Path.Combine(archive, "active-session");
                    Directory.CreateDirectory(oldFolder); Directory.CreateDirectory(freshFolder); Directory.CreateDirectory(activeFolder);
                    File.WriteAllText(Path.Combine(oldFolder, "001.wav"), "old");
                    File.WriteAllText(Path.Combine(freshFolder, "001.wav"), "fresh");
                    File.WriteAllText(Path.Combine(activeFolder, "001.partial.wav"), "active");
                    DateTime now = DateTime.UtcNow;
                    DateTime old = now - TimeSpan.FromDays(2);
                    File.SetLastWriteTimeUtc(Path.Combine(oldFolder, "001.wav"), old);
                    Directory.SetLastWriteTimeUtc(oldFolder, old);
                    var result = BackupCleanup.RemoveOldRecordingParts(playerRoot, now, TimeSpan.FromDays(1));
                    Assert(result.DeletedFolders == 1 && !Directory.Exists(oldFolder), "Old folder was not removed.");
                    Assert(Directory.Exists(freshFolder) && Directory.Exists(activeFolder), "Fresh or active folder was removed.");
                    Assert(result.DeletedBytes == 3, "Deleted byte count is wrong.");
                });
                Case("durable header checkpoint and data sizes", delegate
                {
                    var f = Format(32, true, true);
                    string path = Path.Combine(root, "checkpoint.partial.wav");
                    using (var wave = new DurableWave(path, f))
                    {
                        Assert(new FileInfo(path).Length >= 44, "Header exists before audio.");
                        byte[] bytes = Tone(f, 1);
                        wave.Write(bytes, 0, bytes.Length); wave.Checkpoint();
                    }
                    var info = DurableWave.ReadInfo(path, false);
                    Assert(info.Duration == 1 && info.DataBytes == 384000, "Exact checkpoint sizes.");
                });
                Case("CreateNew collision never replaces existing bytes", delegate
                {
                    string path = Path.Combine(root, "collision.partial.wav");
                    File.WriteAllText(path, "user data");
                    string before = Hash(path);
                    Throws(delegate { using (new DurableWave(path, Format(16, false, false))) { } });
                    Assert(Hash(path) == before, "Collision changed existing file.");
                });
                Case("rename collision preserves both source and destination", delegate
                {
                    string partial = Path.Combine(root, "rename.partial.wav");
                    string final = Path.Combine(root, "rename.wav");
                    File.WriteAllText(final, "existing final");
                    string before = Hash(final);
                    using (var wave = new DurableWave(partial, Format(16, false, false)))
                    {
                        var bytes = Tone(wave.Format, 1); wave.Write(bytes, 0, bytes.Length);
                        Throws(delegate { wave.Complete(); });
                    }
                    Assert(Hash(final) == before && DurableWave.ReadInfo(partial, false).Duration == 1, "Rename destroyed data.");
                });
                Case("empty recording is not published", delegate
                {
                    string path = Path.Combine(root, "empty.partial.wav");
                    using (var wave = new DurableWave(path, Format(16, false, false)))
                        Throws(delegate { wave.Complete(); });
                    Assert(!File.Exists(Path.Combine(root, "empty.wav")), "Published empty file.");
                });
                Case("segmentation retains all frames and avoids empty final part", delegate
                {
                    var f = Format(16, false, false);
                    using (var sink = new SegmentedWave(Path.Combine(root, "segments"), f, f.BlockAlign * 100L))
                    {
                        byte[] data = new byte[f.BlockAlign * 300];
                        sink.Write(data, 0, data.Length); sink.Complete();
                        Assert(sink.Completed.Count == 3 && sink.Frames == 300, "Wrong segment count.");
                        long total = 0;
                        foreach (var part in sink.Completed) total += DurableWave.ReadInfo(part, false).DataBytes / f.BlockAlign;
                        Assert(total == 300, "Frames lost at split.");
                    }
                });
                Case("silence duration and pause exclusion", delegate
                {
                    var f = Format(16, false, false);
                    using (var sink = new SegmentedWave(Path.Combine(root, "timeline"), f, 1048576))
                    {
                        var timeline = new AudioTimeline(sink, f);
                        timeline.Resume(10000000);
                        timeline.FillThrough(20000000);
                        timeline.Resume(50000000); // Three seconds paused.
                        byte[] tone = Tone(f, 0.1);
                        timeline.Packet(tone, 4800, 50000000, 0);
                        timeline.FillThrough(60000000);
                        sink.Complete();
                        Assert(sink.Frames == 96000 && timeline.HeardSignal, "Silence/pause time is wrong.");
                    }
                });
                Case("no synthetic silence is inserted ahead of late packets", delegate
                {
                    var f = Format(16, false, false);
                    using (var sink = new SegmentedWave(Path.Combine(root, "late-packets"), f, 1048576))
                    {
                        var timeline = new AudioTimeline(sink, f);
                        timeline.Resume(10000000);
                        byte[] tone = Tone(f, 0.1);
                        // Simulate a delayed packet: its own device time, not UI clock, defines placement.
                        timeline.Packet(tone, 4800, 10000000, 0);
                        timeline.Packet(tone, 4800, 11000000, 0);
                        sink.Complete();
                        Assert(sink.Frames == 9600, "Late packets were overwritten by synthetic silence.");
                    }
                });
                Case("timestamp jitter preserves continuous PCM byte for byte", delegate
                {
                    var f = Format(32, true, true);
                    byte[] original = Tone(f, 1);
                    using (var sink = new SegmentedWave(Path.Combine(root, "jitter"), f, 1048576))
                    {
                        var timeline = new AudioTimeline(sink, f);
                        timeline.Resume(10000000);
                        for (int i = 0; i < 100; i++)
                        {
                            byte[] packet = new byte[480 * f.BlockAlign];
                            Buffer.BlockCopy(original, i * packet.Length, packet, 0, packet.Length);
                            long jitter = i == 0 ? 0 : (i % 2 == 0 ? 209 : -209);
                            timeline.Packet(packet, 480, (ulong)(10000000 + i * 100000 + jitter), 0);
                        }
                        sink.Complete();
                        Assert(sink.Frames == 48000, "Jitter inserted or removed frames.");
                        byte[] actual = File.ReadAllBytes(sink.Completed[0]);
                        int p = 12;
                        while (Encoding.ASCII.GetString(actual, p, 4) != "data")
                        { int n = BitConverter.ToInt32(actual, p + 4); p += 8 + n + (n & 1); }
                        for (int i = 0; i < original.Length; i++)
                            Assert(actual[p + 8 + i] == original[i], "PCM changed at byte " + i);
                    }
                });
                Case("first-packet discontinuity differs from a real later gap", delegate
                {
                    var f = Format(16, false, false);
                    using (var sink = new SegmentedWave(Path.Combine(root, "discontinuity"), f, 1048576))
                    {
                        var timeline = new AudioTimeline(sink, f);
                        timeline.Resume(10000000);
                        byte[] data = Tone(f, 0.1);
                        timeline.Packet(data, 4800, 10000000, 1);
                        Assert(!timeline.Discontinuity, "First packet flagged as dropped audio.");
                        timeline.Packet(data, 4800, 11000000, 1);
                        Assert(timeline.Discontinuity, "Later real gap was ignored.");
                    }
                });
                Case("abrupt process termination and recovery copy", delegate
                {
                    string path = Path.Combine(root, "crashed.partial.wav");
                    var result = MediaExport.Run(System.Reflection.Assembly.GetExecutingAssembly().Location,
                        "--crash " + MediaExport.Quote(path), 10000);
                    Assert(result.ExitCode == 42, "Crash fixture did not run.");
                    string before = Hash(path);
                    string recovered = DurableWave.RecoverCopy(path);
                    Assert(Hash(path) == before, "Recovery modified original.");
                    Assert(DurableWave.ReadInfo(recovered, false).Duration >= 2, "Recovery lost committed audio.");
                });
                Case("locked active recording cannot be recovered", delegate
                {
                    string path = Path.Combine(root, "active.partial.wav");
                    using (var wave = new DurableWave(path, Format(16, false, false)))
                    {
                        byte[] data = Tone(wave.Format, 1);
                        wave.Write(data, 0, data.Length); wave.Checkpoint();
                        Throws(delegate { DurableWave.RecoverCopy(path); });
                    }
                });
                foreach (var format in new[] { Format(16, false, false), Format(32, false, true), Format(32, true, true) })
                {
                    var f = format;
                    Case("WAV to MKV full decode bits=" + f.Bits + " float=" + f.Float, delegate
                    {
                        string path = WriteWave("codec-" + f.Bits + "-" + f.Float, f, Tone(f, 1));
                        string hash = Hash(path);
                        string mkv = MediaExport.Convert(path, ffmpeg);
                        Assert(File.Exists(mkv) && Hash(path) == hash, "Source was changed by conversion.");
                        Throws(delegate { MediaExport.Convert(path, ffmpeg); });
                        Assert(Hash(path) == hash, "Source changed on repeated conversion.");
                    });
                }
                Case("silent MKV accepted by duration rather than arbitrary size", delegate
                {
                    var f = Format(16, false, false);
                    string path = WriteWave("silence", f, new byte[f.SampleRate * f.BlockAlign]);
                    MediaExport.Convert(path, ffmpeg);
                });
                Case("missing FFmpeg preserves WAV", delegate
                {
                    var f = Format(16, false, false);
                    string path = WriteWave("missing-codec", f, Tone(f, 1));
                    string before = Hash(path);
                    Throws(delegate { MediaExport.Convert(path, Path.Combine(root, "absent.exe")); });
                    Assert(Hash(path) == before, "Missing encoder affected WAV.");
                });
                Case("invalid media fails full decode", delegate
                {
                    string path = Path.Combine(root, "invalid.mkv");
                    File.WriteAllText(path, "not media");
                    Throws(delegate { MediaExport.DecodeDuration(path, ffmpeg, 10000); });
                });
                Case("hung child process is bounded and killed", delegate
                {
                    var watch = Stopwatch.StartNew();
                    Throws(delegate { MediaExport.Run(System.Reflection.Assembly.GetExecutingAssembly().Location, "--hang", 300); });
                    Assert(watch.Elapsed.TotalSeconds < 10, "Unbounded timeout.");
                });
                Case("parallel MKV grows before stop and validates each segment", delegate
                {
                    var f = Format(32, true, true);
                    using (var sink = new SegmentedWave(Path.Combine(root, "parallel"), f, 48000 * f.BlockAlign * 4, ffmpeg))
                    {
                        byte[] tone = Tone(f, 3);
                        // Exercise hundreds of normal 10 ms capture packets during startup.
                        for (int offset = 0; offset < tone.Length; offset += 480 * f.BlockAlign)
                            sink.Write(tone, offset, 480 * f.BlockAlign);
                        var watch = Stopwatch.StartNew();
                        string temp = sink.Encoders[0].TemporaryPath;
                        while ((!File.Exists(temp) || new FileInfo(temp).Length == 0) && watch.ElapsedMilliseconds < 15000)
                            Thread.Sleep(50);
                        Assert(File.Exists(temp) && new FileInfo(temp).Length > 0, "MKV did not grow during recording.");
                        sink.Write(tone, 0, tone.Length);
                        sink.Complete();
                        Assert(sink.Completed.Count == 2, "Segment split was not exercised.");
                        foreach (var encoder in sink.Encoders)
                        {
                            encoder.FinishAndVerify();
                            Assert(File.Exists(Path.ChangeExtension(encoder.Wav, ".mkv")), "MKV missing.");
                        }
                        string merged = MediaExport.ConcatParts(sink.Completed, ffmpeg);
                        Assert(File.Exists(merged), "Combined MKV missing.");
                        Assert(Math.Abs(MediaExport.DecodeDuration(merged, ffmpeg, 60000) - 6.0) < 0.1,
                            "Combined MKV lost audio or duration.");
                        string archived = MediaExport.ArchiveParts(sink.Completed, root, true);
                        Assert(Directory.GetFiles(Path.GetDirectoryName(merged), "*.mkv").Length == 1 &&
                            Directory.GetFiles(Path.GetDirectoryName(merged), "*.wav").Length == 0 &&
                            Directory.GetFiles(archived, "*.wav").Length == 2 &&
                            Directory.GetFiles(archived, "*.mkv").Length == 2,
                            "Combined recording is not one visible file with recoverable originals.");
                    }
                });
                Case("parallel encoder failure retains complete WAV", delegate
                {
                    var f = Format(16, false, false);
                    using (var sink = new SegmentedWave(Path.Combine(root, "parallel-failure"), f, 1048576, Path.Combine(root, "missing.exe")))
                    {
                        byte[] tone = Tone(f, 0.1);
                        sink.Write(tone, 0, tone.Length); sink.Complete();
                        string before = Hash(sink.Completed[0]);
                        Throws(delegate { sink.Encoders[0].FinishAndVerify(); });
                        Assert(Hash(sink.Completed[0]) == before, "Encoder failure damaged WAV.");
                    }
                });
                Case("parallel publication collision retains WAV and existing MKV", delegate
                {
                    var f = Format(16, false, false);
                    using (var sink = new SegmentedWave(Path.Combine(root, "parallel-collision"), f, 1048576, ffmpeg))
                    {
                        byte[] tone = Tone(f, 0.1);
                        sink.Write(tone, 0, tone.Length); sink.Complete();
                        string target = Path.ChangeExtension(sink.Completed[0], ".mkv");
                        File.WriteAllText(target, "existing");
                        Throws(delegate { sink.Encoders[0].FinishAndVerify(); });
                        Assert(File.Exists(sink.Completed[0]) && File.ReadAllText(target) == "existing", "Collision lost data.");
                    }
                });
                Case("session pause acknowledgement and repeated stop are safe", delegate
                {
                    using (var ready = new ManualResetEvent(false))
                    using (var paused = new ManualResetEvent(false))
                    using (var done = new ManualResetEvent(false))
                    {
                        int terminal = 0;
                        SessionUpdate last = null;
                        FakeAudio fake = null;
                        var session = new RecordingSession(Path.Combine(root, "session-stop"), ffmpeg,
                            new AppLog(Path.Combine(root, "logs")), delegate(SessionUpdate u)
                            {
                                if (u.State == RecordState.Recording) ready.Set();
                                if (u.State == RecordState.Paused) paused.Set();
                                if (u.State == RecordState.Stopped || u.State == RecordState.Faulted)
                                { last = u; Interlocked.Increment(ref terminal); done.Set(); }
                            }, delegate { fake = new FakeAudio(false); return fake; });
                        session.Start();
                        Assert(ready.WaitOne(5000), "Session did not start.");
                        Thread.Sleep(100);
                        session.Pause(true);
                        Assert(paused.WaitOne(5000), "Pause was not acknowledged.");
                        ready.Reset(); session.Pause(false);
                        Assert(ready.WaitOne(5000), "Resume was not acknowledged.");
                        Thread.Sleep(100);
                        for (int i = 0; i < 10; i++) session.Stop();
                        Assert(done.WaitOne(30000), "Stop did not complete.");
                        Assert(last.State == RecordState.Stopped && terminal == 1, "Repeated stop duplicated/failed finalization.");
                        Assert(fake.Disposed && !fake.WrongThread, "Audio objects crossed threads or leaked.");
                        string saved = Path.Combine(root, "session-stop");
                        Assert(Directory.GetFiles(saved, "*.wav").Length == 0, "Verified session retained WAV.");
                        Assert(Directory.GetFiles(saved, "*.mkv").Length == 1, "Verified MKV missing.");
                    }
                });
                Case("device loss saves audio and reports failure once", delegate
                {
                    using (var done = new ManualResetEvent(false))
                    {
                        SessionUpdate last = null;
                        string folder = Path.Combine(root, "device-loss");
                        var session = new RecordingSession(folder, ffmpeg, new AppLog(Path.Combine(root, "logs")),
                            delegate(SessionUpdate u)
                            { if (u.State == RecordState.Faulted || u.State == RecordState.Stopped) { last = u; done.Set(); } },
                            delegate { return new FakeAudio(true); });
                        session.Start();
                        Assert(done.WaitOne(30000), "Device failure was not handled.");
                        Assert(last.State == RecordState.Faulted && last.Message.Contains("simulated device loss"),
                            "Device loss was hidden.");
                        Assert(Directory.GetFiles(folder, "*.mkv").Length == 1, "Captured MKV was not published.");
                        string archiveRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                            "Backups", "RecordingParts");
                        Assert(Directory.GetFiles(archiveRoot, "*.wav", SearchOption.AllDirectories).Length > 0,
                            "Captured WAV was not retained in backup.");
                    }
                });
                Case("unavailable output reports failure and releases device", delegate
                {
                    using (var done = new ManualResetEvent(false))
                    {
                        string path = Path.Combine(root, "not-a-directory");
                        File.WriteAllText(path, "existing file");
                        string before = Hash(path);
                        FakeAudio fake = null;
                        SessionUpdate last = null;
                        var session = new RecordingSession(path, ffmpeg, new AppLog(Path.Combine(root, "logs")),
                            delegate(SessionUpdate u)
                            { if (u.State == RecordState.Faulted || u.State == RecordState.Stopped) { last = u; done.Set(); } },
                            delegate { fake = new FakeAudio(false); return fake; });
                        session.Start();
                        Assert(done.WaitOne(10000) && last.State == RecordState.Faulted, "Output error was not reported.");
                        Assert(Hash(path) == before && fake.Disposed && !fake.WrongThread, "Output failure damaged files or leaked audio.");
                    }
                });
                Case("recovery finds finalized and partial WAVs without mixing sessions", delegate
                {
                    string folder = Path.Combine(root, "pending-find"); Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345-001.wav"), "test");
                    File.WriteAllText(Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345-002.partial.wav"), "test");
                    File.WriteAllText(Path.Combine(folder, "2026-10-01 08-53-23-abcdef012346-001.wav"), "test");
                    File.WriteAllText(Path.Combine(folder, "personal.wav"), "test");
                    var sessions = SessionRecovery.Find(folder);
                    Assert(sessions.Count == 2 && sessions[0].Parts.Count == 2, "Session grouping lost or mixed parts.");
                    File.WriteAllText(Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345-001.partial.wav"), "duplicate");
                    Assert(SessionRecovery.Find(folder)[0].Duplicate, "Duplicate part was not flagged.");
                });
                Case("recovery joins seven parts and archives originals byte for byte", delegate
                {
                    string home = Path.Combine(root, "recovery-seven");
                    string folder = Path.Combine(home, "Recordings"); Directory.CreateDirectory(folder);
                    var original = new System.Collections.Generic.List<string>();
                    for (int i = 1; i <= 7; i++)
                    {
                        string path = Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345-" + i.ToString("D3") + ".partial.wav");
                        using (var wave = new DurableWave(path, Format(16, false, false)))
                        { byte[] bytes = Tone(wave.Format, 0.2); wave.Write(bytes, 0, bytes.Length); original.Add(Hash(wave.Complete())); }
                    }
                    File.WriteAllText(Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345-001.old.partial.mkv"), "failed encoder");
                    var phases = new System.Collections.Generic.List<string>();
                    string final = SessionRecovery.Recover(SessionRecovery.Find(folder)[0], home, ffmpeg, phases.Add);
                    Assert(File.Exists(final) && Directory.GetFiles(folder).Length == 1, "Recovery did not leave one final MKV.");
                    Assert(Math.Abs(MediaExport.DecodeDuration(final, ffmpeg, 30000) - 1.4) < 0.25, "Recovered duration is wrong.");
                    string archive = Directory.GetDirectories(Path.Combine(home, "Backups", "RecoveredSessions"))[0];
                    string[] wavs = Directory.GetFiles(archive, "*.wav"); Array.Sort(wavs, StringComparer.Ordinal);
                    Assert(wavs.Length == 7, "Originals were not retained.");
                    for (int i = 0; i < wavs.Length; i++) Assert(Hash(wavs[i]) == original[i], "Recovery changed original bytes.");
                    Assert(phases.Contains("Объединение частей") && phases.Contains("Проверка итогового файла"), "Save stages missing.");
                    Assert(SessionRecovery.Find(folder).Count == 0, "Verified session still pending.");
                });
                Case("recovery rejects a missing part before touching source files", delegate
                {
                    string folder = Path.Combine(root, "recovery-gap"); Directory.CreateDirectory(folder);
                    string a = Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345-001.wav");
                    string b = Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345-003.wav");
                    File.WriteAllText(a, "first"); File.WriteAllText(b, "third");
                    Throws(delegate { SessionRecovery.Recover(SessionRecovery.Find(folder)[0], root, ffmpeg, null); });
                    Assert(File.ReadAllText(a) == "first" && File.ReadAllText(b) == "third", "Gap damaged original files.");
                });
                Case("recovery never overwrites an existing final file", delegate
                {
                    string folder = Path.Combine(root, "recovery-collision"); Directory.CreateDirectory(folder);
                    string a = Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345-001.wav");
                    string final = Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345.mkv");
                    File.WriteAllText(a, "original"); File.WriteAllText(final, "existing"); string before = Hash(final);
                    Throws(delegate { SessionRecovery.Recover(SessionRecovery.Find(folder)[0], root, ffmpeg, null); });
                    Assert(Hash(final) == before && File.Exists(a), "Collision overwrote user data.");
                });
                Case("recovery rejects an active writer and retains its WAV", delegate
                {
                    string folder = Path.Combine(root, "recovery-locked"); Directory.CreateDirectory(folder);
                    string a = Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345-001.partial.wav");
                    using (var wave = new DurableWave(a, Format(16, false, false)))
                    {
                        byte[] bytes = Tone(wave.Format, 0.2); wave.Write(bytes, 0, bytes.Length); wave.Checkpoint();
                        Throws(delegate { SessionRecovery.Recover(SessionRecovery.Find(folder)[0], root, ffmpeg, null); });
                        Assert(File.Exists(a), "Active WAV removed.");
                    }
                });
                Case("recovery repairs a partial header using a copy", delegate
                {
                    string home = Path.Combine(root, "recovery-partial");
                    string folder = Path.Combine(home, "Recordings"); Directory.CreateDirectory(folder);
                    string a = Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345-001.partial.wav");
                    using (var wave = new DurableWave(a, Format(16, false, false)))
                    { byte[] bytes = Tone(wave.Format, 0.5); wave.Write(bytes, 0, bytes.Length); }
                    string before = Hash(a);
                    string final = SessionRecovery.Recover(SessionRecovery.Find(folder)[0], home, ffmpeg, null);
                    Assert(File.Exists(final), "Recovered partial MKV missing.");
                    string original = Directory.GetFiles(Path.Combine(home, "Backups", "RecoveredSessions"), "*.wav", SearchOption.AllDirectories)[0];
                    Assert(Hash(original) == before, "Partial original was modified.");
                });
                Case("recovery encoder failure leaves every original in place", delegate
                {
                    string folder = Path.Combine(root, "recovery-failed"); Directory.CreateDirectory(folder);
                    string a = Path.Combine(folder, "2026-10-01 07-53-23-abcdef012345-001.partial.wav");
                    string saved;
                    using (var wave = new DurableWave(a, Format(16, false, false)))
                    { byte[] bytes = Tone(wave.Format, 0.5); wave.Write(bytes, 0, bytes.Length); saved = wave.Complete(); }
                    string before = Hash(saved);
                    Throws(delegate { SessionRecovery.Recover(SessionRecovery.Find(folder)[0], root, "missing-encoder.exe", null); });
                    Assert(Hash(saved) == before && SessionRecovery.Find(folder).Count == 1, "Failed recovery hid/damaged the pending session.");
                });
                ReadinessTests.Run(Case, root, ffmpeg);
            ProfileTests.Run(Case, root, ffmpeg);
                string resultText = "PASS " + passed + " checks; artifacts: " + root;
                File.WriteAllText(Path.Combine(root, "result.txt"), resultText);
                Console.WriteLine(resultText);
                return 0;
            }
            catch (Exception error)
            {
                Console.WriteLine("FAIL " + error);
                File.WriteAllText(Path.Combine(root, "result.txt"), "FAIL " + error);
                return 1;
            }
        }
        private sealed class FakeAudio : IAudioSource
        {
            private readonly int owner = Thread.CurrentThread.ManagedThreadId;
            private readonly bool fail;
            private bool emitted;
            internal bool Disposed, WrongThread;
            internal FakeAudio(bool fail) { this.fail = fail; }
            public WaveFormat Format { get { return Tests.Format(16, false, false); } }
            public string DeviceId { get { return "test-device"; } }
            private void CheckThread()
            { WrongThread |= owner != Thread.CurrentThread.ManagedThreadId; }
            public void Start() { CheckThread(); }
            public void Stop() { CheckThread(); }
            public void Reset() { CheckThread(); }
            public void Drain(Action<byte[], int, ulong, uint> consume)
            {
                CheckThread();
                if (!emitted)
                {
                    emitted = true;
                    consume(Tone(Format, 0.1), 4800, (ulong)AudioCapture.Clock100ns, 0);
                }
            }
            public void CheckDevice() { CheckThread(); if (fail) throw new IOException("simulated device loss"); }
            public void Dispose() { CheckThread(); Disposed = true; }
        }
    }
}
