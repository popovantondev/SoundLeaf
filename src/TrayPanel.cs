using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;

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
        internal LeafHeader() { DoubleBuffered = true; Height = 66; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = e.Graphics.DpiX / 96f * Font.Size / 9.5f;
            using (var leaf = SoundLeafIcons.Brand((int)(36 * scale))) e.Graphics.DrawImage(leaf, 14 * scale, 10 * scale, 36 * scale, 36 * scale);
            using (var title = new Font("Segoe UI", Font.Size * 2, FontStyle.Bold))
            using (var brush = new SolidBrush(ForeColor)) e.Graphics.DrawString("SoundLeaf", title, brush, 58 * scale, 7 * scale);
            TextRenderer.DrawText(e.Graphics, TextCatalog.T("educational"), Font, new Rectangle((int)(16 * scale), (int)(43 * scale), Width - (int)(32 * scale), (int)(22 * scale)), ForeColor, TextFormatFlags.EndEllipsis);
            using (var pen = new Pen(Color.FromArgb(40, ForeColor), 1.5f))
            { e.Graphics.DrawBezier(pen, Width - 55, 7, Width - 23, 6, Width - 4, 20, Width - 22, 34); e.Graphics.DrawLine(pen, Width - 50, 25, Width - 18, 11); }
        }
    }
    internal sealed class LeafButton : Button
    {
        private bool hover;
        internal LeafButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? BackColor : Parent.BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float radius = Math.Min(16, Height / 3f);
            using (var shape = new GraphicsPath())
            {
                float w = Width - 1, h = Height - 1;
                shape.AddArc(0, 0, radius, radius, 180, 90); shape.AddArc(w - radius, 0, radius, radius, 270, 90); shape.AddArc(w - radius, h - radius, radius, radius, 0, 90); shape.AddArc(0, h - radius, radius, radius, 90, 90); shape.CloseFigure();
                using (var brush = new SolidBrush(hover && Enabled ? ControlPaint.Light(BackColor, 0.08f) : BackColor)) e.Graphics.FillPath(brush, shape);
                if (Focused) using (var pen = new Pen(Color.FromArgb(45, 139, 85), 2)) e.Graphics.DrawPath(pen, shape);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Color.FromArgb(110, 130, 117), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
    internal sealed class TrayPanel : Form
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        internal static void InitializeDpi() { SetProcessDPIAware(); }
        private readonly SoundLeafSettings settings;
        private readonly string root;
        private readonly PanelActions actions;
        private readonly Func<bool> isBusy;
        private readonly Panel page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16, 10, 16, 12) };
        private readonly TableLayoutPanel shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        private readonly FlowLayoutPanel nav = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0), WrapContents = false };
        private readonly LeafHeader header = new LeafHeader { Dock = DockStyle.Fill };
        private string currentPage = "control";
        private SessionUpdate update = new SessionUpdate(RecordState.Starting, "", 0, false);
        private bool wavAvailable, startup, modal;
        private Label status, details;
        private Button start, pause, stop, wav;
        private ListBox list;
        private readonly List<RecordingEntry> records = new List<RecordingEntry>();
        private int scanVersion;
        private float ScaleFactor { get { using (var g = CreateGraphics()) return g.DpiX / 96f * Font.Size / 9.5f; } }
        private int Px(int value) { return (int)Math.Round(value * ScaleFactor); }
        internal TrayPanel(string root, SoundLeafSettings settings, PanelActions actions, Func<bool> isBusy)
        {
            this.root = root; this.settings = settings; this.actions = actions; this.isBusy = isBusy;
            Text = "SoundLeaf"; ShowInTaskbar = false; TopMost = true; FormBorderStyle = FormBorderStyle.None;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.5f); ClientSize = new Size(430, 560); MinimumSize = new Size(300, 240);
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 72)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.Controls.Add(header, 0, 0); shell.Controls.Add(nav, 0, 1); shell.Controls.Add(page, 0, 2); Controls.Add(shell);
            Deactivate += delegate { if (!modal) Hide(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            Rebuild();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        { if (keyData == Keys.Escape) { Hide(); return true; } return base.ProcessCmdKey(ref msg, keyData); }
        internal static Rectangle Placement(Point point, Size desired, Rectangle work)
        {
            int width = Math.Min(desired.Width, work.Width - 16), height = Math.Min(desired.Height, work.Height - 16);
            return new Rectangle(Math.Max(work.Left + 8, Math.Min(point.X - width, work.Right - width - 8)),
                Math.Max(work.Top + 8, Math.Min(point.Y - height, work.Bottom - height - 8)), width, height);
        }
        internal void Toggle(Point point)
        {
            if (Visible) { Hide(); return; }
            Rectangle bounds = Placement(point, new Size(Px(430), Px(560)), Screen.FromPoint(point).WorkingArea);
            Bounds = bounds; Rebuild(); Show(); Activate();
        }
        internal void SetState(SessionUpdate value, bool canWav, bool autoStart)
        {
            update = value; wavAvailable = canWav; startup = autoStart;
            if (currentPage == "control") UpdateControls();
            else if (Visible) SetProfileEnabled(page, !isBusy());
        }
        private void SetProfileEnabled(Control parent, bool value)
        { foreach (Control item in parent.Controls) { if (item.Tag as string == "profile") item.Enabled = value; if (item.HasChildren) SetProfileEnabled(item, value); } }
        internal void Rebuild()
        {
            DisposeChildren(nav); DisposeChildren(page);
            shell.RowStyles[0].Height = Px(72); shell.RowStyles[1].Height = Px(42); nav.Padding = new Padding(Px(12), 0, 0, 0); page.Padding = new Padding(Px(16), Px(10), Px(16), Px(12));
            foreach (string name in new[] { "control", "recordings", "settings" })
            { string target = name; Button button = ButtonFor(TextCatalog.T(name), delegate { currentPage = target; Rebuild(); }); button.Width = Px(124); button.Tag = name; nav.Controls.Add(button); }
            if (currentPage == "control") BuildControls(); else if (currentPage == "recordings") BuildRecords(); else BuildSettings();
            ApplyTheme(); header.Invalidate();
        }
        internal void ShowPage(string name) { currentPage = name; Rebuild(); }
        internal void VerifyEscape() { Message message = new Message(); ProcessCmdKey(ref message, Keys.Escape); }
        private static void DisposeChildren(Control parent) { while (parent.Controls.Count > 0) parent.Controls[0].Dispose(); }
        private TableLayoutPanel Stack()
        { var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(0), Margin = new Padding(0) }; stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); page.Controls.Add(stack); return stack; }
        private Label LabelFor(string text)
        { return new Label { Text = text, AutoSize = true, MaximumSize = new Size(Math.Max(Px(220), ClientSize.Width - Px(42)), 0), Margin = new Padding(0, Px(5), 0, Px(8)), AccessibleName = text }; }
        private Button ButtonFor(string text, Action action)
        { var button = new LeafButton { Text = text, Height = Px(36), AutoSize = false, Width = Px(370), Dock = DockStyle.Top, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, Px(3), 0, Px(5)), AccessibleName = text }; button.FlatAppearance.BorderSize = 0; button.Click += delegate { action(); }; return button; }
        private void AddAction(TableLayoutPanel stack, string text, Action action) { stack.Controls.Add(ButtonFor(text, action)); }
        private void BuildControls()
        {
            var stack = Stack(); status = LabelFor(""); status.Font = new Font(Font.FontFamily, Font.Size + 3, FontStyle.Bold); stack.Controls.Add(status);
            details = LabelFor(""); stack.Controls.Add(details);
            start = ButtonFor(TextCatalog.T("message.2"), actions.Start); pause = ButtonFor(TextCatalog.T("message.5"), actions.Pause); stop = ButtonFor(TextCatalog.T("message.6"), actions.Stop);
            wav = ButtonFor(TextCatalog.T("message.3"), actions.Wav);
            stack.Controls.Add(start); stack.Controls.Add(pause); stack.Controls.Add(stop); stack.Controls.Add(wav);
            AddAction(stack, TextCatalog.T("message.7"), actions.Recover);
            AddAction(stack, TextCatalog.T("message.16"), actions.OpenLog);
            AddAction(stack, TextCatalog.T("exit"), actions.Exit); UpdateControls();
        }
        private void UpdateControls()
        {
            if (status == null || status.IsDisposed) return;
            status.Text = TextCatalog.Translate(update.Message);
            status.AccessibleName = status.Text;
            details.Text = (update.Mode == RecordingMode.WavOnly ? "WAV" : update.Profile.Label) + "\n" + TimeSpan.FromSeconds(update.Seconds).ToString(@"hh\:mm\:ss") +
                (string.IsNullOrEmpty(update.Warning) ? "" : "\n" + TextCatalog.Translate(update.Warning));
            bool active = update.State == RecordState.Recording || update.State == RecordState.Paused;
            start.Enabled = !isBusy(); pause.Enabled = active; stop.Enabled = active || update.State == RecordState.Starting;
            pause.Text = TextCatalog.T(update.State == RecordState.Paused ? "message.27" : "message.5"); wav.Visible = wavAvailable; wav.Enabled = wavAvailable && !isBusy();
            details.AccessibleName = details.Text; pause.AccessibleName = pause.Text;
        }
        private void BuildSettings()
        {
            var stack = Stack(); stack.Controls.Add(LabelFor(TextCatalog.T("format")));
            ComboBox formats = Combo(); for (int i = 0; i < 5; i++) formats.Items.Add(new RecordingProfile((AudioOutputFormat)i, settings.Bitrates[i]).Label); formats.SelectedIndex = (int)settings.Format;
            formats.Tag = "profile"; formats.Enabled = !isBusy(); stack.Controls.Add(formats);
            stack.Controls.Add(LabelFor(TextCatalog.T("bitrate")));
            ComboBox rates = Combo(); int[] available = settings.Format == AudioOutputFormat.Opus ? RecordingProfile.OpusRates : RecordingProfile.OtherRates;
            foreach (int rate in available) rates.Items.Add(rate + " kbps"); rates.SelectedIndex = Array.IndexOf(available, settings.Profile.Bitrate); rates.Tag = "profile"; rates.Enabled = !isBusy(); stack.Controls.Add(rates);
            formats.SelectedIndexChanged += delegate { if (isBusy()) return; settings.Format = (AudioOutputFormat)formats.SelectedIndex; Save(); Rebuild(); };
            rates.SelectedIndexChanged += delegate { if (isBusy()) return; settings.Bitrates[(int)settings.Format] = available[rates.SelectedIndex]; Save(); };
            stack.Controls.Add(LabelFor(TextCatalog.T("channels")));
            stack.Controls.Add(LabelFor(TextCatalog.T("language"))); ComboBox language = Combo(); language.Items.AddRange(new object[] { "Deutsch", "Русский", "English" }); language.SelectedIndex = settings.Language == "de" ? 0 : settings.Language == "ru" ? 1 : 2; stack.Controls.Add(language);
            language.SelectedIndexChanged += delegate { settings.Language = new[] { "de", "ru", "en" }[language.SelectedIndex]; TextCatalog.SetLanguage(settings.Language); Save(); Rebuild(); };
            stack.Controls.Add(LabelFor(TextCatalog.T("theme"))); ComboBox theme = Combo(); foreach (string key in new[] { "system", "light", "dark" }) theme.Items.Add(TextCatalog.T(key)); theme.SelectedIndex = Array.IndexOf(new[] { "system", "light", "dark" }, settings.Theme); stack.Controls.Add(theme);
            theme.SelectedIndexChanged += delegate { settings.Theme = new[] { "system", "light", "dark" }[theme.SelectedIndex]; Save(); Rebuild(); };
            stack.Controls.Add(LabelFor(TextCatalog.T("folder"))); stack.Controls.Add(LabelFor(settings.RecordingsFolder(root)));
            Button choose = ButtonFor(TextCatalog.T("choose"), ChooseFolder); choose.Tag = "profile"; choose.Enabled = !isBusy(); stack.Controls.Add(choose);
            AddAction(stack, TextCatalog.T("autostart") + (startup ? " ✓" : ""), actions.Autostart);
        }
        private ComboBox Combo()
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Px(24), Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, Px(8)), AccessibleName = TextCatalog.T("settings") };
            combo.DrawItem += delegate(object sender, DrawItemEventArgs e) { using (var brush = new SolidBrush(combo.BackColor)) e.Graphics.FillRectangle(brush, e.Bounds); if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, combo.Items[e.Index].ToString(), combo.Font, e.Bounds, combo.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis); };
            return combo;
        }
        private void Save()
        {
            try { settings.Save(root); actions.SaveSettings(); }
            catch (Exception error)
            {
                bool restored; var persisted = SoundLeafSettings.Load(root, out restored);
                settings.Format = persisted.Format; settings.Bitrates = persisted.Bitrates;
                settings.Language = persisted.Language; settings.Theme = persisted.Theme;
                settings.OutputFolder = persisted.OutputFolder; settings.LibraryFolders = persisted.LibraryFolders;
                TextCatalog.SetLanguage(settings.Language); Error(error); Rebuild();
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
                        string previous = settings.RecordingsFolder(root); string destination = Path.GetFullPath(dialog.SelectedPath);
                        var check = StartupChecks.Check(destination, "", RecordingMode.WavOnly);
                        if (!check.CanRecord) throw new IOException(check.Error);
                        var folders = new List<string>(settings.LibraryFolders ?? new string[0]); if (!folders.Contains(previous)) folders.Add(previous);
                        settings.OutputFolder = destination; settings.LibraryFolders = folders.ToArray(); Save(); Rebuild();
                    }
            }
            catch (Exception error) { Error(error); }
            finally { modal = false; }
        }
        private void BuildRecords()
        {
            var stack = Stack(); AddAction(stack, TextCatalog.T("refresh"), ScanRecords);
            list = new ListBox { Dock = DockStyle.Top, Height = Px(245), DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Px(72), IntegralHeight = false, BorderStyle = BorderStyle.None, AccessibleName = TextCatalog.T("recordings") };
            list.DrawItem += DrawRecord; stack.Controls.Add(list);
            AddAction(stack, TextCatalog.T("open"), delegate { Selected(delegate(RecordingEntry e) { Process.Start(new ProcessStartInfo(e.Paths[0]) { UseShellExecute = true }); }); });
            AddAction(stack, TextCatalog.T("reveal"), delegate { Selected(delegate(RecordingEntry e) { Process.Start("explorer.exe", "/select," + MediaExport.Quote(e.Paths[0])); }); });
            Button convert = ButtonFor(TextCatalog.T("convert"), delegate { if (!isBusy()) Selected(BuildConversion); }); convert.Tag = "profile"; convert.Enabled = !isBusy(); stack.Controls.Add(convert);
            ScanRecords();
        }
        private void DrawRecord(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return; e.DrawBackground(); RecordingEntry entry = list.Items[e.Index] as RecordingEntry;
            string text = entry == null ? list.Items[e.Index].ToString() : entry.Date.ToString("yyyy-MM-dd HH:mm") + " · " + entry.Format + "\n" +
                (entry.Bytes / 1048576.0).ToString("0.0") + " MiB · " + (entry.Seconds.HasValue ? TimeSpan.FromSeconds(entry.Seconds.Value).ToString(@"hh\:mm\:ss") : TextCatalog.T("unknown")) + "\n" + TextCatalog.T(entry.Verified ? "verified" : "existing");
            TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 6, e.Bounds.Width - 16, e.Bounds.Height - 8), e.ForeColor, TextFormatFlags.WordBreak); e.DrawFocusRectangle();
        }
        private void Selected(Action<RecordingEntry> action)
        { var entry = list == null ? null : list.SelectedItem as RecordingEntry; if (entry == null) return; try { action(entry); } catch (Exception error) { Error(error); } }
        private void ScanRecords()
        {
            if (list == null || list.IsDisposed) return; int version = ++scanVersion; list.Items.Clear(); list.Items.Add(TextCatalog.T("loading"));
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
                try { BeginInvoke(new Action(delegate { if (version != scanVersion || list == null || list.IsDisposed || currentPage != "recordings") return; records.Clear(); records.AddRange(result); list.Items.Clear(); foreach (var item in records) list.Items.Add(item); if (list.Items.Count == 0) list.Items.Add(failure == null ? TextCatalog.T("empty") : failure.Message); })); } catch (InvalidOperationException) { }
            });
        }
        private void BuildConversion(RecordingEntry entry)
        {
            currentPage = "convert"; DisposeChildren(page); var stack = Stack(); stack.Controls.Add(LabelFor(entry.Id)); stack.Controls.Add(LabelFor(TextCatalog.T("conversionWarning")));
            ComboBox format = Combo(); foreach (AudioOutputFormat value in Enum.GetValues(typeof(AudioOutputFormat))) format.Items.Add(value.ToString()); format.SelectedIndex = (int)settings.Format; stack.Controls.Add(format);
            ComboBox rate = Combo(); Action refill = delegate { rate.Items.Clear(); int[] values = format.SelectedIndex == 1 ? RecordingProfile.OpusRates : RecordingProfile.OtherRates; foreach (int value in values) rate.Items.Add(value); rate.SelectedItem = settings.Bitrates[format.SelectedIndex]; }; refill(); format.SelectedIndexChanged += delegate { refill(); }; stack.Controls.Add(rate);
            AddAction(stack, TextCatalog.T("export"), delegate
            {
                if (isBusy()) return; modal = true;
                try
                {
                    var profile = new RecordingProfile((AudioOutputFormat)format.SelectedIndex, (int)rate.SelectedItem);
                    using (var dialog = new SaveFileDialog { Title = TextCatalog.T("convert"), Filter = profile.Extension + "|*" + profile.Extension, AddExtension = true, DefaultExt = profile.Extension.TrimStart('.'), OverwritePrompt = true,
                        InitialDirectory = settings.RecordingsFolder(root), FileName = entry.Id + "-export-" + profile.Format + "-" + profile.Bitrate + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + profile.Extension })
                        if (dialog.ShowDialog(this) == DialogResult.OK) { if (File.Exists(dialog.FileName) || !string.Equals(Path.GetExtension(dialog.FileName), profile.Extension, StringComparison.OrdinalIgnoreCase)) throw new IOException(TextCatalog.T("extensionCollision")); actions.Convert(entry, profile, dialog.FileName); currentPage = "control"; Rebuild(); }
                }
                catch (Exception error) { Error(error); }
                finally { modal = false; }
            });
            AddAction(stack, TextCatalog.T("back"), delegate { currentPage = "recordings"; Rebuild(); }); ApplyTheme();
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
                if (child is Button) { child.BackColor = ColorTranslator.FromHtml(dark ? "#254B36" : "#DCECDC"); child.ForeColor = foreground; }
                if (child.Parent == nav && child.Tag as string == currentPage) { child.BackColor = ColorTranslator.FromHtml(dark ? "#326D47" : "#217347"); child.ForeColor = Color.White; }
                if (child is ComboBox || child is ListBox) child.BackColor = ColorTranslator.FromHtml(dark ? "#193025" : "#FFFFFF");
            }
        }
    }
}
