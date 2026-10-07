using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("SoundLeaf Setup")]
[assembly: AssemblyVersion("3.0.5.0")]
[assembly: AssemblyFileVersion("3.0.5.0")]
namespace SoundLeafSetup
{
    [DataContract] public sealed class Receipt
    {
        [DataMember] public string Root;
        [DataMember] public Dictionary<string,string> Files = new Dictionary<string,string>();
        [DataMember] public string Shortcut;
        [DataMember] public string ShortcutHash;
    }
    public static class Engine
    {
        public const bool BundledEncoder = false;
        public const string BundledEncoderHash = "";
        public const string BundledSourceHash = "";
        public const string EncoderUrl = "https://github.com/GyanD/codexffmpeg/releases/download/9.0.1/ffmpeg-9.0.1-essentials_build.zip";
        public const string EncoderZipHash = "FEC81AE03971D9DD4BE3EBE02E263BD2EC1D789483F931BDBA5F5715E65DA2E9";
        public const string EncoderExeHash = "72A489ECCD008C2EC2C0A5856C5C75BC3D8BBFA90166C4566865C246445E6AA3";
        public const string RuntimeExeHash = "BB2E3DED749FE456FC3D0418D2596B59303975C98340933E3FAE8ECC6B44E7A3";
        public const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SoundLeaf";
        public static string Hash(string path)
        { using (var s=File.OpenRead(path)) using (var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-",""); }
        public static string Child(string root, string relative)
        {
            if (string.IsNullOrEmpty(relative) || relative.Contains(":") || relative.StartsWith("/") || relative.StartsWith("\\") || relative.Split('/','\\').Any(p=>p=="..")) throw new IOException("Unsafe package path.");
            string full=Path.GetFullPath(Path.Combine(root,relative));
            if (!full.StartsWith(Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe package path.");
            return full;
        }
        static void CheckParents(string path)
        {
            for (var d=new DirectoryInfo(path);d!=null;d=d.Parent)
                if (d.Exists && (d.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException("Installation through a junction is not supported.");
        }
        public static void Extract(Stream stream, string root)
        {
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,true)) foreach(var entry in zip.Entries)
            {
                string path=Child(root,entry.FullName);
                if (!names.Add(path)) throw new IOException("Duplicate package path.");
                if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(path); continue; }
                if (entry.Length>160L*1024*1024) throw new IOException("Package entry too large.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var input=entry.Open()) using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write)) { input.CopyTo(output); output.Flush(true); }
            }
        }
        static void Save(Receipt receipt,string path)
        { using(var s=new FileStream(path,FileMode.CreateNew,FileAccess.Write)) { new DataContractJsonSerializer(typeof(Receipt)).WriteObject(s,receipt); s.Flush(true); } }
        public static Receipt Read(string root)
        {
            using(var s=File.OpenRead(Path.Combine(root,"installation.json"))) return (Receipt)new DataContractJsonSerializer(typeof(Receipt)).ReadObject(s);
        }
        public static string Download(string cache, Action<string> status)
        {
            string path=Path.Combine(cache,"encoder-download.zip");
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(EncoderUrl); request.Timeout=90000; request.ReadWriteTimeout=15000;
            using(var response=(HttpWebResponse)request.GetResponse())
            {
                if (response.ResponseUri.Scheme!="https") throw new IOException("HTTPS required.");
                if (response.ContentLength>160L*1024*1024) throw new IOException("Encoder package too large.");
                using(var input=response.GetResponseStream()) using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write))
                {
                    var bytes=new byte[65536]; long total=0; int count; var deadline=Stopwatch.StartNew(); long last=-1;
                    while((count=input.Read(bytes,0,bytes.Length))>0) { total+=count; if(total>160L*1024*1024 || deadline.Elapsed.TotalSeconds>180) throw new IOException("Encoder download limit exceeded."); output.Write(bytes,0,count); long mib=total/1048576; if(mib!=last){status("FFmpeg: "+mib+" MiB");last=mib;} }
                    output.Flush(true);
                }
            }
            if(Hash(path)!=EncoderZipHash) throw new IOException("FFmpeg ZIP SHA-256 mismatch; nothing installed.");
            return path;
        }
        public static void AddEncoder(string zipPath,string stage)
        {
            if(Hash(zipPath)!=EncoderZipHash) throw new IOException("FFmpeg ZIP SHA-256 mismatch.");
            string tools=Path.Combine(stage,"tools"); Directory.CreateDirectory(tools);
            using(var zip=ZipFile.OpenRead(zipPath))
            {
                string prefix="ffmpeg-9.0.1-essentials_build/";
                var exe=zip.GetEntry(prefix+"bin/ffmpeg.exe");
                if(exe==null || exe.Length>160L*1024*1024) throw new IOException("Expected encoder missing.");
                using(var input=exe.Open()) using(var output=new FileStream(Path.Combine(tools,"ffmpeg.exe"),FileMode.CreateNew,FileAccess.Write)) { input.CopyTo(output); output.Flush(true); }
                foreach(var name in new[]{"LICENSE","README.txt"})
                {
                    var entry=zip.GetEntry(prefix+name);
                    if(entry==null || entry.Length>1024*1024) throw new IOException("Encoder notice missing: "+name);
                    string path=Path.Combine(tools,"FFmpeg-"+name+".txt");
                    using(var input=entry.Open()) using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write)) { input.CopyTo(output); output.Flush(true); }
                }
            }
            if(Hash(Path.Combine(tools,"ffmpeg.exe"))!=EncoderExeHash) throw new IOException("FFmpeg EXE SHA-256 mismatch.");
        }
        public static void Install(string root, Stream payload, string uninstaller, string encoderZip, bool integrate)
        {
            root=Path.GetFullPath(root).TrimEnd('\\'); CheckParents(root);
            string parent=Path.GetDirectoryName(root);
            if(string.IsNullOrEmpty(parent) || root==Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) throw new IOException("Choose a dedicated application folder.");
            if(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new IOException("Destination is not empty. Existing installations and recordings are not overwritten; choose another empty folder.");
            if(integrate) using(var key=Registry.CurrentUser.OpenSubKey(RegistryPath)) if(key!=null) throw new IOException("A registered SoundLeaf installation already exists. Uninstall it first; recordings are retained.");
            Directory.CreateDirectory(parent);
            string stage=Path.Combine(parent,".SoundLeaf-install-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            try
            {
                Extract(payload,stage);
                if(!File.Exists(Path.Combine(stage,"SoundLeaf.exe")) || Hash(Path.Combine(stage,"SoundLeaf.exe"))!=RuntimeExeHash) throw new IOException("Verified application hash mismatch.");
                if(BundledEncoder)
                {
                    if(!string.IsNullOrEmpty(encoderZip)) throw new IOException("Offline setup does not accept an external encoder archive.");
                    if(Hash(Path.Combine(stage,"tools","ffmpeg.exe"))!=BundledEncoderHash || Hash(Path.Combine(stage,"tools","ffmpeg-9.0.2-corresponding-source.zip"))!=BundledSourceHash)
                        throw new IOException("Bundled encoder or corresponding source hash mismatch.");
                }
                if(!string.IsNullOrEmpty(encoderZip)) AddEncoder(encoderZip,stage);
                File.Copy(uninstaller,Path.Combine(stage,"Uninstall.exe"),false);
                var receipt=new Receipt{Root=root};
                foreach(string file in Directory.GetFiles(stage,"*",SearchOption.AllDirectories)) receipt.Files.Add(file.Substring(stage.Length+1),Hash(file));
                // Only setup-owned program files are listed, never recordings, state or logs.
                if(Directory.Exists(root)) Directory.Delete(root,false);
                Directory.Move(stage,root);
                try
                {
                    if(integrate)
                    {
                        string link=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),"Programs","SoundLeaf.lnk");
                        if(File.Exists(link)) throw new IOException("Existing start-menu shortcut was not overwritten.");
                        var type=Type.GetTypeFromProgID("WScript.Shell"); dynamic shell=Activator.CreateInstance(type); dynamic shortcut=shell.CreateShortcut(link);
                        shortcut.TargetPath=Path.Combine(root,"SoundLeaf.exe"); shortcut.WorkingDirectory=root; shortcut.IconLocation=Path.Combine(root,"SoundLeaf.exe")+",0"; shortcut.Save();
                        receipt.Shortcut=link; receipt.ShortcutHash=Hash(link);
                    }
                    Save(receipt,Path.Combine(root,"installation.json"));
                    if(integrate) using(var key=Registry.CurrentUser.CreateSubKey(RegistryPath))
                    {
                        key.SetValue("DisplayName","SoundLeaf"); key.SetValue("DisplayVersion",typeof(Engine).Assembly.GetName().Version.ToString()); key.SetValue("Publisher","popovantondev"); key.SetValue("InstallLocation",root);
                        key.SetValue("DisplayIcon",Path.Combine(root,"SoundLeaf.exe")); key.SetValue("UninstallString","\""+Path.Combine(root,"Uninstall.exe")+"\"");
                        key.SetValue("NoModify",1,RegistryValueKind.DWord); key.SetValue("NoRepair",1,RegistryValueKind.DWord);
                    }
                }
                catch
                {
                    if(receipt.Shortcut!=null && File.Exists(receipt.Shortcut) && Hash(receipt.Shortcut)==receipt.ShortcutHash) File.Delete(receipt.Shortcut);
                    foreach(var item in receipt.Files) { string file=Child(root,item.Key); if(File.Exists(file) && Hash(file)==item.Value) File.Delete(file); }
                    throw;
                }
            }
            finally
            {
                // The staging directory is uniquely owned and never contains user data.
                if(Directory.Exists(stage)) Directory.Delete(stage,true);
            }
        }
        public static void Uninstall(string root,bool integrate)
        {
            root=Path.GetFullPath(root).TrimEnd('\\'); CheckParents(root);
            var receipt=Read(root);
            if(!string.Equals(root,receipt.Root,StringComparison.OrdinalIgnoreCase)) throw new IOException("Installation receipt belongs to another path.");
            foreach(var item in receipt.Files) Child(root,item.Key);
            string app=Path.Combine(root,"SoundLeaf.exe");
            foreach(var process in Process.GetProcessesByName("SoundLeaf"))
            {
                try { if(string.Equals(process.MainModule.FileName,app,StringComparison.OrdinalIgnoreCase)) throw new IOException("Close this SoundLeaf installation first; recording was not interrupted."); }
                catch(System.ComponentModel.Win32Exception) { throw new IOException("Cannot check a running SoundLeaf process; uninstall cancelled."); }
                finally { process.Dispose(); }
            }
            foreach(var item in receipt.Files)
            {
                string file=Child(root,item.Key); CheckParents(Path.GetDirectoryName(file));
                if(string.Equals(file,Application.ExecutablePath,StringComparison.OrdinalIgnoreCase)) continue;
                if(File.Exists(file) && Hash(file)==item.Value) { using(var guard=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) { } }
            }
            foreach(var item in receipt.Files)
            {
                string file=Child(root,item.Key);
                if(!string.Equals(file,Application.ExecutablePath,StringComparison.OrdinalIgnoreCase) && File.Exists(file) && Hash(file)==item.Value) File.Delete(file);
            }
            if(receipt.Shortcut!=null && File.Exists(receipt.Shortcut) && Hash(receipt.Shortcut)==receipt.ShortcutHash) File.Delete(receipt.Shortcut);
            if(integrate) using(var key=Registry.CurrentUser.OpenSubKey(RegistryPath))
            {
                if(key!=null && string.Equals(key.GetValue("InstallLocation") as string,root,StringComparison.OrdinalIgnoreCase)) Registry.CurrentUser.DeleteSubKeyTree(RegistryPath,false);
            }
            if(integrate) using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true))
                if(key!=null) { string command=key.GetValue("SoundLeaf") as string; if(command=="\""+app+"\"" || command==app) key.DeleteValue("SoundLeaf",false); }
            // Keep receipt, self and all user-created files. No recursive deletion of installed folders.
        }
    }
    public sealed class SetupForm : Form
    {
        readonly ComboBox language=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
        readonly TextBox folder=new TextBox(); readonly Button browse=new Button(),action=new Button();
        readonly CheckBox encoder=new CheckBox{Checked=true}; readonly Label explanation=new Label(),status=new Label();
        readonly bool uninstall; bool busy,completed;
        string T(string en,string de,string ru) { return language.SelectedIndex==1?de:language.SelectedIndex==2?ru:en; }
        public SetupForm(bool uninstall)
        {
            this.uninstall=uninstall; Text="SoundLeaf"; ClientSize=new Size(600,320); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen;
            Font=new Font("Segoe UI",10); BackColor=Color.FromArgb(242,248,243);
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
            language.Items.AddRange(new object[]{"English","Deutsch","Русский"}); language.SetBounds(20,20,180,30);
            folder.SetBounds(20,65,470,30); browse.SetBounds(500,65,80,30);
            folder.Text=uninstall?AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","SoundLeaf");
            encoder.SetBounds(20,108,560,32); explanation.SetBounds(20,150,560,90); status.SetBounds(20,245,440,65); action.SetBounds(465,260,115,35);
            Controls.AddRange(new Control[]{language,folder,browse,encoder,explanation,status,action});
            folder.ReadOnly=uninstall; browse.Enabled=!uninstall; encoder.Visible=!uninstall; encoder.Enabled=!Engine.BundledEncoder;
            language.SelectedIndexChanged+=(s,e)=>Translate(); string lang=CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; language.SelectedIndex=lang=="de"?1:lang=="ru"?2:0;
            browse.Click+=(s,e)=>{using(var dialog=new FolderBrowserDialog()) if(dialog.ShowDialog(this)==DialogResult.OK) folder.Text=dialog.SelectedPath;};
            action.Click+=async(s,e)=>
            {
                if(completed) { Close(); return; }
                busy=true; action.Enabled=browse.Enabled=language.Enabled=folder.Enabled=encoder.Enabled=false;
                string root=folder.Text; bool fetch=encoder.Checked && !Engine.BundledEncoder;
                try
                {
                    await System.Threading.Tasks.Task.Run(()=>
                    {
                        if(uninstall) { Engine.Uninstall(root,true); return; }
                        string cache=Path.Combine(Path.GetTempPath(),"SoundLeaf-setup-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(cache);
                        try
                        {
                            string zip=fetch?Engine.Download(cache,text=>BeginInvoke((Action)(()=>status.Text=text))):null;
                            using(var output=File.Create(Path.Combine(cache,"Uninstall.exe"))) using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("uninstaller.exe")) input.CopyTo(output);
                            using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")) Engine.Install(root,input,Path.Combine(cache,"Uninstall.exe"),zip,true);
                        }
                        finally { Directory.Delete(cache,true); }
                    });
                    status.Text=T("Completed. Recordings were not moved or deleted.","Fertig. Aufnahmen wurden nicht verschoben oder gelöscht.","Готово. Записи не перемещались и не удалялись.");
                    completed=true; action.Text=T("Close","Schließen","Закрыть");
                }
                catch(Exception error) { status.Text=T("Failed: ","Fehler: ","Ошибка: ")+error.Message; }
                finally { busy=false; action.Enabled=true; if(!completed) { browse.Enabled=!uninstall; language.Enabled=true; encoder.Enabled=!Engine.BundledEncoder; folder.Enabled=!uninstall; } }
            };
            FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};
        }
        void Translate()
        {
            browse.Text="…";
            encoder.Text=T("Download FFmpeg 9.0.1 (109 MB, SHA-256 verified)","FFmpeg 9.0.1 laden (109 MB, SHA-256 geprüft)","Скачать FFmpeg 9.0.1 (109 МБ, проверка SHA-256)");
            if(Engine.BundledEncoder) encoder.Text=T("FFmpeg 9.0.2 included — no download needed", "FFmpeg 9.0.2 enthalten — kein Download nötig", "FFmpeg 9.0.2 включён — скачивание не нужно");
            explanation.Text=uninstall?T("Only unchanged installed program files are removed. Recordings, settings, logs and changed files remain. Close SoundLeaf first.","Nur unveränderte Programmdateien werden entfernt. Aufnahmen, Einstellungen, Protokolle und geänderte Dateien bleiben. SoundLeaf zuerst schließen.","Удаляются только неизменённые файлы программы. Записи, настройки, журналы и изменённые файлы остаются. Сначала закройте SoundLeaf."):
                T("Educational project. Current-user installation, no administrator rights. FFmpeg downloads from GyanD/GitHub under separate GPL terms. Without it, select WAV-only explicitly. Start SoundLeaf from the Start menu; it begins recording after checks. Existing folders are not overwritten.","Lernprojekt. Installation für den aktuellen Benutzer ohne Administratorrechte. FFmpeg wird von GyanD/GitHub unter separaten GPL-Bedingungen geladen. Ohne FFmpeg WAV ausdrücklich wählen. SoundLeaf im Startmenü starten; Aufnahme nach Prüfungen. Vorhandene Ordner werden nicht überschrieben.","Учебный проект. Установка для текущего пользователя без прав администратора. FFmpeg загружается с GyanD/GitHub, условия GPL отдельные. Без него режим WAV выбирается явно. Запуск из меню «Пуск» начинает запись после проверок. Существующие папки не перезаписываются.");
            if(!uninstall && Engine.BundledEncoder) explanation.Text=T("Educational project. Offline installation for the current user; no administrator rights. FFmpeg is included under separate LGPL terms with source and licenses. Setup never starts recording or enables startup. Launching SoundLeaf later begins recording after checks. Existing folders are not overwritten.", "Lernprojekt. Offline-Installation ohne Administratorrechte für den aktuellen Benutzer. FFmpeg mit Quellcode und Lizenzen unter separaten LGPL-Bedingungen enthalten. Setup startet keine Aufnahme und aktiviert keinen Autostart. Ein späterer Programmstart beginnt die Aufnahme nach Prüfungen. Bestehende Ordner werden nicht überschrieben.", "Учебный проект. Офлайн-установка для текущего пользователя без прав администратора. FFmpeg, исходники и лицензии включены; условия LGPL отдельные. Установщик не начинает запись и не включает автозапуск. Запуск программы позже начинает запись после проверок. Существующие папки не перезаписываются.");
            action.Text=uninstall?T("Uninstall","Entfernen","Удалить"):T("Install","Installieren","Установить");
        }
    }
    static class Program
    {
        [STAThread] static int Main()
        { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new SetupForm(Path.GetFileName(Application.ExecutablePath).Equals("Uninstall.exe",StringComparison.OrdinalIgnoreCase))); return 0; }
    }
}
