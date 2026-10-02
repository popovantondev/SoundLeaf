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
        internal const int HistogramBarCount = 21, MiniBarCount = 5, FreshnessMilliseconds = 150;
        internal const float MainAttackMilliseconds = 50, MainReleaseMilliseconds = 50;
        internal const float MiniAttackMilliseconds = 65, MiniReleaseMilliseconds = 65;
        internal const float FadeReleaseMilliseconds = 42;
        private readonly float[] history = new float[25];
        private readonly float[] targets = new float[HistogramBarCount];
        private readonly float[] displayed = new float[HistogramBarCount];
        private readonly Timer timer = new Timer { Interval = 16 };
        private readonly Stopwatch freshness = new Stopwatch();
        private readonly Stopwatch frameClock = new Stopwatch();
        private float level, envelope, activity;
        private bool active;
        private readonly AudioPresenceGate presence = new AudioPresenceGate();
        private string caption = "";
        internal bool Mini, Dark;
        internal float Scale = 1;
        internal bool HasCurrentSignal { get { return active && level > 0 && freshness.IsRunning && freshness.ElapsedMilliseconds <= FreshnessMilliseconds; } }
        internal bool TimerRunning { get { return timer.Enabled; } }
        internal float CurrentEnvelope { get { return envelope; } }
        internal float Activity { get { return activity; } }
        internal float DisplayedBar(int index) { return displayed[index]; }
        internal float IndicatorSpan { get { return Math.Min(PlotBounds.Width, Mini ? 31 * Scale : 204 * Scale); } }
        internal AudioLevelView()
        {
            DoubleBuffered = true; TabStop = false; AccessibleRole = AccessibleRole.Graphic;
            timer.Tick += delegate
            {
                // A stalled UI does not jump straight to a large new shape on its first frame.
                double elapsed = frameClock.IsRunning ? Math.Min(32, frameClock.Elapsed.TotalMilliseconds) : 16;
                frameClock.Restart(); AdvanceEnvelope(elapsed);
                Invalidate();
            };
        }
        private void ClearLevels() { Array.Clear(history, 0, history.Length); Array.Clear(targets, 0, targets.Length); Array.Clear(displayed, 0, displayed.Length); envelope = activity = 0; }
        private static float Approach(float value, float target, double milliseconds, float attack, float release)
        { return value + (target - value) * (float)(1 - Math.Exp(-Math.Max(0, milliseconds) / (target >= value ? attack : release))); }
        private void UpdateTargets()
        {
            float maximum = 0; foreach (float sample in history) maximum = Math.Max(maximum, sample);
            for (int i = 0; i < targets.Length; i++)
            {
                int sample = (int)Math.Round(i * (history.Length - 1.0) / (targets.Length - 1));
                // This is measured level history scaled by current loudness, not spectrum bins.
                targets[i] = maximum > 0 ? level * history[sample] / maximum : 0;
            }
        }
        internal void AdvanceEnvelope(double elapsedMilliseconds)
        {
            if (!HasCurrentSignal)
            {
                presence.Reset();
                level = 0; Array.Clear(targets, 0, targets.Length);
                if (active) { caption = TextCatalog.T("meterQuiet"); AccessibleName = TextCatalog.T("meterLabel") + ": " + caption; }
            }
            float attack = Mini ? MiniAttackMilliseconds : MainAttackMilliseconds;
            float release = Mini ? MiniReleaseMilliseconds : MainReleaseMilliseconds;
            envelope = Approach(envelope, level, elapsedMilliseconds, attack, release);
            activity = Approach(activity, HasCurrentSignal ? 1 : 0, elapsedMilliseconds, attack, FadeReleaseMilliseconds);
            for (int i = 0; i < displayed.Length; i++) displayed[i] = Approach(displayed[i], targets[i], elapsedMilliseconds, attack, release);
            if (!HasCurrentSignal && activity < .015f) { ClearLevels(); timer.Stop(); frameClock.Reset(); }
        }
        internal void SetLevel(SessionUpdate update)
        {
            active = update.State == RecordState.Recording && update.DeviceAvailable;
            level = presence.Observe(update.Rms, active) ? ScaleLevel(update) : 0;
            freshness.Restart();
            for (int i = 0; i < history.Length - 1; i++) history[i] = history[i + 1];
            history[history.Length - 1] = level;
            UpdateTargets();
            // Quiet input fades; explicit pause/stop/device loss is immediately quiescent.
            if (!active) ClearLevels();
            caption = TextCatalog.T(update.State == RecordState.Paused ? "meterPaused" : update.State == RecordState.Saving ? "meterSaving" :
                update.State == RecordState.Stopped || update.State == RecordState.Faulted ? "meterStopped" : level > 0 ? "meterSound" : "meterQuiet");
            AccessibleName = TextCatalog.T("meterLabel") + ": " + caption;
            if (Visible && (HasCurrentSignal || activity >= .015f)) { if (!timer.Enabled) frameClock.Restart(); timer.Start(); }
            else { timer.Stop(); frameClock.Reset(); }
            Invalidate();
        }
        internal static float DisplayLevel(SessionUpdate update)
        {
            if (update.State != RecordState.Recording || !update.DeviceAvailable) return 0;
            if (float.IsNaN(update.Rms) || float.IsInfinity(update.Rms) || update.Rms < AudioPresenceGate.StopRms) return 0;
            return ScaleLevel(update);
        }
        private static float ScaleLevel(SessionUpdate update)
        {
            float value = Math.Max(update.Rms, update.Peak * .35f);
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) return 0;
            // Presentation floor only: faint background must not inflate the histogram.
            // Capture bytes and the session's historical signal detection are untouched.
            double normalized = Math.Max(0, Math.Min(1, (20 * Math.Log10(value) + 60) / 60));
            return (float)Math.Pow(normalized, 1.5) * Expressiveness;
        }
        internal void SeedHistory(float[] values)
        {
            Array.Clear(history, 0, history.Length);
            for (int i = 0; i < Math.Min(values.Length, history.Length); i++)
                history[i] = float.IsNaN(values[i]) || float.IsInfinity(values[i]) ? 0 : Math.Max(0, Math.Min(Expressiveness, values[i]));
            // A history seed only sets targets. It never snaps the visible columns.
            UpdateTargets();
        }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible && (HasCurrentSignal || activity >= .015f)) { frameClock.Restart(); timer.Start(); } else { timer.Stop(); frameClock.Reset(); } }
        private RectangleF PlotBounds
        {
            get
            {
                float margin = Mini ? 3 * Scale : 16 * Scale, captionHeight = Mini ? 0 : 22 * Scale;
                margin = Math.Min(margin, Math.Max(0, (Width - 1) / 2f));
                float padding = Math.Min(5 * Scale, Math.Max(0, (Height - captionHeight - 1) / 2f));
                return new RectangleF(margin, padding, Math.Max(1, Width - margin * 2), Math.Max(1, Height - captionHeight - padding * 2));
            }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            if (!Mini) using (var shape = LeafDrawing.Rounded(new RectangleF(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 12 * Scale))
                using (var brush = new SolidBrush(BackColor)) g.FillPath(brush, shape);
            Color ink = ColorTranslator.FromHtml(Dark ? "#74C991" : "#278C55");
            float captionHeight = Mini ? 0 : 22 * Scale;
            var plot = PlotBounds;
            float middle = plot.Top + plot.Height / 2;
            float span = IndicatorSpan, left = plot.Left + (plot.Width - span) / 2;
            if (activity < 1)
            {
                using (var pen = new Pen(Color.FromArgb((int)Math.Round(130 * (1 - activity)), ink), Math.Max(1, 1.4f * Scale)))
                { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLine(pen, left, middle, left + span, middle); }
            }
            if (activity > 0)
            {
                int count = Mini ? MiniBarCount : HistogramBarCount;
                float fit = span / (Mini ? 31 * Scale : 204 * Scale);
                float width = (Mini ? 3 : 4) * Scale * fit, gap = (Mini ? 4 : 6) * Scale * fit;
                using (var brush = new SolidBrush(Color.FromArgb((int)Math.Round(255 * activity), ink)))
                    for (int i = 0; i < count; i++)
                    {
                        // Shared rounded-column grammar; each size keeps its own pacing and span.
                        float value = Mini ? envelope * (1 - Math.Abs(i - 2) * .22f) : displayed[i];
                        float baseline = Math.Min(2 * Scale, plot.Height);
                        float height = Math.Min(plot.Height, baseline + value * Math.Max(0, plot.Height - baseline));
                        using (var bar = LeafDrawing.Rounded(new RectangleF(left + i * (width + gap), middle - height / 2, width, height), Math.Min(width, height) / 2)) g.FillPath(brush, bar);
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
            int top = (int)(42 * Scale);
            int height = Math.Min((int)(24 * Scale), Math.Max(1, Height - top - (int)(2 * Scale)));
            Meter.Bounds = new Rectangle(Math.Max(0, Width - (int)(88 * Scale)), top, (int)(74 * Scale), height);
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
