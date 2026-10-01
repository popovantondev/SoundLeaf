using System;
using System.IO;

namespace SoundLeaf
{
    internal sealed class PreflightResult
    {
        internal bool CanRecord, CanRecordWavOnly;
        internal string Error, Warning;
    }
    internal static class StartupChecks
    {
        internal const long MinimumBytes = 256L * 1024 * 1024;
        internal const long WarningBytes = 1024L * 1024 * 1024;
        internal static long? AvailableSpace(string folder)
        {
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))).AvailableFreeSpace; }
            catch (Exception) { return null; }
        }
        internal static PreflightResult Check(string folder, string ffmpeg, RecordingMode mode,
            Func<string, long?> space = null, Action<string, string, int> run = null, RecordingProfile profile = null)
        {
            var result = new PreflightResult();
            profile = profile ?? RecordingProfile.Default;
            string probe = null;
            bool owned = false;
            try
            {
                Directory.CreateDirectory(folder);
                probe = Path.Combine(Path.GetFullPath(folder), ".preflight-" + Guid.NewGuid().ToString("N"));
                if (Directory.Exists(probe)) { probe = null; throw new IOException(TextCatalog.T("message.87")); }
                Directory.CreateDirectory(probe);
                string write = Path.Combine(probe, "write.tmp"), renamed = Path.Combine(probe, "renamed.tmp");
                using (var file = new FileStream(write, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { owned = true; file.WriteByte(1); file.Flush(true); }
                File.Move(write, renamed);
                if (File.ReadAllBytes(renamed).Length != 1) throw new IOException(TextCatalog.T("message.88"));
                long? bytes = (space ?? AvailableSpace)(folder);
                if (bytes.HasValue && bytes.Value < MinimumBytes)
                { result.Error = TextCatalog.T("message.89"); return result; }
                result.Warning = !bytes.HasValue ? TextCatalog.T("message.90") :
                    bytes.Value < WarningBytes ? TextCatalog.T("message.91") : null;
                if (mode == RecordingMode.MkvWithWavBackup)
                {
                    try
                    {
                        if (!File.Exists(ffmpeg)) throw new FileNotFoundException(TextCatalog.T("message.92"));
                        string mkv = Path.Combine(probe, "audio" + profile.Extension);
                        string arguments = "-hide_banner -loglevel error -nostdin -n -f lavfi -i anullsrc=r=48000:cl=stereo" +
                            " -t 0.1" + profile.Arguments(2) + " " + MediaExport.Quote(mkv);
                        if (run != null) run(ffmpeg, arguments, 3000);
                        else
                        {
                            var encoded = MediaExport.Run(ffmpeg, arguments, 3000);
                            if (encoded.ExitCode != 0) throw new IOException(TextCatalog.T("message.93") + encoded.Error);
                        }
                        if (run != null) run(ffmpeg, "decode " + mkv, 3000);
                        else if (MediaExport.DecodeDuration(mkv, ffmpeg, 3000) < 0.05)
                            throw new IOException(TextCatalog.T("message.94"));
                    }
                    catch (Exception error)
                    {
                        result.CanRecordWavOnly = true;
                        result.Error = profile.Label + TextCatalog.T("message.95") + error.Message +
                            TextCatalog.T("message.96");
                        return result;
                    }
                }
                result.CanRecord = true;
                return result;
            }
            catch (Exception error) { result.Error = TextCatalog.T("message.97") + error.Message; return result; }
            finally
            {
                // Exact filenames in our fresh GUID folder; never recurse into user data.
                if (probe != null && owned && string.Equals(Path.GetDirectoryName(probe).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string name in new[] { "write.tmp", "renamed.tmp", "audio" + profile.Extension })
                        try { File.Delete(Path.Combine(probe, name)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    try { Directory.Delete(probe, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
