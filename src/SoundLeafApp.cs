using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("SoundLeaf")]
[assembly: System.Reflection.AssemblyProduct("SoundLeaf")]
[assembly: System.Reflection.AssemblyVersion("3.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.0.0.0")]

namespace SoundLeaf
{
    internal static class SoundLeafApp
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool verifyWav = args.Length == 1 && args[0] == "--verify-wav-fallback";
            bool verifyOpus = args.Length == 1 && args[0] == "--verify-opus";
            bool verify = verifyOpus || verifyWav || (args.Length == 1 && args[0] == "--verify-live");
            string root = AppDomain.CurrentDomain.BaseDirectory;
            AppLog log = null;
            try
            {
                log = new AppLog(Path.Combine(root, "logs"));
                log.Write("START version=" + typeof(SoundLeafApp).Assembly.GetName().Version +
                    " pid=" + Process.GetCurrentProcess().Id + " verify=" + verify);
                bool first;
                using (var mutex = new Mutex(true, verify ? "Local\\PlayerVerificationV2" : "Local\\PlayerCaptureSingle", out first))
                {
                    if (!first) { log.TryWrite("Already running; second instance exits."); return 0; }
                    TrayPanel.InitializeDpi();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                    using (var context = new SoundLeafContext(root, log, verify, verifyWav, verifyOpus))
                    {
                        Application.Run(context);
                        return context.Failed ? 1 : 0;
                    }
                }
            }
            catch (Exception error)
            {
                if (log != null) log.TryWrite(error.ToString());
                if (!verify) MessageBox.Show(error.Message, TextCatalog.T("message.0"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
    internal sealed class SoundLeafContext : ApplicationContext
    {
        private readonly Control dispatcher = new Control();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly ToolStripMenuItem startItem, pauseItem, stopItem, exitItem, recoverItem, autoStartItem,
            resultItem, openResultItem, pendingItem, wavItem, warningItem;
        private string[] lastPaths = new string[0];
        private FinalResultKind lastKind;
        private RecordingMode activeMode;
        private string lastResult, lastMessage = TextCatalog.T("message.1");
        private int pendingCount;
        private readonly Icon recordingIcon = SoundLeafIcons.Create(0, 0), pausedIcon = SoundLeafIcons.Create(1, 0),
            stoppedIcon = SoundLeafIcons.Create(2, 0), errorIcon = SoundLeafIcons.Create(3, 0);
        private readonly Icon[] savingIcons = new Icon[SoundLeafIcons.FrameCount];
        private readonly System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer();
        private int animationFrame, animationTicks;
        private readonly AppLog log;
        private readonly string root, ffmpeg;
        private string output;
        private string StorageRoot { get { return verify ? root : SoundLeafSettings.StorageRoot(root, output); } }
        private readonly SoundLeafSettings settings;
        private readonly TrayPanel panel;
        private SessionUpdate lastUpdate;
        private bool conversionBusy;
        private readonly bool verify, verifyWav;
        private readonly Stopwatch uptime = Stopwatch.StartNew();
        private readonly System.Windows.Forms.Timer verificationTimer = new System.Windows.Forms.Timer();
        private readonly string verificationReport;
        private RecordState state = RecordState.Stopped;
        private RecordingSession session;
        private bool exitAfterSave, exiting, recoveryBusy;
        private int verifyStage;
        private long verifyAt;
        internal bool Failed { get; private set; }

        internal SoundLeafContext(string root, AppLog log, bool verify, bool verifyWav = false, bool verifyOpus = false)
        {
            this.root = root; this.log = log; this.verify = verify; this.verifyWav = verifyWav;
            bool restored = false;
            settings = verify ? new SoundLeafSettings { Language = "ru" } : SoundLeafSettings.Load(root, out restored);
            if (verifyOpus) settings.Format = AudioOutputFormat.Opus;
            TextCatalog.SetLanguage(settings.Language);
            for (int i = 0; i < savingIcons.Length; i++) savingIcons[i] = SoundLeafIcons.Create(4, i);
            animationTimer.Interval = 80;
            animationTimer.Tick += delegate
            {
                if (exiting || state != RecordState.Saving) return;
                animationFrame = (animationFrame + 1) % savingIcons.Length;
                tray.Icon = savingIcons[animationFrame];
                animationTicks++;
            };
            output = verify ? Path.Combine(root, "Verification", DateTime.Now.ToString("yyyyMMdd-HHmmss") +
                "-" + Guid.NewGuid().ToString("N").Substring(0, 6)) : settings.RecordingsFolder(root);
            ffmpeg = verifyWav ? Path.Combine(output, "missing-ffmpeg.exe") : Path.Combine(root, "tools", "ffmpeg.exe");
            verificationReport = Path.Combine(output, "live-result.txt");
            // Create the dispatcher on the UI thread before any worker can post a callback.
            IntPtr handle = dispatcher.Handle;
            startItem = new ToolStripMenuItem(TextCatalog.T("message.2"), null, delegate { Start(); });
            wavItem = new ToolStripMenuItem(TextCatalog.T("message.3"), null, delegate { Start(RecordingMode.WavOnly); }) { Enabled = false };
            warningItem = new ToolStripMenuItem(TextCatalog.T("message.4")) { Enabled = false };
            pauseItem = new ToolStripMenuItem(TextCatalog.T("message.5"), null, delegate { SetPause(); });
            stopItem = new ToolStripMenuItem(TextCatalog.T("message.6"), null, delegate { Stop(false); });
            recoverItem = new ToolStripMenuItem(TextCatalog.T("message.7"), null, delegate { Recover(); });
            resultItem = new ToolStripMenuItem(TextCatalog.T("message.8"), null, delegate
            {
                MessageBox.Show(lastMessage + (lastPaths.Length == 0 ? "" : Environment.NewLine + string.Join(Environment.NewLine, lastPaths)) +
                    Environment.NewLine + TextCatalog.T("message.9") + pendingCount, TextCatalog.T("message.10"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            openResultItem = new ToolStripMenuItem(TextCatalog.T("message.11"), null, delegate
            {
                try
                {
                    if (lastKind == FinalResultKind.WavParts && lastResult != null) OpenFolder(Path.GetDirectoryName(lastResult));
                    else if (lastResult != null) Process.Start(new ProcessStartInfo(lastResult) { UseShellExecute = true });
                }
                catch (Exception error) { tray.ShowBalloonTip(5000, "SoundLeaf", error.Message, ToolTipIcon.Error); }
            });
            pendingItem = new ToolStripMenuItem(TextCatalog.T("message.12")) { Enabled = false };
            autoStartItem = new ToolStripMenuItem(TextCatalog.T("message.13"), null, delegate { ToggleAutostart(); });
            exitItem = new ToolStripMenuItem(TextCatalog.T("message.14"), null, delegate { Stop(true); });
            menu.Items.Add(startItem); menu.Items.Add(pauseItem); menu.Items.Add(stopItem);
            menu.Items.Add(wavItem); menu.Items.Add(warningItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(resultItem); menu.Items.Add(openResultItem); menu.Items.Add(pendingItem);
            menu.Items.Add(TextCatalog.T("message.15"), null, delegate { OpenFolder(output); });
            menu.Items.Add(TextCatalog.T("message.16"), null, delegate { OpenFolder(Path.Combine(root, "logs")); });
            menu.Items.Add(recoverItem);
            menu.Items.Add(autoStartItem);
            menu.Items.Add(exitItem);
            tray.Icon = stoppedIcon;
            tray.Text = TextCatalog.T("message.17");
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Left) panel.Toggle(Cursor.Position);
            };
            panel = new TrayPanel(root, settings, new PanelActions
            {
                Start = Start, Pause = SetPause, Stop = delegate { Stop(false); }, Wav = delegate { Start(RecordingMode.WavOnly); },
                Recover = Recover, Exit = delegate { Stop(true); }, Autostart = ToggleAutostart, OpenLog = delegate { OpenFolder(Path.Combine(root, "logs")); },
                SaveSettings = SettingsChanged, Convert = ConvertRecording
            }, delegate { return state == RecordState.Starting || state == RecordState.Recording || state == RecordState.Paused || state == RecordState.Saving || recoveryBusy || conversionBusy; });
            tray.Visible = true;
            if (restored) tray.ShowBalloonTip(8000, "SoundLeaf", TextCatalog.T("restored"), ToolTipIcon.Warning);
            RefreshPending();
            if (!verify && pendingCount > 0) tray.ShowBalloonTip(7000, TextCatalog.T("message.18"),
                TextCatalog.T("message.19") + pendingCount + TextCatalog.T("message.20"), ToolTipIcon.Warning);
            RefreshAutostart();
            if (!verify) MigrateStartup();
            ThreadPool.QueueUserWorkItem(delegate { CleanupOldBackups(); });
            log.TryWrite("TRAY_VISIBLE ms=" + uptime.ElapsedMilliseconds);
            SystemEvents.SessionEnding += OnSessionEnding;
            SystemEvents.UserPreferenceChanged += OnThemeChanged;
            Apply(new SessionUpdate(RecordState.Starting, TextCatalog.T("message.21"), 0, false));
            dispatcher.BeginInvoke(new Action(Start));
            verificationTimer.Interval = 100;
            verificationTimer.Tick += VerifyTick;
            if (verify) verificationTimer.Start();
        }
        private void Start() { Start(RecordingMode.MkvWithWavBackup); }
        private void Start(RecordingMode mode)
        {
            if (session != null && session.IsAlive || recoveryBusy || conversionBusy) return;
            Failed = false;
            activeMode = mode;
            warningItem.Text = TextCatalog.T("message.4");
            Apply(new SessionUpdate(RecordState.Starting, TextCatalog.T("message.4"), 0, false) { Mode = mode });
            try
            {
                string day = Path.Combine(output, DateTime.Now.ToString("yyyy-MM-dd"));
                session = new RecordingSession(day, ffmpeg, log, Post, mode, settings.Profile, StorageRoot);
                session.Start();
            }
            catch (Exception error) { Apply(new SessionUpdate(RecordState.Faulted, error.Message, 0, false)); }
        }
        private void Post(SessionUpdate update)
        {
            if (!exiting) dispatcher.BeginInvoke(new Action(delegate { Apply(update); }));
        }
        private void Apply(SessionUpdate update)
        {
            update.Message = TextCatalog.Translate(update.Message); update.Warning = TextCatalog.Translate(update.Warning); lastUpdate = update;
            RecordState previous = state;
            state = update.State;
            lastMessage = update.Message;
            activeMode = update.Mode;
            if (!string.IsNullOrEmpty(update.FinalPath)) lastResult = update.FinalPath;
            if (update.FinalPaths.Length > 0) { lastPaths = update.FinalPaths; lastKind = update.ResultKind; }
            else if (update.FinalPath != null) { lastPaths = new[] { update.FinalPath }; lastKind = FinalResultKind.Mkv; }
            if (state == RecordState.Starting) { lastResult = null; lastPaths = new string[0]; lastKind = FinalResultKind.None; }
            if (!string.IsNullOrEmpty(update.Warning)) warningItem.Text = TextCatalog.T("message.22") + update.Warning;
            else if (state == RecordState.Recording) warningItem.Text = TextCatalog.T("message.23");
            if (state == RecordState.Stopped || state == RecordState.Faulted) RefreshPending();
            openResultItem.Enabled = lastResult != null && File.Exists(lastResult);
            openResultItem.Text = lastKind == FinalResultKind.WavParts ? TextCatalog.T("message.24") : TextCatalog.T("message.11");
            resultItem.Text = state == RecordState.Saving ? update.Message :
                state == RecordState.Stopped && lastKind == FinalResultKind.WavParts ? update.Message :
                state == RecordState.Stopped && lastResult != null ? TextCatalog.T("message.25") + Path.GetFileName(lastResult) : update.Message;
            bool active = state == RecordState.Recording || state == RecordState.Paused;
            if (active && activeMode == RecordingMode.WavOnly) resultItem.Text += TextCatalog.T("message.26");
            bool busy = active || state == RecordState.Starting || state == RecordState.Saving || conversionBusy;
            startItem.Enabled = !busy && !recoveryBusy;
            wavItem.Enabled = !busy && !recoveryBusy && update.CanRecordWavOnly;
            pauseItem.Enabled = active;
            stopItem.Enabled = active || state == RecordState.Starting;
            recoverItem.Enabled = !busy && !recoveryBusy;
            exitItem.Enabled = !recoveryBusy && !conversionBusy;
            pauseItem.Text = state == RecordState.Paused ? TextCatalog.T("message.27") : TextCatalog.T("message.5");
            if (state == RecordState.Saving)
            {
                if (previous != state) animationFrame = 0;
                animationTimer.Start();
            }
            else animationTimer.Stop();
            tray.Icon = state == RecordState.Saving ? savingIcons[animationFrame] : state == RecordState.Recording ? recordingIcon :
                state == RecordState.Paused ? pausedIcon :
                state == RecordState.Faulted || (state == RecordState.Stopped && pendingCount != 0) ? errorIcon : stoppedIcon;
            string label = state == RecordState.Recording ? TextCatalog.T("message.28") :
                state == RecordState.Paused ? TextCatalog.T("message.29") : state == RecordState.Saving ? update.Message :
                state == RecordState.Starting ? TextCatalog.T("message.30") : state == RecordState.Faulted ? TextCatalog.T("message.31") :
                pendingCount < 0 ? TextCatalog.T("message.32") :
                pendingCount > 0 ? TextCatalog.T("message.33") + pendingCount :
                lastKind == FinalResultKind.WavParts ? TextCatalog.T("message.34") :
                lastResult != null ? TextCatalog.T("message.35") + Path.GetFileName(lastResult) : TextCatalog.T("message.36") + update.Message;
            string text = "SoundLeaf — " + label;
            if (active && activeMode == RecordingMode.WavOnly) text += TextCatalog.T("message.26");
            if (active) text += " " + TimeSpan.FromSeconds(update.Seconds).ToString(@"hh\:mm\:ss") +
                (update.HeardSignal ? "" : TextCatalog.T("message.37"));
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            panel.SetState(update, update.CanRecordWavOnly, autoStartItem.Checked);
            if (verify && previous != state)
            {
                Directory.CreateDirectory(output);
                File.AppendAllText(verificationReport, uptime.ElapsedMilliseconds + " " + state + " " + update.Message + Environment.NewLine);
            }
            if (state == RecordState.Faulted)
            {
                Failed = true;
                log.TryWrite("UI ERROR " + update.Message);
                if (!verify) tray.ShowBalloonTip(8000, TextCatalog.T("message.0"), update.Message, ToolTipIcon.Error);
            }
            if (verify && state == RecordState.Stopped && lastKind == FinalResultKind.WavParts)
            {
                if (pendingCount != 0 || lastPaths.Length == 0 || !openResultItem.Enabled || animationTimer.Enabled || tray.Icon != stoppedIcon)
                    throw new InvalidOperationException("WAV-only stopped UI is inconsistent.");
                foreach (string path in lastPaths) if (!File.Exists(path) || !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("WAV-only output missing.");
                File.AppendAllText(verificationReport, "WAV_ONLY_UI_PASS" + Environment.NewLine);
            }
            if (previous != state && state == RecordState.Recording) log.TryWrite("RECORDING_VISIBLE ms=" + uptime.ElapsedMilliseconds);
            if ((state == RecordState.Stopped || state == RecordState.Faulted) &&
                (exitAfterSave || (verify && state == RecordState.Faulted && !verifyWav)))
            {
                if (verify) File.AppendAllText(verificationReport, "FINAL_SECONDS=" +
                    update.Seconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + Environment.NewLine);
                ExitThread();
            }
        }
        private void SetPause()
        {
            if (session == null || (state != RecordState.Recording && state != RecordState.Paused)) return;
            pauseItem.Enabled = false; // Icon changes only after worker acknowledges the command.
            session.Pause(state == RecordState.Recording);
        }
        private void Stop(bool exit)
        {
            exitAfterSave |= exit;
            if (state == RecordState.Stopped || state == RecordState.Faulted)
            {
                if (exit) ExitThread();
                return;
            }
            if (session != null) session.Stop();
            stopItem.Enabled = false; pauseItem.Enabled = false;
        }
        private void Recover()
        {
            if (recoveryBusy || conversionBusy || (session != null && session.IsAlive)) return;
            System.Collections.Generic.List<PendingSession> pending;
            try { pending = SessionRecovery.Find(output, StorageRoot); }
            catch (Exception error) { Apply(new SessionUpdate(RecordState.Faulted, error.Message, 0, false)); return; }
            if (pending.Count == 0)
            { tray.ShowBalloonTip(4000, "SoundLeaf", TextCatalog.T("message.38"), ToolTipIcon.Info); return; }
            if (!verify && MessageBox.Show(TextCatalog.T("message.39") + pending.Count +
                TextCatalog.T("message.40"), TextCatalog.T("message.41"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            lastResult = null;
            lastPaths = new string[0]; lastKind = FinalResultKind.None;
            recoveryBusy = true;
            Apply(new SessionUpdate(RecordState.Saving, TextCatalog.T("message.42"), 0, false));
            ThreadPool.QueueUserWorkItem(delegate
            {
                int recovered = 0, errors = 0;
                string final = null;
                try
                {
                    foreach (var pendingSession in pending)
                    {
                        try
                        {
                            string file = SessionRecovery.Recover(pendingSession, StorageRoot, ffmpeg,
                                delegate(string phase) { Post(new SessionUpdate(RecordState.Saving, phase, 0, false)); });
                            final = file;
                            recovered++;
                            log.TryWrite("Recovered session: " + pendingSession.Stem + " -> " + file);
                        }
                        catch (Exception error) { errors++; log.TryWrite("Recovery skipped: " + pendingSession.Stem + " " + error); }
                    }
                }
                catch (Exception error) { errors++; log.TryWrite(error.ToString()); }
                dispatcher.BeginInvoke(new Action(delegate
                {
                    recoveryBusy = false;
                    Apply(new SessionUpdate(errors == 0 ? RecordState.Stopped : RecordState.Faulted,
                        TextCatalog.T("message.43") + recovered + TextCatalog.T("message.44") + errors, 0, false) { FinalPath = final });
                    if (errors == 0) tray.ShowBalloonTip(5000, "SoundLeaf", TextCatalog.T("message.45") + recovered, ToolTipIcon.Info);
                }));
            });
        }
        private void RefreshPending()
        {
            try { pendingCount = SessionRecovery.Find(output, StorageRoot).Count; }
            catch (Exception error) { log.TryWrite("Pending scan failed: " + error); pendingCount = -1; }
            pendingItem.Text = pendingCount < 0 ? TextCatalog.T("message.46") :
                TextCatalog.T("message.9") + pendingCount;
        }
        private void OpenFolder(string folder)
        {
            try { Directory.CreateDirectory(folder); Process.Start("explorer.exe", MediaExport.Quote(folder)); }
            catch (Exception error) { log.TryWrite(error.ToString()); tray.ShowBalloonTip(5000, "SoundLeaf", error.Message, ToolTipIcon.Error); }
        }
        private void RefreshAutostart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    string value = key == null ? null : key.GetValue("SoundLeaf") as string;
                    string expected = "\"" + Application.ExecutablePath + "\"";
                    autoStartItem.Checked = string.Equals(value, expected, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception error) { autoStartItem.Checked = false; log.TryWrite("Autostart read failed: " + error); }
        }
        private void ToggleAutostart()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    object current = key.GetValue("SoundLeaf");
                    if (current != null && !StartupOwnership.Matches(current as string, Application.ExecutablePath))
                        throw new IOException(TextCatalog.T("startupForeign"));
                    if (autoStartItem.Checked) key.DeleteValue("SoundLeaf", false);
                    else key.SetValue("SoundLeaf", "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
                }
                RefreshAutostart();
                if (lastUpdate != null) panel.SetState(lastUpdate, lastUpdate.CanRecordWavOnly, autoStartItem.Checked);
                tray.ShowBalloonTip(3000, "SoundLeaf", autoStartItem.Checked ?
                    TextCatalog.T("message.47") : TextCatalog.T("message.48"), ToolTipIcon.Info);
            }
            catch (Exception error)
            {
                log.TryWrite("Autostart update failed: " + error);
                tray.ShowBalloonTip(5000, "SoundLeaf", TextCatalog.T("message.49") + error.Message, ToolTipIcon.Error);
            }
        }
        private void CleanupOldBackups()
        {
            try
            {
                var result = BackupCleanup.RemoveOldRecordingParts(root, DateTime.UtcNow, TimeSpan.FromHours(24));
                if (result.DeletedFolders > 0 || result.Errors.Count > 0)
                    log.TryWrite("Backup cleanup: deleted folders=" + result.DeletedFolders +
                        ", bytes=" + result.DeletedBytes + ", skipped=" + result.SkippedFolders +
                        ", errors=" + result.Errors.Count);
                foreach (string error in result.Errors) log.TryWrite("Backup cleanup error: " + error);
            }
            catch (Exception error) { log.TryWrite("Backup cleanup failed: " + error); }
        }
        private void SettingsChanged()
        {
            if (!verify && (state == RecordState.Stopped || state == RecordState.Faulted)) output = settings.RecordingsFolder(root);
            foreach (ToolStripItem item in menu.Items) item.Text = TextCatalog.Translate(item.Text);
            if (lastUpdate != null) Apply(lastUpdate);
        }
        private void MigrateStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    string old = key.GetValue("Player") as string;
                    string expected = Path.Combine(root, "Player.exe");
                    if (StartupOwnership.Matches(old, expected))
                    {
                        string target = "\"" + Application.ExecutablePath + "\"";
                        object existing = key.GetValue("SoundLeaf");
                        if (existing != null && !StartupOwnership.Matches(existing as string, Application.ExecutablePath)) return;
                        key.SetValue("SoundLeaf", target); key.DeleteValue("Player", false);
                    }
                }
                RefreshAutostart();
            }
            catch (Exception error) { log.TryWrite("Startup migration failed: " + error); }
        }
        private void ConvertRecording(RecordingEntry entry, RecordingProfile profile, string final)
        {
            if (conversionBusy || recoveryBusy || (session != null && session.IsAlive)) return;
            lastResult = null; lastPaths = new string[0]; lastKind = FinalResultKind.None;
            conversionBusy = true; Apply(new SessionUpdate(RecordState.Saving, TextCatalog.T("conversionBusy"), 0, false) { Profile = profile });
            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null; double seconds = 0;
                try
                {
                    if (entry.Paths.Length > 1 || Path.GetExtension(entry.Paths[0]).Equals(".wav", StringComparison.OrdinalIgnoreCase))
                    { foreach (string wav in entry.Paths) seconds += DurableWave.ReadInfo(wav, false).Duration; ProfileExport.Rebuild(entry.Paths, final, ffmpeg, profile, null); }
                    else { ProfileExport.Transcode(entry.Paths[0], final, ffmpeg, profile, null); seconds = MediaExport.DecodeDuration(final, ffmpeg, ProfileExport.Timeout(entry.Seconds ?? 3600)); }
                    string folder = Path.GetDirectoryName(final); string workspace = SoundLeafSettings.StorageRoot(root, folder);
                    ResultReceipt.Write(workspace, Path.GetFileNameWithoutExtension(final), profile, seconds, false, new[] { final });
                }
                catch (Exception error) { failure = error; log.TryWrite(error.ToString()); }
                dispatcher.BeginInvoke(new Action(delegate
                {
                    conversionBusy = false;
                    if (failure == null)
                    {
                        string folder = Path.GetDirectoryName(final); var folders = new System.Collections.Generic.List<string>(settings.LibraryFolders ?? new string[0]);
                        if (!folders.Contains(folder)) { folders.Add(folder); settings.LibraryFolders = folders.ToArray(); try { settings.Save(root); } catch (Exception error) { log.TryWrite(error.ToString()); } }
                    }
                    Apply(new SessionUpdate(failure == null ? RecordState.Stopped : RecordState.Faulted, failure == null ? TextCatalog.T("conversionDone") : failure.Message, seconds, false)
                    { Profile = profile, FinalPath = failure == null ? final : null, FinalPaths = failure == null ? new[] { final } : new string[0], ResultKind = FinalResultKind.EncodedFile });
                }));
            });
        }
        private void VerifyTick(object sender, EventArgs args)
        {
            if (verifyWav)
            {
                if (verifyStage == 0 && state == RecordState.Faulted)
                {
                    if (!wavItem.Enabled || Directory.GetFiles(output, "*.wav", SearchOption.AllDirectories).Length != 0)
                        throw new InvalidOperationException("FFmpeg failure did not offer explicit WAV-only capture.");
                    File.AppendAllText(verificationReport, "WAV_OFFER_PASS_NO_AUTOMATIC_RECORDING" + Environment.NewLine);
                    Start(RecordingMode.WavOnly); verifyStage = 1; verifyAt = uptime.ElapsedMilliseconds;
                }
                if (verifyStage == 1 && state == RecordState.Recording && uptime.ElapsedMilliseconds - verifyAt > 1500)
                { Stop(true); verifyStage = 2; }
                if (uptime.ElapsedMilliseconds > 30000) throw new InvalidOperationException("WAV fallback verification timeout.");
                return;
            }
            // Same pause/stop commands and real UI callbacks as interactive use.
            if (state == RecordState.Recording && verifyStage == 0) { verifyStage = 1; verifyAt = uptime.ElapsedMilliseconds; }
            if (verifyStage == 1 && uptime.ElapsedMilliseconds - verifyAt >= 2000)
            { SetPause(); verifyStage = 2; verifyAt = uptime.ElapsedMilliseconds; }
            if (verifyStage == 2 && state == RecordState.Paused && uptime.ElapsedMilliseconds - verifyAt >= 1000)
            { SetPause(); verifyStage = 3; verifyAt = uptime.ElapsedMilliseconds; }
            if (verifyStage == 3 && state == RecordState.Recording && uptime.ElapsedMilliseconds - verifyAt >= 2000)
            { Stop(false); verifyStage = 4; }
            if (verifyStage == 4 && state == RecordState.Stopped)
            {
                if (!startItem.Enabled || pauseItem.Enabled || stopItem.Enabled || tray.Icon != stoppedIcon || animationTimer.Enabled ||
                    lastResult == null || !File.Exists(lastResult) || pendingCount != 0 || !openResultItem.Enabled)
                    throw new InvalidOperationException("Stopped tray controls are inconsistent.");
                File.AppendAllText(verificationReport, "STOPPED_UI_PASS SAVING_ANIMATION_TICKS=" + animationTicks + Environment.NewLine);
                Start(); verifyStage = 5; verifyAt = uptime.ElapsedMilliseconds;
            }
            if (verifyStage == 5 && state == RecordState.Recording && uptime.ElapsedMilliseconds - verifyAt >= 500)
            { Stop(true); verifyStage = 6; }
            if (uptime.ElapsedMilliseconds > 120000) { log.TryWrite("Live verification timeout"); Stop(true); }
        }
        private void OnSessionEnding(object sender, SessionEndingEventArgs args) { if (session != null) session.Stop(); }
        private void OnThemeChanged(object sender, UserPreferenceChangedEventArgs args)
        { if (!exiting) try { dispatcher.BeginInvoke(new Action(delegate { if (!exiting && settings.Theme == "system") panel.ApplyTheme(); })); } catch (InvalidOperationException) { } }
        protected override void ExitThreadCore()
        {
            if (conversionBusy || recoveryBusy) return;
            if (session != null && session.IsAlive && state != RecordState.Stopped && state != RecordState.Faulted)
            { exitAfterSave = true; session.Stop(); return; }
            exiting = true;
            SystemEvents.SessionEnding -= OnSessionEnding;
            SystemEvents.UserPreferenceChanged -= OnThemeChanged;
            verificationTimer.Stop(); verificationTimer.Dispose();
            animationTimer.Stop(); animationTimer.Dispose();
            tray.Visible = false; tray.Dispose(); menu.Dispose(); dispatcher.Dispose();
            panel.Dispose();
            recordingIcon.Dispose(); pausedIcon.Dispose(); stoppedIcon.Dispose(); errorIcon.Dispose();
            foreach (var icon in savingIcons) if (icon != null) icon.Dispose();
            log.TryWrite("EXIT");
            base.ExitThreadCore();
        }
    }
}
