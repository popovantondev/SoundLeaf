# SoundLeaf

[Deutsch](../docs/README.de.md) · [Русский](../docs/README.ru.md) · [English](../README.md)

Lernprojekt zur Aufnahme von Windows-Systemaudio mit Tray-Steuerung, WAV-Sicherung und Prüfung fertiger Dateien.

**Windows 11 · x64 · Vorabversion 3.0.5**

**[3.0.5 herunterladen](https://github.com/popovantondev/SoundLeaf/releases/tag/v3.0.5)** · **[Website](https://popovantondev.github.io/SoundLeaf/index-de.html)** · **[Anleitung](https://popovantondev.github.io/SoundLeaf/Guide-de.html)**

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="../assets/screenshots/de-control-dark.png">
  <img src="../assets/screenshots/de-control-light.png" width="360" alt="SoundLeaf">
</picture>

*Echte Oberfläche mit Demonstrationsdaten; keine Aufnahmen realer Gespräche.*

## Erste Schritte

SHA-256 prüfen. Setup installieren oder Portable vollständig entpacken: FFmpeg, passende Quellen und Lizenzen sind enthalten. Keine Administratorrechte nötig. Setup startet keine Aufnahme; der spätere App-Start beginnt sie nach Prüfungen. Aufgenommen wird das Windows-Standardausgabegerät, nicht das Mikrofon.

[Rechte](https://popovantondev.github.io/SoundLeaf/rights-de.html) · [Komponenten](https://popovantondev.github.io/SoundLeaf/notices-de.html) · [Fehler melden](https://github.com/popovantondev/SoundLeaf/issues/new/choose)

**Grenzen:** unsignierte Lernprojekt-Vorabversion; praktische Prüfung bisher auf einem Windows-PC. Keine professionelle Aufnahmelösung. App-Rechte und Drittanbieter-Lizenzen gelten getrennt.

<details>
<summary>Technische Details</summary>

## Speicherung und Wiederherstellung

Zunächst wird eine WAV-Sicherung mit regelmäßiger Aktualisierung und Datenträger-Flush geschrieben. Parallel entsteht AAC mit 192 kbit/s. Interne Teile werden nach dem Stoppen zu einer MKV zusammengeführt; vor dem Entfernen der WAV-Sicherung werden Dekodierung und Dauer geprüft. Die Oberfläche zeigt Verarbeitungsschritte und den geprüften Ausgabepfad. Frühere offene Sitzungen werden separat gezählt.

Bei fehlgeschlagener FFmpeg-Prüfung startet keine automatische Aufnahme. Der Menüpunkt „WAV-Aufnahme starten“ bietet ausdrücklich WAV ohne Encoder an. Menü und Tooltip kennzeichnen diesen Modus. Nach dem Stoppen werden geprüfte Pfade und der Ordnerbefehl angezeigt: WAV gespeichert, keine MKV erstellt. Lange Aufnahmen bleiben Teile bis 256 MiB, keine unbegrenzt große WAV. Ein atomarer Abschlussvermerk in `State/Sessions` verhindert die Einstufung abgeschlossener WAV-Sitzungen als Absturz. Bei Vermerkfehler bleibt Audio erhalten und der Status meldet einen Fehler. `State` beim Umzug mitnehmen.

Die Wiederherstellung nutzt vollständige und unvollständige WAV-Teile derselben Sitzung. Fehlende/mehrdeutige Teile, aktive Schreiber, unterschiedliche Formate und vorhandene Zieldateien werden abgelehnt. Nach erfolgreicher Prüfung werden Originale nach `Backups/RecoveredSessions` verschoben; die automatische 24-Stunden-Bereinigung betrifft diesen Ordner nicht. Bei Fehlern bleiben Originale erhalten, Arbeitsdateien können in `RecoveryWork` verbleiben.

## Profile, Panel und Aufnahmen

Das Panel erscheint sofort über dem tatsächlichen Tray-Symbol und wird sofort ausgeblendet. Die Animation mit einem zwischengespeicherten Fensterbild wurde entfernt: Sie verursachte Ruckeln und einen verspäteten Schatten. Die übliche Größe ist 430 × 520 logische Pixel. Es bleibt innerhalb der Monitor-Arbeitsfläche und berücksichtigt Per-Monitor-DPI. Bei fehlenden Symbolkoordinaten dient der gespeicherte Klickpunkt als Ersatz. Erneuter Klick, Escape und Fokusverlust verbergen nur das Panel, nicht die Aufnahme.

Über Start/Pause/Stopp zeigt ein zentriertes Histogramm aus 21 getrennten abgerundeten Säulen echte PCM-Pegel mit 95% visueller Ausprägung. Die laufende Aufnahme hat fünf kompakte Balken; ihre Ruhelinie ist nur 31 logische Pixel breit. Beide Anzeigen besitzen eigene zeitbasierte Glättung; neue Daten lassen die sichtbaren Höhen nicht springen. Stille blendet kurz und weich zur ruhenden Linie über; Pause/Stopp beenden die Bewegung sofort. Daten kommen alle 50 ms und verfallen nach 150 ms; die restliche Darstellung klingt kurz ab (bis 200 ms modellierte Hüllkurvenzeit, keine Windows-Zeitgarantie). Es sind Pegelanzeigen, keine Wellenform oder Frequenzspektren. Die aktuelle Anzeige schaltet bei etwa −48 dBFS RMS ein und unter −54 dBFS aus: Schwaches Rauschen und einzelne Spitzen aktivieren sie nicht. Sehr leiser echter Ton kann als Stille angezeigt werden. Aufgenommener Ton bleibt unverändert. „Ton erkannt“ bleibt während Sprechpausen bestätigt; Geräteverfügbarkeit beweist keine bestimmte hörbare Anwendung. Aufnahme, PCM, Codecs, Tray-Zustände und natürliches Blatt bleiben unverändert.

Keine Seiten-Scrollleisten: Unter 450 logischen Pixeln Höhe gliedern sich Einstellungen in Audio / Dateien / Design. Ordnerpfad und Codec-Details stehen vollständig im Tooltip. Aufnahmen werden seitenweise angezeigt. Abgerundete Auswahlfelder unterstützen Tab, Pfeile, Enter, Leertaste und Escape; Escape schließt zuerst die Liste, dann das Panel. Formatnamen werden kleingeschrieben; Audioeinstellungen bleiben unverändert.

Standard bleibt **MKV / AAC 192 kbit/s**, mit ursprünglicher Abtastrate und Kanalzahl. Einstellungen wählen ein Ausgabeformat: MKV/AAC, OGG/Opus (anfangs 24 kbit/s, mono), MP3, M4A/AAC oder AAC/ADTS. Opus nutzt 48 kHz, Sprachprofil und VBR-Zielbitrate, keine feste Dateigröße. MP3 nutzt CBR. Weitere AAC/MP3-Profile nutzen 48 kHz, behalten mono/stereo und mischen Mehrkanalton zu stereo. WAV sichert unverändertes PCM.

Opus: 16/24/32/48/64/96/128; AAC und MP3: 64/96/128/160/192/256/320 kbit/s. Bitraten werden pro Format gespeichert. Format und Speicherordner ändern sich nur im gestoppten Zustand; das Sitzungsprofil bleibt unveränderlich. Sprache Deutsch / Русский / English und helles/dunkles/systemabhängiges Design wechseln sofort, auch während einer Aufnahme.

Linksklick öffnet das kompakte Laub-Panel mit Steuerung, Aufnahmen und Einstellungen; Rechtsklick das Ersatzmenü. Escape oder ein Klick außerhalb blendet es aus, ohne die Aufnahme zu stoppen. Kein Hauptfenster und keine Taskleisten-Schaltfläche.

Der Speicherordner ist wählbar. Vorhandene Dateien werden nicht verschoben; vorher gewählte Ordner bleiben in der Bibliothek. Standard bleibt `Recordings` neben der EXE. Eigene Ziele enthalten auch `State/Sessions`, Sicherungen und Wiederherstellungsdateien; Metadaten mitnehmen. Programmeinstellungen bleiben neben der EXE.

Aufnahmen laden im Hintergrund, neueste zuerst, mit Format, Größe und bekannter Dauer. Abgeschlossene WAV-Teile sind gruppiert; temporäre Dateien ausgeschlossen. Alte Dateien ohne gültige Metadaten gelten als vorhanden, nicht geprüft; unbekannte Dauer wird nicht erfunden. Keine Massendekodierung beim Öffnen. Öffnen, im Ordner zeigen und in anderem Format speichern sind möglich; Löschen und Momentmarken fehlen bewusst.

Konvertieren erzeugt eine neue geprüfte Datei mit gewähltem Format, Bitrate und Ziel; das Original bleibt unverändert. Erneutes Kodieren erhöht die Qualität nicht. Bestehende Ziele und falsche Dateiendungen werden abgelehnt; WAV-Teile werden als ein PCM-Strom gelesen.

Zusatzformate verwenden einen Encoder über WAV-Grenzen hinweg und eine begrenzte Warteschlange. Fehler führen zur Neuerstellung aus geprüftem WAV-PCM. Vollständige Dekodierung, maximal 250 ms Dauerabweichung, Flush und Umbenennen ohne Überschreiben erfolgen vor WAV-Löschung. Sitzungsprofile werden vor Aufnahmebeginn atomar festgehalten; alte Sitzungen ohne Profil bleiben MKV/AAC 192.

## Build und Prüfungen

Version 3.0.5 ist auf GitHub veröffentlicht. Die lokale Vorbereitung erstellt Quelltext- und portable x64-ZIPs mit SHA-256; Das veröffentlichte 3.0.5-Paket enthält FFmpeg 9.0.2. Das portable ZIP vollständig entpacken und dessen README lesen. Für Quelltext-Builds erzeugt `Build-Launcher.ps1` unter Windows PowerShell 5.1 die `SoundLeaf.next.exe`. Für MKV muss ein kompatibles `tools/ffmpeg.exe` neben der EXE liegen; ausdrücklich gewähltes WAV benötigt keinen Encoder. Einen beschreibbaren Ordner verwenden; beim Umzug den gesamten Ordner mitnehmen und Autostart neu aktivieren.

`Run-Checks.ps1` führt Build, synthetische Tests, Symbolprüfungen und echte MKV-/WAV-Loopback-Tests aus. Die letzten Tests zeichnen Systemaudio im separaten Ordner `Verification` auf. Für SoundLeaf-Builds sind Python und ein separates SDK nicht erforderlich.

Praktisch nur auf einem Windows-Rechner geprüft. Der Besitzer bestätigt normalen Ton der letzten Aufnahme und funktionierende lange Sitzungen; dies ist kein eigenständiger instrumentierter Lasttest. Stromausfall und voller echter Datenträger wurden nicht getestet. Keine absolute Datensicherheitsgarantie. Bei Wechsel/Trennung des Ausgabegeräts eine neue Sitzung beginnen. Private Aufnahmen und persönliche Pfade nicht öffentlich teilen.

## Bilder und Release

![Steuerung](../assets/screenshots/de-control-light.png)

Testpanel mit Beispielsitzungen und synthetischen Pegeln, keine echten Gespräche. [Release-Vorbereitung](RELEASE.md). Aktuell: `Prepare-OfflineRelease.ps1` mit vollständig geprüftem Lauf, unveränderten Eingaben und App-/Encoder-Hashes. `Test-Setup.ps1` prüft Offline-Setup, `Test-OfflineRelease.ps1` Archive und Prüfsummen. Der Windows-Workflow prüft Quelltext, Build und Symbole, nicht vollständige Aufnahme. App-Rechte bleiben unverändert; die LGPL-Ausnahme betrifft nur das FFmpeg-Buildskript.

[Anleitung](https://popovantondev.github.io/SoundLeaf/Guide-de.html) · [Architektur](ARCHITECTURE.md) · [Prüfungen](VERIFICATION.md) · [Änderungen](../CHANGELOG.md) · [Rechte](https://popovantondev.github.io/SoundLeaf/rights-de.html) · [Drittanbieter](https://popovantondev.github.io/SoundLeaf/notices-de.html)

</details>
