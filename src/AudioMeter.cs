using System;

namespace SoundLeaf
{
    // Observation only: never changes or buffers the PCM passed to durable storage.
    internal sealed class AudioMeter
    {
        internal const long Freshness100ns = 2500000;
        private readonly WaveFormat format;
        private long observed;
        private float peak, rms;
        internal AudioMeter(WaveFormat format) { this.format = format; }
        internal void Reset() { peak = rms = 0; observed = 0; }
        internal float Peak(long now) { return observed != 0 && now >= observed && now - observed <= Freshness100ns ? peak : 0; }
        internal float Rms(long now) { return observed != 0 && now >= observed && now - observed <= Freshness100ns ? rms : 0; }
        internal void Observe(byte[] bytes, int frames, uint flags, long now)
        {
            observed = now; peak = rms = 0;
            if ((flags & 2) != 0 || frames <= 0) return;
            int count = checked(frames * format.BlockAlign);
            if (bytes == null || bytes.Length < count) throw new ArgumentException("Incomplete PCM meter packet.");
            int stride = format.Bits / 8, samples = count / stride;
            double energy = 0, maximum = 0;
            for (int i = 0; i < count; i += stride)
            {
                double value;
                if (format.Float) value = BitConverter.ToSingle(bytes, i);
                else if (format.Bits == 16) value = BitConverter.ToInt16(bytes, i) / 32768.0;
                else if (format.Bits == 24)
                {
                    int signed = bytes[i] | bytes[i + 1] << 8 | bytes[i + 2] << 16;
                    if ((signed & 0x800000) != 0) signed |= unchecked((int)0xFF000000);
                    value = signed / 8388608.0;
                }
                else value = BitConverter.ToInt32(bytes, i) / 2147483648.0;
                if (double.IsNaN(value) || double.IsInfinity(value)) continue;
                value = Math.Min(1, Math.Abs(value));
                maximum = Math.Max(maximum, value); energy += value * value;
            }
            // Suppress numerical noise, not the audio being recorded.
            peak = maximum >= .0001 ? (float)maximum : 0;
            rms = peak == 0 ? 0 : (float)Math.Sqrt(energy / Math.Max(1, samples));
        }
    }
}
