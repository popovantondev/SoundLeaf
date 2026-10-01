using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Player
{
    [DataContract]
    internal sealed class WavReceiptData
    {
        [DataMember] internal string SessionId;
        [DataMember] internal string Mode;
        [DataMember] internal bool Completed;
        [DataMember] internal List<WavReceiptPart> Parts;
    }
    [DataContract]
    internal sealed class WavReceiptPart
    {
        [DataMember] internal string Path;
        [DataMember] internal long Length;
        [DataMember] internal long ModifiedUtcTicks;
    }
    internal static class WavReceipt
    {
        internal static void Write(string root, IList<string> wavs)
        {
            if (wavs.Count == 0) throw new IOException("Нет WAV для фиксации результата.");
            string name = Path.GetFileNameWithoutExtension(wavs[0]);
            int dash = name.LastIndexOf('-');
            if (dash < 0) throw new InvalidDataException("Неверный идентификатор WAV-сессии.");
            var receipt = new WavReceiptData { SessionId = name.Substring(0, dash), Mode = "WavOnly",
                Completed = true, Parts = new List<WavReceiptPart>() };
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (string wav in wavs)
            {
                string full = Path.GetFullPath(wav);
                if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("WAV вне папки приложения.");
                DurableWave.ReadInfo(full, false);
                var file = new FileInfo(full);
                receipt.Parts.Add(new WavReceiptPart { Path = full.Substring(prefix.Length).Replace('\\', '/'),
                    Length = file.Length, ModifiedUtcTicks = file.LastWriteTimeUtc.Ticks });
            }
            string directory = Path.Combine(root, "State", "Sessions");
            string stateDirectory = Path.Combine(root, "State");
            if (Directory.Exists(stateDirectory) && (File.GetAttributes(stateDirectory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Папка состояния не должна быть ссылкой.");
            Directory.CreateDirectory(directory);
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Папка состояния не должна быть ссылкой.");
            string target = Path.Combine(directory, receipt.SessionId + ".json");
            string temp = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { new DataContractJsonSerializer(typeof(WavReceiptData)).WriteObject(file, receipt); file.Flush(true); }
                File.Move(temp, target); // Publish the completion marker only after every WAV was validated.
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        internal static bool IsCompleted(PendingSession group, string root)
        {
            if (root == null || group.Duplicate) return false;
            string path = Path.Combine(root, "State", "Sessions", group.Stem + ".json");
            if (!File.Exists(path)) return false;
            try
            {
                if ((File.GetAttributes(Path.Combine(root, "State")) & FileAttributes.ReparsePoint) != 0 ||
                    (File.GetAttributes(Path.GetDirectoryName(path)) & FileAttributes.ReparsePoint) != 0) return false;
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return false;
                WavReceiptData receipt;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > 1048576) return false;
                    receipt = (WavReceiptData)new DataContractJsonSerializer(typeof(WavReceiptData)).ReadObject(stream);
                }
                if (receipt == null || receipt.SessionId != group.Stem || receipt.Mode != "WavOnly" ||
                    !receipt.Completed || receipt.Parts == null || receipt.Parts.Count != group.Parts.Count) return false;
                string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                int index = 0, number = 1;
                foreach (var item in group.Parts)
                {
                    if (item.Key != number++) return false;
                    if (item.Value.EndsWith(".partial.wav", StringComparison.OrdinalIgnoreCase)) return false;
                    var part = receipt.Parts[index++];
                    if (part == null || string.IsNullOrEmpty(part.Path) || Path.IsPathRooted(part.Path)) return false;
                    string full = Path.GetFullPath(Path.Combine(root, part.Path.Replace('/', Path.DirectorySeparatorChar)));
                    if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(full, Path.GetFullPath(item.Value), StringComparison.OrdinalIgnoreCase)) return false;
                    var file = new FileInfo(full);
                    if (file.Length != part.Length || file.LastWriteTimeUtc.Ticks != part.ModifiedUtcTicks) return false;
                    DurableWave.ReadInfo(full, false);
                }
                return true;
            }
            catch (Exception) { return false; } // Malformed/incomplete markers never hide pending audio.
        }
    }
}
