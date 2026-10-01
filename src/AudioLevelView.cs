using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SoundLeaf
{
    internal sealed class AudioLevelView : Control
    {
        internal const float Expressiveness = .95f;
        private readonly float[] history = new float[25];
        private readonly float[] displayed = new float[25];
        private readonly Timer timer = new Timer { Interval = 33 };
        private readonly Stopwatch freshness = new Stopwatch();
        private float level;
        private bool active;
        private bool seeded;
        private string caption = "";
        internal bool Mini, Dark;
        internal float Scale = 1;
        internal bool HasCurrentSignal { get { return active && level > 0 && freshness.IsRunning && freshness.ElapsedMilliseconds <= 250; } }
        internal bool TimerRunning { get { return timer.Enabled; } }
        internal AudioLevelView()
        {
            DoubleBuffered = true; TabStop = false; AccessibleRole = AccessibleRole.Graphic;
            timer.Tick += delegate
            {
                if (!HasCurrentSignal)
                {
                    Array.Clear(history, 0, history.Length); Array.Clear(displayed, 0, displayed.Length); timer.Stop();
                    if (active) { caption = TextCatalog.T("meterQuiet"); AccessibleName = TextCatalog.T("meterLabel") + ": " + caption; }
                }
                else for (int i = 0; i < history.Length; i++) displayed[i] += (history[i] - displayed[i]) * .4f;
                Invalidate();
            };
        }
        internal void SetLevel(SessionUpdate update)
        {
            active = update.State == RecordState.Recording && update.DeviceAvailable;
            level = active ? Math.Max(update.Rms, update.Peak * .35f) : 0;
            if (float.IsNaN(level) || float.IsInfinity(level)) level = 0;
            level = Math.Max(0, Math.Min(1, level));
            freshness.Restart();
            for (int i = 0; i < history.Length - 1; i++) history[i] = history[i + 1];
            history[history.Length - 1] = level > 0 ? (float)Math.Max(0, Math.Min(1, (20 * Math.Log10(Math.Max(.0001, level)) + 80) / 80)) * Expressiveness : 0;
            if (level == 0) { Array.Clear(history, 0, history.Length); Array.Clear(displayed, 0, displayed.Length); }
            caption = TextCatalog.T(update.State == RecordState.Paused ? "meterPaused" : update.State == RecordState.Saving ? "meterSaving" :
                update.State == RecordState.Stopped || update.State == RecordState.Faulted ? "meterStopped" : level > 0 ? "meterSound" : "meterQuiet");
            AccessibleName = TextCatalog.T("meterLabel") + ": " + caption;
            if (HasCurrentSignal && Visible) timer.Start(); else timer.Stop();
            Invalidate();
        }
        internal static float DisplayLevel(SessionUpdate update)
        {
            if (update.State != RecordState.Recording || !update.DeviceAvailable) return 0;
            float value = Math.Max(update.Rms, update.Peak * .35f);
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) return 0;
            return (float)Math.Max(0, Math.Min(1, (20 * Math.Log10(Math.Max(.0001, value)) + 80) / 80)) * Expressiveness;
        }
        internal void SeedHistory(float[] values) { Array.Copy(values, history, Math.Min(values.Length, history.Length)); if (!seeded) { Array.Copy(history, displayed, history.Length); seeded = true; } }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible && HasCurrentSignal) timer.Start(); else timer.Stop(); }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (!Mini) using (var shape = LeafDrawing.Rounded(new RectangleF(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 12 * Scale))
                using (var brush = new SolidBrush(BackColor)) g.FillPath(brush, shape);
            Color ink = ColorTranslator.FromHtml(Dark ? "#74C991" : "#278C55");
            float margin = Mini ? 3 * Scale : 16 * Scale, captionHeight = Mini ? 0 : 22 * Scale;
            var plot = new RectangleF(margin, 5 * Scale, Math.Max(1, Width - margin * 2), Math.Max(1, Height - captionHeight - 10 * Scale));
            float middle = plot.Top + plot.Height / 2;
            if (!HasCurrentSignal)
            {
                using (var pen = new Pen(Color.FromArgb(130, ink), Math.Max(1, 1.4f * Scale))) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLine(pen, plot.Left, middle, plot.Right, middle); }
            }
            else
            {
                float slot = plot.Width / history.Length, width = Math.Max(1, Math.Min(5 * Scale, slot * .48f));
                using (var brush = new SolidBrush(ink))
                    for (int i = 0; i < history.Length; i++)
                    {
                        if (displayed[i] <= 0) continue;
                        float height = Math.Max(2 * Scale, displayed[i] * plot.Height * .88f);
                        using (var bar = LeafDrawing.Rounded(new RectangleF(plot.Left + i * slot + (slot - width) / 2, middle - height / 2, width, height), Math.Min(width, height) / 2)) g.FillPath(brush, bar);
                    }
            }
            if (!Mini && Height >= 40 * Scale) TextRenderer.DrawText(g, caption, Font, new Rectangle(0, Height - (int)captionHeight, Width, (int)captionHeight), ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
    internal enum ActionGlyph { None, Play, Pause, Stop }
    internal static class ActionGlyphDrawing
    {
        internal static void Draw(Graphics g, ActionGlyph glyph, Rectangle bounds, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(color))
            {
                if (glyph == ActionGlyph.Play) g.FillPolygon(brush, new[] { new PointF(bounds.Left + bounds.Width * .15f, bounds.Top), new PointF(bounds.Right, bounds.Top + bounds.Height * .5f), new PointF(bounds.Left + bounds.Width * .15f, bounds.Bottom) });
                if (glyph == ActionGlyph.Stop) using (var shape = LeafDrawing.Rounded(bounds, Math.Max(1, bounds.Width / 9f))) g.FillPath(brush, shape);
                if (glyph == ActionGlyph.Pause)
                {
                    int width = Math.Max(2, (int)Math.Round(bounds.Width / 3.5));
                    // Same integer width, height, radius and pixel phase for both bars.
                    using (var first = LeafDrawing.Rounded(new Rectangle(bounds.Left, bounds.Top, width, bounds.Height), 1))
                    using (var second = LeafDrawing.Rounded(new Rectangle(bounds.Right - width, bounds.Top, width, bounds.Height), 1)) { g.FillPath(brush, first); g.FillPath(brush, second); }
                }
            }
        }
    }
    internal sealed class LiveRecordingCard : LeafButton
    {
        private SessionUpdate update;
        internal readonly AudioLevelView Meter = new AudioLevelView { Mini = true };
        internal LiveRecordingCard() { Controls.Add(Meter); TabStop = false; AccessibleRole = AccessibleRole.Grouping; }
        internal void SetUpdate(SessionUpdate value, float[] history)
        {
            update = value; Meter.Scale = Scale; Meter.SetLevel(value);
            Meter.SeedHistory(history);
            Meter.Bounds = new Rectangle(Math.Max(0, Width - (int)(88 * Scale)), (int)(45 * Scale), (int)(74 * Scale), (int)(18 * Scale));
            AccessibleName = TextCatalog.T("currentRecording") + ", " + value.SessionStarted.ToString("yyyy-MM-dd HH:mm") + ", " + TextCatalog.Translate(value.Message);
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); if (update == null) return;
            var rect = new Rectangle((int)(10 * Scale), (int)(4 * Scale), Width - (int)(20 * Scale), (int)(20 * Scale));
            TextRenderer.DrawText(e.Graphics, TextCatalog.T("currentRecording") + " · " + update.SessionStarted.ToString("HH:mm") + " · " + (update.Mode == RecordingMode.WavOnly ? TextCatalog.T("wavOnlyShort") : update.Profile.Label), Font, rect, ForeColor, TextFormatFlags.EndEllipsis);
            rect.Y += (int)(20 * Scale);
            TextRenderer.DrawText(e.Graphics, TimeSpan.FromSeconds(update.Seconds).ToString(@"hh\:mm\:ss") + " · " + (update.CapturedBytes / 1048576.0).ToString("0.0") + " MiB PCM", Font, rect, ForeColor, TextFormatFlags.EndEllipsis);
            rect.Y += (int)(20 * Scale); rect.Width = Math.Max(1, Width - (int)(102 * Scale));
            TextRenderer.DrawText(e.Graphics, TextCatalog.T(update.State == RecordState.Paused ? "meterPaused" : update.State == RecordState.Saving ? "meterSaving" : "currentUnfinished"), Font, rect, ForeColor, TextFormatFlags.EndEllipsis);
        }
    }
}
