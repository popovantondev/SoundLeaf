using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SoundLeaf
{
    internal static class BackupCleanup
    {
        internal sealed class Result
        {
            internal int DeletedFolders;
            internal long DeletedBytes;
            internal int SkippedFolders;
            internal readonly List<string> Errors = new List<string>();
        }

        internal static Result RemoveOldRecordingParts(string playerRoot, DateTime nowUtc, TimeSpan maxAge)
        {
            var result = new Result();
            string root = Path.GetFullPath(Path.Combine(playerRoot, "Backups", "RecordingParts"));
            if (!Directory.Exists(root)) return result;
            DirectoryInfo parent = new DirectoryInfo(root);
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) return result;
            DateTime cutoff = nowUtc - maxAge;
            DirectoryInfo[] folders;
            try { folders = parent.GetDirectories(); }
            catch (Exception error) { result.Errors.Add(error.Message); return result; }
            foreach (DirectoryInfo folder in folders)
            {
                if ((folder.Attributes & FileAttributes.ReparsePoint) != 0) { result.SkippedFolders++; continue; }
                try
                {
                    FileInfo[] files = folder.GetFiles("*", SearchOption.AllDirectories);
                    DateTime newest = folder.LastWriteTimeUtc;
                    bool protectedFolder = false;
                    long bytes = 0;
                    foreach (FileInfo file in files)
                    {
                        if ((file.Attributes & FileAttributes.ReparsePoint) != 0) { protectedFolder = true; break; }
                        if (file.LastWriteTimeUtc > newest) newest = file.LastWriteTimeUtc;
                        if (file.Name.EndsWith(".partial", StringComparison.OrdinalIgnoreCase) ||
                            file.Name.IndexOf(".partial.", StringComparison.OrdinalIgnoreCase) >= 0)
                            protectedFolder = true;
                        bytes += file.Length;
                    }
                    if (protectedFolder || newest >= cutoff || folder.LastWriteTimeUtc >= cutoff)
                    { result.SkippedFolders++; continue; }
                    if (folder.LastWriteTimeUtc >= cutoff) { result.SkippedFolders++; continue; }
                    Directory.Delete(folder.FullName, true);
                    result.DeletedFolders++;
                    result.DeletedBytes += bytes;
                }
                catch (Exception error) { result.Errors.Add(folder.FullName + ": " + error.Message); }
            }
            return result;
        }
    }

    internal sealed class WaveFormat
    {
        internal readonly byte[] Bytes;
        internal readonly int SampleRate;
        internal readonly int Channels;
        internal readonly int BlockAlign;
        internal readonly int Bits;
        internal readonly bool Float;
        internal WaveFormat(byte[] bytes)
        {
            if (bytes.Length < 16) throw new InvalidDataException(TextCatalog.T("diagnostic.18"));
            Bytes = (byte[])bytes.Clone();
            int tag = BitConverter.ToUInt16(bytes, 0);
            Channels = BitConverter.ToUInt16(bytes, 2);
            SampleRate = BitConverter.ToInt32(bytes, 4);
            BlockAlign = BitConverter.ToUInt16(bytes, 12);
            Bits = BitConverter.ToUInt16(bytes, 14);
            if (tag == 65534)
            {
                if (bytes.Length < 40 || BitConverter.ToUInt16(bytes, 16) < 22)
                    throw new InvalidDataException(TextCatalog.T("diagnostic.19"));
                byte[] guid = new byte[16];
                Buffer.BlockCopy(bytes, 24, guid, 0, 16);
                Guid sub = new Guid(guid);
                if (sub == new Guid("00000001-0000-0010-8000-00aa00389b71")) tag = 1;
                else if (sub == new Guid("00000003-0000-0010-8000-00aa00389b71")) tag = 3;
                else throw new InvalidDataException(TextCatalog.T("diagnostic.20"));
            }
            if ((tag != 1 && tag != 3) || Channels < 1 || Channels > 32 ||
                SampleRate < 8000 || SampleRate > 384000 ||
                (Bits != 16 && Bits != 24 && Bits != 32) ||
                (tag == 3 && Bits != 32) || BlockAlign != Channels * Bits / 8 ||
                BitConverter.ToInt32(bytes, 8) != SampleRate * BlockAlign)
                throw new InvalidDataException(TextCatalog.T("diagnostic.21"));
            Float = tag == 3;
        }
    }

    internal sealed class WaveInfo
    {
        internal WaveFormat Format;
        internal long DataOffset, DataBytes, SizeOffset, FactOffset;
        internal double Duration { get { return DataBytes / (double)Format.BlockAlign / Format.SampleRate; } }
    }

    internal sealed class DurableWave : IDisposable
    {
        private FileStream stream;
        private readonly long factOffset, sizeOffset, dataOffset;
        internal readonly WaveFormat Format;
        internal readonly string PartialPath;
        internal long Frames { get; private set; }
        internal DurableWave(string partial, WaveFormat format)
        {
            PartialPath = partial;
            Format = format;
            stream = new FileStream(partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read,
                65536, FileOptions.SequentialScan);
            try
            {
                using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(0u);
                    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write((uint)format.Bytes.Length); writer.Write(format.Bytes);
                    if ((format.Bytes.Length & 1) != 0) writer.Write((byte)0);
                    writer.Write(Encoding.ASCII.GetBytes("fact")); writer.Write(4u);
                    factOffset = stream.Position; writer.Write(0u);
                    writer.Write(Encoding.ASCII.GetBytes("data"));
                    sizeOffset = stream.Position; writer.Write(0u);
                    dataOffset = stream.Position;
                }
                Checkpoint(); // A real, flushed WAV header exists before audio starts.
            }
            catch { stream.Dispose(); stream = null; throw; }
        }
        internal void Write(byte[] bytes, int offset, int count)
        {
            if (count % Format.BlockAlign != 0) throw new InvalidDataException(TextCatalog.T("diagnostic.22"));
            if (dataOffset + (Frames * Format.BlockAlign) + count > uint.MaxValue)
                throw new IOException(TextCatalog.T("diagnostic.23"));
            stream.Write(bytes, offset, count);
            Frames += count / Format.BlockAlign;
        }
        internal void Checkpoint()
        {
            long end = stream.Position;
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                stream.Position = 4; writer.Write(checked((uint)(dataOffset + Frames * Format.BlockAlign - 8)));
                stream.Position = factOffset; writer.Write(checked((uint)Frames));
                stream.Position = sizeOffset; writer.Write(checked((uint)(Frames * Format.BlockAlign)));
            }
            stream.Position = end;
            stream.Flush(true);
        }
        internal string Complete()
        {
            Checkpoint();
            Dispose();
            ReadInfo(PartialPath, false);
            if (Frames == 0) throw new IOException(TextCatalog.T("diagnostic.35"));
            string final = PartialPath.Substring(0, PartialPath.Length - ".partial.wav".Length) + ".wav";
            File.Move(PartialPath, final); // Never overwrite, including rename races.
            return final;
        }
        public void Dispose()
        {
            if (stream != null) { stream.Dispose(); stream = null; }
        }
        internal static WaveInfo ReadInfo(string path, bool recovery)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file, Encoding.ASCII))
            {
                if (file.Length < 44 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
                    throw new InvalidDataException(TextCatalog.T("diagnostic.24"));
                long riffLength = reader.ReadUInt32() + 8L;
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
                    throw new InvalidDataException(TextCatalog.T("diagnostic.25"));
                var info = new WaveInfo();
                info.FactOffset = -1;
                while (file.Position + 8 <= file.Length)
                {
                    string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    long sizePosition = file.Position;
                    long size = reader.ReadUInt32();
                    long begin = file.Position;
                    if (id == "data")
                    {
                        if (info.Format == null) throw new InvalidDataException(TextCatalog.T("diagnostic.26"));
                        info.SizeOffset = sizePosition;
                        info.DataOffset = begin;
                        info.DataBytes = recovery ? file.Length - begin : size;
                        info.DataBytes -= recovery ? info.DataBytes % info.Format.BlockAlign : 0;
                        if (info.DataBytes <= 0 || info.DataBytes % info.Format.BlockAlign != 0 ||
                            begin + info.DataBytes > file.Length ||
                            (!recovery && (riffLength != file.Length || begin + size != file.Length)))
                            throw new InvalidDataException(TextCatalog.T("diagnostic.27"));
                        return info;
                    }
                    if (size > file.Length - begin || size > 1048576)
                        throw new InvalidDataException(TextCatalog.T("diagnostic.28"));
                    if (id == "fmt ") info.Format = new WaveFormat(reader.ReadBytes((int)size));
                    if (id == "fact" && size >= 4) info.FactOffset = begin;
                    file.Position = begin + size + (size & 1);
                }
                throw new InvalidDataException(TextCatalog.T("diagnostic.29"));
            }
        }
        internal static string RecoverCopy(string source)
        {
            // Source is never opened for writing. FileShare.Read rejects active writers.
            var info = ReadInfo(source, true);
            string name = Path.Combine(Path.GetDirectoryName(source),
                "Recovered-" + Guid.NewGuid().ToString("N") + ".partial.wav");
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new DurableWave(name, info.Format))
            {
                input.Position = info.DataOffset;
                long left = info.DataBytes;
                byte[] buffer = new byte[info.Format.BlockAlign * 8192];
                while (left > 0)
                {
                    int needed = (int)Math.Min(left, buffer.Length);
                    int read = 0;
                    while (read < needed)
                    {
                        int got = input.Read(buffer, read, needed - read);
                        if (got == 0) throw new EndOfStreamException();
                        read += got;
                    }
                    output.Write(buffer, 0, read);
                    left -= read;
                }
                return output.Complete();
            }
        }
    }

    internal sealed class SegmentedWave : IDisposable
    {
        internal readonly List<string> Completed = new List<string>();
        private readonly string session;
        private readonly WaveFormat format;
        private readonly long maxFrames;
        private DurableWave current;
        private int part;
        private readonly byte[] silence;
        private readonly string ffmpeg;
        private readonly RecordingProfile profile;
        internal Action<byte[], int, int> PcmWritten;
        internal string SessionId { get { return Path.GetFileName(session); } }
        private LiveMkv live;
        internal readonly List<LiveMkv> Encoders = new List<LiveMkv>();
        internal long Frames { get; private set; }
        internal string CurrentPath { get { return current == null ? null : current.PartialPath; } }
        internal SegmentedWave(string folder, WaveFormat format, long maxBytes, string ffmpeg = null, RecordingProfile profile = null, string sessionId = null)
        {
            Directory.CreateDirectory(folder);
            this.format = format;
            this.ffmpeg = ffmpeg;
            this.profile = profile;
            maxFrames = Math.Max(1, maxBytes / format.BlockAlign);
            session = Path.Combine(folder, sessionId ?? (DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") +
                "-" + Guid.NewGuid().ToString("N").Substring(0, 12)));
            silence = new byte[format.BlockAlign * 8192];
            OpenPart();
        }
        private void OpenPart()
        {
            current = new DurableWave(session + "-" + (++part).ToString("D3") + ".partial.wav", format);
            if (ffmpeg != null)
            {
                live = new LiveMkv(current.PartialPath, format, ffmpeg, profile);
                Encoders.Add(live);
            }
        }
        internal void Write(byte[] data, int offset, int count)
        {
            if (count % format.BlockAlign != 0) throw new InvalidDataException(TextCatalog.T("diagnostic.30"));
            while (count > 0)
            {
                if (current == null) OpenPart();
                int bytes = (int)Math.Min(count, (maxFrames - current.Frames) * format.BlockAlign);
                current.Write(data, offset, bytes);
                if (PcmWritten != null) PcmWritten(data, offset, bytes);
                if (live != null) live.Enqueue(data, offset, bytes);
                Frames += bytes / format.BlockAlign;
                count -= bytes; offset += bytes;
                if (current.Frames == maxFrames)
                {
                    if (live != null) { live.CompleteInput(); live = null; }
                    Completed.Add(current.Complete()); current = null;
                }
            }
        }
        internal void SilenceTo(long frame)
        {
            while (Frames < frame)
            {
                int bytes = (int)Math.Min(frame - Frames, silence.Length / format.BlockAlign) * format.BlockAlign;
                Write(silence, 0, bytes);
            }
        }
        internal void Checkpoint() { if (current != null) current.Checkpoint(); }
        internal void Complete()
        {
            if (live != null) { live.CompleteInput(); live = null; }
            if (current != null && current.Frames > 0)
            {
                Completed.Add(current.Complete()); current = null;
            }
        }
        public void Dispose()
        {
            if (current != null) { current.Dispose(); current = null; }
            foreach (var encoder in Encoders) encoder.Dispose();
        }
    }

    internal sealed class AudioTimeline
    {
        private readonly SegmentedWave sink;
        private readonly WaveFormat format;
        private long origin, baseFrame;
        private bool receivedPacket;
        internal bool HeardSignal { get; private set; }
        internal bool Discontinuity { get; private set; }
        internal AudioTimeline(SegmentedWave sink, WaveFormat format) { this.sink = sink; this.format = format; }
        internal void Resume(long qpc) { origin = qpc; baseFrame = sink.Frames; receivedPacket = false; }
        internal long FrameAt(long qpc)
        { return baseFrame + Math.Max(0, checked((qpc - origin) * format.SampleRate) / 10000000L); }
        internal void FillThrough(long qpc) { sink.SilenceTo(FrameAt(qpc)); }
        internal void Packet(byte[] bytes, int frames, ulong qpc, uint flags)
        {
            long start = (flags & 4) != 0 ? sink.Frames : FrameAt((long)qpc);
            // QPC-to-frame rounding and clock drift must not splice a continuous stream.
            // Keep every captured sample. Only use timestamps to preserve substantial gaps.
            if (receivedPacket && Math.Abs(start - sink.Frames) <= format.SampleRate / 50)
                start = sink.Frames;
            // The first packet after Start/Reset has no predecessor to be continuous with.
            if ((flags & 5) != 0 && receivedPacket) Discontinuity = true;
            receivedPacket = true;
            if (start > sink.Frames) sink.SilenceTo(start);
            int skip = 0;
            int offset = skip * format.BlockAlign;
            if (!HeardSignal)
            {
                if (format.Float)
                {
                    for (int i = offset; i + 4 <= bytes.Length; i += 4)
                        if (Math.Abs(BitConverter.ToSingle(bytes, i)) > 0.0001f) { HeardSignal = true; break; }
                }
                else for (int i = offset; i < bytes.Length; i++)
                    if (bytes[i] != 0) { HeardSignal = true; break; }
            }
            sink.Write(bytes, offset, (frames - skip) * format.BlockAlign);
            if ((flags & 4) == 0)
            {
                origin = (long)qpc;
                baseFrame = sink.Frames - frames;
            }
        }
    }
}
