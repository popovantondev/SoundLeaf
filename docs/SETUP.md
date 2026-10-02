# SoundLeaf Setup — local preview / lokale Vorschau / локальная подготовка

No release has been published yet. Deutsch · Русский · English.

## Русский

`SoundLeaf-3.0.4-Setup-online.exe` устанавливает программу для текущего пользователя в `%LOCALAPPDATA%\Programs\SoundLeaf`, без администратора. EXE не подписан; контрольная сумма не заменяет подпись. Не отключайте защиту Windows.

Видимая галочка загрузки FFmpeg включена: установщик получает закреплённый архив GyanD 9.0.1 с GitHub (109 МБ), проверяет SHA-256 архива и EXE, сохраняет лицензию и описание. Интернет нужен только для загрузки; аудио не отправляется. Ошибка загрузки отменяет установку. Галочку можно снять; тогда нужен ручной `tools/ffmpeg.exe` или явно выбранный WAV. Автоматической загрузки «latest» нет.

Установщик не запускает запись и не включает автозапуск. После установки откройте SoundLeaf из «Пуска»: запись начинается после успешных проверок. Управление в трее. Записывайте только разрешённые материалы с необходимым согласием.

Непустые папки не перезаписываются. Автоматические обновления и перенос прежней установки `C:\Users\Public\Player` не реализованы. Перед переустановкой сохраните запись, выйдите и сделайте резервную копию; используйте другую пустую папку. Существующие записи не перемещаются.

Удаление через «Установленные приложения» удаляет только неизменённые файлы программы. Записи, настройки, журналы и изменённые файлы остаются. Запущенная установка не удаляется: сначала выйдите. Собственный автозапуск убирается только при точном совпадении пути. Деинсталлятор и квитанция могут остаться; пользовательская папка рекурсивно не удаляется.

Офлайн-комплект пока не подготовлен к публикации: нужен соответствующий конкретной сборке комплект исходников и материалов сборки всех включённых компонентов FFmpeg. Права SoundLeaf и стороннего компонента независимы.

## Deutsch

Das Online-Setup installiert ohne Administratorrechte unter `%LOCALAPPDATA%\Programs\SoundLeaf`. Der sichtbare Download-Schalter lädt GyanD FFmpeg 9.0.1 von GitHub (109 MB); ZIP und EXE werden per SHA-256 geprüft, Lizenz und Hinweise bleiben erhalten. Ohne Download FFmpeg selbst bereitstellen oder WAV ausdrücklich wählen. EXE nicht signiert; Windows-Schutz nicht deaktivieren.

Setup startet weder Aufnahme noch Autostart. SoundLeaf später im Startmenü öffnen: nach erfolgreichen Prüfungen startet die Aufnahme automatisch. Nur zulässige Inhalte aufnehmen. Vorhandene nicht leere Ordner werden nicht überschrieben. Keine automatische Aktualisierung oder Migration. Vor einer neuen Installation stoppen, speichern lassen, schließen, Daten sichern und einen leeren Zielordner wählen.

Deinstallation entfernt nur unveränderte Programmdateien. Aufnahmen, Einstellungen, Protokolle und geänderte Dateien bleiben. Eine laufende Installation wird nicht unterbrochen. Nur der exakt zugehörige Autostart wird entfernt. Uninstaller und Beleg können bleiben. Das Offline-Paket wartet auf Prüfung passender Encoder-Quelltexte und Build-Materialien.

## English

Online Setup installs without administrator rights under `%LOCALAPPDATA%\Programs\SoundLeaf`. Its visible checkbox fetches pinned GyanD FFmpeg 9.0.1 from GitHub (109 MB), verifies ZIP and EXE SHA-256 hashes and retains license/notices. Without this download, supply FFmpeg yourself or explicitly select WAV-only. EXE unsigned; do not disable Windows protection.

Setup does not start capture or enable startup. Launch SoundLeaf from Start later: recording begins after successful checks. Only record permitted material. Nonempty destinations are refused. Automatic upgrades and migration are not implemented. Before reinstalling, stop, wait for saving, exit and back up data; choose an empty folder.

Uninstall removes only unchanged program files. Recordings, settings, logs and modified files remain. A running installation is not interrupted. Only an exactly matching startup entry is removed. Uninstaller and receipt may remain. The offline package awaits review of matching encoder sources and build materials; original and third-party rights are separate.
