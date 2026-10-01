using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SoundLeaf
{
    internal static class ReadinessTests
    {
        private static void Assert(bool condition, string text) { if (!condition) throw new Exception(text); }
        internal static void Run(Action<string, Action> check, string root, string ffmpeg)
        {
            check("preflight rejects an unavailable folder without WAV fallback", delegate
            {
                string path = Path.Combine(root, "blocked-output"); File.WriteAllText(path, "original");
                var result = StartupChecks.Check(path, ffmpeg, RecordingMode.MkvWithWavBackup);
                Assert(!result.CanRecord && !result.CanRecordWavOnly && File.ReadAllText(path) == "original", "Folder failure was bypassed.");
            });
            check("preflight disk thresholds and unknown space are explicit", delegate
            {
                string path = Path.Combine(root, "disk-probe");
                var low = StartupChecks.Check(path, ffmpeg, RecordingMode.WavOnly, delegate { return StartupChecks.MinimumBytes - 1; });
                var edge = StartupChecks.Check(path, ffmpeg, RecordingMode.WavOnly, delegate { return StartupChecks.MinimumBytes; });
                var enough = StartupChecks.Check(path, ffmpeg, RecordingMode.WavOnly, delegate { return StartupChecks.WarningBytes; });
                var unknown = StartupChecks.Check(path, ffmpeg, RecordingMode.WavOnly, delegate { return null; });
                Assert(!low.CanRecord && !low.CanRecordWavOnly, "Low disk bypassed.");
                Assert(edge.CanRecord && edge.Warning != null && enough.CanRecord && enough.Warning == null, "Disk boundaries wrong.");
                Assert(unknown.CanRecord && unknown.Warning != null, "Unknown disk space silently accepted.");
                Assert(Directory.GetDirectories(path, ".preflight-*").Length == 0, "Probe directory leaked.");
            });
            check("missing FFmpeg offers but never automatically starts WAV", delegate
            {
                string path = Path.Combine(root, "missing-preflight");
                var result = StartupChecks.Check(path, "missing.exe", RecordingMode.MkvWithWavBackup);
                Assert(!result.CanRecord && result.CanRecordWavOnly, "WAV offer missing.");
                Assert(Directory.GetFiles(path, "*.wav", SearchOption.AllDirectories).Length == 0, "Automatic WAV capture started.");
            });
            check("invalid executable fails preflight safely", delegate
            {
                string executable = Path.Combine(root, "invalid-ffmpeg.exe"); File.WriteAllText(executable, "not executable");
                var result = StartupChecks.Check(Path.Combine(root, "invalid-preflight"), executable, RecordingMode.MkvWithWavBackup);
                Assert(!result.CanRecord && result.CanRecordWavOnly, "Invalid FFmpeg accepted.");
            });
            check("hung preflight child is killed within bounded time", delegate
            {
                string executable = Path.Combine(root, "FakeFfmpegHang.exe");
                File.Copy(Process.GetCurrentProcess().MainModule.FileName, executable);
                var clock = Stopwatch.StartNew();
                var result = StartupChecks.Check(Path.Combine(root, "hung-preflight"), executable, RecordingMode.MkvWithWavBackup);
                Assert(!result.CanRecord && result.CanRecordWavOnly && clock.ElapsedMilliseconds < 8000, "Preflight hang was unbounded.");
            });
            check("real AAC encode and decode preflight leaves user files alone", delegate
            {
                string folder = Path.Combine(root, "real-preflight"); Directory.CreateDirectory(folder);
                string sentinel = Path.Combine(folder, "personal.txt"); File.WriteAllText(sentinel, "keep");
                var result = StartupChecks.Check(folder, ffmpeg, RecordingMode.MkvWithWavBackup, delegate { return StartupChecks.WarningBytes; });
                Assert(result.CanRecord, "Real preflight failed: " + result.Error);
                Assert(File.ReadAllText(sentinel) == "keep" && Directory.GetDirectories(folder).Length == 0, "Probe cleanup touched user files.");
            });
            check("preflight decode failure does not declare MKV ready", delegate
            {
                int calls = 0;
                var result = StartupChecks.Check(Path.Combine(root, "decode-preflight"), ffmpeg, RecordingMode.MkvWithWavBackup,
                    delegate { return StartupChecks.WarningBytes; }, delegate(string exe, string arguments, int timeout)
                    { Assert(timeout == 3000, "Timeout changed."); if (++calls == 2) throw new IOException("simulated decoder failure"); });
                Assert(calls == 2 && !result.CanRecord && result.CanRecordWavOnly, "Decode failure was ignored.");
            });
            check("blocked startup never opens the capture device", delegate
            {
                using (var done = new ManualResetEvent(false))
                {
                    bool opened = false; SessionUpdate last = null;
                    string home = Path.Combine(root, "blocked-session");
                    var session = new RecordingSession(Path.Combine(home, "Recordings"), ffmpeg, new AppLog(Path.Combine(home, "logs")),
                        delegate(SessionUpdate update) { if (update.State == RecordState.Faulted) { last = update; done.Set(); } },
                        delegate { opened = true; throw new Exception("must not open"); }, RecordingMode.MkvWithWavBackup, home, 8192,
                        delegate { return new PreflightResult { Error = "encoder unavailable", CanRecordWavOnly = true }; });
                    session.Start(); Assert(done.WaitOne(5000), "Blocked startup did not finish.");
                    Assert(!opened && last.CanRecordWavOnly && last.ResultKind == FinalResultKind.None, "Preflight incorrectly recorded audio.");
                }
            });
            foreach (long limit in new[] { 1024L * 1024, 8192L })
            {
                long bytes = limit;
                check("WAV-only session preserves verified parts and restart marker limit=" + limit, delegate
                {
                    string home = Path.Combine(root, "wav-mode-" + bytes);
                    var last = RunWav(home, bytes);
                    Assert(last.State == RecordState.Stopped && last.ResultKind == FinalResultKind.WavParts &&
                        last.Mode == RecordingMode.WavOnly && last.Message.Contains("MKV не создавался"), "WAV result mislabelled.");
                    Assert(last.FinalPaths.Length == (bytes == 8192 ? 5 : 1), "WAV segmentation incorrect.");
                    foreach (string path in last.FinalPaths) Assert(DurableWave.ReadInfo(path, false).Duration > 0, "Invalid completed WAV.");
                    Assert(Directory.GetFiles(home, "*.mkv", SearchOption.AllDirectories).Length == 0, "WAV mode invoked encoder.");
                    Assert(SessionRecovery.Find(Path.Combine(home, "Recordings"), home).Count == 0, "Restart treats deliberate WAV as a crash.");
                    Assert(SessionRecovery.Find(Path.Combine(home, "Recordings"), home).Count == 0, "Completion marker not repeatable.");
                });
            }
            check("failed WAV completion marker retains audio and reports failure", delegate
            {
                string home = Path.Combine(root, "receipt-failure"); Directory.CreateDirectory(home);
                File.WriteAllText(Path.Combine(home, "State"), "blocking file");
                var last = RunWav(home, 1024 * 1024);
                Assert(last.State == RecordState.Faulted, "Receipt failure claimed success.");
                Assert(Directory.GetFiles(Path.Combine(home, "Recordings"), "*.wav").Length == 1 &&
                    SessionRecovery.Find(Path.Combine(home, "Recordings"), home).Count == 1, "Receipt failure lost/hid audio.");
            });
            check("process crash after WAV completion stays recoverable without marker", delegate
            {
                string home = Path.Combine(root, "receipt-crash"), folder = Path.Combine(home, "Recordings"); Directory.CreateDirectory(folder);
                string partial = Path.Combine(folder, "2026-10-01 10-00-00-abcdef012345-001.partial.wav");
                using (var process = Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,
                    "--crash-completed-wav " + MediaExport.Quote(partial)) { UseShellExecute = false, CreateNoWindow = true }))
                { Assert(process.WaitForExit(5000) && process.ExitCode == 42, "Crash fixture failed."); }
                Assert(SessionRecovery.Find(folder, home).Count == 1, "Unmarked completed WAV was hidden.");
            });
            check("malformed or changed WAV receipts never hide pending data", delegate
            {
                string home = Path.Combine(root, "receipt-invalid"); var last = RunWav(home, 1024 * 1024);
                string receipt = Directory.GetFiles(Path.Combine(home, "State", "Sessions"), "*.json")[0];
                File.WriteAllText(receipt, "invalid JSON");
                Assert(SessionRecovery.Find(Path.Combine(home, "Recordings"), home).Count == 1, "Malformed receipt trusted.");
                File.Delete(receipt); WavReceipt.Write(home, last.FinalPaths);
                File.SetLastWriteTimeUtc(last.FinalPaths[0], DateTime.UtcNow.AddDays(1));
                Assert(SessionRecovery.Find(Path.Combine(home, "Recordings"), home).Count == 1, "Modified WAV trusted.");
            });
            check("completion receipt collision cannot overwrite existing metadata", delegate
            {
                string home = Path.Combine(root, "receipt-collision"); var last = RunWav(home, 1024 * 1024);
                string receipt = Directory.GetFiles(Path.Combine(home, "State", "Sessions"), "*.json")[0];
                string before = File.ReadAllText(receipt); bool failed = false;
                try { WavReceipt.Write(home, last.FinalPaths); } catch (IOException) { failed = true; }
                Assert(failed && File.ReadAllText(receipt) == before && File.Exists(last.FinalPaths[0]), "Receipt overwritten or WAV deleted.");
            });
            check("incomplete and escaping receipt paths cannot suppress recovery", delegate
            {
                string home = Path.Combine(root, "receipt-untrusted"); RunWav(home, 1024 * 1024);
                string receipt = Directory.GetFiles(Path.Combine(home, "State", "Sessions"), "*.json")[0];
                string valid = File.ReadAllText(receipt);
                File.WriteAllText(receipt, valid.Replace("\"Completed\":true", "\"Completed\":false"));
                Assert(SessionRecovery.Find(Path.Combine(home, "Recordings"), home).Count == 1, "Incomplete marker trusted.");
                File.WriteAllText(receipt, valid.Replace("Recordings", ".."));
                Assert(SessionRecovery.Find(Path.Combine(home, "Recordings"), home).Count == 1, "Escaping marker path trusted.");
            });
        }
        internal static void WriteCompletedWav(string path)
        {
            using (var wave = new DurableWave(path, Tests.Format(16, false, false)))
            { var audio = new byte[38400]; wave.Write(audio, 0, audio.Length); wave.Complete(); }
        }
        private static SessionUpdate RunWav(string home, long limit)
        {
            using (var wrote = new ManualResetEvent(false))
            using (var done = new ManualResetEvent(false))
            {
                SessionUpdate last = null;
                var session = new RecordingSession(Path.Combine(home, "Recordings"), "missing-encoder.exe", new AppLog(Path.Combine(home, "logs")),
                    delegate(SessionUpdate update)
                    { if (update.State == RecordState.Stopped || update.State == RecordState.Faulted) { last = update; done.Set(); } },
                    delegate { return new SilentSource(wrote); }, RecordingMode.WavOnly, home, limit,
                    delegate { return StartupChecks.Check(Path.Combine(home, "Recordings"), "missing-encoder.exe", RecordingMode.WavOnly); });
                session.Start(); Assert(wrote.WaitOne(5000), "WAV recording did not start."); session.Stop();
                Assert(done.WaitOne(5000), "WAV recording did not finish."); return last;
            }
        }
        private sealed class SilentSource : IAudioSource
        {
            private readonly ManualResetEvent wrote;
            private bool sent;
            internal SilentSource(ManualResetEvent wrote) { this.wrote = wrote; }
            public WaveFormat Format { get { return Tests.Format(16, false, false); } }
            public string DeviceId { get { return "synthetic WAV-only source"; } }
            public void Start() { } public void Stop() { } public void Reset() { } public void CheckDevice() { } public void Dispose() { }
            public void Drain(Action<byte[], int, ulong, uint> consume)
            {
                if (sent) return; sent = true;
                consume(new byte[38400], 9600, (ulong)AudioCapture.Clock100ns, 0); wrote.Set();
            }
        }
    }
}
