using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SoundLeaf
{
    internal sealed class PendingSession
    {
        internal string Folder, Stem;
        internal readonly SortedDictionary<int, string> Parts = new SortedDictionary<int, string>();
        internal bool Duplicate;
    }
    internal static class SessionRecovery
    {
        private static readonly Regex Name = new Regex(
            @"^(?<stem>\d{4}-\d{2}-\d{2} \d{2}-\d{2}-\d{2}-[a-f0-9]{12})-(?<part>\d{3,})\.(partial\.)?wav$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        internal static List<PendingSession> Find(string root, string playerRoot = null)
        {
            var groups = new Dictionary<string, PendingSession>(StringComparer.OrdinalIgnoreCase);
            Scan(root, groups);
            var result = new List<PendingSession>(groups.Values);
            result.RemoveAll(delegate(PendingSession group) { return WavReceipt.IsCompleted(group, playerRoot); });
            result.Sort(delegate(PendingSession a, PendingSession b) { return string.CompareOrdinal(a.Stem, b.Stem); });
            return result;
        }
        private static void Scan(string root, Dictionary<string, PendingSession> groups)
        {
            if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) return;
            foreach (string file in Directory.GetFiles(root, "*.wav"))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                Match match = Name.Match(Path.GetFileName(file));
                if (!match.Success) continue;
                int part;
                if (!int.TryParse(match.Groups["part"].Value, out part) || part < 1) continue;
                string stem = match.Groups["stem"].Value, key = Path.Combine(root, stem);
                PendingSession group;
                if (!groups.TryGetValue(key, out group))
                { group = new PendingSession { Folder = root, Stem = stem }; groups.Add(key, group); }
                if (group.Parts.ContainsKey(part)) group.Duplicate = true;
                else group.Parts.Add(part, file);
            }
            foreach (string directory in Directory.GetDirectories(root)) Scan(directory, groups);
        }
        internal static string Recover(PendingSession session, string playerRoot, string ffmpeg, Action<string> phase)
        {
            if (WavReceipt.IsCompleted(session, playerRoot)) throw new IOException(TextCatalog.T("message.102"));
            if (session.Duplicate || session.Parts.Count == 0) throw new IOException(TextCatalog.T("message.103"));
            int expected = 1;
            foreach (int part in session.Parts.Keys)
                if (part != expected++) throw new IOException(TextCatalog.T("message.104"));
            RecordingProfile profile = SessionProfile.Read(playerRoot, session.Stem);
            string final = Path.Combine(session.Folder, session.Stem + profile.Extension);
            if (File.Exists(final)) throw new IOException(TextCatalog.T("message.105"));
            string work = Path.Combine(playerRoot, "RecoveryWork", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            var locks = new List<FileStream>();
            var wavs = new List<string>();
            double duration = 0;
            byte[] firstFormat = null;
            try
            {
                foreach (string source in session.Parts.Values)
                {
                    // FileShare.Read rejects active writers and protects the snapshot during copying.
                    locks.Add(new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read));
                    WaveInfo info = DurableWave.ReadInfo(source, source.EndsWith(".partial.wav", StringComparison.OrdinalIgnoreCase));
                    if (firstFormat == null) firstFormat = info.Format.Bytes;
                    else if (!SameFormat(firstFormat, info.Format.Bytes)) throw new IOException(TextCatalog.T("message.106"));
                    duration += info.Duration;
                }
                int index = 0;
                foreach (string source in session.Parts.Values)
                {
                    if (phase != null) phase(TextCatalog.T("message.107") + (++index) + "/" + session.Parts.Count);
                    else index++;
                    string target = Path.Combine(work, session.Stem + "-" + index.ToString("D3") + ".wav");
                    if (source.EndsWith(".partial.wav", StringComparison.OrdinalIgnoreCase))
                    {
                        string copy = Path.Combine(work, "input-" + index + ".partial.wav");
                        File.Copy(source, copy, false);
                        File.Move(DurableWave.RecoverCopy(copy), target);
                        File.Delete(copy);
                    }
                    else File.Copy(source, target, false);
                    wavs.Add(target);
                    if (profile.Format == AudioOutputFormat.Mkv) MediaExport.Convert(target, ffmpeg, phase, profile);
                }
                string result = profile.Format == AudioOutputFormat.Mkv ?
                    (wavs.Count == 1 ? Path.ChangeExtension(wavs[0], ".mkv") : MediaExport.ConcatParts(wavs, ffmpeg, phase)) :
                    ProfileExport.Rebuild(wavs, Path.Combine(work, session.Stem + profile.Extension), ffmpeg, profile, phase);
                if (phase != null) phase(TextCatalog.T("message.85"));
                double actual = MediaExport.DecodeDuration(result, ffmpeg,
                    (int)Math.Min(1800000, Math.Max(60000, duration * 1000 + 30000)));
                if (Math.Abs(actual - duration) > Math.Max(0.25, duration * 0.001)) throw new IOException(TextCatalog.T("message.108"));
                File.Move(result, final); // Atomic publication on the same volume; never overwrite.
                ResultReceipt.Write(playerRoot, session.Stem, profile, duration, false, new[] { final });
            }
            finally { foreach (var file in locks) file.Dispose(); }
            string archive = Path.Combine(playerRoot, "Backups", "RecoveredSessions", session.Stem + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(archive);
            // Original files are archived, never deleted by recovery or the 24-hour cleanup.
            foreach (string source in session.Parts.Values) File.Move(source, Path.Combine(archive, Path.GetFileName(source)));
            foreach (string old in Directory.GetFiles(session.Folder, session.Stem + "*.partial" + profile.Extension))
                if ((File.GetAttributes(old) & FileAttributes.ReparsePoint) == 0)
                    File.Move(old, Path.Combine(archive, Path.GetFileName(old)));
            string workRoot = Path.GetFullPath(Path.Combine(playerRoot, "RecoveryWork"));
            if (Path.GetDirectoryName(Path.GetFullPath(work)) != workRoot) throw new IOException(TextCatalog.T("diagnostic.10"));
            Directory.Delete(work, true); // Only our generated GUID directory, never the source folder.
            return final;
        }
        private static bool SameFormat(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
