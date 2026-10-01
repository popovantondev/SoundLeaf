using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SoundLeaf
{
    internal static class UiTests
    {
        [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
        [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
        private static int checks, renders;
        private static void Assert(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        private static void Pump(int milliseconds)
        { var watch = Stopwatch.StartNew(); do { Application.DoEvents(); Thread.Sleep(5); } while (watch.ElapsedMilliseconds < milliseconds); }
        private static void Key(Control target, Keys key)
        { typeof(Control).GetMethod("ProcessCmdKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, new object[] { new Message(), key }); }
        private static void Outside(TrayPanel panel)
        {
            using (var outside = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(30, 30, 100, 80), FormBorderStyle = FormBorderStyle.FixedToolWindow, TopMost = true })
            { outside.Show(); outside.Activate(); Pump(350); Assert(!panel.Visible, "Outside focus did not hide panel."); }
        }
        private static IEnumerable<Control> Children(Control parent)
        { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Children(child)) yield return nested; } }
        private static void Layout(TrayPanel panel)
        {
            panel.PerformLayout(); Application.DoEvents(); Assert(panel.Visible, "Panel hidden during layout.");
            Assert(panel.StartPosition == FormStartPosition.Manual && !panel.ShowInTaskbar && panel.MinimumSize == Size.Empty, "Window positioning/taskbar/minimum mismatch.");
            foreach (Control child in Children(panel))
            {
                var scrolling = child as ScrollableControl;
                if (scrolling != null) Assert(!scrolling.AutoScroll && !scrolling.VerticalScroll.Visible && !scrolling.HorizontalScroll.Visible, "Scrollbar enabled.");
                if (!child.Visible || child.Width == 0 || child.Height == 0) continue;
                if (child is Label || child is Button || child is LeafSelect)
                {
                    int lineHeight = TextRenderer.MeasureText("Agyp Дру", child.Font, Size.Empty, TextFormatFlags.SingleLine).Height;
                    Assert(child.Height >= lineHeight, "Text line clipped vertically: " + child.AccessibleName + " height=" + child.Height + " needs=" + lineHeight);
                    if (child is RecordingCard) Assert(child.Height >= lineHeight * 3 + (int)(4 * ((RecordingCard)child).Scale), "Recording card text clipped.");
                }
                Rectangle rect = child.RectangleToScreen(child.ClientRectangle);
                Assert(panel.RectangleToScreen(panel.ClientRectangle).Contains(rect), "Element outside window: " + child.AccessibleName);
                if (child.Parent != null) Assert(child.Parent.RectangleToScreen(child.Parent.ClientRectangle).Contains(rect), "Element clipped by parent: " + child.AccessibleName);
            }
            var controls = new List<Control>(); foreach (Control child in Children(panel)) if (child.Visible && child.Parent != panel && (child is Button || child is LeafSelect)) controls.Add(child);
            for (int i = 0; i < controls.Count; i++) for (int j = i + 1; j < controls.Count; j++)
                if (controls[i].Parent == controls[j].Parent) Assert(!controls[i].Bounds.IntersectsWith(controls[j].Bounds), "Interactive elements overlap.");
        }
        private static void Render(TrayPanel panel, string path)
        {
            Layout(panel); using (var image = panel.CapturePanel())
            {
                Assert(image.GetPixel(0, 0).ToArgb() == panel.BackColor.ToArgb(), "Cached client image includes a non-client caption.");
                int variation = 0; var color = image.GetPixel(0, 0);
                for (int y = 8; y < image.Height; y += 8) for (int x = 8; x < image.Width; x += 8) if (image.GetPixel(x, y).ToArgb() != color.ToArgb()) variation++;
                Assert(variation > 100, "Blank render."); image.Save(path); renders++;
            }
        }
        private static PanelActions Commands()
        { return new PanelActions { Start = delegate { }, Pause = delegate { }, Stop = delegate { }, Wav = delegate { }, Recover = delegate { }, Exit = delegate { }, Autostart = delegate { }, OpenLog = delegate { }, SaveSettings = delegate { }, Convert = delegate { } }; }
        private static List<RecordingEntry> Demo()
        {
            var entries = new List<RecordingEntry>();
            for (int i = 0; i < 31; i++) entries.Add(new RecordingEntry { Id = "Demo-" + i, Format = "ogg / opus · 24 kbps", Date = new DateTime(2026, 10, 1, 12, 0, 0).AddMinutes(-i), Bytes = 1234567, Seconds = i == 1 ? (double?)null : 120, Verified = i != 1, Paths = new[] { @"C:\Demo\Recording-" + i + ".ogg" } });
            return entries;
        }
        private static void ComposedCorners(TrayPanel panel, TrayAnchor anchor, string root)
        {
            Rectangle backdropBounds = panel.Bounds; backdropBounds.Inflate(8, 8);
            using (var backdrop = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, FormBorderStyle = FormBorderStyle.None, Bounds = backdropBounds, BackColor = Color.Black, TopMost = true })
            {
                backdrop.Show(); panel.Open(anchor, false); Pump(100);
                using (var picture = new Bitmap(panel.Width, panel.Height))
                {
                    using (var graphics = Graphics.FromImage(picture)) graphics.CopyFromScreen(panel.Location, Point.Empty, panel.Size);
                    picture.Save(Path.Combine(root, "native-composed-corners-" + (panel.BackColor.R > 100 ? "light" : "dark") + ".png"));
                    Assert(picture.GetPixel(0, 0).R < 30 && picture.GetPixel(0, 0).G < 30, "Native compositor did not round the window.");
                    int soft = 0; Color surface = panel.BackColor;
                    for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++) { Color p = picture.GetPixel(x, y); if (p.G > 1 && p.G < surface.G - 1 && Math.Abs(p.R - p.G) < 20) soft++; }
                    Assert(soft > 0, "Rounded corner has no composited antialiasing.");
                }
            }
        }
        [STAThread] private static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            int result = 1;
            using (var dispatcher = new Control())
            {
                IntPtr handle = dispatcher.Handle;
                dispatcher.BeginInvoke(new Action(delegate { result = Run(args); Application.ExitThread(); }));
                Application.Run();
            }
            return result;
        }
        private static int Run(string[] args)
        {
            try
            {
                Assert(AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), new IntPtr(-4)), "Per-Monitor V2 manifest missing.");
                string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Verification", "ui-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)); Directory.CreateDirectory(root);
                if (args.Length == 0) foreach (string language in new[] { "ru", "de", "en" }) foreach (string theme in new[] { "light", "dark" }) foreach (float scale in new[] { 1f, 1.5f, 2f }) foreach (int height in new[] { 520, 260 })
                {
                    TextCatalog.SetLanguage(language);
                    var settings = new SoundLeafSettings { Language = language, Theme = theme, OutputFolder = @"C:\Demo\A deliberately long audio storage folder with Unicode — пример\Recordings" };
                    using (var panel = new TrayPanel(root, settings, Commands(), delegate { return false; }))
                    {
                        Console.WriteLine("UI " + language + " " + theme + " " + scale + " " + height);
                        panel.ConfigureForVerification(scale, new Size(height == 260 ? 320 : 430, height));
                        panel.Location = new Point(20, 20); panel.Show(); Pump(10);
                        foreach (string name in height == 260 ? new[] { "control", "capture", "files", "appearance", "recordings", "convert" } : new[] { "control", "settings", "recordings", "convert" })
                        {
                            if (name == "capture" || name == "files" || name == "appearance") panel.ShowSettingsGroup(name);
                            else if (name == "convert") panel.ShowConversionForVerification(Demo()[0]);
                            else panel.ShowPage(name);
                            string longPath = settings.OutputFolder + new string('x', 350);
                            panel.SetState(new SessionUpdate(RecordState.Faulted, TextCatalog.T("diagnostic.11") + " " + longPath, 123, false) { Warning = TextCatalog.T("restored") + " " + longPath }, true, true);
                            if (name == "recordings") panel.SetRecordsForVerification(Demo());
                            Render(panel, Path.Combine(root, language + "-" + theme + "-" + (int)(scale * 100) + "-" + height + "-" + name + ".png"));
                            foreach (Control child in new List<Control>(Children(panel)))
                            {
                                var select = child as LeafSelect; if (select == null || !select.Enabled) continue;
                                Assert(select.AccessibilityObject.Role == AccessibleRole.ComboBox, "Missing combo accessibility.");
                                if (select.AccessibleName == TextCatalog.T("format")) foreach (object item in select.Items) Assert(item.ToString() == item.ToString().ToLowerInvariant(), "Mixed-case format.");
                                select.TogglePopup(); Pump(10);
                                Assert(select.IsOpen && panel.Visible, "Dropdown hid owner.");
                                Assert(Screen.FromRectangle(select.PopupBounds).WorkingArea.Contains(select.PopupBounds), "Dropdown off-screen.");
                                panel.VerifyEscape(); Assert(!select.IsOpen && panel.Visible, "First Escape should close only dropdown.");
                            }
                            if (name == "recordings")
                            {
                                int total = panel.PageCount; Assert(total > 1, "Pagination missing."); panel.NextRecordPage(1); Assert(panel.RecordPage == 1, "Next failed."); Layout(panel);
                                panel.NextRecordPage(999); Assert(panel.RecordPage == total - 1, "Last page wrong."); Layout(panel);
                            }
                        }
                        Assert(panel.SelectNextControl(null, true, true, true, false), "No keyboard target.");
                        panel.ShowPage("control");
                        var liveUpdate = new SessionUpdate(RecordState.Recording, TextCatalog.T("message.52"), 256, true)
                        { DeviceAvailable = true, SessionId = "Demo-live", SessionStarted = new DateTime(2026, 10, 1, 12, 0, 0), CapturedBytes = 1234567, Peak = .3f, Rms = .1f };
                        for (int sample = 0; sample < 25; sample++) { liveUpdate.Peak = .05f + sample % 7 * .08f; liveUpdate.Rms = liveUpdate.Peak * .3f; panel.SetState(liveUpdate, false, true); }
                        Pump(40);
                        Render(panel, Path.Combine(root, language + "-" + theme + "-" + (int)(scale * 100) + "-" + height + "-live-control.png"));
                        var meter = new List<Control>(Children(panel)).Find(delegate(Control c) { return c is AudioLevelView; }) as AudioLevelView;
                        if (meter != null) Assert(meter.HasCurrentSignal && Math.Abs(AudioLevelView.Expressiveness - .95f) < .0001, "Live level/95% expressiveness missing.");
                        panel.ShowPage("recordings"); panel.SetRecordsForVerification(Demo()); panel.SetState(liveUpdate, false, true); Pump(40);
                        var card = new List<Control>(Children(panel)).Find(delegate(Control c) { return c is LiveRecordingCard; }) as LiveRecordingCard;
                        Assert(card != null && card.Meter.HasCurrentSignal, "Current recording absent from list.");
                        Render(panel, Path.Combine(root, language + "-" + theme + "-" + (int)(scale * 100) + "-" + height + "-live-recordings.png"));
                        liveUpdate.Peak = liveUpdate.Rms = 0; panel.SetState(liveUpdate, false, true);
                        Assert(!card.Meter.HasCurrentSignal && !card.Meter.TimerRunning && liveUpdate.HeardSignal, "Silence caused false error or synthetic animation.");
                        Assert(new List<Control>(Children(panel)).Contains(card), "Telemetry rebuilt the live card.");
                        panel.NextRecordPage(999); Layout(panel); Assert(new List<Control>(Children(panel)).Exists(delegate(Control c) { return c is RecordingCard; }), "Live card displaced completed records.");
                        panel.SetState(new SessionUpdate(RecordState.Paused, TextCatalog.T("message.5"), 256, true) { SessionId = "Demo-live", DeviceAvailable = true }, false, true);
                        panel.NextRecordPage(-999); Layout(panel);
                        var pausedCard = new List<Control>(Children(panel)).Find(delegate(Control c) { return c is LiveRecordingCard; }) as LiveRecordingCard;
                        Assert(pausedCard != null && !pausedCard.Meter.HasCurrentSignal, "Pause still showed audio.");
                        panel.SetState(new SessionUpdate(RecordState.Faulted, "demo", 256, false), true, true);
                        if (height == 260)
                        {
                            panel.ConfigureForVerification(scale, new Size(320, 350));
                            foreach (string name in new[] { "control", "capture", "files", "appearance", "recordings", "convert" })
                            {
                                if (name == "capture" || name == "files" || name == "appearance") panel.ShowSettingsGroup(name);
                                else if (name == "convert") panel.ShowConversionForVerification(Demo()[0]); else panel.ShowPage(name);
                                if (name == "recordings") panel.SetRecordsForVerification(Demo());
                                Layout(panel);
                            }
                        }
                        panel.VerifyEscape(); Assert(!panel.Visible && !panel.Animating && !panel.SnapshotAllocated, "Escape left resources.");
                    }
                }
                foreach (float scale in new[] { 1f, 1.5f, 2f }) foreach (var work in new[] { new Rectangle(0, 0, 1920, 1040), new Rectangle(-1280, 0, 1280, 680), new Rectangle(0, 40, 800, 560) }) foreach (TrayEdge edge in Enum.GetValues(typeof(TrayEdge)))
                {
                    foreach (var point in new[] { work.Location, new Point(work.Right, work.Bottom), new Point(work.Left, work.Bottom), new Point(work.Right, work.Top) })
                    {
                        var anchor = new TrayAnchor { Icon = new Rectangle(point, new Size(20, 20)), Work = work, Monitor = work, Edge = edge, Scale = scale };
                        Rectangle placed = TrayAnchorResolver.Place(anchor, new Size((int)(430 * scale), (int)(520 * scale))); Assert(work.Contains(placed), "Placement off-screen.");
                    }
                }
                using (var icon = new NotifyIcon { Icon = SoundLeafIcons.Create(0, 0), Text = "SoundLeaf verification", Visible = true })
                {
                    Pump(200); Rectangle rect; bool native = TrayAnchorResolver.TryGetRectangle(icon, out rect);
                    Assert(native && rect.Width > 0, "Actual tray identity/rectangle lookup failed.");
                    var resolved = TrayAnchorResolver.Resolve(icon, new Point(1, 1));
                    Assert(resolved.Native && resolved.Icon == rect, "Cursor incorrectly replaced native anchor.");
                    var nativeSettings = new SoundLeafSettings { Theme = "light" };
                    using (var panel = new TrayPanel(root, nativeSettings, Commands(), delegate { return false; }))
                    {
                        panel.Open(resolved, false); Pump(20); Assert(resolved.Work.Contains(panel.Bounds), "Actual first-open outside work area."); Layout(panel); Assert(panel.Region == null, "Aliased window region is still used.");
                        ComposedCorners(panel, resolved, root);
                        nativeSettings.Theme = "dark"; panel.ApplyTheme(); ComposedCorners(panel, resolved, root); nativeSettings.Theme = "light"; panel.ApplyTheme();
                        Assert(Math.Abs(TrayAnchorResolver.GetDpiForWindow(panel.Handle) / 96f - resolved.Scale) < .01, "Native monitor scale mismatch.");
                        var stable = panel.Bounds; panel.HidePanel(); panel.Open(resolved, false); Assert(panel.Bounds == stable, "Repeated open moved window.");
                        panel.HidePanel(); panel.Open(resolved, true); Assert(panel.Animating && panel.SnapshotAllocated, "Animation did not allocate frame.");
                        var motionBounds = panel.Bounds; Pump(60); Assert(panel.Bounds == motionBounds, "Opening animation resized real controls.");
                        using (var image = panel.CaptureMotionForVerification()) { Assert(image != null && image.GetPixel(0, 0).A == 0, "Motion corners are opaque."); image.Save(Path.Combine(root, "native-opening-frame.png")); }
                        panel.VerifyEscape(); Pump(350); Assert(!panel.Visible && !panel.Animating && !panel.SnapshotAllocated, "Animation cancellation leaked.");
                        panel.Open(resolved, true); Pump(300); Assert(panel.Visible && !panel.Animating && !panel.SnapshotAllocated && panel.Bounds == stable, "Animation finish incorrect."); Layout(panel);
                        panel.HidePanel(); panel.Open(resolved, true); panel.Toggle(resolved);
                        Pump(350); Assert(!panel.Visible && !panel.Animating && !panel.SnapshotAllocated, "Repeated click did not cancel growth.");
                        panel.Open(resolved, true); Outside(panel);
                        Assert(!panel.Visible && !panel.Animating && !panel.SnapshotAllocated, "Focus loss did not cancel growth.");
                        panel.Open(resolved, false);
                        using (var selector = new LeafSelect { Parent = panel, Bounds = new Rectangle(10, 110, 280, 32), BackColor = Color.White, ForeColor = Color.Black, Font = panel.Font })
                        {
                            selector.BringToFront(); selector.Items.AddRange(new object[] { "first", "second", "third" }); selector.SelectedIndex = 0; selector.Focus();
                            Key(selector, Keys.Down); Assert(selector.SelectedIndex == 1, "Arrow selection failed.");
                            Key(selector, Keys.Space); Assert(selector.IsOpen && panel.Visible, "Space did not open dropdown.");
                            var choices = (Control)typeof(LeafSelect).GetField("choices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(selector);
                            Assert(choices.AccessibilityObject.GetChildCount() == 3, "List accessibility missing choices.");
                            Assert(selector.AccessibilityObject.GetChildCount() == 1 && choices.AccessibilityObject.Parent == selector.AccessibilityObject, "Accessible popup is not linked to field.");
                            Assert(!choices.AccessibilityObject.GetChild(1).Bounds.IsEmpty && choices.AccessibilityObject.GetSelected().Name == "second", "Accessible selection or bounds missing.");
                            Key(choices, Keys.End); Key(choices, Keys.Enter); Pump(20); Assert(selector.SelectedIndex == 2 && !selector.IsOpen && panel.Visible, "Enter did not commit choice: index=" + selector.SelectedIndex + " open=" + selector.IsOpen + " visible=" + panel.Visible);
                            Key(selector, Keys.Enter); panel.VerifyEscape(); Pump(20); Assert(!selector.IsOpen && panel.Visible, "Escape hierarchy failed.");
                            selector.Location = new Point(10, panel.Height - 40); selector.TogglePopup(); Assert(selector.PopupBounds.Bottom <= selector.RectangleToScreen(selector.ClientRectangle).Top, "Near-bottom dropdown did not open upwards.");
                            var previous = (ToolStripDropDown)typeof(LeafSelect).GetField("popup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(selector);
                            previous.Close(ToolStripDropDownCloseReason.AppClicked); selector.TogglePopup(); Assert(previous.IsDisposed && selector.IsOpen, "Auto-closed popup leaked on reopen.");
                            choices = (Control)typeof(LeafSelect).GetField("choices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(selector);
                            Key(choices, Keys.Tab); Assert(!selector.IsOpen && panel.Visible, "Tab closed owner instead of dropdown.");
                        }
                        panel.HidePanel();
                        Pump(350);
                        panel.Open(resolved, true); Pump(300); panel.HidePanel(); Pump(60);
                        Assert(panel.Animating && panel.Bounds == stable, "Closing has no smooth fixed-bounds motion.");
                        panel.Toggle(resolved); Pump(350); Assert(panel.Visible && !panel.Animating && !panel.SnapshotAllocated, "Closing reversal failed.");
                        panel.HidePanel(); Pump(350);
                    }
                    using (var notification = new BrandedNotifications())
                    {
                        Icon recording = icon.Icon;
                        notification.Show(2000, "SoundLeaf", "Проверка листа уведомления / notification leaf verification", ToolTipIcon.Info);
                        Assert(notification.LastPublished && icon.Icon == recording, "Branded notification changed real tray state or was rejected.");
                        Pump(1200);
                        using (var picture = new Bitmap(420, 200))
                        {
                            var work = Screen.PrimaryScreen.WorkingArea;
                            using (var graphics = Graphics.FromImage(picture)) graphics.CopyFromScreen(new Point(work.Right - 420, work.Bottom - 200), Point.Empty, picture.Size);
                            picture.Save(Path.Combine(root, "native-branded-notification.png"));
                        }
                    }
                }
                var fallback = TrayAnchorResolver.Resolve(null, new Point(20, 20)); Assert(!fallback.Native, "Fallback claimed native lookup.");
                Assert(TrayPanel.Ease(0) == 0 && TrayPanel.Ease(1) == 1 && TrayPanel.Ease(.5) > .5, "Growth easing wrong.");
                foreach (int size in new[] { 12, 18, 24 }) using (var image = new Bitmap(size, size))
                {
                    using (var graphics = Graphics.FromImage(image)) ActionGlyphDrawing.Draw(graphics, ActionGlyph.Pause, new Rectangle(0, 0, size, size), Color.White);
                    int width = Math.Max(2, (int)Math.Round(size / 3.5));
                    for (int y = 0; y < size; y++) for (int x = 0; x < width; x++) Assert(image.GetPixel(x, y).A == image.GetPixel(size - width + x, y).A, "Unequal pause bar thickness.");
                }
                Console.WriteLine("PASS " + checks + " UI checks; " + renders + " panel renders: " + root); return 0;
            }
            catch (Exception error) { Console.WriteLine(error); return 1; }
        }
    }
}
