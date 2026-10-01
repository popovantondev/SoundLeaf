using System;
using System.IO;

namespace Player
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
            Func<string, long?> space = null, Action<string, string, int> run = null)
        {
            var result = new PreflightResult();
            string probe = null;
            bool owned = false;
            try
            {
                Directory.CreateDirectory(folder);
                probe = Path.Combine(Path.GetFullPath(folder), ".preflight-" + Guid.NewGuid().ToString("N"));
                if (Directory.Exists(probe)) { probe = null; throw new IOException("Папка проверки уже существует."); }
                Directory.CreateDirectory(probe);
                string write = Path.Combine(probe, "write.tmp"), renamed = Path.Combine(probe, "renamed.tmp");
                using (var file = new FileStream(write, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { owned = true; file.WriteByte(1); file.Flush(true); }
                File.Move(write, renamed);
                if (File.ReadAllBytes(renamed).Length != 1) throw new IOException("Проверка записи в папку не пройдена.");
                long? bytes = (space ?? AvailableSpace)(folder);
                if (bytes.HasValue && bytes.Value < MinimumBytes)
                { result.Error = "Свободного места меньше 256 МиБ. Запись не начата."; return result; }
                result.Warning = !bytes.HasValue ? "Не удалось определить свободное место; запись в папку проверена." :
                    bytes.Value < WarningBytes ? "Свободного места меньше 1 ГиБ. Его может не хватить на всю запись." : null;
                if (mode == RecordingMode.MkvWithWavBackup)
                {
                    try
                    {
                        if (!File.Exists(ffmpeg)) throw new FileNotFoundException("FFmpeg отсутствует в tools.");
                        string mkv = Path.Combine(probe, "audio.mkv");
                        string arguments = "-hide_banner -loglevel error -nostdin -n -f lavfi -i anullsrc=r=48000:cl=stereo" +
                            " -t 0.1 -c:a aac -b:a 192k -f matroska " + MediaExport.Quote(mkv);
                        if (run != null) run(ffmpeg, arguments, 3000);
                        else
                        {
                            var encoded = MediaExport.Run(ffmpeg, arguments, 3000);
                            if (encoded.ExitCode != 0) throw new IOException("Тест AAC/MKV не пройден: " + encoded.Error);
                        }
                        if (run != null) run(ffmpeg, "decode " + mkv, 3000);
                        else if (MediaExport.DecodeDuration(mkv, ffmpeg, 3000) < 0.05)
                            throw new IOException("Проверка тестового MKV не пройдена.");
                    }
                    catch (Exception error)
                    {
                        result.CanRecordWavOnly = true;
                        result.Error = "MKV недоступен: " + error.Message +
                            " Для резервной записи выберите «Начать запись только в WAV».";
                        return result;
                    }
                }
                result.CanRecord = true;
                return result;
            }
            catch (Exception error) { result.Error = "Запись не начата: " + error.Message; return result; }
            finally
            {
                // Exact filenames in our fresh GUID folder; never recurse into user data.
                if (probe != null && owned && string.Equals(Path.GetDirectoryName(probe).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string name in new[] { "write.tmp", "renamed.tmp", "audio.mkv" })
                        try { File.Delete(Path.Combine(probe, name)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    try { Directory.Delete(probe, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
