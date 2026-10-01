using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Globalization;

namespace SoundLeaf
{
    internal enum AudioOutputFormat { Mkv, Opus, Mp3, M4a, Aac }
    [DataContract]
    internal sealed class RecordingProfile
    {
        [DataMember] private AudioOutputFormat format;
        [DataMember] private int bitrate;
        internal AudioOutputFormat Format { get { return format; } }
        internal int Bitrate { get { return bitrate; } }
        internal static readonly int[] OpusRates = { 16, 24, 32, 48, 64, 96, 128 };
        internal static readonly int[] OtherRates = { 64, 96, 128, 160, 192, 256, 320 };
        internal RecordingProfile(AudioOutputFormat format, int bitrate)
        { this.format = format; this.bitrate = bitrate; Validate(); }
        internal static RecordingProfile Default { get { return new RecordingProfile(AudioOutputFormat.Mkv, 192); } }
        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(AudioOutputFormat), format) || Array.IndexOf(format == AudioOutputFormat.Opus ? OpusRates : OtherRates, bitrate) < 0)
                throw new InvalidDataException(TextCatalog.T("diagnostic.0"));
        }
        internal string Extension { get { return new[] { ".mkv", ".ogg", ".mp3", ".m4a", ".aac" }[(int)format]; } }
        internal string Codec { get { return format == AudioOutputFormat.Opus ? "libopus" : format == AudioOutputFormat.Mp3 ? "libmp3lame" : "aac"; } }
        internal string Label { get { return new[] { "MKV / AAC", "OGG / Opus", "MP3", "M4A / AAC", "AAC / ADTS" }[(int)format] + " · " + bitrate + " kbps" + (format == AudioOutputFormat.Opus ? " · mono" : ""); } }
        internal string Arguments(int channels)
        {
            string args = " -c:a " + Codec + " -b:a " + bitrate.ToString(CultureInfo.InvariantCulture) + "k -flush_packets 1";
            if (format == AudioOutputFormat.Opus) return args + " -ar 48000 -ac 1 -vbr on -application voip -frame_duration 20 -f ogg";
            if (format != AudioOutputFormat.Mkv) args += " -ar 48000 -ac " + Math.Min(2, channels);
            if (format != AudioOutputFormat.Mp3) args += " -profile:a aac_low";
            return args + (format == AudioOutputFormat.Mkv ? " -flush_packets 1 -cluster_time_limit 1000 -f matroska" :
                format == AudioOutputFormat.Mp3 ? " -f mp3" : format == AudioOutputFormat.M4a ? " -movflags +faststart -f ipod" : " -f adts");
        }
    }
    internal static class StartupOwnership
    {
        internal static bool Matches(string command, string executable)
        {
            return string.Equals(command, executable, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(command, "\"" + executable + "\"", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal static class AtomicJson
    {
        internal static void Write<T>(string path, T value, bool replace)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            for (string parent = directory; parent != null; parent = Path.GetDirectoryName(parent))
                if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException(TextCatalog.T("diagnostic.1"));
            Directory.CreateDirectory(directory);
            string temp = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); stream.Flush(true); }
                if (replace && File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        internal static T Read<T>(string path)
        {
            for (string parent = Path.GetDirectoryName(Path.GetFullPath(path)); parent != null; parent = Path.GetDirectoryName(parent))
                if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) throw new IOException(TextCatalog.T("diagnostic.2"));
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException(TextCatalog.T("diagnostic.3"));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > 1048576) throw new InvalidDataException(TextCatalog.T("diagnostic.4"));
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            }
        }
    }
    [DataContract]
    internal sealed class SoundLeafSettings
    {
        [DataMember] internal int Version = 1;
        [DataMember] internal AudioOutputFormat Format = AudioOutputFormat.Mkv;
        [DataMember] internal int[] Bitrates = { 192, 24, 192, 192, 192 };
        [DataMember] internal string Language = "en";
        [DataMember] internal string Theme = "system";
        [DataMember] internal string OutputFolder;
        [DataMember] internal string[] LibraryFolders = new string[0];
        internal string RecordingsFolder(string root) { return string.IsNullOrEmpty(OutputFolder) ? Path.Combine(root, "Recordings") : Path.GetFullPath(OutputFolder); }
        internal static string StorageRoot(string root, string folder)
        { return string.Equals(Path.GetFullPath(folder).TrimEnd('\\'), Path.Combine(Path.GetFullPath(root), "Recordings").TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ? root : folder; }
        internal RecordingProfile Profile { get { return new RecordingProfile(Format, Bitrates[(int)Format]); } }
        internal static SoundLeafSettings Load(string root, out bool restored)
        {
            string path = Path.Combine(root, "State", "settings.json"); restored = false;
            var defaults = new SoundLeafSettings();
            string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            defaults.Language = File.Exists(Path.Combine(root, "Player.exe")) || Directory.Exists(Path.Combine(root, "Recordings")) ? "ru" : language == "ru" || language == "de" ? language : "en";
            if (!File.Exists(path)) return defaults;
            try
            {
                var value = AtomicJson.Read<SoundLeafSettings>(path);
                if (value == null || value.Version != 1 || value.Bitrates == null || value.Bitrates.Length != 5 ||
                    (value.Language != "ru" && value.Language != "de" && value.Language != "en") ||
                    (value.Theme != "system" && value.Theme != "light" && value.Theme != "dark")) throw new InvalidDataException(TextCatalog.T("diagnostic.5"));
                for (int i = 0; i < 5; i++) new RecordingProfile((AudioOutputFormat)i, value.Bitrates[i]);
                if (value.OutputFolder != null && (!Path.IsPathRooted(value.OutputFolder) || value.OutputFolder.Length > 2048)) throw new InvalidDataException(TextCatalog.T("diagnostic.6"));
                if (value.OutputFolder != null) Path.GetFullPath(value.OutputFolder);
                if (value.LibraryFolders == null) value.LibraryFolders = new string[0];
                if (value.LibraryFolders.Length > 64) throw new InvalidDataException(TextCatalog.T("diagnostic.7"));
                foreach (string folder in value.LibraryFolders) { if (string.IsNullOrEmpty(folder) || !Path.IsPathRooted(folder)) throw new InvalidDataException(TextCatalog.T("diagnostic.8")); Path.GetFullPath(folder); }
                value.Profile.Validate(); return value;
            }
            catch (Exception) { restored = true; return defaults; }
        }
        internal void Save(string root) { AtomicJson.Write(Path.Combine(root, "State", "settings.json"), this, true); }
    }
    internal static class SessionProfile
    {
        internal static string PathFor(string root, string stem) { return Path.Combine(root, "State", "Sessions", stem + ".profile.json"); }
        internal static void Write(string root, string stem, RecordingProfile profile) { AtomicJson.Write(PathFor(root, stem), profile, false); }
        internal static RecordingProfile Read(string root, string stem)
        {
            string path = PathFor(root, stem);
            if (!File.Exists(path)) return RecordingProfile.Default;
            var profile = AtomicJson.Read<RecordingProfile>(path); profile.Validate(); return profile;
        }
    }
}
