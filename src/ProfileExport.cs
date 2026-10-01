using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;

namespace SoundLeaf
{
    internal static class ProfileExport
    {
        internal static int Timeout(double seconds) { return (int)Math.Min(1800000, Math.Max(60000, seconds * 4000 + 30000)); }
        internal static string Rebuild(IList<string> wavs, string final, string ffmpeg, RecordingProfile profile, Action<string> phase)
        {
            var locks = new List<FileStream>();
            try
            {
                foreach (string wav in wavs) locks.Add(new FileStream(wav, FileMode.Open, FileAccess.Read, FileShare.Read));
                return RebuildLocked(wavs, final, ffmpeg, profile, phase);
            }
            finally { foreach (var file in locks) file.Dispose(); }
        }
        private static string RebuildLocked(IList<string> wavs, string final, string ffmpeg, RecordingProfile profile, Action<string> phase)
        {
            if (wavs.Count == 0 || File.Exists(final)) throw new IOException(TextCatalog.T("collision"));
            WaveInfo first = DurableWave.ReadInfo(wavs[0], false);
            double expected = 0;
            foreach (string wav in wavs)
            {
                var info = DurableWave.ReadInfo(wav, false);
                if (info.Format.SampleRate != first.Format.SampleRate || info.Format.Channels != first.Format.Channels ||
                    info.Format.Bits != first.Format.Bits || info.Format.Float != first.Format.Float) throw new IOException(TextCatalog.T("incompatible"));
                expected += info.Duration;
            }
            string partial = Path.Combine(Path.GetDirectoryName(final), Path.GetFileNameWithoutExtension(final) + "." + Guid.NewGuid().ToString("N") + ".partial" + profile.Extension);
            if (phase != null) phase(TextCatalog.T("message.74"));
            var errors = new StringBuilder();
            var start = new ProcessStartInfo(ffmpeg, InputArguments(first.Format) + profile.Arguments(first.Format.Channels) + " " + MediaExport.Quote(partial))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true };
            using (var process = new Process { StartInfo = start })
            {
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (errors) { if (errors.Length < 8192) errors.AppendLine(e.Data); } };
                process.Start(); process.BeginErrorReadLine();
                Exception feedFailure = null;
                var feeder = new Thread(delegate()
                {
                    try
                    {
                        byte[] buffer = new byte[65536];
                        foreach (string wav in wavs)
                        {
                            var info = DurableWave.ReadInfo(wav, false);
                            using (var input = new FileStream(wav, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                input.Position = info.DataOffset; long left = info.DataBytes;
                                while (left > 0)
                                { int n = input.Read(buffer, 0, (int)Math.Min(left, buffer.Length)); if (n == 0) throw new EndOfStreamException(); process.StandardInput.BaseStream.Write(buffer, 0, n); left -= n; }
                            }
                        }
                    }
                    catch (Exception error) { feedFailure = error; }
                    finally { try { process.StandardInput.Close(); } catch (Exception) { } }
                }) { IsBackground = true, Name = "SoundLeaf PCM rebuild" };
                feeder.Start();
                try
                {
                    if (!process.WaitForExit(Timeout(expected))) { process.Kill(); throw new IOException(TextCatalog.T("encoderTimeout")); }
                    process.WaitForExit();
                    if (!feeder.Join(5000)) throw new IOException(TextCatalog.T("feedTimeout"));
                    if (feedFailure != null) throw new IOException(TextCatalog.T("feedFailed"), feedFailure);
                    if (process.ExitCode != 0) throw new IOException(TextCatalog.T("encodeFailed") + errors);
                }
                finally { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } feeder.Join(5000); }
            }
            Publish(partial, final, expected, ffmpeg, phase); return final;
        }
        internal static string InputArguments(WaveFormat format)
        {
            return "-hide_banner -loglevel error -nostdin -n -xerror -f " + (format.Float ? "f32le" : "s" + format.Bits + "le") +
                " -ar " + format.SampleRate + " -ac " + format.Channels + " -probesize 32 -analyzeduration 0 -i pipe:0";
        }
        internal static string Transcode(string source, string final, string ffmpeg, RecordingProfile profile, Action<string> phase)
        {
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(final), StringComparison.OrdinalIgnoreCase) || File.Exists(final))
                throw new IOException(TextCatalog.T("originalCollision"));
            if (phase != null) phase(TextCatalog.T("message.85"));
            using (var sourceLock = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
            double seconds = MediaExport.DecodeDuration(source, ffmpeg, 1800000);
            int channels = ProbeChannels(source, ffmpeg);
            string partial = Path.Combine(Path.GetDirectoryName(final), Path.GetFileNameWithoutExtension(final) + "." + Guid.NewGuid().ToString("N") + ".partial" + profile.Extension);
            var result = MediaExport.Run(ffmpeg, "-hide_banner -loglevel error -nostdin -n -xerror -i " + MediaExport.Quote(source) +
                " -map 0:a:0 -vn" + profile.Arguments(channels) + " " + MediaExport.Quote(partial), Timeout(seconds));
            if (result.ExitCode != 0) throw new IOException(TextCatalog.T("convertFailed") + result.Error);
            Publish(partial, final, seconds, ffmpeg, phase); return final;
            }
        }
        internal static int ProbeChannels(string source, string ffmpeg)
        {
            string info = MediaExport.Run(ffmpeg, "-hide_banner -nostdin -i " + MediaExport.Quote(source), 3000).Error;
            foreach (string line in info.Split('\n')) if (line.Contains("Audio:"))
            { if (Regex.IsMatch(line, @"\bmono\b")) return 1; return 2; }
            throw new InvalidDataException(TextCatalog.T("audioMetadata"));
        }
        internal static void Publish(string partial, string final, double expected, string ffmpeg, Action<string> phase)
        {
            if (phase != null) phase(TextCatalog.T("message.85"));
            double actual = MediaExport.DecodeDuration(partial, ffmpeg, Timeout(expected));
            if (Math.Abs(actual - expected) > 0.25) throw new IOException(TextCatalog.T("durationFailed"));
            using (var file = new FileStream(partial, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) file.Flush(true);
            File.Move(partial, final);
        }
    }
}
