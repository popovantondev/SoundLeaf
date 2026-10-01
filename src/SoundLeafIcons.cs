using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace SoundLeaf
{
    // Vector artwork shared by the executable resource and every tray state.
    public static class SoundLeafIcons
    {
        public const int FrameCount = 24;
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
        private static readonly int[] Sizes = { 16, 20, 24, 32, 48, 64, 128, 256 };
        public static Bitmap Brand(int size)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            using (var leaf = new GraphicsPath())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.ScaleTransform(size / 64f, size / 64f);
                // A curved stem, uneven silhouette and translucent veins retain a natural small-size shape.
                leaf.AddBezier(12, 49, 5, 38, 8, 23, 19, 17);
                leaf.AddBezier(19, 17, 31, 10, 42, 12, 56, 5);
                leaf.AddBezier(56, 5, 54, 18, 58, 28, 48, 41);
                leaf.AddBezier(48, 41, 39, 52, 23, 57, 12, 49); leaf.CloseFigure();
                if (size > 48) using (var shadow = new SolidBrush(Color.FromArgb(32, 7, 49, 24)))
                { g.TranslateTransform(1, 1.5f); g.FillPath(shadow, leaf); g.TranslateTransform(-1, -1.5f); }
                using (var fill = new PathGradientBrush(leaf))
                {
                    fill.CenterPoint = new PointF(28, 29); fill.CenterColor = Color.FromArgb(111, 179, 61);
                    fill.SurroundColors = new[] { Color.FromArgb(28, 99, 40) }; g.FillPath(fill, leaf);
                }
                var saved = g.Save(); g.SetClip(leaf);
                using (var highlight = new LinearGradientBrush(new PointF(14, 13), new PointF(45, 50), Color.FromArgb(100, 206, 233, 133), Color.FromArgb(0, 68, 153, 45)))
                    g.FillRectangle(highlight, 4, 4, 56, 56);
                using (var shade = new GraphicsPath())
                using (var brush = new SolidBrush(Color.FromArgb(46, 11, 69, 32)))
                { shade.AddBezier(11, 51, 31, 42, 39, 23, 56, 5); shade.AddLine(56, 5, 63, 60); shade.AddLine(63, 60, 11, 60); shade.CloseFigure(); g.FillPath(brush, shade); }
                using (var veins = new Pen(Color.FromArgb(size <= 48 ? 210 : 150, 178, 205, 115), size <= 48 ? 48f / size : .8f))
                {
                    veins.StartCap = veins.EndCap = LineCap.Round;
                    g.DrawBezier(veins, 12, 50, 29, 41, 40, 20, 54, 8);
                    if (size >= 24)
                    {
                        g.DrawBezier(veins, 21, 44, 18, 35, 15, 29, 18, 22);
                        g.DrawBezier(veins, 22, 43, 31, 45, 39, 43, 47, 39);
                        g.DrawBezier(veins, 32, 31, 40, 32, 46, 30, 52, 26);
                        if (size >= 32) g.DrawBezier(veins, 29, 35, 24, 27, 23, 23, 25, 18);
                        if (size > 48)
                        {
                            g.DrawBezier(veins, 38, 24, 33, 20, 32, 17, 33, 14);
                            g.DrawBezier(veins, 42, 19, 47, 20, 51, 17, 54, 14);
                        }
                    }
                    if (size >= 64)
                    {
                        veins.Color = Color.FromArgb(45, 180, 213, 124); veins.Width = .35f;
                        for (int i = 0; i < 4; i++) { g.DrawLine(veins, 18 + i * 5, 36 - i * 5, 12 + i * 6, 30 - i * 5); g.DrawLine(veins, 29 + i * 5, 42 - i * 5, 31 + i * 6, 48 - i * 5); }
                    }
                }
                g.Restore(saved);
                if (size <= 48) using (var edge = new Pen(Color.FromArgb(170, 23, 84, 35), 35.2f / size)) g.DrawPath(edge, leaf);
                using (var stem = new Pen(Color.FromArgb(110, 121, 45), size <= 48 ? 48f / size : 1.3f))
                { stem.StartCap = stem.EndCap = LineCap.Round; g.DrawBezier(stem, 7, 57, 9, 53, 13, 49, 20, 45); }
            }
            return bitmap;
        }

        public static Icon CreateBrand(int size)
        {
            if (size < 8 || size > 256) throw new ArgumentOutOfRangeException("size");
            using (var bitmap = Brand(size))
            {
                IntPtr handle = bitmap.GetHicon();
                try { using (var native = Icon.FromHandle(handle)) return (Icon)native.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }

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
                    if (state == 4)
                    {
                        shape.AddLines(new[] { new PointF(23, 20), new PointF(44, 30),
                            new PointF(44, 34), new PointF(23, 44), new PointF(20, 42), new PointF(20, 22) });
                        shape.CloseFigure();
                    }
                    else if (state == 0)
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
                        using (var track = new Pen(Color.FromArgb(100, 124, 145, 138), 7.5f))
                        using (var arc = new Pen(Color.FromArgb(25, 204, 129), 7.5f))
                        using (var outline = new Pen(Color.FromArgb(215, 255, 255, 255), 10.5f))
                        {
                            arc.StartCap = arc.EndCap = LineCap.Round;
                            outline.StartCap = outline.EndCap = LineCap.Round;
                            g.DrawEllipse(track, 7, 7, 50, 50);
                            float angle = -90 + frame * 360f / FrameCount;
                            g.DrawArc(outline, 7, 7, 50, 50, angle, 110);
                            g.DrawArc(arc, 7, 7, 50, 50, angle, 110);
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
                using (var bitmap = state == 5 ? Brand(Sizes[i]) : Render(state, Sizes[i], frame))
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
            File.WriteAllBytes(Path.Combine(folder, "SoundLeaf.ico"), IcoBytes(5, 0));
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
