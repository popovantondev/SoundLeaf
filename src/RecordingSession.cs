using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace SoundLeaf
{
    internal enum RecordState { Starting, Recording, Paused, Saving, Stopped, Faulted }
    internal enum RecordingMode { MkvWithWavBackup, WavOnly }
    internal enum FinalResultKind { None, Mkv, WavParts, EncodedFile }
    internal sealed class SessionUpdate
    {
        internal RecordState State;
        internal string Message;
        internal double Seconds;
        internal bool HeardSignal;
        internal float Peak, Rms;
        internal bool DeviceAvailable, Telemetry;
        internal string SessionId;
        internal DateTime SessionStarted;
        internal long CapturedBytes;
        internal string FinalPath;
        internal string[] FinalPaths = new string[0];
        internal FinalResultKind ResultKind;
        internal RecordingMode Mode;
        internal bool CanRecordWavOnly;
        internal string Warning;
        internal RecordingProfile Profile = RecordingProfile.Default;
        internal SessionUpdate(RecordState state, string message, double seconds, bool signal)
        { State = state; Message = message; Seconds = seconds; HeardSignal = signal; }
    }
    internal sealed class AppLog
    {
        private readonly string path;
        private readonly object gate = new object();
        internal AppLog(string root)
        {
            Directory.CreateDirectory(root);
            path = Path.Combine(root, "player-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
        }
        internal void Write(string message)
        {
            lock (gate) File.AppendAllText(path, DateTime.Now.ToString("O") + " " + message + Environment.NewLine, Encoding.UTF8);
        }
        internal void TryWrite(string message) { try { Write(message); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    internal sealed class RecordingSession
    {
        internal const long TelemetryInterval100ns = 500000;
        private readonly string folder, ffmpeg;
        private readonly AppLog log;
        private readonly Action<SessionUpdate> update;
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private int stop, pause;
        private readonly Thread worker;
        private readonly Func<IAudioSource> sourceFactory;
        internal readonly RecordingMode Mode;
        internal readonly RecordingProfile Profile;
        private readonly bool persistProfile;
        private readonly string playerRoot;
        private readonly long segmentBytes;
        private readonly Func<PreflightResult> preflight;
        private string preflightWarning;
        private string[] finalPaths = new string[0];
        private FinalResultKind resultKind;
        private string sessionId;
        private DateTime sessionStarted;
        private long capturedBytes;
        private double capturedSeconds;
        private bool deviceAvailable;
        internal RecordingSession(string folder, string ffmpeg, AppLog log, Action<SessionUpdate> update)
            : this(folder, ffmpeg, log, update, RecordingMode.MkvWithWavBackup) { }
        internal RecordingSession(string folder, string ffmpeg, AppLog log, Action<SessionUpdate> update, RecordingMode mode)
            : this(folder, ffmpeg, log, update, mode, RecordingProfile.Default) { }
        internal RecordingSession(string folder, string ffmpeg, AppLog log, Action<SessionUpdate> update, RecordingMode mode, RecordingProfile profile, string storageRoot = null)
            : this(folder, ffmpeg, log, update, delegate { return new AudioCapture(); }, mode,
                storageRoot ?? AppDomain.CurrentDomain.BaseDirectory, 256L * 1024 * 1024,
                delegate { return StartupChecks.Check(folder, ffmpeg, mode, null, null, profile); }, profile) { }
        internal RecordingSession(string folder, string ffmpeg, AppLog log, Action<SessionUpdate> update,
            Func<IAudioSource> sourceFactory)
            : this(folder, ffmpeg, log, update, sourceFactory, RecordingMode.MkvWithWavBackup,
                AppDomain.CurrentDomain.BaseDirectory, 256L * 1024 * 1024, null) { }
        internal RecordingSession(string folder, string ffmpeg, AppLog log, Action<SessionUpdate> update,
            Func<IAudioSource> sourceFactory, RecordingMode mode, string playerRoot, long segmentBytes,
            Func<PreflightResult> preflight, RecordingProfile profile = null)
        {
            this.folder = folder; this.ffmpeg = ffmpeg; this.log = log; this.update = update;
            this.sourceFactory = sourceFactory;
            Mode = mode; this.playerRoot = playerRoot; this.segmentBytes = segmentBytes; this.preflight = preflight;
            Profile = profile ?? RecordingProfile.Default; persistProfile = profile != null;
            worker = new Thread(Run);
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Name = "Player audio and storage";
            worker.IsBackground = false;
        }
        internal bool IsAlive { get { return worker.IsAlive; } }
        internal void Start() { worker.Start(); }
        internal void Pause(bool value) { Interlocked.Exchange(ref pause, value ? 1 : 0); Signal(); }
        internal void Stop() { Interlocked.Exchange(ref stop, 1); Signal(); }
        private void Signal() { try { wake.Set(); } catch (ObjectDisposedException) { } }
        private void Publish(RecordState state, string text, double duration, bool signal, string finalPath = null, bool canWav = false)
        {
            log.TryWrite(state + " " + text);
            update(new SessionUpdate(state, text, duration, signal) { FinalPath = finalPath, FinalPaths = finalPaths,
                ResultKind = resultKind, Mode = Mode, CanRecordWavOnly = canWav, Warning = preflightWarning, Profile = Profile,
                SessionId = sessionId, SessionStarted = sessionStarted, CapturedBytes = capturedBytes, DeviceAvailable = deviceAvailable });
        }
        private void Run()
        {
            SegmentedWave sink = null;
            LiveMkv continuous = null;
            AudioTimeline timeline = null;
            WaveFormat format = null;
            string failure = null;
            double seconds = 0;
            string finalPath = null;
            if (preflight != null)
            {
                Publish(RecordState.Starting, TextCatalog.T("message.4"), 0, false);
                PreflightResult readiness;
                try { readiness = preflight(); }
                catch (Exception error) { readiness = new PreflightResult { Error = error.Message }; }
                preflightWarning = readiness.Warning;
                if (Interlocked.CompareExchange(ref stop, 0, 0) != 0)
                { wake.Dispose(); Publish(RecordState.Stopped, TextCatalog.T("message.50"), 0, false); return; }
                if (!readiness.CanRecord)
                { wake.Dispose(); Publish(RecordState.Faulted, readiness.Error, 0, false, null, readiness.CanRecordWavOnly); return; }
            }
            try
            {
                sessionStarted = DateTime.Now;
                sessionId = sessionStarted.ToString("yyyy-MM-dd HH-mm-ss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 12);
                if (persistProfile) SessionProfile.Write(playerRoot, sessionId, Profile);
                using (var audio = sourceFactory())
                {
                    format = audio.Format;
                    log.Write("Device " + audio.DeviceId + "; " + format.SampleRate + " Hz; " +
                        format.Channels + " channels; " + format.Bits + " bits; float=" + format.Float);
                    sink = new SegmentedWave(folder, format, segmentBytes,
                        Mode == RecordingMode.WavOnly || Profile.Format != AudioOutputFormat.Mkv ? null : ffmpeg, Profile, sessionId);
                    if (Mode != RecordingMode.WavOnly && Profile.Format != AudioOutputFormat.Mkv)
                    { continuous = new LiveMkv(sink.CurrentPath, format, ffmpeg, Profile, true); sink.PcmWritten = continuous.Enqueue; }
                    timeline = new AudioTimeline(sink, format);
                    timeline.Resume(AudioCapture.Clock100ns);
                    audio.Start();
                    deviceAvailable = true;
                    Publish(RecordState.Recording, TextCatalog.T("message.51"), 0, false);
                    bool isPaused = false;
                    long lastCheck = AudioCapture.Clock100ns;
                    long lastMeter = 0;
                    while (Interlocked.CompareExchange(ref stop, 0, 0) == 0)
                    {
                        bool requestedPause = Interlocked.CompareExchange(ref pause, 0, 0) != 0;
                        if (requestedPause != isPaused)
                        {
                            if (requestedPause)
                            {
                                audio.Stop();
                                audio.Drain(timeline.Packet);
                                sink.Checkpoint();
                                timeline.Meter.Reset();
                            }
                            else
                            {
                                audio.Reset();
                                timeline.Resume(AudioCapture.Clock100ns);
                                audio.Start();
                            }
                            isPaused = requestedPause;
                            Publish(isPaused ? RecordState.Paused : RecordState.Recording,
                                isPaused ? TextCatalog.T("message.5") : TextCatalog.T("message.51"),
                                sink.Frames / (double)format.SampleRate, timeline.HeardSignal);
                        }
                        if (!isPaused)
                        {
                            audio.Drain(timeline.Packet);
                        }
                        long now = AudioCapture.Clock100ns;
                        if (now - lastCheck >= 10000000)
                        {
                            audio.CheckDevice();
                            sink.Checkpoint();
                            lastCheck = now;
                        }
                        capturedBytes = sink.Frames * format.BlockAlign;
                        capturedSeconds = sink.Frames / (double)format.SampleRate;
                        if (now - lastMeter >= TelemetryInterval100ns)
                        {
                            update(new SessionUpdate(isPaused ? RecordState.Paused : RecordState.Recording,
                                isPaused ? TextCatalog.T("message.5") : (timeline.HeardSignal ? TextCatalog.T("message.52") : TextCatalog.T("message.53")),
                                capturedSeconds, timeline.HeardSignal) { Mode = Mode, Warning = preflightWarning, Profile = Profile,
                                SessionId = sessionId, SessionStarted = sessionStarted, CapturedBytes = capturedBytes,
                                DeviceAvailable = deviceAvailable, Telemetry = true,
                                Peak = isPaused ? 0 : timeline.Meter.Peak(now), Rms = isPaused ? 0 : timeline.Meter.Rms(now) });
                            lastMeter = now;
                        }
                        wake.WaitOne(10);
                    }
                    if (!isPaused)
                    {
                        audio.Stop();
                        audio.Drain(timeline.Packet);
                    }
                    capturedBytes = sink.Frames * format.BlockAlign;
                    capturedSeconds = sink.Frames / (double)format.SampleRate;
                }
            }
            catch (Exception error)
            {
                deviceAvailable = false;
                failure = error.Message;
                log.TryWrite(error.ToString());
            }
            try
            {
                deviceAvailable = false;
                Publish(RecordState.Saving, TextCatalog.T("message.54"), capturedSeconds, timeline != null && timeline.HeardSignal);
                if (sink != null)
                {
                    seconds = sink.Frames / (double)format.SampleRate;
                    sink.Complete();
                    if (sink.Completed.Count == 0) throw new IOException(TextCatalog.T("message.55"));
                    if (Mode == RecordingMode.WavOnly)
                    {
                        Publish(RecordState.Saving, TextCatalog.T("message.56"), seconds, timeline.HeardSignal);
                        foreach (string wav in sink.Completed) DurableWave.ReadInfo(wav, false);
                        if (failure == null) WavReceipt.Write(playerRoot, sink.Completed);
                        finalPaths = sink.Completed.ToArray();
                        finalPath = finalPaths[0];
                        resultKind = FinalResultKind.WavParts;
                        if (persistProfile && failure == null) ResultReceipt.Write(playerRoot, sink.SessionId, Profile, seconds, true, finalPaths);
                    }
                    else if (continuous != null)
                    {
                        finalPath = continuous.FinishSession(sink.Completed,
                            delegate(string phase) { Publish(RecordState.Saving, phase, seconds, timeline.HeardSignal); });
                        finalPaths = new[] { finalPath }; resultKind = FinalResultKind.EncodedFile;
                        if (persistProfile && failure == null) ResultReceipt.Write(playerRoot, sink.SessionId, Profile, seconds, false, finalPaths);
                        if (failure == null && !timeline.Discontinuity)
                            foreach (string wav in sink.Completed) File.Delete(wav);
                        else MediaExport.ArchiveParts(sink.Completed, playerRoot, false);
                    }
                    else
                    {
                        foreach (var encoder in sink.Encoders)
                        {
                            string wav = encoder.Wav;
                            log.TryWrite("WAV saved: " + wav);
                            encoder.FinishAndVerify(delegate(string phase) { Publish(RecordState.Saving, phase, seconds, timeline.HeardSignal); });
                            log.TryWrite("MKV verified: " + Path.ChangeExtension(wav, ".mkv"));
                        }
                        if (sink.Completed.Count > 1)
                        {
                            finalPath = MediaExport.ConcatParts(sink.Completed, ffmpeg,
                                delegate(string phase) { Publish(RecordState.Saving, phase, seconds, timeline.HeardSignal); });
                            log.TryWrite("Combined MKV verified: " + finalPath);
                        }
                        else finalPath = Path.ChangeExtension(sink.Completed[0], ".mkv");
                        finalPaths = new[] { finalPath }; resultKind = FinalResultKind.Mkv;
                        if (persistProfile && failure == null) ResultReceipt.Write(playerRoot, sink.SessionId, Profile, seconds, false, finalPaths);
                        if (failure == null && !timeline.Discontinuity)
                        {
                            foreach (string wav in sink.Completed)
                            {
                                File.Delete(wav);
                                log.TryWrite("Verified backup WAV removed: " + wav);
                            }
                            if (sink.Completed.Count > 1)
                                foreach (string wav in sink.Completed)
                                {
                                    string part = Path.ChangeExtension(wav, ".mkv");
                                    File.Delete(part);
                                    log.TryWrite("Verified MKV part removed: " + part);
                                }
                        }
                        else
                        {
                            MediaExport.ArchiveParts(sink.Completed, playerRoot, sink.Completed.Count > 1);
                            log.TryWrite("Backup parts archived after audio discontinuity or capture fault.");
                        }
                    }
                }
                else if (failure == null) failure = TextCatalog.T("message.57");
            }
            catch (Exception error)
            {
                failure = (failure == null ? "" : failure + Environment.NewLine) + error.Message;
                log.TryWrite(error.ToString());
            }
            finally
            {
                if (continuous != null) continuous.Dispose();
                if (sink != null) sink.Dispose();
                wake.Dispose();
            }
            bool signal = timeline != null && timeline.HeardSignal;
            string warning = null;
            if (timeline != null && timeline.Discontinuity)
                warning = TextCatalog.T("message.58");
            Publish(failure == null ? RecordState.Stopped : RecordState.Faulted,
                failure ?? (Mode == RecordingMode.WavOnly ? TextCatalog.T("message.59") + finalPaths.Length +
                    (warning == null ? "" : Environment.NewLine + warning) :
                    warning ?? (signal ? Profile.Label + TextCatalog.T("message.60") : TextCatalog.T("message.61"))),
                seconds, signal, finalPath);
        }
    }

    // Bounded PCM queue isolates capture from a slow or failed encoder.
    internal sealed class LiveMkv : IDisposable
    {
        private readonly BlockingCollection<byte[]> queue = new BlockingCollection<byte[]>(4096);
        private int queuedBytes;
        private readonly Thread worker;
        private readonly string ffmpeg, partial;
        private readonly WaveFormat format;
        private readonly RecordingProfile profile;
        private readonly string final;
        private volatile Exception failure;
        private Process process;
        private readonly object gate = new object();
        private bool cancelled;
        internal readonly string Wav;
        internal string TemporaryPath { get { return partial; } }
        internal bool HasFailed { get { return failure != null; } }
        internal int QueuedBytes { get { return Interlocked.CompareExchange(ref queuedBytes, 0, 0); } }
        internal void AbortForVerification() { failure = new IOException("Simulated live encoder failure."); CompleteInput(); Cancel(); }
        internal LiveMkv(string partialWav, WaveFormat format, string ffmpeg, RecordingProfile profile = null, bool wholeSession = false)
        {
            this.format = format; this.ffmpeg = ffmpeg;
            this.profile = profile ?? RecordingProfile.Default;
            Wav = partialWav.Substring(0, partialWav.Length - ".partial.wav".Length) + ".wav";
            string baseName = Path.GetFileNameWithoutExtension(Wav);
            if (wholeSession) baseName = baseName.Substring(0, baseName.LastIndexOf('-'));
            final = Path.Combine(Path.GetDirectoryName(Wav), baseName + this.profile.Extension);
            partial = Path.Combine(Path.GetDirectoryName(Wav), baseName + "." + Guid.NewGuid().ToString("N") + ".partial" + this.profile.Extension);
            worker = new Thread(Encode) { IsBackground = true, Name = "Player MKV encoder" };
            worker.Start();
        }
        internal void Enqueue(byte[] data, int offset, int count)
        {
            if (failure != null) return;
            int limit = 65536 / format.BlockAlign * format.BlockAlign;
            while (count > 0)
            {
                int n = Math.Min(count, limit);
                if (Interlocked.Add(ref queuedBytes, n) > 8 * 1024 * 1024)
                {
                    Interlocked.Add(ref queuedBytes, -n);
                    failure = new IOException(TextCatalog.T("message.62"));
                    return;
                }
                var copy = new byte[n];
                Buffer.BlockCopy(data, offset, copy, 0, n);
                if (!queue.TryAdd(copy))
                {
                    Interlocked.Add(ref queuedBytes, -n);
                    failure = new IOException(TextCatalog.T("message.62"));
                    return;
                }
                count -= n; offset += n;
            }
        }
        internal void CompleteInput() { if (!queue.IsAddingCompleted) queue.CompleteAdding(); }
        private void Encode()
        {
            try
            {
                var info = new ProcessStartInfo(ffmpeg, ProfileExport.InputArguments(format) + profile.Arguments(format.Channels) + " " + MediaExport.Quote(partial));
                info.UseShellExecute = false; info.CreateNoWindow = true;
                info.RedirectStandardInput = true; info.RedirectStandardError = true;
                info.StandardErrorEncoding = Encoding.UTF8;
                var errors = new StringBuilder();
                using (var p = new Process { StartInfo = info })
                {
                    p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                    { if (e.Data != null) lock (errors) { if (errors.Length < 8192) errors.AppendLine(e.Data); } };
                    lock (gate)
                    {
                        if (cancelled) return;
                        p.Start(); process = p;
                    }
                    try
                    {
                    p.BeginErrorReadLine();
                    foreach (var chunk in queue.GetConsumingEnumerable())
                    {
                        Interlocked.Add(ref queuedBytes, -chunk.Length);
                        if (failure != null) break;
                        p.StandardInput.BaseStream.Write(chunk, 0, chunk.Length);
                    }
                    p.StandardInput.Close();
                    if (!p.WaitForExit(30000)) { p.Kill(); throw new IOException(TextCatalog.T("message.63")); }
                    p.WaitForExit();
                    if (p.ExitCode != 0) throw new IOException(TextCatalog.T("message.64") + errors);
                    }
                    finally
                    {
                        lock (gate)
                        {
                            try { if (!p.HasExited) p.Kill(); } catch (InvalidOperationException) { }
                            process = null;
                        }
                    }
                }
            }
            catch (Exception e) { failure = e; }
            finally
            {
                lock (gate) { process = null; }
                byte[] discarded;
                while (queue.TryTake(out discarded)) { }
            }
        }
        internal void FinishAndVerify(Action<string> phase = null)
        {
            if (phase != null) phase(TextCatalog.T("message.65"));
            CompleteInput();
            if (!worker.Join(60000))
            {
                Cancel();
                throw new IOException(TextCatalog.T("message.66"));
            }
            if (failure != null)
            {
                // A live encoder may lose real-time during startup or disk load.
                // The durable WAV is authoritative; rebuild this part synchronously.
                try { if (File.Exists(partial)) File.Delete(partial); } catch { }
                MediaExport.Convert(Wav, ffmpeg, phase, profile);
                return;
            }
            var source = DurableWave.ReadInfo(Wav, false);
            int timeout = (int)Math.Min(1800000, Math.Max(60000, source.Duration * 4000 + 30000));
            if (phase != null) phase(TextCatalog.T("message.67"));
            double actual = MediaExport.DecodeDuration(partial, ffmpeg, timeout);
            if (Math.Abs(actual - source.Duration) > Math.Max(0.05, 2048.0 / source.Format.SampleRate))
                throw new IOException(TextCatalog.T("message.68"));
            using (var file = new FileStream(partial, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) file.Flush(true);
            File.Move(partial, final);
        }
        internal string FinishSession(System.Collections.Generic.IList<string> wavs, Action<string> phase)
        {
            if (phase != null) phase(TextCatalog.T("message.65"));
            CompleteInput();
            if (!worker.Join(60000)) { Cancel(); if (!worker.Join(5000)) throw new IOException(TextCatalog.T("encoderTimeout")); failure = new IOException(TextCatalog.T("diagnostic.11")); }
            double duration = 0; foreach (string wav in wavs) duration += DurableWave.ReadInfo(wav, false).Duration;
            if (failure != null) return ProfileExport.Rebuild(wavs, final, ffmpeg, profile, phase);
            ProfileExport.Publish(partial, final, duration, ffmpeg, phase); return final;
        }
        private void Cancel()
        {
            lock (gate)
            {
                cancelled = true;
                try { if (process != null && !process.HasExited) process.Kill(); }
                catch (InvalidOperationException) { }
            }
        }
        public void Dispose()
        {
            CompleteInput();
            Cancel();
            if (worker.Join(5000)) queue.Dispose();
        }
    }

    internal sealed class ProcessResult
    {
        internal int ExitCode;
        internal string Output, Error;
    }
    internal static class MediaExport
    {
        internal static ProcessResult Run(string executable, string arguments, int timeoutMs)
        {
            var output = new StringBuilder();
            var error = new StringBuilder();
            var info = new ProcessStartInfo(executable, arguments);
            info.UseShellExecute = false; info.CreateNoWindow = true;
            info.RedirectStandardOutput = true; info.RedirectStandardError = true;
            info.StandardOutputEncoding = Encoding.UTF8; info.StandardErrorEncoding = Encoding.UTF8;
            using (var process = new Process())
            {
                process.StartInfo = info;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args)
                { if (args.Data != null) lock (output) { if (output.Length < 65536) output.AppendLine(args.Data); } };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
                { if (args.Data != null) lock (error) { if (error.Length < 65536) error.AppendLine(args.Data); } };
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                if (!process.WaitForExit(timeoutMs))
                {
                    process.Kill();
                    process.WaitForExit();
                    throw new IOException(TextCatalog.T("message.69"));
                }
                process.WaitForExit(); // Drain asynchronous stdout/stderr callbacks.
                return new ProcessResult { ExitCode = process.ExitCode, Output = output.ToString(), Error = error.ToString() };
            }
        }
        internal static string Quote(string path)
        {
            if (path.IndexOf('"') >= 0) throw new ArgumentException(TextCatalog.T("diagnostic.12"));
            return "\"" + path + "\"";
        }
        internal static double DecodeDuration(string path, string ffmpeg, int timeout)
        {
            var result = Run(ffmpeg, "-hide_banner -loglevel error -nostdin -xerror -progress pipe:1 -nostats -i " +
                Quote(path) + " -map 0:a:0 -f null -", timeout);
            if (result.ExitCode != 0) throw new IOException(TextCatalog.T("message.70") + result.Error);
            double seconds = -1;
            foreach (string line in result.Output.Split('\n'))
            {
                if (line.StartsWith("out_time_us=", StringComparison.Ordinal))
                {
                    long us;
                    if (long.TryParse(line.Substring(12).Trim(), out us)) seconds = us / 1000000.0;
                }
            }
            if (seconds <= 0) throw new IOException(TextCatalog.T("message.71"));
            return seconds;
        }
        internal static string Convert(string wav, string ffmpeg, Action<string> phase = null, RecordingProfile profile = null)
        {
            WaveInfo source = DurableWave.ReadInfo(wav, false);
            profile = profile ?? RecordingProfile.Default;
            if (!File.Exists(ffmpeg)) throw new FileNotFoundException(TextCatalog.T("message.72"), ffmpeg);
            string final = Path.ChangeExtension(wav, ".mkv");
            if (File.Exists(final)) throw new IOException(TextCatalog.T("message.73"));
            string partial = Path.Combine(Path.GetDirectoryName(wav),
                Path.GetFileNameWithoutExtension(wav) + "-" + Guid.NewGuid().ToString("N") + ".partial.mkv");
            int timeout = (int)Math.Min(1800000, Math.Max(60000, source.Duration * 4000 + 30000));
            if (phase != null) phase(TextCatalog.T("message.74"));
            var result = Run(ffmpeg, "-hide_banner -loglevel error -nostdin -n -xerror -i " + Quote(wav) +
                " -map 0:a:0" + profile.Arguments(source.Format.Channels) + " " + Quote(partial), timeout);
            if (result.ExitCode != 0) throw new IOException(TextCatalog.T("message.75") + result.Error);
            if (phase != null) phase(TextCatalog.T("message.67"));
            double actual = DecodeDuration(partial, ffmpeg, timeout);
            if (Math.Abs(actual - source.Duration) > Math.Max(0.25, source.Duration * 0.001))
                throw new IOException(TextCatalog.T("message.76"));
            using (var stream = new FileStream(partial, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                stream.Flush(true);
            File.Move(partial, final);
            return final;
        }
        internal static string ConcatParts(System.Collections.Generic.IList<string> wavs, string ffmpeg, Action<string> phase = null)
        {
            if (wavs == null || wavs.Count < 2) throw new ArgumentException(TextCatalog.T("diagnostic.13"));
            if (!File.Exists(ffmpeg)) throw new FileNotFoundException(TextCatalog.T("message.77"), ffmpeg);
            string first = Path.ChangeExtension(wavs[0], ".mkv");
            string name = Path.GetFileNameWithoutExtension(first);
            int dash = name.LastIndexOf('-');
            if (dash < 0 || name.Length - dash != 4 || name.Substring(dash + 1) != "001")
                throw new InvalidDataException(TextCatalog.T("message.78"));
            string final = Path.Combine(Path.GetDirectoryName(first), name.Substring(0, dash) + ".mkv");
            if (File.Exists(final)) throw new IOException(TextCatalog.T("message.79"));
            string id = Guid.NewGuid().ToString("N");
            string list = Path.Combine(Path.GetDirectoryName(first), id + ".partial.ffconcat");
            string partial = Path.Combine(Path.GetDirectoryName(first), id + ".partial.mkv");
            double expected = 0;
            using (var file = new FileStream(list, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
            {
                writer.WriteLine("ffconcat version 1.0");
                for (int i = 0; i < wavs.Count; i++)
                {
                    string part = Path.ChangeExtension(wavs[i], ".mkv");
                    if (!File.Exists(part)) throw new FileNotFoundException(TextCatalog.T("message.80"), part);
                    if (Path.GetDirectoryName(part) != Path.GetDirectoryName(first))
                        throw new InvalidDataException(TextCatalog.T("message.81"));
                    expected += DurableWave.ReadInfo(wavs[i], false).Duration;
                    // ffconcat uses forward slashes on Windows; reject its only quote delimiter.
                    string path = Path.GetFullPath(part).Replace('\\', '/');
                    if (path.IndexOf('\'') >= 0) throw new InvalidDataException(TextCatalog.T("message.82"));
                    writer.WriteLine("file '" + path + "'");
                }
            }
            int timeout = (int)Math.Min(1800000, Math.Max(60000, expected * 250 + 30000));
            ProcessResult joined;
            if (phase != null) phase(TextCatalog.T("message.83"));
            try
            {
                joined = Run(ffmpeg, "-hide_banner -loglevel error -nostdin -n -xerror -f concat -safe 0 -i " +
                    Quote(list) + " -map 0:a:0 -c:a copy -f matroska " + Quote(partial), timeout);
            }
            finally { File.Delete(list); }
            if (joined.ExitCode != 0) throw new IOException(TextCatalog.T("message.84") + joined.Error);
            if (phase != null) phase(TextCatalog.T("message.85"));
            double actual = DecodeDuration(partial, ffmpeg, Math.Max(timeout, 60000));
            if (Math.Abs(actual - expected) > Math.Max(2.0, wavs.Count * 0.05))
                throw new IOException(TextCatalog.T("message.86"));
            using (var stream = new FileStream(partial, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                stream.Flush(true);
            File.Move(partial, final);
            return final;
        }
        internal static string ArchiveParts(System.Collections.Generic.IList<string> wavs,
            string playerRoot, bool includeMkv)
        {
            if (wavs == null || wavs.Count == 0) throw new ArgumentException(TextCatalog.T("diagnostic.14"));
            string first = Path.GetFileNameWithoutExtension(wavs[0]);
            int dash = first.LastIndexOf('-');
            if (dash < 0 || first.Length - dash != 4) throw new InvalidDataException(TextCatalog.T("diagnostic.15"));
            string archive = Path.Combine(playerRoot, "Backups", "RecordingParts", first.Substring(0, dash));
            Directory.CreateDirectory(archive);
            foreach (string wav in wavs)
            {
                string backupWav = Path.Combine(archive, Path.GetFileName(wav));
                if (File.Exists(backupWav)) throw new IOException(TextCatalog.T("diagnostic.16"));
                File.Move(wav, backupWav);
                if (includeMkv)
                {
                    string part = Path.ChangeExtension(wav, ".mkv");
                    string backupMkv = Path.Combine(archive, Path.GetFileName(part));
                    if (File.Exists(backupMkv)) throw new IOException(TextCatalog.T("diagnostic.17"));
                    File.Move(part, backupMkv);
                }
            }
            return archive;
        }
    }
}
