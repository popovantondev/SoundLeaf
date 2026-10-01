using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Player
{
    internal enum RecordState { Starting, Recording, Paused, Saving, Stopped, Faulted }
    internal enum RecordingMode { MkvWithWavBackup, WavOnly }
    internal enum FinalResultKind { None, Mkv, WavParts }
    internal sealed class SessionUpdate
    {
        internal RecordState State;
        internal string Message;
        internal double Seconds;
        internal bool HeardSignal;
        internal string FinalPath;
        internal string[] FinalPaths = new string[0];
        internal FinalResultKind ResultKind;
        internal RecordingMode Mode;
        internal bool CanRecordWavOnly;
        internal string Warning;
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
        private readonly string folder, ffmpeg;
        private readonly AppLog log;
        private readonly Action<SessionUpdate> update;
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private int stop, pause;
        private readonly Thread worker;
        private readonly Func<IAudioSource> sourceFactory;
        internal readonly RecordingMode Mode;
        private readonly string playerRoot;
        private readonly long segmentBytes;
        private readonly Func<PreflightResult> preflight;
        private string preflightWarning;
        private string[] finalPaths = new string[0];
        private FinalResultKind resultKind;
        internal RecordingSession(string folder, string ffmpeg, AppLog log, Action<SessionUpdate> update)
            : this(folder, ffmpeg, log, update, RecordingMode.MkvWithWavBackup) { }
        internal RecordingSession(string folder, string ffmpeg, AppLog log, Action<SessionUpdate> update, RecordingMode mode)
            : this(folder, ffmpeg, log, update, delegate { return new AudioCapture(); }, mode,
                AppDomain.CurrentDomain.BaseDirectory, 256L * 1024 * 1024,
                delegate { return StartupChecks.Check(folder, ffmpeg, mode); }) { }
        internal RecordingSession(string folder, string ffmpeg, AppLog log, Action<SessionUpdate> update,
            Func<IAudioSource> sourceFactory)
            : this(folder, ffmpeg, log, update, sourceFactory, RecordingMode.MkvWithWavBackup,
                AppDomain.CurrentDomain.BaseDirectory, 256L * 1024 * 1024, null) { }
        internal RecordingSession(string folder, string ffmpeg, AppLog log, Action<SessionUpdate> update,
            Func<IAudioSource> sourceFactory, RecordingMode mode, string playerRoot, long segmentBytes,
            Func<PreflightResult> preflight)
        {
            this.folder = folder; this.ffmpeg = ffmpeg; this.log = log; this.update = update;
            this.sourceFactory = sourceFactory;
            Mode = mode; this.playerRoot = playerRoot; this.segmentBytes = segmentBytes; this.preflight = preflight;
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
                ResultKind = resultKind, Mode = Mode, CanRecordWavOnly = canWav, Warning = preflightWarning });
        }
        private void Run()
        {
            SegmentedWave sink = null;
            AudioTimeline timeline = null;
            WaveFormat format = null;
            string failure = null;
            double seconds = 0;
            string finalPath = null;
            if (preflight != null)
            {
                Publish(RecordState.Starting, "Проверка перед записью", 0, false);
                PreflightResult readiness;
                try { readiness = preflight(); }
                catch (Exception error) { readiness = new PreflightResult { Error = error.Message }; }
                preflightWarning = readiness.Warning;
                if (Interlocked.CompareExchange(ref stop, 0, 0) != 0)
                { wake.Dispose(); Publish(RecordState.Stopped, "Запуск отменён; запись не начата", 0, false); return; }
                if (!readiness.CanRecord)
                { wake.Dispose(); Publish(RecordState.Faulted, readiness.Error, 0, false, null, readiness.CanRecordWavOnly); return; }
            }
            try
            {
                using (var audio = sourceFactory())
                {
                    format = audio.Format;
                    log.Write("Device " + audio.DeviceId + "; " + format.SampleRate + " Hz; " +
                        format.Channels + " channels; " + format.Bits + " bits; float=" + format.Float);
                    sink = new SegmentedWave(folder, format, segmentBytes, Mode == RecordingMode.WavOnly ? null : ffmpeg);
                    timeline = new AudioTimeline(sink, format);
                    timeline.Resume(AudioCapture.Clock100ns);
                    audio.Start();
                    Publish(RecordState.Recording, "Запись системного вывода", 0, false);
                    bool isPaused = false;
                    long lastCheck = AudioCapture.Clock100ns;
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
                            }
                            else
                            {
                                audio.Reset();
                                timeline.Resume(AudioCapture.Clock100ns);
                                audio.Start();
                            }
                            isPaused = requestedPause;
                            Publish(isPaused ? RecordState.Paused : RecordState.Recording,
                                isPaused ? "Пауза" : "Запись системного вывода",
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
                            update(new SessionUpdate(isPaused ? RecordState.Paused : RecordState.Recording,
                                isPaused ? "Пауза" : (timeline.HeardSignal ? "Запись; звук поступал" : "Запись; пока тишина"),
                                sink.Frames / (double)format.SampleRate, timeline.HeardSignal) { Mode = Mode, Warning = preflightWarning });
                            lastCheck = now;
                        }
                        wake.WaitOne(10);
                    }
                    if (!isPaused)
                    {
                        audio.Stop();
                        audio.Drain(timeline.Packet);
                    }
                }
            }
            catch (Exception error)
            {
                failure = error.Message;
                log.TryWrite(error.ToString());
            }
            try
            {
                Publish(RecordState.Saving, "Сохранение", 0, timeline != null && timeline.HeardSignal);
                if (sink != null)
                {
                    seconds = sink.Frames / (double)format.SampleRate;
                    sink.Complete();
                    if (sink.Completed.Count == 0) throw new IOException("Аудиоданные не получены. Временный WAV оставлен.");
                    if (Mode == RecordingMode.WavOnly)
                    {
                        Publish(RecordState.Saving, "Проверка WAV и фиксация результата", seconds, timeline.HeardSignal);
                        foreach (string wav in sink.Completed) DurableWave.ReadInfo(wav, false);
                        if (failure == null) WavReceipt.Write(playerRoot, sink.Completed);
                        finalPaths = sink.Completed.ToArray();
                        finalPath = finalPaths[0];
                        resultKind = FinalResultKind.WavParts;
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
                else if (failure == null) failure = "Не удалось открыть аудиоустройство.";
            }
            catch (Exception error)
            {
                failure = (failure == null ? "" : failure + Environment.NewLine) + error.Message;
                log.TryWrite(error.ToString());
            }
            finally
            {
                if (sink != null) sink.Dispose();
                wake.Dispose();
            }
            bool signal = timeline != null && timeline.HeardSignal;
            string warning = null;
            if (timeline != null && timeline.Discontinuity)
                warning = "Windows сообщила о разрыве аудиопотока; сохранённый звук стоит проверить.";
            Publish(failure == null ? RecordState.Stopped : RecordState.Faulted,
                failure ?? (Mode == RecordingMode.WavOnly ? "WAV сохранён; MKV не создавался. Частей: " + finalPaths.Length +
                    (warning == null ? "" : Environment.NewLine + warning) :
                    warning ?? (signal ? "MKV сохранён и проверен" : "Сохранено; за время записи был только тихий сигнал или тишина")),
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
        private volatile Exception failure;
        private Process process;
        private readonly object gate = new object();
        private bool cancelled;
        internal readonly string Wav;
        internal string TemporaryPath { get { return partial; } }
        internal LiveMkv(string partialWav, WaveFormat format, string ffmpeg)
        {
            this.format = format; this.ffmpeg = ffmpeg;
            Wav = partialWav.Substring(0, partialWav.Length - ".partial.wav".Length) + ".wav";
            partial = Path.ChangeExtension(Wav, "." + Guid.NewGuid().ToString("N") + ".partial.mkv");
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
                    failure = new IOException("Кодировщик не успевает. Резервный WAV сохранён.");
                    return;
                }
                var copy = new byte[n];
                Buffer.BlockCopy(data, offset, copy, 0, n);
                if (!queue.TryAdd(copy))
                {
                    Interlocked.Add(ref queuedBytes, -n);
                    failure = new IOException("Кодировщик не успевает. Резервный WAV сохранён.");
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
                string pcm = format.Float ? "f32le" : "s" + format.Bits + "le";
                var info = new ProcessStartInfo(ffmpeg, "-hide_banner -loglevel error -nostdin -n -xerror -f " + pcm +
                    " -ar " + format.SampleRate + " -ac " + format.Channels + " -probesize 32 -analyzeduration 0 -i pipe:0 -c:a aac -b:a 192k" +
                    " -flush_packets 1 -cluster_time_limit 1000 -f matroska " + MediaExport.Quote(partial));
                info.UseShellExecute = false; info.CreateNoWindow = true;
                info.RedirectStandardInput = true; info.RedirectStandardError = true;
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
                    if (!p.WaitForExit(30000)) { p.Kill(); throw new IOException("FFmpeg не завершился вовремя. WAV сохранён."); }
                    p.WaitForExit();
                    if (p.ExitCode != 0) throw new IOException("Ошибка MKV; WAV сохранён. " + errors);
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
            if (phase != null) phase("Завершение кодирования");
            CompleteInput();
            if (!worker.Join(60000))
            {
                Cancel();
                throw new IOException("Превышено время сохранения MKV. WAV оставлен.");
            }
            if (failure != null)
            {
                // A live encoder may lose real-time during startup or disk load.
                // The durable WAV is authoritative; rebuild this part synchronously.
                try { if (File.Exists(partial)) File.Delete(partial); } catch { }
                MediaExport.Convert(Wav, ffmpeg, phase);
                return;
            }
            var source = DurableWave.ReadInfo(Wav, false);
            int timeout = (int)Math.Min(1800000, Math.Max(60000, source.Duration * 4000 + 30000));
            if (phase != null) phase("Проверка части MKV");
            double actual = MediaExport.DecodeDuration(partial, ffmpeg, timeout);
            if (Math.Abs(actual - source.Duration) > Math.Max(0.05, 2048.0 / source.Format.SampleRate))
                throw new IOException("Длительность MKV не совпадает с WAV. WAV сохранён.");
            using (var file = new FileStream(partial, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) file.Flush(true);
            File.Move(partial, Path.ChangeExtension(Wav, ".mkv"));
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
                    throw new IOException("FFmpeg превысил время ожидания. Исходный WAV сохранён.");
                }
                process.WaitForExit(); // Drain asynchronous stdout/stderr callbacks.
                return new ProcessResult { ExitCode = process.ExitCode, Output = output.ToString(), Error = error.ToString() };
            }
        }
        internal static string Quote(string path)
        {
            if (path.IndexOf('"') >= 0) throw new ArgumentException("Invalid path.");
            return "\"" + path + "\"";
        }
        internal static double DecodeDuration(string path, string ffmpeg, int timeout)
        {
            var result = Run(ffmpeg, "-hide_banner -loglevel error -nostdin -xerror -progress pipe:1 -nostats -i " +
                Quote(path) + " -map 0:a:0 -f null -", timeout);
            if (result.ExitCode != 0) throw new IOException("Проверка MKV не пройдена: " + result.Error);
            double seconds = -1;
            foreach (string line in result.Output.Split('\n'))
            {
                if (line.StartsWith("out_time_us=", StringComparison.Ordinal))
                {
                    long us;
                    if (long.TryParse(line.Substring(12).Trim(), out us)) seconds = us / 1000000.0;
                }
            }
            if (seconds <= 0) throw new IOException("MKV не содержит декодируемого аудио.");
            return seconds;
        }
        internal static string Convert(string wav, string ffmpeg, Action<string> phase = null)
        {
            WaveInfo source = DurableWave.ReadInfo(wav, false);
            if (!File.Exists(ffmpeg)) throw new FileNotFoundException("FFmpeg отсутствует в tools. WAV сохранён.", ffmpeg);
            string final = Path.ChangeExtension(wav, ".mkv");
            if (File.Exists(final)) throw new IOException("MKV уже существует; перезапись запрещена.");
            string partial = Path.Combine(Path.GetDirectoryName(wav),
                Path.GetFileNameWithoutExtension(wav) + "-" + Guid.NewGuid().ToString("N") + ".partial.mkv");
            int timeout = (int)Math.Min(1800000, Math.Max(60000, source.Duration * 4000 + 30000));
            if (phase != null) phase("Кодирование резервного WAV");
            var result = Run(ffmpeg, "-hide_banner -loglevel error -nostdin -n -xerror -i " + Quote(wav) +
                " -map 0:a:0 -c:a aac -b:a 192k -f matroska " + Quote(partial), timeout);
            if (result.ExitCode != 0) throw new IOException("MKV не создан. WAV сохранён. " + result.Error);
            if (phase != null) phase("Проверка части MKV");
            double actual = DecodeDuration(partial, ffmpeg, timeout);
            if (Math.Abs(actual - source.Duration) > Math.Max(0.25, source.Duration * 0.001))
                throw new IOException("Длительности WAV и MKV различаются; временный MKV и исходный WAV сохранены.");
            using (var stream = new FileStream(partial, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                stream.Flush(true);
            File.Move(partial, final);
            return final;
        }
        internal static string ConcatParts(System.Collections.Generic.IList<string> wavs, string ffmpeg, Action<string> phase = null)
        {
            if (wavs == null || wavs.Count < 2) throw new ArgumentException("At least two parts are required.");
            if (!File.Exists(ffmpeg)) throw new FileNotFoundException("FFmpeg отсутствует; части сохранены.", ffmpeg);
            string first = Path.ChangeExtension(wavs[0], ".mkv");
            string name = Path.GetFileNameWithoutExtension(first);
            int dash = name.LastIndexOf('-');
            if (dash < 0 || name.Length - dash != 4 || name.Substring(dash + 1) != "001")
                throw new InvalidDataException("Неверное имя первой части MKV.");
            string final = Path.Combine(Path.GetDirectoryName(first), name.Substring(0, dash) + ".mkv");
            if (File.Exists(final)) throw new IOException("Итоговый MKV уже существует; части сохранены.");
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
                    if (!File.Exists(part)) throw new FileNotFoundException("Часть MKV отсутствует; WAV сохранены.", part);
                    if (Path.GetDirectoryName(part) != Path.GetDirectoryName(first))
                        throw new InvalidDataException("Части MKV находятся в разных папках.");
                    expected += DurableWave.ReadInfo(wavs[i], false).Duration;
                    // ffconcat uses forward slashes on Windows; reject its only quote delimiter.
                    string path = Path.GetFullPath(part).Replace('\\', '/');
                    if (path.IndexOf('\'') >= 0) throw new InvalidDataException("Апостроф в пути MKV не поддерживается.");
                    writer.WriteLine("file '" + path + "'");
                }
            }
            int timeout = (int)Math.Min(1800000, Math.Max(60000, expected * 250 + 30000));
            ProcessResult joined;
            if (phase != null) phase("Объединение частей");
            try
            {
                joined = Run(ffmpeg, "-hide_banner -loglevel error -nostdin -n -xerror -f concat -safe 0 -i " +
                    Quote(list) + " -map 0:a:0 -c:a copy -f matroska " + Quote(partial), timeout);
            }
            finally { File.Delete(list); }
            if (joined.ExitCode != 0) throw new IOException("Объединение MKV не удалось; части и WAV сохранены. " + joined.Error);
            if (phase != null) phase("Проверка итогового файла");
            double actual = DecodeDuration(partial, ffmpeg, Math.Max(timeout, 60000));
            if (Math.Abs(actual - expected) > Math.Max(2.0, wavs.Count * 0.05))
                throw new IOException("Длительность объединённого MKV неверна; части и WAV сохранены.");
            using (var stream = new FileStream(partial, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                stream.Flush(true);
            File.Move(partial, final);
            return final;
        }
        internal static string ArchiveParts(System.Collections.Generic.IList<string> wavs,
            string playerRoot, bool includeMkv)
        {
            if (wavs == null || wavs.Count == 0) throw new ArgumentException("No WAV parts to archive.");
            string first = Path.GetFileNameWithoutExtension(wavs[0]);
            int dash = first.LastIndexOf('-');
            if (dash < 0 || first.Length - dash != 4) throw new InvalidDataException("Invalid part name.");
            string archive = Path.Combine(playerRoot, "Backups", "RecordingParts", first.Substring(0, dash));
            Directory.CreateDirectory(archive);
            foreach (string wav in wavs)
            {
                string backupWav = Path.Combine(archive, Path.GetFileName(wav));
                if (File.Exists(backupWav)) throw new IOException("Backup WAV already exists; nothing overwritten.");
                File.Move(wav, backupWav);
                if (includeMkv)
                {
                    string part = Path.ChangeExtension(wav, ".mkv");
                    string backupMkv = Path.Combine(archive, Path.GetFileName(part));
                    if (File.Exists(backupMkv)) throw new IOException("Backup MKV already exists; nothing overwritten.");
                    File.Move(part, backupMkv);
                }
            }
            return archive;
        }
    }
}
