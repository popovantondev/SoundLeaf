using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
namespace SoundLeaf
{
    internal static class ProfileTests
    {
        private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
        internal static void Run(Action<string, Action> check, string root, string ffmpeg)
        {
            foreach (AudioOutputFormat value in Enum.GetValues(typeof(AudioOutputFormat)))
            {
                AudioOutputFormat format = value;
                check("profile preflight and continuous multi-WAV export " + format, delegate
                {
                    string folder = Path.Combine(root, "profile-" + format); var profile = new RecordingProfile(format, format == AudioOutputFormat.Opus ? 24 : 192);
                    var preflight = StartupChecks.Check(folder, ffmpeg, RecordingMode.MkvWithWavBackup, null, null, profile);
                    Assert(preflight.CanRecord, preflight.Error);
                    using (var sink = new SegmentedWave(folder, Tests.Format(16, false, false), 8192))
                    using (var encoder = new LiveMkv(sink.CurrentPath, Tests.Format(16, false, false), ffmpeg, profile, true))
                    {
                        sink.PcmWritten = encoder.Enqueue;
                        var pcm = new byte[192000]; for (int i = 0; i < pcm.Length; i += 2) { short sample = (short)(Math.Sin(i / 4.0 * 2 * Math.PI * 440 / 48000) * 9000); pcm[i] = (byte)sample; pcm[i + 1] = (byte)(sample >> 8); }
                        sink.Write(pcm, 0, pcm.Length);
                        for (int attempt = 0; attempt < 30 && (!File.Exists(encoder.TemporaryPath) || new FileInfo(encoder.TemporaryPath).Length == 0); attempt++) Thread.Sleep(100);
                        Assert(File.Exists(encoder.TemporaryPath) && new FileInfo(encoder.TemporaryPath).Length > 0, "Encoder did not grow before stop.");
                        sink.Complete(); string final = encoder.FinishSession(sink.Completed, null);
                        Assert(sink.Completed.Count > 1 && final.EndsWith(profile.Extension), "Wrong segmentation or extension.");
                        Assert(Math.Abs(MediaExport.DecodeDuration(final, ffmpeg, 10000) - 1) <= 0.25, "Wrong duration.");
                        string info = MediaExport.Run(ffmpeg, "-hide_banner -nostdin -i " + MediaExport.Quote(final), 3000).Error;
                        Assert(info.Contains("Audio: " + (format == AudioOutputFormat.Opus ? "opus" : format == AudioOutputFormat.Mp3 ? "mp3" : "aac")), "Wrong codec.");
                        if (format == AudioOutputFormat.Opus) Assert(info.Contains("mono") && info.Contains("48000 Hz"), "Opus not mono / 48 kHz.");
                        string rebuilt = Path.Combine(folder, "rebuilt" + profile.Extension); ProfileExport.Rebuild(sink.Completed, rebuilt, ffmpeg, profile, null);
                        Assert(Math.Abs(MediaExport.DecodeDuration(rebuilt, ffmpeg, 10000) - 1) <= 0.25, "PCM rebuild wrong.");
                        bool collision = false; try { ProfileExport.Rebuild(sink.Completed, rebuilt, ffmpeg, profile, null); } catch (IOException) { collision = true; }
                        Assert(collision && File.Exists(final), "Publication collision replaced source.");
                        ResultReceipt.Write(folder, sink.SessionId, profile, 1, false, new[] { final });
                        var entries = RecordingCatalog.Read(folder, folder); Assert(entries.Exists(delegate(RecordingEntry e) { return e.Verified && e.Id == sink.SessionId; }), "Verified output not indexed.");
                    }
                });
            }
            check("settings persistence, profile immutability and corrupt fallback", delegate
            {
                string home = Path.Combine(root, "settings"); Directory.CreateDirectory(home); var settings = new SoundLeafSettings { Format = AudioOutputFormat.Opus, Language = "de", Theme = "dark", OutputFolder = Path.Combine(home, "audio") };
                RecordingProfile snapshot = settings.Profile; settings.Bitrates[1] = 32; settings.Save(home); bool restored;
                var loaded = SoundLeafSettings.Load(home, out restored);
                Assert(!restored && loaded.Profile.Bitrate == 32 && snapshot.Bitrate == 24 && loaded.Language == "de" && loaded.Theme == "dark" && loaded.OutputFolder == settings.OutputFolder, "Settings not preserved or snapshot mutated.");
                File.WriteAllText(Path.Combine(home, "State", "settings.json"), "broken"); loaded = SoundLeafSettings.Load(home, out restored);
                Assert(restored && loaded.Profile.Format == AudioOutputFormat.Mkv && loaded.Profile.Bitrate == 192, "Corrupt settings not defaulted.");
                SessionProfile.Write(home, "session", snapshot); Assert(SessionProfile.Read(home, "session").Bitrate == 24, "Profile not roundtripped.");
            });
            check("selected external folder WAV-only completion remains intentional", delegate
            {
                string home = Path.Combine(root, "outside-workspace"), folder = Path.Combine(home, "2026-10-01"); Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "2026-10-01 12-00-00-abcdef012345-001.partial.wav"); string final;
                using (var wave = new DurableWave(file, Tests.Format(16, false, false))) { wave.Write(new byte[192000], 0, 192000); final = wave.Complete(); }
                WavReceipt.Write(home, new[] { final }); Assert(SessionRecovery.Find(folder, home).Count == 0, "External WAV incorrectly pending.");
                Assert(RecordingCatalog.Read(home, folder)[0].Verified, "Legacy WAV completion not listed.");
            });
            check("new profile recovery honors persisted profile", delegate
            {
                string home = Path.Combine(root, "opus-recovery"), folder = Path.Combine(home, "Recordings"); Directory.CreateDirectory(folder);
                string id = "2026-10-01 12-00-00-abcdabcdef12";
                using (var wave = new DurableWave(Path.Combine(folder, id + "-001.partial.wav"), Tests.Format(16, false, false))) { wave.Write(new byte[192000], 0, 192000); wave.Complete(); }
                SessionProfile.Write(home, id, new RecordingProfile(AudioOutputFormat.Opus, 24));
                string final = SessionRecovery.Recover(SessionRecovery.Find(folder, home)[0], home, ffmpeg, null);
                Assert(final.EndsWith(".ogg") && RecordingCatalog.Read(home, folder)[0].Verified, "Recovery ignored profile.");
            });
            check("ready audio can be transcoded without changing the original", delegate
            {
                string source = Directory.GetFiles(Path.Combine(root, "profile-Mkv"), "*.mkv")[0]; byte[] before = File.ReadAllBytes(source);
                string final = Path.Combine(root, "converted.mp3"); ProfileExport.Transcode(source, final, ffmpeg, new RecordingProfile(AudioOutputFormat.Mp3, 128), null);
                Assert(Convert.ToBase64String(before) == Convert.ToBase64String(File.ReadAllBytes(source)) && File.Exists(final), "Original changed.");
                bool refused = false; try { ProfileExport.Transcode(source, source, ffmpeg, RecordingProfile.Default, null); } catch (IOException) { refused = true; }
                Assert(refused, "In-place transcode allowed.");
            });
            check("failed encoder rebuild retains all WAV parts", delegate
            {
                string folder = Path.Combine(root, "profile-fault");
                using (var sink = new SegmentedWave(folder, Tests.Format(16, false, false), 8192))
                using (var encoder = new LiveMkv(sink.CurrentPath, Tests.Format(16, false, false), "missing.exe", new RecordingProfile(AudioOutputFormat.Opus, 24), true))
                {
                    sink.PcmWritten = encoder.Enqueue; sink.Write(new byte[38400], 0, 38400); sink.Complete(); bool failed = false;
                    try { encoder.FinishSession(sink.Completed, null); } catch (Exception) { failed = true; }
                    Assert(failed && sink.Completed.Count == 5 && File.Exists(sink.Completed[4]), "Failed encoder lost WAV.");
                }
            });
            check("live encoder failure rebuilds one Opus result directly from WAV", delegate
            {
                string folder = Path.Combine(root, "forced-fallback"); var format = Tests.Format(16, false, false);
                using (var sink = new SegmentedWave(folder, format, 8192))
                using (var encoder = new LiveMkv(sink.CurrentPath, format, ffmpeg, new RecordingProfile(AudioOutputFormat.Opus, 24), true))
                {
                    sink.PcmWritten = encoder.Enqueue; sink.Write(new byte[192000], 0, 192000); sink.Complete();
                    encoder.AbortForVerification(); string final = encoder.FinishSession(sink.Completed, null);
                    Assert(final.EndsWith(".ogg") && Math.Abs(MediaExport.DecodeDuration(final, ffmpeg, 10000) - 1) < .25, "Fallback failed.");
                }
            });
            check("bounded encoder queue does not block capture on overflow", delegate
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                using (var encoder = new LiveMkv(Path.Combine(root, "overflow-001.partial.wav"), Tests.Format(16, false, false), Path.Combine(root, "FakeFfmpegHang.exe"), new RecordingProfile(AudioOutputFormat.Opus, 24), true))
                {
                    encoder.Enqueue(new byte[10 * 1024 * 1024], 0, 10 * 1024 * 1024);
                    Assert(encoder.HasFailed && encoder.QueuedBytes <= 8 * 1024 * 1024 && watch.ElapsedMilliseconds < 2000, "Overflow blocked or exceeded bound.");
                }
            });
            check("corrupt profile metadata prevents recovery without changing WAV", delegate
            {
                string home = Path.Combine(root, "corrupt-profile"), folder = Path.Combine(home, "Recordings"); Directory.CreateDirectory(folder);
                string id = "2026-10-01 12-00-00-abcdefabcdef"; string final;
                using (var wave = new DurableWave(Path.Combine(folder, id + "-001.partial.wav"), Tests.Format(16, false, false))) { wave.Write(new byte[192000], 0, 192000); final = wave.Complete(); }
                SessionProfile.Write(home, id, RecordingProfile.Default); File.WriteAllText(Path.Combine(home, "State", "Sessions", id + ".profile.json"), "broken");
                bool failed = false; try { SessionRecovery.Recover(SessionRecovery.Find(folder, home)[0], home, ffmpeg, null); } catch (Exception) { failed = true; }
                Assert(failed && DurableWave.ReadInfo(final, false).DataBytes == 192000 && Directory.GetFiles(folder, "*.mkv").Length == 0, "Bad profile caused audio loss.");
            });
            check("custom-folder library excludes backup audio and recognizes nested exports", delegate
            {
                string folder = Path.Combine(root, "library-scope"), dated = Path.Combine(folder, "2026-10-01"); Directory.CreateDirectory(dated);
                string final = Path.Combine(dated, "demo-export.ogg");
                File.Copy(Directory.GetFiles(Path.Combine(root, "profile-Opus"), "*.ogg")[0], final);
                ResultReceipt.Write(dated, "demo-export", new RecordingProfile(AudioOutputFormat.Opus, 24), 1, false, new[] { final });
                Directory.CreateDirectory(Path.Combine(folder, "Backups")); File.Copy(final, Path.Combine(folder, "Backups", "private-copy.ogg"));
                File.Copy(final, Path.Combine(dated, "unpublished.partial.ogg"));
                var entries = RecordingCatalog.Read(folder, folder);
                Assert(entries.Count == 1 && entries[0].Verified && entries[0].Seconds == 1, "Wrong metadata scope or private files indexed.");
            });
            check("startup ownership matches only this installation without extra arguments", delegate
            {
                string path = @"C:\Users\Public\Player\Player.exe";
                Assert(StartupOwnership.Matches(path, path) && StartupOwnership.Matches("\"" + path + "\"", path), "Own startup not matched.");
                Assert(!StartupOwnership.Matches(path + " --other", path) && !StartupOwnership.Matches(@"C:\Other\Player.exe", path) && !StartupOwnership.Matches(null, path), "Foreign startup matched.");
            });
            check("translation catalogs contain all three languages", delegate
            {
                Assert(TextCatalog.Complete, "Incomplete translations."); foreach (string lang in new[] { "ru", "en", "de" }) { TextCatalog.SetLanguage(lang); Assert(TextCatalog.T("settings") != "settings" && TextCatalog.T("conversionDone") != "conversionDone", "Missing translation."); } TextCatalog.SetLanguage("ru");
            });
            check("UTF-8 diagnostics retain Cyrillic, accents and Unicode paths", delegate
            {
                string name = "Жёлтый лист — Grüße.wav";
                var result = MediaExport.Run(ffmpeg, "-hide_banner -nostdin -i " + MediaExport.Quote(Path.Combine(root, name)), 3000);
                Assert(result.ExitCode != 0 && result.Error.Contains(name) && result.Error.IndexOf('\uFFFD') < 0, "External UTF-8 diagnostic was corrupted.");
                TextCatalog.SetLanguage("ru"); Assert(TextCatalog.T("settings") == "Настройки", "Cyrillic catalog corrupted.");
                TextCatalog.SetLanguage("de"); Assert(TextCatalog.T("previous") == "Zurück", "German accents corrupted."); TextCatalog.SetLanguage("ru");
            });
            check("translation preserves codec words instead of replacing embedded labels", delegate
            {
                foreach (string lang in new[] { "ru", "de", "en" })
                {
                    TextCatalog.SetLanguage(lang);
                    foreach (AudioOutputFormat format in Enum.GetValues(typeof(AudioOutputFormat))) { string label = new RecordingProfile(format, format == AudioOutputFormat.Opus ? 24 : 192).Label; Assert(TextCatalog.Translate(label) == label, "Profile words were translated internally."); }
                    Assert(TextCatalog.Translate("mono monotonic someone AAC ffmpeg libopus libmp3lame") == "mono monotonic someone AAC ffmpeg libopus libmp3lame", "Short label changed technical text.");
                    Assert(TextCatalog.Translate("on") == TextCatalog.T("on"), "Standalone label no longer translates.");
                }
                TextCatalog.SetLanguage("ru"); Assert(TextCatalog.Translate(" — saved and verified").Contains("сохранён"), "Known completion suffix no longer translates.");
            });
        }
    }
}
