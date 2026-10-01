using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace SoundLeaf
{
    [DataContract]
    internal sealed class ResultReceipt
    {
        [DataMember] internal int Version = 1;
        [DataMember] internal string SessionId;
        [DataMember] internal RecordingProfile Profile;
        [DataMember] internal double Seconds;
        [DataMember] internal bool WavOnly;
        [DataMember] internal List<WavReceiptPart> Files;
        internal static void Write(string root, string id, RecordingProfile profile, double seconds, bool wavOnly, string[] paths)
        {
            string prefix = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            var value = new ResultReceipt { SessionId = id, Profile = profile, Seconds = seconds, WavOnly = wavOnly, Files = new List<WavReceiptPart>() };
            foreach (string path in paths)
            {
                string full = Path.GetFullPath(path);
                if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException(TextCatalog.T("diagnostic.9"));
                var file = new FileInfo(full);
                value.Files.Add(new WavReceiptPart { Path = full.Substring(prefix.Length).Replace('\\', '/'), Length = file.Length, ModifiedUtcTicks = file.LastWriteTimeUtc.Ticks });
            }
            AtomicJson.Write(Path.Combine(root, "State", "Sessions", id + ".result.json"), value, false);
        }
    }
    internal sealed class RecordingEntry
    {
        internal string Id, Format;
        internal DateTime Date;
        internal string[] Paths;
        internal long Bytes;
        internal double? Seconds;
        internal bool Verified;
        public override string ToString() { return Date.ToString("yyyy-MM-dd HH:mm") + " · " + Format + " · " + TextCatalog.T(Verified ? "verified" : "existing"); }
    }
    internal static class RecordingCatalog
    {
        private static readonly Regex WavPart = new Regex(@"^(.*-[a-f0-9]{12})-\d{3,}\.wav$", RegexOptions.IgnoreCase);
        internal static List<RecordingEntry> Read(string root, string recordings)
        {
            var entries = new Dictionary<string, RecordingEntry>(StringComparer.OrdinalIgnoreCase);
            Scan(recordings, entries);
            foreach (var entry in entries.Values)
            {
                string metadataRoot = root;
                string metadata = Path.Combine(metadataRoot, "State", "Sessions", entry.Id + ".result.json");
                if (!File.Exists(metadata))
                {
                    metadataRoot = Path.GetDirectoryName(entry.Paths[0]);
                    metadata = Path.Combine(metadataRoot, "State", "Sessions", entry.Id + ".result.json");
                }
                if (!File.Exists(metadata)) continue;
                try
                {
                    var value = AtomicJson.Read<ResultReceipt>(metadata);
                    if (value == null || value.Version != 1 || value.SessionId != entry.Id || value.Files == null || value.Files.Count != entry.Paths.Length ||
                        double.IsNaN(value.Seconds) || double.IsInfinity(value.Seconds) || value.Seconds <= 0) continue;
                    value.Profile.Validate(); bool matches = true;
                    foreach (string path in entry.Paths)
                    {
                        var file = new FileInfo(path); bool found = false;
                        foreach (var part in value.Files)
                        {
                            if (part == null || string.IsNullOrEmpty(part.Path) || Path.IsPathRooted(part.Path)) continue;
                            string full = Path.GetFullPath(Path.Combine(metadataRoot, part.Path.Replace('/', '\\')));
                            if (string.Equals(full, file.FullName, StringComparison.OrdinalIgnoreCase) && part.Length == file.Length && part.ModifiedUtcTicks == file.LastWriteTimeUtc.Ticks) found = true;
                        }
                        if (!found) matches = false;
                    }
                    if (matches) { entry.Verified = true; entry.Seconds = value.Seconds; entry.Format = value.WavOnly ? "WAV" : value.Profile.Label; }
                }
                catch (Exception) { }
            }
            // Older intentional WAV receipts remain supported without an index migration.
            foreach (var entry in entries.Values)
                if (!entry.Verified && entry.Format == "WAV")
                {
                    var group = new PendingSession { Stem = entry.Id, Folder = Path.GetDirectoryName(entry.Paths[0]) };
                    foreach (string path in entry.Paths)
                    { string name = Path.GetFileNameWithoutExtension(path); int number; if (int.TryParse(name.Substring(name.LastIndexOf('-') + 1), out number)) group.Parts[number] = path; }
                    if (group.Parts.Count == entry.Paths.Length && WavReceipt.IsCompleted(group, root))
                    { entry.Verified = true; double seconds = 0; foreach (string path in entry.Paths) seconds += DurableWave.ReadInfo(path, false).Duration; entry.Seconds = seconds; }
                }
            var list = new List<RecordingEntry>(entries.Values); list.Sort(delegate(RecordingEntry a, RecordingEntry b) { return b.Date.CompareTo(a.Date); }); return list;
        }
        private static void Scan(string directory, Dictionary<string, RecordingEntry> entries)
        {
            if (!Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return;
            foreach (string path in Directory.GetFiles(directory))
            {
                var file = new FileInfo(path);
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0 || file.Name.IndexOf(".partial", StringComparison.OrdinalIgnoreCase) >= 0 || file.Length == 0) continue;
                string ext = file.Extension.ToLowerInvariant(); if (ext != ".mkv" && ext != ".ogg" && ext != ".mp3" && ext != ".m4a" && ext != ".aac" && ext != ".wav") continue;
                string id = Path.GetFileNameWithoutExtension(path); Match match = WavPart.Match(file.Name);
                if (match.Success) id = match.Groups[1].Value;
                if (ext != ".wav" && Regex.IsMatch(id, @"-[a-f0-9]{12}-001$", RegexOptions.IgnoreCase)) id = id.Substring(0, id.Length - 4);
                // Include extension and folder in the key so unrelated formats never merge.
                string key = Path.Combine(directory, id + ext); RecordingEntry entry;
                if (!entries.TryGetValue(key, out entry))
                { entry = new RecordingEntry { Id = id, Format = ext.TrimStart('.').ToUpperInvariant(), Date = file.CreationTime, Paths = new string[0] }; entries.Add(key, entry); }
                var paths = new List<string>(entry.Paths); paths.Add(path); paths.Sort(StringComparer.OrdinalIgnoreCase); entry.Paths = paths.ToArray(); entry.Bytes += file.Length;
            }
            foreach (string child in Directory.GetDirectories(directory))
            {
                string name = Path.GetFileName(child);
                if (name.Equals("State", StringComparison.OrdinalIgnoreCase) || name.Equals("Backups", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("RecoveryWork", StringComparison.OrdinalIgnoreCase) || name.Equals("logs", StringComparison.OrdinalIgnoreCase)) continue;
                Scan(child, entries);
            }
        }
    }
}
