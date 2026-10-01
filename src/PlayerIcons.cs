using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Player
{
    // Vector artwork shared by the executable resource and every tray state.
    public static class PlayerIcons
    {
        public const int FrameCount = 24;
        private static readonly int[] Sizes = { 16, 20, 24, 32, 48, 64, 128, 256 };

        public static Bitmap Render(int state, int size, int frame)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform(size / 64f, size / 64f);
                Color light = state == 1 ? Color.FromArgb(255, 224, 89) :
                    state == 2 ? Color.FromArgb(68, 76, 83) :
                    state == 3 ? Color.FromArgb(255, 105, 104) : Color.FromArgb(99, 242, 171);
                Color dark = state == 1 ? Color.FromArgb(239, 166, 20) :
                    state == 2 ? Color.FromArgb(10, 16, 22) :
                    state == 3 ? Color.FromArgb(210, 34, 61) : Color.FromArgb(0, 155, 99);
                using (var fill = new LinearGradientBrush(new Point(12, 8), new Point(45, 57), light, dark))
                using (var edge = new Pen(Color.FromArgb(230, 14, 55, 41), 2))
                using (var rim = new Pen(Color.FromArgb(225, 255, 255, 255), 4))
                using (var shape = new GraphicsPath())
                {
                    if (state == 0 || state == 4)
                    {
                        float inset = state == 4 ? 11 : 0;
                        shape.AddLines(new[] { new PointF(17 + inset / 2, 9 + inset),
                            new PointF(54 - inset, 30), new PointF(54 - inset, 34),
                            new PointF(17 + inset / 2, 55 - inset), new PointF(13 + inset / 2, 53 - inset),
                            new PointF(13 + inset / 2, 11 + inset) });
                        shape.CloseFigure();
                    }
                    else if (state == 1)
                    {
                        shape.AddRectangle(new RectangleF(14, 11, 12, 42));
                        shape.AddRectangle(new RectangleF(38, 11, 12, 42));
                    }
                    else if (state == 2) shape.AddRectangle(new RectangleF(13, 13, 38, 38));
                    else shape.AddEllipse(8, 8, 48, 48);
                    rim.LineJoin = edge.LineJoin = LineJoin.Round;
                    g.DrawPath(rim, shape);
                    g.FillPath(fill, shape);
                    if (state == 2) edge.Color = Color.FromArgb(10, 16, 22);
                    if (state == 3) edge.Color = Color.FromArgb(137, 22, 41);
                    g.DrawPath(edge, shape);
                    if (state == 3)
                    {
                        using (var mark = new Pen(Color.White, 5))
                        {
                            mark.StartCap = mark.EndCap = LineCap.Round;
                            g.DrawLine(mark, 32, 21, 32, 34);
                        }
                        g.FillEllipse(Brushes.White, 29.5f, 40, 5, 5);
                    }
                    if (state == 4)
                    {
                        using (var track = new Pen(Color.FromArgb(100, 124, 145, 138), 5))
                        using (var arc = new Pen(Color.FromArgb(25, 204, 129), 5))
                        using (var outline = new Pen(Color.FromArgb(215, 255, 255, 255), 8))
                        {
                            arc.StartCap = arc.EndCap = LineCap.Round;
                            outline.StartCap = outline.EndCap = LineCap.Round;
                            g.DrawEllipse(track, 6, 6, 52, 52);
                            float angle = -90 + frame * 360f / FrameCount;
                            g.DrawArc(outline, 6, 6, 52, 52, angle, 110);
                            g.DrawArc(arc, 6, 6, 52, 52, angle, 110);
                        }
                    }
                }
            }
            return bitmap;
        }

        public static byte[] IcoBytes(int state, int frame)
        {
            var images = new byte[Sizes.Length][];
            for (int i = 0; i < Sizes.Length; i++)
                using (var bitmap = Render(state, Sizes[i], frame))
                using (var png = new MemoryStream())
                { bitmap.Save(png, ImageFormat.Png); images[i] = png.ToArray(); }
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)Sizes.Length);
                int offset = 6 + 16 * Sizes.Length;
                for (int i = 0; i < Sizes.Length; i++)
                {
                    writer.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i]));
                    writer.Write((byte)(Sizes[i] == 256 ? 0 : Sizes[i]));
                    writer.Write((byte)0); writer.Write((byte)0);
                    writer.Write((ushort)1); writer.Write((ushort)32);
                    writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
                }
                foreach (byte[] image in images) writer.Write(image);
                return stream.ToArray();
            }
        }

        public static Icon Create(int state, int frame)
        {
            using (var stream = new MemoryStream(IcoBytes(state, frame)))
            using (var icon = new Icon(stream, 32, 32)) return (Icon)icon.Clone();
        }

        public static void Export(string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "Player.ico"), IcoBytes(0, 0));
            using (var sheet = new Bitmap(640, 256))
            using (var g = Graphics.FromImage(sheet))
            using (var font = new Font("Segoe UI", 10))
            {
                g.Clear(Color.FromArgb(245, 247, 249));
                string[] labels = { "Recording", "Paused", "Stopped", "Error", "Saving" };
                for (int state = 0; state < 5; state++)
                {
                    using (var icon = Render(state, 96, 0)) g.DrawImageUnscaled(icon, state * 128 + 16, 8);
                    g.DrawString(labels[state], font, Brushes.Black, state * 128 + 22, 110);
                    g.FillRectangle(Brushes.White, state * 128, 144, 128, 48);
                    g.FillRectangle(Brushes.DarkSlateGray, state * 128, 200, 128, 48);
                    foreach (int size in new[] { 16, 24, 32 })
                        using (var icon = Render(state, size, 0))
                        {
                            int x = state * 128 + (size == 16 ? 12 : size == 24 ? 44 : 82);
                            g.DrawImageUnscaled(icon, x, 152);
                            g.DrawImageUnscaled(icon, x, 208);
                        }
                }
                sheet.Save(Path.Combine(folder, "tray-preview.png"), ImageFormat.Png);
            }
        }
    }
}
