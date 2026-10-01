using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace SoundLeaf
{
    internal static class UiTests
    {
        private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        [STAThread] private static int Main()
        {
            try
            {
                TrayPanel.InitializeDpi(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Verification", "ui-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)); Directory.CreateDirectory(root);
                int checks = 0;
                foreach (string language in new[] { "ru", "de", "en" }) foreach (string theme in new[] { "light", "dark" }) foreach (float scale in new[] { 1f, 1.5f, 2f })
                {
                    TextCatalog.SetLanguage(language); var settings = new SoundLeafSettings { Language = language, Theme = theme };
                    var commands = new PanelActions { Start = delegate { }, Pause = delegate { }, Stop = delegate { }, Wav = delegate { }, Recover = delegate { }, Exit = delegate { }, Autostart = delegate { }, OpenLog = delegate { }, SaveSettings = delegate { }, Convert = delegate { } };
                    using (var panel = new TrayPanel(root, settings, commands, delegate { return false; }))
                    {
                        Assert(!panel.ShowInTaskbar && panel.FormBorderStyle == FormBorderStyle.None, "Not tray-only."); checks++;
                        panel.Scale(new SizeF(scale, scale));
                        panel.Font = new Font("Segoe UI", 9.5f * scale);
                        foreach (string page in new[] { "control", "settings", "recordings" })
                        {
                            panel.ShowPage(page); panel.Show(); Application.DoEvents(); panel.PerformLayout();
                            panel.SetState(new SessionUpdate(RecordState.Stopped, TextCatalog.T("stopped"), 123, false), false, false);
                            using (var image = new Bitmap(panel.Width, panel.Height))
                            {
                                panel.DrawToBitmap(image, new Rectangle(Point.Empty, panel.Size));
                                int variations = 0; Color background = image.GetPixel(0, 0);
                                for (int y = 8; y < image.Height; y += 8) for (int x = 8; x < image.Width; x += 8) if (image.GetPixel(x, y).ToArgb() != background.ToArgb()) variations++;
                                Assert(variations > 100, "Blank panel render.");
                                image.Save(Path.Combine(root, language + "-" + theme + "-" + (int)(scale * 100) + "-" + page + ".png"));
                            }
                            Assert(panel.Controls[0].Bounds.Right <= panel.ClientSize.Width && panel.Controls[0].Bounds.Bottom <= panel.ClientSize.Height, "Shell clipped."); checks++;
                        }
                        panel.Show(); Assert(panel.SelectNextControl(null, true, true, true, false), "No keyboard-focusable control."); checks++;
                        panel.VerifyEscape(); Assert(!panel.Visible, "Escape did not hide."); checks++;
                        panel.Show();
                        typeof(Form).GetMethod("OnDeactivate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(panel, new object[] { EventArgs.Empty });
                        Assert(!panel.Visible, "Focus loss did not hide."); checks++;
                    }
                }
                foreach (var work in new[] { new Rectangle(0, 0, 1920, 1040), new Rectangle(-1280, 0, 1280, 680), new Rectangle(0, 40, 800, 560) })
                    foreach (var point in new[] { work.Location, new Point(work.Right, work.Bottom), new Point(work.Left, work.Bottom), new Point(work.Right, work.Top) })
                    { var placed = TrayPanel.Placement(point, new Size(860, 1120), work); Assert(work.Contains(placed), "Popup outside work area."); checks++; }
                Console.WriteLine("PASS " + checks + " UI checks; 54 panel renders: " + root); return 0;
            }
            catch (Exception error) { Console.WriteLine(error); return 1; }
        }
    }
}
