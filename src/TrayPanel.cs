using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SoundLeaf
{
    internal sealed class PanelActions
    {
        internal Action Start, Pause, Stop, Wav, Recover, Exit, Autostart, OpenLog;
        internal Action SaveSettings;
        internal Action<RecordingEntry, RecordingProfile, string> Convert;
    }
    internal sealed class LeafHeader : Control
    {
        internal float Scale = 1;
        internal LeafHeader() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool shortHeader = Height < 52 * Scale;
            using (var leaf = SoundLeafIcons.Brand((int)(35 * Scale))) e.Graphics.DrawImage(leaf, 13 * Scale, 7 * Scale, 35 * Scale, 35 * Scale);
            using (var title = new Font("Segoe UI", 23 * Scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(ForeColor)) e.Graphics.DrawString("SoundLeaf", title, brush, 58 * Scale, 5 * Scale);
            if (!shortHeader) TextRenderer.DrawText(e.Graphics, TextCatalog.T("educational"), Font, new Rectangle((int)(16 * Scale), (int)(39 * Scale), Width - (int)(32 * Scale), (int)(20 * Scale)), ForeColor, TextFormatFlags.EndEllipsis);
        }
    }
    internal class LeafButton : Button
    {
        private bool hover;
        internal bool Selected;
        internal float Scale = 1;
        internal ActionGlyph Glyph;
        internal LeafButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = LeafDrawing.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 7 * Scale))
            {
                using (var brush = new SolidBrush(hover && Enabled ? ControlPaint.Light(BackColor, .08f) : BackColor)) e.Graphics.FillPath(brush, shape);
                if (Focused || Selected) using (var pen = new Pen(Color.FromArgb(55, 149, 90), Math.Max(1, 1.5f * Scale))) e.Graphics.DrawPath(pen, shape);
            }
            var textRect = new Rectangle((int)(6 * Scale), 0, Width - (int)(12 * Scale), Height);
            if (Glyph != ActionGlyph.None)
            {
                int icon = (int)Math.Round(12 * Scale), gap = (int)Math.Round(7 * Scale);
                int textWidth = Math.Min(TextRenderer.MeasureText(Text, Font).Width, Math.Max(1, textRect.Width - icon - gap));
                int left = Math.Max(textRect.Left, (Width - textWidth - icon - gap) / 2);
                Color color = !Enabled ? Color.FromArgb(116, 136, 121) : Glyph == ActionGlyph.Play ? Color.FromArgb(81, 166, 109) : Glyph == ActionGlyph.Pause ? Color.FromArgb(202, 173, 76) : ForeColor;
                ActionGlyphDrawing.Draw(e.Graphics, Glyph, new Rectangle(left, (Height - icon) / 2, icon, icon), color);
                textRect = new Rectangle(left + icon + gap, 0, textWidth, Height);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, Enabled ? ForeColor : Color.FromArgb(116, 136, 121), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
    internal sealed class RecordingCard : LeafButton
    {
        internal RecordingEntry Entry;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var rect = new Rectangle((int)(10 * Scale), (int)(4 * Scale), Width - (int)(20 * Scale), (int)(20 * Scale));
            TextRenderer.DrawText(e.Graphics, Entry.Date.ToString("yyyy-MM-dd HH:mm") + " · " + Entry.Format, Font, rect, ForeColor, TextFormatFlags.EndEllipsis);
            rect.Y += (int)(20 * Scale);
            TextRenderer.DrawText(e.Graphics, (Entry.Bytes / 1048576.0).ToString("0.0") + " MiB · " + (Entry.Seconds.HasValue ? TimeSpan.FromSeconds(Entry.Seconds.Value).ToString(@"hh\:mm\:ss") : TextCatalog.T("unknown")), Font, rect, ForeColor, TextFormatFlags.EndEllipsis);
            rect.Y += (int)(20 * Scale);
            TextRenderer.DrawText(e.Graphics, TextCatalog.T(Entry.Verified ? "verified" : "existing"), Font, rect, ForeColor, TextFormatFlags.EndEllipsis);
        }
    }
    internal sealed class GrowthSurface : Control
    {
        internal Bitmap Snapshot; internal Rectangle Destination;
        internal float Alpha = 1;
        internal GrowthSurface() { DoubleBuffered = true; TabStop = false; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor); e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (Snapshot != null) using (var attributes = new ImageAttributes())
            {
                var matrix = new ColorMatrix(); matrix.Matrix33 = Alpha; attributes.SetColorMatrix(matrix);
                e.Graphics.DrawImage(Snapshot, Destination, 0, 0, Snapshot.Width, Snapshot.Height, GraphicsUnit.Pixel, attributes);
            }
        }
    }
    internal sealed class TrayPanel : Form
    {
        private readonly SoundLeafSettings settings;
        private readonly string root;
        private readonly PanelActions actions;
        private readonly Func<bool> isBusy;
        private readonly Panel host = new Panel { Dock = DockStyle.Fill };
        private readonly Panel page = new Panel { AutoScroll = false };
        private readonly Panel nav = new Panel();
        private readonly LeafHeader header = new LeafHeader();
        private readonly ToolTip tips = new ToolTip { AutoPopDelay = 10000, InitialDelay = 400 };
        private readonly System.Windows.Forms.Timer growthTimer = new System.Windows.Forms.Timer { Interval = 16 };
        private readonly Stopwatch growthWatch = new Stopwatch();
        private PanelMotionSurface growth;
        private Rectangle growthBounds;
        private bool shrinking;
        private double motionFrom, motionProgress = 1;
        private double motionDuration;
        private readonly System.Windows.Forms.Timer focusTimer = new System.Windows.Forms.Timer { Interval = 45 };
        private TrayAnchor anchor;
        private float scale = 1;
        private bool initialized, verificationScale, modal, hiding, wavAvailable, startup;
        private string currentPage = "control", settingsGroup = "capture", selectedPath, libraryMessage;
        private int scanVersion, recordPage, pageSize = 1;
        private SessionUpdate update = new SessionUpdate(RecordState.Starting, "", 0, false);
        private Label status, details, warning;
        private LeafButton start, pause, stop;
        private AudioLevelView levelView;
        private LiveRecordingCard liveCard;
        private readonly float[] recentLevels = new float[25];
        private readonly List<RecordingEntry> records = new List<RecordingEntry>();
        private readonly List<Font> ownedFonts = new List<Font>();
        private RecordingEntry conversionEntry;
        internal Func<TrayAnchor> AnchorProvider;
        internal bool Animating { get { return growthTimer.Enabled; } }
        internal bool SnapshotAllocated { get { return growth != null; } }
        internal Rectangle Viewport { get { return page.RectangleToScreen(page.ClientRectangle); } }
        private bool HasLiveRecording { get { return !string.IsNullOrEmpty(update.SessionId) && (update.State == RecordState.Recording || update.State == RecordState.Paused || update.State == RecordState.Saving); } }
        private List<RecordingEntry> FinishedRecords { get { return records.FindAll(delegate(RecordingEntry entry) { return !HasLiveRecording || !entry.Id.StartsWith(update.SessionId, StringComparison.OrdinalIgnoreCase); }); } }
        internal int PageCount { get { return Math.Max(1, (FinishedRecords.Count + (HasLiveRecording ? 1 : 0) + pageSize - 1) / pageSize); } }
        internal int RecordPage { get { return recordPage; } }
        internal bool Compact { get { return ClientSize.Height / scale < 450; } }
        private int Px(float value) { return Math.Max(1, (int)Math.Round(value * scale)); }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.Style |= 0x00C40000; return value; }
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyCorners(); }
        private void ApplyCorners()
        {
            if (!IsHandleCreated) return;
            int preference = 2; DwmSetWindowAttribute(Handle, 33, ref preference, 4);
            int noBorder = unchecked((int)0xFFFFFFFE); DwmSetWindowAttribute(Handle, 34, ref noBorder, 4);
        }
        internal TrayPanel(string root, SoundLeafSettings settings, PanelActions actions, Func<bool> isBusy)
        {
            this.root = root; this.settings = settings; this.actions = actions; this.isBusy = isBusy;
            Text = "SoundLeaf"; StartPosition = FormStartPosition.Manual; ShowInTaskbar = false; TopMost = true; FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None; MinimumSize = Size.Empty; ClientSize = new Size(430, 520);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            host.Controls.Add(header); host.Controls.Add(nav); host.Controls.Add(page); Controls.Add(host);
            SetScale(1); initialized = true;
            Deactivate += delegate
            {
                if (verificationScale || modal || IsDisposed || !IsHandleCreated) return;
                focusTimer.Start();
            };
            focusTimer.Tick += delegate
            {
                if (anchor != null && anchor.Icon.Contains(Cursor.Position) && Control.MouseButtons != MouseButtons.None) return;
                focusTimer.Stop(); if (!modal && !LeafSelect.OpenFor(this) && !ContainsFocus && TrayAnchorResolver.GetForegroundWindow() != Handle) HidePanel();
            };
            VisibleChanged += delegate { if (!Visible) { CancelGrowth(); LeafSelect.CloseFor(this); } };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HidePanel(); } };
            growthTimer.Tick += delegate { GrowthTick(); };
            Rebuild();
        }
        private void SetScale(float value)
        {
            scale = value; header.Scale = scale;
            if (Font.Unit == GraphicsUnit.Pixel && Math.Abs(Font.Size - 12.6667f * scale) < .01f) return;
            var font = new Font("Segoe UI", 12.6667f * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            ownedFonts.Add(font); Font = font;
        }
        protected override void WndProc(ref Message message)
        {
            // Preserve compositor frame styles without exposing a caption or resize border.
            if (message.Msg == 0x0083) { message.Result = IntPtr.Zero; return; }
            if (message.Msg == 0x0084) { message.Result = new IntPtr(growth == null ? 1 : -1); return; }
            base.WndProc(ref message);
            if (message.Msg == 0x02E0 && initialized && !verificationScale && AnchorProvider != null)
            {
                CancelGrowth(); var next = AnchorProvider(); anchor = next; SetScale(next.Scale);
                Bounds = TrayAnchorResolver.Place(next, new Size(Px(430), Px(520))); Rebuild();
            }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { if (!LeafSelect.CloseFor(this)) HidePanel(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        internal static Rectangle Placement(Point point, Size desired, Rectangle work)
        { return TrayAnchorResolver.Place(new TrayAnchor { Icon = new Rectangle(point, new Size(1, 1)), Work = work, Monitor = work }, desired); }
        internal void Toggle(TrayAnchor value) { if (Visible && !shrinking) HidePanel(); else Open(value, TrayAnchorResolver.AnimationsEnabled()); }
        internal void Open(TrayAnchor value, bool animate)
        {
            IntPtr createdHandle = Handle;
            focusTimer.Stop();
            if (growth != null && shrinking) { shrinking = false; BeginMotion(true, motionProgress); Activate(); return; }
            CancelGrowth(); anchor = value; SetScale(value.Scale);
            Bounds = TrayAnchorResolver.Place(value, new Size(Px(430), Px(520))); Rebuild();
            Opacity = animate ? 0 : 1; Show(); Activate(); PerformLayout();
            if (animate)
            {
                var image = CapturePanel();
                try { growthBounds = Bounds; growth = new PanelMotionSurface(Handle, growthBounds, image); BeginMotion(true, 0); }
                catch { image.Dispose(); CancelGrowth(); }
            }
        }
        internal void HidePanel()
        {
            if (hiding || IsDisposed) return; hiding = true;
            try
            {
                focusTimer.Stop(); LeafSelect.CloseFor(this);
                if (!Visible) { CancelGrowth(); return; }
                if (!verificationScale && !modal && TrayAnchorResolver.AnimationsEnabled())
                {
                    if (growth == null)
                    {
                        Bitmap image = CapturePanel();
                        try { growthBounds = Bounds; growth = new PanelMotionSurface(Handle, growthBounds, image); }
                        catch { image.Dispose(); CancelGrowth(); Hide(); return; }
                    }
                    if (!shrinking) BeginMotion(false, motionProgress);
                }
                else { Hide(); CancelGrowth(); }
            }
            finally { hiding = false; }
        }
        internal void CancelGrowth()
        {
            growthTimer.Stop(); growthWatch.Reset();
            if (growth != null) { growth.Dispose(); growth = null; }
            shrinking = false; motionProgress = 1;
            host.Visible = true; if (!IsDisposed) { Opacity = 1; ApplyCorners(); }
        }
        internal static double Ease(double t) { return 1 - Math.Pow(1 - Math.Max(0, Math.Min(1, t)), 3); }
        private void BeginMotion(bool opening, double from)
        {
            shrinking = !opening; motionFrom = from; motionDuration = Math.Max(80, (opening ? 280 : 220) * Math.Abs((opening ? 1 : 0) - from));
            Opacity = 0; growthWatch.Restart(); SetGrowthFrame(from); growthTimer.Start();
        }
        private void SetGrowthFrame(double t)
        {
            if (growth == null) return; double ease = Math.Max(0, Math.Min(1, t)); motionProgress = ease;
            int width = Math.Max(1, (int)(Px(24) + (growthBounds.Width - Px(24)) * ease)), height = Math.Max(1, (int)(Px(24) + (growthBounds.Height - Px(24)) * ease));
            Point origin = new Point(anchor.Center.X - growthBounds.Left, anchor.Center.Y - growthBounds.Top);
            int x = (int)(Math.Max(0, Math.Min(growthBounds.Width, origin.X)) * (1 - width / (double)growthBounds.Width)), y = growthBounds.Height - height;
            if (anchor.Edge == TrayEdge.Top) y = 0;
            if (anchor.Edge == TrayEdge.Left || anchor.Edge == TrayEdge.Right) { x = anchor.Edge == TrayEdge.Left ? 0 : growthBounds.Width - width; y = (int)(Math.Max(0, Math.Min(growthBounds.Height, origin.Y)) * (1 - height / (double)growthBounds.Height)); }
            growth.Draw(new RectangleF(x, y, width, height), (float)Math.Min(1, ease * 2.5), 8 * scale);
        }
        private void GrowthTick()
        {
            try
            {
                double t = Math.Min(1, growthWatch.Elapsed.TotalMilliseconds / motionDuration);
                SetGrowthFrame(motionFrom + ((shrinking ? 0 : 1) - motionFrom) * Ease(t));
                if (t >= 1) { if (shrinking) Hide(); else Opacity = 1; CancelGrowth(); }
            }
            catch { bool close = shrinking; if (close) Hide(); CancelGrowth(); }
        }
        internal Bitmap CaptureMotionForVerification() { return growth == null ? null : growth.Capture(); }
        internal void ConfigureForVerification(float value, Size logical)
        { verificationScale = true; IntPtr createdHandle = Handle; CancelGrowth(); SetScale(value); Size = new Size(Px(logical.Width), Px(logical.Height)); Rebuild(); }
        internal Bitmap CapturePanel()
        {
            // Capture client controls only. WM_PRINT on a framed HWND can draw a synthetic caption.
            var image = new Bitmap(ClientSize.Width, ClientSize.Height); host.DrawToBitmap(image, host.ClientRectangle); return image;
        }
        internal void ShowPage(string name) { currentPage = name; Rebuild(); }
        internal void ShowSettingsGroup(string name) { currentPage = "settings"; settingsGroup = name; Rebuild(); }
        internal void ShowConversionForVerification(RecordingEntry entry) { conversionEntry = entry; currentPage = "convert"; Rebuild(); }
        internal void VerifyEscape() { Message message = new Message(); ProcessCmdKey(ref message, Keys.Escape); }
        internal void SetRecordsForVerification(List<RecordingEntry> entries) { ++scanVersion; records.Clear(); records.AddRange(entries); libraryMessage = null; recordPage = 0; RenderRecords(); }
        internal void NextRecordPage(int delta) { recordPage = Math.Max(0, Math.Min(PageCount - 1, recordPage + delta)); RenderRecords(); }
        internal void SetState(SessionUpdate value, bool canWav, bool autoStart)
        {
            bool changedOffer = wavAvailable != canWav;
            bool hadLive = HasLiveRecording; string oldId = update.SessionId; bool wasLive = hadLive;
            if (oldId != value.SessionId || AudioLevelView.DisplayLevel(value) == 0) Array.Clear(recentLevels, 0, recentLevels.Length);
            else { for (int i = 0; i < recentLevels.Length - 1; i++) recentLevels[i] = recentLevels[i + 1]; }
            recentLevels[recentLevels.Length - 1] = AudioLevelView.DisplayLevel(value);
            update = value; wavAvailable = canWav; startup = autoStart;
            if (currentPage == "control") { if (changedOffer) Rebuild(); else UpdateControls(); }
            else if (currentPage == "recordings")
            {
                if (hadLive != HasLiveRecording || oldId != value.SessionId) { RenderRecords(); if (wasLive && !HasLiveRecording) ScanRecords(); }
                else if (liveCard != null && !liveCard.IsDisposed) liveCard.SetUpdate(update, recentLevels);
            }
            else if (Visible) SetProfileEnabled(page, !isBusy());
        }
        private void SetProfileEnabled(Control parent, bool value)
        { foreach (Control item in parent.Controls) { if (item.Tag as string == "profile") item.Enabled = value; if (item.HasChildren) SetProfileEnabled(item, value); } }
        private static void DisposeChildren(Control parent) { while (parent.Controls.Count > 0) parent.Controls[0].Dispose(); }
        internal void Rebuild()
        {
            if (growth != null) CancelGrowth();
            DisposeChildren(nav); DisposeChildren(page); ++scanVersion;
            int headerHeight = Px(ClientSize.Height / scale < 350 ? 44 : 60), navHeight = Px(34);
            header.Bounds = new Rectangle(0, 0, ClientSize.Width, headerHeight);
            nav.Bounds = new Rectangle(Px(12), headerHeight, Math.Max(1, ClientSize.Width - Px(24)), navHeight);
            page.Bounds = new Rectangle(Px(16), headerHeight + navHeight + Px(10), Math.Max(1, ClientSize.Width - Px(32)), Math.Max(1, ClientSize.Height - headerHeight - navHeight - Px(22)));
            for (int i = 0; i < 3; i++)
            {
                string name = new[] { "control", "recordings", "settings" }[i]; string target = name;
                var button = ButtonFor(TextCatalog.T(name), delegate { currentPage = target; Rebuild(); });
                button.Tag = name; button.Selected = name == currentPage; button.Bounds = new Rectangle(i * nav.Width / 3, 0, nav.Width / 3 - Px(2), nav.Height); nav.Controls.Add(button);
            }
            if (currentPage == "control") BuildControls();
            else if (currentPage == "settings") BuildSettings();
            else if (currentPage == "convert" && conversionEntry != null) BuildConversion();
            else BuildRecords();
            ApplyTheme(); ApplyCorners();
        }
        private Label LabelAt(string text, int y, int height)
        {
            var label = new Label { Text = text, AutoSize = false, AutoEllipsis = true, Bounds = new Rectangle(0, y, page.Width, height), AccessibleName = text, UseMnemonic = false };
            page.Controls.Add(label); return label;
        }
        private LeafButton ButtonFor(string text, Action action)
        {
            var button = new LeafButton { Text = text, Scale = scale, FlatStyle = FlatStyle.Flat, AccessibleName = text, UseMnemonic = false };
            button.FlatAppearance.BorderSize = 0; button.Click += delegate { action(); }; return button;
        }
        private LeafButton ButtonAt(string text, Action action, int y, int height)
        { var button = ButtonFor(text, action); button.Bounds = new Rectangle(0, y, page.Width, height); page.Controls.Add(button); return button; }
        private void RowActions(string[] labels, Action[] commands, int y, int height, Action<LeafButton, int> prepare)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                var button = ButtonFor(labels[i], commands[i]); button.Bounds = new Rectangle(i * page.Width / labels.Length, y, page.Width / labels.Length - Px(3), height);
                page.Controls.Add(button); if (prepare != null) prepare(button, i);
            }
        }
        private void BuildControls()
        {
            bool tiny = ClientSize.Height / scale < 380;
            status = LabelAt("", 0, Px(tiny ? 20 : 38)); details = LabelAt("", status.Bottom + Px(2), Px(tiny ? 20 : 54));
            warning = LabelAt("", details.Bottom + Px(2), Px(tiny ? 20 : 30));
            int actionsHeight = Px(tiny ? (wavAvailable ? 88 : 60) : (wavAvailable ? 116 : 80));
            int y = Math.Max(warning.Bottom + Px(4), page.Height - actionsHeight);
            levelView = new AudioLevelView { Scale = scale, Mini = tiny, Bounds = new Rectangle(0, warning.Bottom + Px(3), page.Width, Math.Max(1, y - warning.Bottom - Px(8))) };
            if (levelView.Height >= Px(tiny ? 5 : 30)) page.Controls.Add(levelView); else { levelView.Dispose(); levelView = null; }
            RowActions(new[] { TextCatalog.T("startShort"), TextCatalog.T("pauseShort"), TextCatalog.T("stopShort") }, new[] { actions.Start, actions.Pause, actions.Stop }, y, Px(tiny ? 28 : 34),
                delegate(LeafButton button, int i) { button.Glyph = i == 0 ? ActionGlyph.Play : i == 1 ? ActionGlyph.Pause : ActionGlyph.Stop; if (i == 0) { start = button; button.AccessibleName = TextCatalog.T("message.2"); } if (i == 1) { pause = button; button.AccessibleName = TextCatalog.T("message.5"); } if (i == 2) { stop = button; button.AccessibleName = TextCatalog.T("message.6"); } });
            y += Px(tiny ? 32 : 40);
            if (wavAvailable) { var wav = ButtonAt(TextCatalog.T("message.3"), actions.Wav, y, Px(tiny ? 24 : 30)); wav.Tag = "profile"; wav.Enabled = !isBusy(); y += Px(tiny ? 28 : 36); }
            RowActions(new[] { TextCatalog.T("recoverShort"), TextCatalog.T("logShort"), TextCatalog.T("exit") }, new[] { actions.Recover, actions.OpenLog, actions.Exit }, y, Px(tiny ? 24 : 30),
                delegate(LeafButton button, int i) { if (i == 0) button.Enabled = !isBusy(); });
            UpdateControls();
        }
        private void UpdateControls()
        {
            if (status == null || status.IsDisposed) return;
            status.Text = TextCatalog.Translate(update.Message); status.AccessibleName = status.Text;
            string profile = update.Mode == RecordingMode.WavOnly ? TextCatalog.T("wavOnlyShort") : update.Profile.Label;
            details.Text = profile + (ClientSize.Height / scale < 380 ? " · " : "\n") + TimeSpan.FromSeconds(update.Seconds).ToString(@"hh\:mm\:ss"); details.AccessibleName = details.Text;
            if (update.DeviceAvailable && ClientSize.Height / scale >= 380) details.Text += "\n" + TextCatalog.T("captureAvailable");
            if (levelView != null && !levelView.IsDisposed) { levelView.SetLevel(update); levelView.SeedHistory(recentLevels); }
            warning.Text = TextCatalog.Translate(update.Warning); warning.AccessibleName = warning.Text;
            bool active = update.State == RecordState.Recording || update.State == RecordState.Paused;
            start.Enabled = !isBusy(); pause.Enabled = active; stop.Enabled = active || update.State == RecordState.Starting;
            pause.Text = TextCatalog.T(update.State == RecordState.Paused ? "resumeShort" : "pauseShort");
            pause.Glyph = update.State == RecordState.Paused ? ActionGlyph.Play : ActionGlyph.Pause;
        }
        private LeafSelect Selector(string name, int y, object[] values, int index, bool profile)
        {
            int labelWidth = Px(102), rowHeight = Px(ClientSize.Height / scale < 350 ? 30 : 34);
            var label = new Label { Text = TextCatalog.T(name), Bounds = new Rectangle(0, y, labelWidth - Px(5), rowHeight), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, AccessibleName = TextCatalog.T(name) }; page.Controls.Add(label);
            var select = new LeafSelect { Bounds = new Rectangle(labelWidth, y, Math.Max(1, page.Width - labelWidth), rowHeight), Scale = scale, AccessibleName = TextCatalog.T(name) };
            select.Items.AddRange(values); select.SelectedIndex = index; if (profile) { select.Tag = "profile"; select.Enabled = !isBusy(); }
            select.PopupClosed += delegate(bool outside) { if (outside && !verificationScale && !IsDisposed && IsHandleCreated) BeginInvoke(new Action(delegate { if (!modal && !LeafSelect.OpenFor(this) && !ContainsFocus && TrayAnchorResolver.GetForegroundWindow() != Handle) HidePanel(); })); };
            page.Controls.Add(select); return select;
        }
        private object[] Formats()
        { return new object[] { "mkv / aac", "ogg / opus", "mp3", "m4a / aac", "aac / adts" }; }
        private void BuildSettings()
        {
            int y = 0;
            if (Compact)
            {
                RowActions(new[] { TextCatalog.T("captureGroup"), TextCatalog.T("filesGroup"), TextCatalog.T("appearanceGroup") },
                    new Action[] { delegate { settingsGroup = "capture"; Rebuild(); }, delegate { settingsGroup = "files"; Rebuild(); }, delegate { settingsGroup = "appearance"; Rebuild(); } }, 0, Px(28),
                    delegate(LeafButton button, int i) { button.Selected = settingsGroup == new[] { "capture", "files", "appearance" }[i]; });
                y = Px(36);
            }
            if (!Compact || settingsGroup == "capture")
            {
                var formats = Selector("format", y, Formats(), (int)settings.Format, true); y += Px(40);
                int[] available = settings.Format == AudioOutputFormat.Opus ? RecordingProfile.OpusRates : RecordingProfile.OtherRates;
                var items = new object[available.Length]; for (int i = 0; i < items.Length; i++) items[i] = available[i] + " kbps";
                var rates = Selector("bitrate", y, items, Array.IndexOf(available, settings.Profile.Bitrate), true); y += Px(40);
                formats.SelectedIndexChanged += delegate { if (isBusy()) return; settings.Format = (AudioOutputFormat)formats.SelectedIndex; Save(); Rebuild(); };
                rates.SelectedIndexChanged += delegate { if (isBusy()) return; settings.Bitrates[(int)settings.Format] = available[rates.SelectedIndex]; Save(); };
                var info = LabelAt(TextCatalog.T("profileInfo"), y, Px(22)); tips.SetToolTip(info, TextCatalog.T("channels")); info.AccessibleDescription = TextCatalog.T("channels"); y += Px(30);
            }
            if (!Compact || settingsGroup == "appearance")
            {
                var language = Selector("language", y, new object[] { "Deutsch", "Русский", "English" }, settings.Language == "de" ? 0 : settings.Language == "ru" ? 1 : 2, false); y += Px(40);
                var theme = Selector("theme", y, new object[] { TextCatalog.T("system"), TextCatalog.T("light"), TextCatalog.T("dark") }, Array.IndexOf(new[] { "system", "light", "dark" }, settings.Theme), false); y += Px(40);
                language.SelectedIndexChanged += delegate { settings.Language = new[] { "de", "ru", "en" }[language.SelectedIndex]; TextCatalog.SetLanguage(settings.Language); Save(); Rebuild(); };
                theme.SelectedIndexChanged += delegate { settings.Theme = new[] { "system", "light", "dark" }[theme.SelectedIndex]; Save(); Rebuild(); };
            }
            if (!Compact || settingsGroup == "files")
            {
                LabelAt(TextCatalog.T("folder"), y, Px(20)); y += Px(23);
                string folder = settings.RecordingsFolder(root); var path = LabelAt(folder, y, Px(22)); tips.SetToolTip(path, folder); y += Px(28);
                var choose = ButtonAt(TextCatalog.T("choose"), ChooseFolder, y, Px(32)); choose.Tag = "profile"; choose.Enabled = !isBusy(); y += Px(38);
                var auto = ButtonAt(TextCatalog.T("autostart") + (startup ? " · " + TextCatalog.T("on") : " · " + TextCatalog.T("off")), actions.Autostart, y, Px(32));
                auto.AccessibleRole = AccessibleRole.CheckButton; auto.AccessibleDescription = TextCatalog.T(startup ? "on" : "off");
            }
        }
        private void Save()
        {
            try { settings.Save(root); actions.SaveSettings(); }
            catch (Exception error)
            {
                bool restored; var persisted = SoundLeafSettings.Load(root, out restored);
                settings.Format = persisted.Format; settings.Bitrates = persisted.Bitrates; settings.Language = persisted.Language; settings.Theme = persisted.Theme;
                settings.OutputFolder = persisted.OutputFolder; settings.LibraryFolders = persisted.LibraryFolders; TextCatalog.SetLanguage(settings.Language); Error(error); Rebuild();
            }
        }
        private void ChooseFolder()
        {
            if (isBusy()) return; modal = true;
            try
            {
                using (var dialog = new FolderBrowserDialog { Description = TextCatalog.T("folder"), SelectedPath = settings.RecordingsFolder(root) })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        string previous = settings.RecordingsFolder(root), destination = Path.GetFullPath(dialog.SelectedPath);
                        var check = StartupChecks.Check(destination, "", RecordingMode.WavOnly); if (!check.CanRecord) throw new IOException(check.Error);
                        var folders = new List<string>(settings.LibraryFolders ?? new string[0]); if (!folders.Contains(previous)) folders.Add(previous);
                        settings.OutputFolder = destination; settings.LibraryFolders = folders.ToArray(); Save(); Rebuild();
                    }
            }
            catch (Exception error) { Error(error); }
            finally { modal = false; }
        }
        private void BuildRecords() { RenderRecords(); ScanRecords(); }
        private void RenderRecords()
        {
            if (currentPage != "recordings") return; DisposeChildren(page);
            bool tiny = ClientSize.Height / scale < 350;
            int rowHeight = Px(tiny ? 68 : 72), cardsTop = Px(tiny ? 30 : 38), footerHeight = Px(tiny ? 64 : 76);
            pageSize = Math.Max(1, (page.Height - cardsTop - footerHeight) / rowHeight); recordPage = Math.Max(0, Math.Min(recordPage, PageCount - 1));
            ButtonAt(TextCatalog.T("refresh"), ScanRecords, 0, Px(tiny ? 26 : 30));
            var completed = FinishedRecords; int liveCount = HasLiveRecording ? 1 : 0; liveCard = null;
            if (completed.Count + liveCount == 0) LabelAt(libraryMessage ?? TextCatalog.T("empty"), cardsTop, Px(44));
            for (int i = 0; i < pageSize && recordPage * pageSize + i < completed.Count + liveCount; i++)
            {
                int index = recordPage * pageSize + i;
                if (liveCount == 1 && index == 0)
                {
                    liveCard = new LiveRecordingCard { Scale = scale, Bounds = new Rectangle(0, cardsTop + i * rowHeight, page.Width, rowHeight - Px(3)), Text = "" };
                    page.Controls.Add(liveCard); liveCard.SetUpdate(update, recentLevels); continue;
                }
                var entry = completed[index - liveCount]; var card = new RecordingCard { Entry = entry, Scale = scale, Selected = selectedPath == entry.Paths[0], Bounds = new Rectangle(0, cardsTop + i * rowHeight, page.Width, rowHeight - Px(3)), Text = "", AccessibleName = entry.ToString() };
                card.Click += delegate { selectedPath = entry.Paths[0]; RenderRecords(); ApplyTheme(); }; page.Controls.Add(card);
            }
            int footer = page.Height - footerHeight;
            RowActions(new[] { TextCatalog.T("previous"), (recordPage + 1) + " / " + PageCount, TextCatalog.T("next") },
                new Action[] { delegate { NextRecordPage(-1); }, delegate { }, delegate { NextRecordPage(1); } }, footer, Px(28),
                delegate(LeafButton button, int i) { button.Enabled = i == 0 ? recordPage > 0 : i == 2 && recordPage + 1 < PageCount; });
            RowActions(new[] { TextCatalog.T("open"), TextCatalog.T("revealShort"), TextCatalog.T("convertShort") },
                new Action[] { delegate { Selected(delegate(RecordingEntry e) { Process.Start(new ProcessStartInfo(e.Paths[0]) { UseShellExecute = true }); }); },
                    delegate { Selected(delegate(RecordingEntry e) { Process.Start("explorer.exe", "/select," + MediaExport.Quote(e.Paths[0])); }); },
                    delegate { if (!isBusy()) Selected(delegate(RecordingEntry e) { conversionEntry = e; currentPage = "convert"; Rebuild(); }); } }, footer + Px(tiny ? 34 : 36), Px(30),
                delegate(LeafButton button, int i) { button.Enabled = selectedPath != null && (i != 2 || !isBusy()); if (i == 2) button.Tag = "profile"; });
            ApplyTheme();
        }
        private void Selected(Action<RecordingEntry> action)
        { var entry = FinishedRecords.Find(delegate(RecordingEntry e) { return e.Paths[0] == selectedPath; }); if (entry == null) return; try { action(entry); } catch (Exception error) { Error(error); } }
        private void ScanRecords()
        {
            if (currentPage != "recordings") return; int version = ++scanVersion; libraryMessage = TextCatalog.T("loading"); RenderRecords();
            string selectedFolder = settings.RecordingsFolder(root); string[] previous = settings.LibraryFolders ?? new string[0];
            ThreadPool.QueueUserWorkItem(delegate
            {
                var result = new List<RecordingEntry>(); Exception failure = null;
                try
                {
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var folders = new List<string>(previous); folders.Add(selectedFolder);
                    var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string folder in folders) if (seen.Add(Path.GetFullPath(folder).TrimEnd('\\')))
                        foreach (var entry in RecordingCatalog.Read(SoundLeafSettings.StorageRoot(root, folder), folder)) if (files.Add(entry.Paths[0])) result.Add(entry);
                    result.Sort(delegate(RecordingEntry a, RecordingEntry b) { return b.Date.CompareTo(a.Date); });
                }
                catch (Exception error) { failure = error; }
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke(new Action(delegate { if (version != scanVersion || IsDisposed || currentPage != "recordings") return; records.Clear(); records.AddRange(result); libraryMessage = failure == null ? null : failure.Message; RenderRecords(); })); } catch (InvalidOperationException) { }
            });
        }
        private void BuildConversion()
        {
            bool tiny = ClientSize.Height / scale < 350;
            LabelAt(conversionEntry.Id, 0, Px(tiny ? 20 : 22)); tips.SetToolTip(page.Controls[0], conversionEntry.Id);
            LabelAt(TextCatalog.T("conversionCompact"), Px(tiny ? 23 : 26), Px(tiny ? 40 : 38));
            var format = Selector("format", Px(tiny ? 65 : 70), Formats(), (int)settings.Format, true);
            var rate = Selector("bitrate", Px(tiny ? 99 : 110), new object[0], -1, true);
            Action refill = delegate { rate.Items.Clear(); int[] values = format.SelectedIndex == 1 ? RecordingProfile.OpusRates : RecordingProfile.OtherRates; foreach (int value in values) rate.Items.Add(value); rate.SelectedItem = settings.Bitrates[format.SelectedIndex]; };
            refill(); format.SelectedIndexChanged += delegate { refill(); };
            RowActions(new[] { TextCatalog.T("exportShort"), TextCatalog.T("back") },
                new Action[] { delegate { Export(conversionEntry, new RecordingProfile((AudioOutputFormat)format.SelectedIndex, (int)rate.SelectedItem)); }, delegate { currentPage = "recordings"; Rebuild(); } }, Px(tiny ? 134 : 154), Px(tiny ? 26 : 34),
                delegate(LeafButton button, int i) { if (i == 0) { button.Tag = "profile"; button.Enabled = !isBusy(); } });
        }
        private void Export(RecordingEntry entry, RecordingProfile profile)
        {
            if (isBusy()) return; modal = true;
            try
            {
                using (var dialog = new SaveFileDialog { Title = TextCatalog.T("convert"), Filter = profile.Extension + "|*" + profile.Extension, AddExtension = true, DefaultExt = profile.Extension.TrimStart('.'), OverwritePrompt = true,
                    InitialDirectory = settings.RecordingsFolder(root), FileName = entry.Id + "-export-" + profile.Format + "-" + profile.Bitrate + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + profile.Extension })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        if (File.Exists(dialog.FileName) || !string.Equals(Path.GetExtension(dialog.FileName), profile.Extension, StringComparison.OrdinalIgnoreCase)) throw new IOException(TextCatalog.T("extensionCollision"));
                        actions.Convert(entry, profile, dialog.FileName); currentPage = "control"; Rebuild();
                    }
            }
            catch (Exception error) { Error(error); }
            finally { modal = false; }
        }
        internal void Error(Exception error)
        { modal = true; try { MessageBox.Show(this, TextCatalog.Translate(error.Message), "SoundLeaf", MessageBoxButtons.OK, MessageBoxIcon.Error); } finally { modal = false; } }
        internal bool IsDark()
        {
            if (settings.Theme != "system") return settings.Theme == "dark";
            try { using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 1)) == 0; } catch (Exception) { return false; }
        }
        internal void ApplyTheme()
        {
            bool dark = IsDark(); Color background = ColorTranslator.FromHtml(dark ? "#102019" : "#F3F7F2"), foreground = ColorTranslator.FromHtml(dark ? "#E3EFE8" : "#14261B");
            PaintControls(this, background, foreground, dark); Invalidate(true);
        }
        private void PaintControls(Control parent, Color background, Color foreground, bool dark)
        {
            parent.BackColor = background; parent.ForeColor = foreground;
            foreach (Control child in parent.Controls)
            {
                PaintControls(child, background, foreground, dark);
                if (child is LeafButton) { child.BackColor = ColorTranslator.FromHtml(dark ? "#254B36" : "#DCECDC"); child.ForeColor = foreground; }
                var live = child as LiveRecordingCard; if (live != null) live.Meter.BackColor = child.BackColor;
                if (child.Parent == nav && child.Tag as string == currentPage) { child.BackColor = ColorTranslator.FromHtml(dark ? "#326D47" : "#217347"); child.ForeColor = Color.White; }
                var select = child as LeafSelect; if (select != null) { select.Dark = dark; select.BackColor = ColorTranslator.FromHtml(dark ? "#193025" : "#FFFFFF"); }
                var meter = child as AudioLevelView; if (meter != null) { meter.Dark = dark; meter.BackColor = child.Parent is LiveRecordingCard ? child.Parent.BackColor : ColorTranslator.FromHtml(dark ? "#152B20" : "#E7EFE6"); }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { focusTimer.Stop(); focusTimer.Dispose(); CancelGrowth(); growthTimer.Dispose(); tips.Dispose(); }
            base.Dispose(disposing);
            if (disposing) { foreach (var font in ownedFonts) font.Dispose(); ownedFonts.Clear(); }
        }
    }
}
