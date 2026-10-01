using System;
using System.IO;
using System.Media;

namespace SoundLeaf
{
    // Test-only digital silence keeps an otherwise idle shared audio engine producing packets.
    // It changes no system volume and contains no audible tone; never used by the application.
    public sealed class LiveAudioFixture : IDisposable
    {
        private readonly MemoryStream stream = new MemoryStream();
        private readonly SoundPlayer player;
        public LiveAudioFixture()
        {
            const int bytes = 48000 * 2 * 2;
            var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(bytes + 36);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)2); writer.Write(48000); writer.Write(48000 * 4);
            writer.Write((short)4); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            writer.Write(new byte[bytes]); writer.Flush(); stream.Position = 0;
            player = new SoundPlayer(stream); player.Load(); player.PlayLooping();
        }
        public void Dispose() { player.Stop(); player.Dispose(); stream.Dispose(); }
    }
}
