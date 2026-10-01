using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Player")]
[assembly: System.Reflection.AssemblyProduct("Player")]
[assembly: System.Reflection.AssemblyVersion("2.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("2.2.0.0")]

namespace Player
{
    internal static class PlayerApp
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool verify = args.Length == 1 && args[0] == "--verify-live";
            string root = AppDomain.CurrentDomain.BaseDirectory;
            AppLog log = null;
            try
            {
                log = new AppLog(Path.Combine(root, "logs"));
                log.Write("START version=" + typeof(PlayerApp).Assembly.GetName().Version +
                    " pid=" + Process.GetCurrentProcess().Id + " verify=" + verify);
                bool first;
                using (var mutex = new Mutex(true, verify ? "Local\\PlayerVerificationV2" : "Local\\PlayerCaptureSingle", out first))
                {
                    if (!first) { log.TryWrite("Already running; second instance exits."); return 0; }
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                    using (var context = new PlayerContext(root, log, verify))
                    {
                        Application.Run(context);
                        return context.Failed ? 1 : 0;
                    }
                }
            }
            catch (Exception error)
            {
                if (log != null) log.TryWrite(error.ToString());
                if (!verify) MessageBox.Show(error.Message, "Player — ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
    internal sealed class PlayerContext : ApplicationContext
    {
        private readonly Control dispatcher = new Control();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly ToolStripMenuItem startItem, pauseItem, stopItem, exitItem, recoverItem, autoStartItem,
            resultItem, openResultItem, pendingItem;
        private string lastResult, lastMessage = "Запись ещё не сохранена";
        private int pendingCount;
        private readonly Icon recordingIcon = PlayerIcons.Create(0, 0), pausedIcon = PlayerIcons.Create(1, 0),
            stoppedIcon = PlayerIcons.Create(2, 0), errorIcon = PlayerIcons.Create(3, 0);
        private readonly Icon[] savingIcons = new Icon[PlayerIcons.FrameCount];
        private readonly System.Windows.Forms.Timer animationTimer = new System.Windows.Forms.Timer();
        private int animationFrame, animationTicks;
        private readonly AppLog log;
        private readonly string root, output, ffmpeg;
        private readonly bool verify;
        private readonly Stopwatch uptime = Stopwatch.StartNew();
        private readonly System.Windows.Forms.Timer verificationTimer = new System.Windows.Forms.Timer();
        private readonly string verificationReport;
        private RecordState state = RecordState.Stopped;
        private RecordingSession session;
        private bool exitAfterSave, exiting, recoveryBusy;
        private int verifyStage;
        private long verifyAt;
        internal bool Failed { get; private set; }

        internal PlayerContext(string root, AppLog log, bool verify)
        {
            this.root = root; this.log = log; this.verify = verify;
            for (int i = 0; i < savingIcons.Length; i++) savingIcons[i] = PlayerIcons.Create(4, i);
            animationTimer.Interval = 80;
            animationTimer.Tick += delegate
            {
                if (exiting || state != RecordState.Saving) return;
                animationFrame = (animationFrame + 1) % savingIcons.Length;
                tray.Icon = savingIcons[animationFrame];
                animationTicks++;
            };
            output = verify ? Path.Combine(root, "Verification", DateTime.Now.ToString("yyyyMMdd-HHmmss") +
                "-" + Guid.NewGuid().ToString("N").Substring(0, 6)) : Path.Combine(root, "Recordings");
            ffmpeg = Path.Combine(root, "tools", "ffmpeg.exe");
            verificationReport = Path.Combine(output, "live-result.txt");
            // Create the dispatcher on the UI thread before any worker can post a callback.
            IntPtr handle = dispatcher.Handle;
            startItem = new ToolStripMenuItem("Начать новую запись", null, delegate { Start(); });
            pauseItem = new ToolStripMenuItem("Пауза", null, delegate { SetPause(); });
            stopItem = new ToolStripMenuItem("Остановить и сохранить", null, delegate { Stop(false); });
            recoverItem = new ToolStripMenuItem("Собрать незавершённые записи", null, delegate { Recover(); });
            resultItem = new ToolStripMenuItem("Результат записи", null, delegate
            {
                MessageBox.Show(lastMessage + (lastResult == null ? "" : Environment.NewLine + lastResult) +
                    Environment.NewLine + "Незавершённых сессий: " + pendingCount, "Player — результат",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            openResultItem = new ToolStripMenuItem("Открыть последний итоговый файл", null, delegate
            {
                try { if (lastResult != null) Process.Start(new ProcessStartInfo(lastResult) { UseShellExecute = true }); }
                catch (Exception error) { tray.ShowBalloonTip(5000, "Player", error.Message, ToolTipIcon.Error); }
            });
            pendingItem = new ToolStripMenuItem("Незавершённых сессий: 0") { Enabled = false };
            autoStartItem = new ToolStripMenuItem("Автозапуск с Windows", null, delegate { ToggleAutostart(); });
            exitItem = new ToolStripMenuItem("Выход", null, delegate { Stop(true); });
            menu.Items.Add(startItem); menu.Items.Add(pauseItem); menu.Items.Add(stopItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(resultItem); menu.Items.Add(openResultItem); menu.Items.Add(pendingItem);
            menu.Items.Add("Открыть папку записей", null, delegate { OpenFolder(output); });
            menu.Items.Add("Открыть журнал ошибок", null, delegate { OpenFolder(Path.Combine(root, "logs")); });
            menu.Items.Add(recoverItem);
            menu.Items.Add(autoStartItem);
            menu.Items.Add(exitItem);
            tray.Icon = stoppedIcon;
            tray.Text = "Player — запуск";
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Left) menu.Show(Cursor.Position);
            };
            tray.Visible = true;
            RefreshPending();
            if (!verify && pendingCount > 0) tray.ShowBalloonTip(7000, "Player — незавершённые записи",
                "Найдено сессий: " + pendingCount + ". После остановки текущей записи выберите «Собрать незавершённые записи».", ToolTipIcon.Warning);
            RefreshAutostart();
            ThreadPool.QueueUserWorkItem(delegate { CleanupOldBackups(); });
            log.TryWrite("TRAY_VISIBLE ms=" + uptime.ElapsedMilliseconds);
            SystemEvents.SessionEnding += OnSessionEnding;
            Apply(new SessionUpdate(RecordState.Starting, "Запуск", 0, false));
            dispatcher.BeginInvoke(new Action(Start));
            verificationTimer.Interval = 100;
            verificationTimer.Tick += VerifyTick;
            if (verify) verificationTimer.Start();
        }
        private void Start()
        {
            if (session != null && session.IsAlive || recoveryBusy) return;
            Failed = false;
            Apply(new SessionUpdate(RecordState.Starting, "Подключение аудио", 0, false));
            try
            {
                Directory.CreateDirectory(output);
                string day = Path.Combine(output, DateTime.Now.ToString("yyyy-MM-dd"));
                session = new RecordingSession(day, ffmpeg, log, Post);
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
            RecordState previous = state;
            state = update.State;
            lastMessage = update.Message;
            if (!string.IsNullOrEmpty(update.FinalPath)) lastResult = update.FinalPath;
            if (state == RecordState.Starting) lastResult = null;
            if (state == RecordState.Stopped || state == RecordState.Faulted) RefreshPending();
            openResultItem.Enabled = lastResult != null && File.Exists(lastResult);
            resultItem.Text = state == RecordState.Saving ? update.Message :
                state == RecordState.Stopped && lastResult != null ? "Готово: " + Path.GetFileName(lastResult) : update.Message;
            bool active = state == RecordState.Recording || state == RecordState.Paused;
            bool busy = active || state == RecordState.Starting || state == RecordState.Saving;
            startItem.Enabled = !busy && !recoveryBusy;
            pauseItem.Enabled = active;
            stopItem.Enabled = active || state == RecordState.Starting;
            recoverItem.Enabled = !busy && !recoveryBusy;
            exitItem.Enabled = !recoveryBusy;
            pauseItem.Text = state == RecordState.Paused ? "Продолжить запись" : "Пауза";
            if (state == RecordState.Saving)
            {
                if (previous != state) animationFrame = 0;
                animationTimer.Start();
            }
            else animationTimer.Stop();
            tray.Icon = state == RecordState.Saving ? savingIcons[animationFrame] : state == RecordState.Recording ? recordingIcon :
                state == RecordState.Paused ? pausedIcon :
                state == RecordState.Faulted || (state == RecordState.Stopped && pendingCount != 0) ? errorIcon : stoppedIcon;
            string label = state == RecordState.Recording ? "запись" :
                state == RecordState.Paused ? "пауза" : state == RecordState.Saving ? update.Message :
                state == RecordState.Starting ? "запуск" : state == RecordState.Faulted ? "ошибка — откройте журнал" :
                pendingCount < 0 ? "не удалось проверить прошлые записи" :
                pendingCount > 0 ? "есть незавершённые записи: " + pendingCount :
                lastResult != null ? "готово: " + Path.GetFileName(lastResult) : "остановлен; " + update.Message;
            string text = "Player — " + label;
            if (active) text += " " + TimeSpan.FromSeconds(update.Seconds).ToString(@"hh\:mm\:ss") +
                (update.HeardSignal ? "" : " · тишина");
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            if (verify && previous != state)
            {
                Directory.CreateDirectory(output);
                File.AppendAllText(verificationReport, uptime.ElapsedMilliseconds + " " + state + " " + update.Message + Environment.NewLine);
            }
            if (state == RecordState.Faulted)
            {
                Failed = true;
                log.TryWrite("UI ERROR " + update.Message);
                if (!verify) tray.ShowBalloonTip(8000, "Player — ошибка", update.Message, ToolTipIcon.Error);
            }
            if (previous != state && state == RecordState.Recording) log.TryWrite("RECORDING_VISIBLE ms=" + uptime.ElapsedMilliseconds);
            if ((state == RecordState.Stopped || state == RecordState.Faulted) &&
                (exitAfterSave || (verify && state == RecordState.Faulted)))
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
            if (recoveryBusy || (session != null && session.IsAlive)) return;
            System.Collections.Generic.List<PendingSession> pending;
            try { pending = SessionRecovery.Find(output); }
            catch (Exception error) { Apply(new SessionUpdate(RecordState.Faulted, error.Message, 0, false)); return; }
            if (pending.Count == 0)
            { tray.ShowBalloonTip(4000, "Player", "Незавершённых сессий не найдено.", ToolTipIcon.Info); return; }
            if (!verify && MessageBox.Show("Собрать сессий: " + pending.Count +
                "? Исходные WAV сохранятся в резервной папке после проверки MKV.", "Player — восстановление",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            lastResult = null;
            recoveryBusy = true;
            Apply(new SessionUpdate(RecordState.Saving, "Восстановление", 0, false));
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
                            string file = SessionRecovery.Recover(pendingSession, root, ffmpeg,
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
                        "Собрано сессий: " + recovered + "; ошибок: " + errors, 0, false) { FinalPath = final });
                    if (errors == 0) tray.ShowBalloonTip(5000, "Player", "Проверенных итоговых MKV: " + recovered, ToolTipIcon.Info);
                }));
            });
        }
        private void RefreshPending()
        {
            try { pendingCount = SessionRecovery.Find(output).Count; }
            catch (Exception error) { log.TryWrite("Pending scan failed: " + error); pendingCount = -1; }
            pendingItem.Text = pendingCount < 0 ? "Не удалось проверить незавершённые записи" :
                "Незавершённых сессий: " + pendingCount;
        }
        private void OpenFolder(string folder)
        {
            try { Directory.CreateDirectory(folder); Process.Start("explorer.exe", MediaExport.Quote(folder)); }
            catch (Exception error) { log.TryWrite(error.ToString()); tray.ShowBalloonTip(5000, "Player", error.Message, ToolTipIcon.Error); }
        }
        private void RefreshAutostart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    string value = key == null ? null : key.GetValue("Player") as string;
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
                    if (autoStartItem.Checked) key.DeleteValue("Player", false);
                    else key.SetValue("Player", "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
                }
                RefreshAutostart();
                tray.ShowBalloonTip(3000, "Player", autoStartItem.Checked ?
                    "Player будет запускаться при входе в Windows." : "Автозапуск отключён.", ToolTipIcon.Info);
            }
            catch (Exception error)
            {
                log.TryWrite("Autostart update failed: " + error);
                tray.ShowBalloonTip(5000, "Player", "Не удалось изменить автозапуск: " + error.Message, ToolTipIcon.Error);
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
        private void VerifyTick(object sender, EventArgs args)
        {
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
        protected override void ExitThreadCore()
        {
            if (session != null && session.IsAlive && state != RecordState.Stopped && state != RecordState.Faulted)
            { exitAfterSave = true; session.Stop(); return; }
            exiting = true;
            SystemEvents.SessionEnding -= OnSessionEnding;
            verificationTimer.Stop(); verificationTimer.Dispose();
            animationTimer.Stop(); animationTimer.Dispose();
            tray.Visible = false; tray.Dispose(); menu.Dispose(); dispatcher.Dispose();
            recordingIcon.Dispose(); pausedIcon.Dispose(); stoppedIcon.Dispose(); errorIcon.Dispose();
            foreach (var icon in savingIcons) if (icon != null) icon.Dispose();
            log.TryWrite("EXIT");
            base.ExitThreadCore();
        }
    }
}
