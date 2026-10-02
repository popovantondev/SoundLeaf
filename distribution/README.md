# SoundLeaf 3.0.4 — Windows x64

Deutsch · Русский · English

## Русский

Сугубо учебный проект работы с системным звуком Windows, не профессиональное средство записи. Распакуйте архив целиком в доступную для записи папку, например `C:\Users\Public\SoundLeaf`. Запустите `SoundLeaf.exe`. Консоль и отдельное главное окно не появляются; управление находится в трее. При исправном кодировщике запись начинается автоматически после проверок. Записывайте только разрешённые материалы с необходимым согласием участников.

**FFmpeg в архив не включён.** Для MKV/AAC, OGG/Opus, MP3, M4A/AAC и AAC положите совместимый `ffmpeg.exe` в `tools` рядом с программой: инструкции в `tools/README.txt`. Пока его нет, автоматическая запись не начинается; можно явно выбрать «Начать запись только в WAV» в меню трея. Не обещается один WAV неограниченного размера.

Левый щелчок открывает панель, правый — резервное меню. При остановке дождитесь проверки результата. Файлы по умолчанию сохраняются в `Recordings` рядом с EXE; папка выбирается в настройках. Не запускайте из ZIP, OneDrive или `Program Files`, если папка недоступна для записи. Администратор не требуется. Перед обновлением остановите запись, дождитесь сохранения и выйдите из программы; не заменяйте EXE во время записи. Сохраняйте `Recordings`, `State` и резервные копии вместе; при переносе заново включите автозапуск.

[Руководство](docs/Guide-ru.html) · [Права](RIGHTS.md) · [Сторонние компоненты](THIRD_PARTY_NOTICES.md)

## Deutsch

Reines Lernprojekt zu Windows-Systemaudio, keine professionelle Aufnahmelösung. Das gesamte ZIP in einen beschreibbaren Ordner entpacken und `SoundLeaf.exe` öffnen. Nur Tray-Steuerung, kein Konsolen-/Hauptfenster. Bei verfügbarer Kodierung startet die Aufnahme nach der Prüfung automatisch. Nur erlaubte Inhalte mit notwendiger Zustimmung aufnehmen.

**FFmpeg ist nicht enthalten.** Eine kompatible `ffmpeg.exe` nach `tools` kopieren; siehe `tools/README.txt`. Ohne Encoder startet keine automatische Aufnahme; ausdrücklich „Nur WAV aufnehmen“ im Tray-Menü wählen. Lange WAV-Aufnahmen bleiben mehrere Teile. Einstellungen erlauben einen Speicherordner; vorhandene Dateien werden nicht verschoben. Zum Aktualisieren erst stoppen, Speicherung abwarten und die App beenden. `Recordings`, `State` und Sicherungen behalten; Autostart nach einem Umzug neu aktivieren. Keine Administratorrechte erforderlich.

[Anleitung](docs/Guide-de.html) · [Rechte](RIGHTS.md) · [Drittanbieter](THIRD_PARTY_NOTICES.md)

## English

Strictly an educational Windows system-audio project, not a professional recording solution. Extract the entire ZIP to a writable folder and run `SoundLeaf.exe`. Controls live in the tray; there is no console or main window. Recording starts automatically after successful preflight when an encoder is available. Record only permitted material with required consent.

**FFmpeg is not included.** Provide a compatible `ffmpeg.exe` in `tools`; see `tools/README.txt`. Without it, automatic recording is blocked; explicitly choose WAV-only recording from the tray menu. Long WAV recordings remain multiple parts. Choose a save folder in Settings; old files are not moved. Before updating, stop, wait for final verification and exit. Keep `Recordings`, `State` and backups; re-enable startup after moving the app folder. Administrator rights are not required.

[User guide](docs/Guide-en.html) · [Rights](RIGHTS.md) · [Third-party components](THIRD_PARTY_NOTICES.md)

## Checksums / Prüfsummen / Контрольные суммы

Compare the archive SHA-256 with its accompanying `.sha256` file before extracting. Windows PowerShell: `Get-FileHash -Algorithm SHA256 -LiteralPath .\SoundLeaf-3.0.4-win-x64-no-ffmpeg.zip`. A matching hash detects a changed download; it does not constitute code signing. The EXE is unsigned and Windows may warn about an unknown publisher. Do not disable Windows protection.
