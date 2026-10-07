# SoundLeaf 3.0.5 — Setup

## English

Windows 11 x64 · .NET Framework 4.8. Download Offline Setup or the portable ZIP from the release page. Setup installs for the current user without administrator rights; FFmpeg 9.0.2, corresponding source and licenses are included. No Internet is needed during installation. Setup does not launch the application or enable startup. Extract the portable archive in full to a writable folder. Check SHA256SUMS.txt. The unsigned preview may trigger a Windows publisher warning; do not disable protection.

Launching SoundLeaf starts recording automatically after folder, free-space and selected-profile checks. Capture uses the default Windows output device, not the microphone. Only record permitted content with required consent. Left-click the tray to open the panel; right-click for the fallback menu. Pause excludes paused time. Escape or clicking outside hides only the panel.

Strictly a learning project, not a professional recording solution. Tested on one Windows computer; screen/DPI matrices include modeled tests, not a claim of testing every display or a clean machine. Below 256 MiB free recording is blocked; low/unknown space warns. This is not a capacity guarantee. The quiet threshold changes only the level display, never captured audio. After changing output device start a new session. Before updates stop, wait, exit and back up Recordings and State. Uninstall retains user files; upgrades and automatic migration are not implemented.

## Deutsch

Windows 11 x64 · .NET Framework 4.8. Offline-Setup oder portables ZIP von der Versionsseite laden. Setup installiert nur für den aktuellen Benutzer ohne Administratorrechte; FFmpeg 9.0.2, passende Quellen und Lizenzen sind enthalten. Kein Internet während der Installation nötig. Setup startet weder App noch Autostart. Portables ZIP vollständig in einen beschreibbaren Ordner entpacken. SHA256SUMS.txt vergleichen. Unsignierte Vorschau: Windows kann vor unbekanntem Herausgeber warnen; Schutz nicht deaktivieren.

Beim Start beginnt die Aufnahme nach Prüfung von Ordner, freiem Platz und Audio-Profil automatisch. Quelle ist das Standard-Ausgabegerät von Windows, nicht das Mikrofon. Nur erlaubte Inhalte mit notwendiger Zustimmung aufnehmen. Linksklick öffnet die Tray-Oberfläche, Rechtsklick das Menü. Pausenzeiten fehlen im Ergebnis. Escape oder Klick außerhalb versteckt nur die Oberfläche.

Reines Lernprojekt, keine professionelle Aufnahmelösung. Auf einem Windows-Rechner getestet; Bildschirm-/DPI-Matrizen enthalten Simulationen, keine Zusage für alle Displays oder Neuinstallationen. Unter 256 MiB frei wird blockiert, bei wenig/unbekanntem Platz gewarnt. Keine Platzgarantie. Ruhe-Schwelle betrifft nur die Pegelanzeige, niemals aufgenommenes Audio. Nach Gerätewechsel neue Sitzung beginnen. Vor Updates stoppen, warten, beenden und Recordings/State sichern. Deinstallation behält Benutzerdaten; automatische Updates und Migration sind nicht implementiert.

## Русский

Windows 11 x64 · .NET Framework 4.8. Скачайте автономный Setup или portable ZIP со страницы выпуска. Setup устанавливает программу для текущего пользователя без администратора; FFmpeg 9.0.2, соответствующие исходники и лицензии вложены. Интернет при установке не нужен. Установщик не запускает приложение и не включает автозапуск. Portable ZIP распакуйте целиком в доступную для записи папку. Сверьте SHA256SUMS.txt. Версия не подписана: Windows может предупредить о неизвестном издателе; не отключайте защиту.

При запуске SoundLeaf запись начинается автоматически после проверки папки, свободного места и выбранного профиля. Источник — устройство вывода Windows по умолчанию, не микрофон. Записывайте только разрешённые материалы с необходимым согласием. Левый щелчок открывает панель, правый — меню. Время паузы исключается из результата. Escape или щелчок снаружи скрывает только панель.

Сугубо учебный проект, не профессиональное средство записи. Проверен на одном компьютере Windows; матрицы экранов/DPI включают моделирование, а не проверку всех мониторов и чистой системы. Ниже 256 МиБ свободного места запись запрещена, при малом или неизвестном месте есть предупреждение. Это не гарантия места на всю запись. Порог тишины влияет только на индикатор, не на звук в файле. После смены устройства начните новую сессию. Перед обновлением остановите запись, дождитесь сохранения, выйдите и сделайте копию Recordings и State. Удаление оставляет пользовательские данные; автоматическое обновление и перенос не реализованы.

## Installation safety

Default: `%LOCALAPPDATA%\Programs\SoundLeaf`. Nonempty folders and an existing registered installation are refused. Stop the previous installation and back up its data before reinstalling. No existing Public/Player installation or recording is migrated. Per-user Start menu and installed-app integration use HKCU, not HKLM.

Uninstall checks running/locked files and removes only unchanged installation-owned files. Recordings, settings, logs and modified files remain. Only an exactly matching owned startup entry is removed. The uninstaller and receipt can remain; the destination is never recursively deleted.

FFmpeg and the matching source kit are SHA-256 checked before installation is published. Installation is offline, with no automatic encoder download. Component rights are independent of SoundLeaf rights.
