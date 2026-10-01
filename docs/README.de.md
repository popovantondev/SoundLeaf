# Player

Deutsch · [Русский](README.ru.md) · [English](../README.md)

**Reines Lernprojekt · Windows 11 · x64 · Vorabversion 2.3.0**

Player ist ein C#-Lernprojekt zu Windows-Systemaudio, Hintergrundverarbeitung, sicherer Dateispeicherung und Bedienung im Infobereich. Es ist keine professionelle Aufnahmelösung. Die Oberfläche ist derzeit russisch; die Dokumentation ist dreisprachig.

![Demonstration der Statussymbole](../assets/tray-preview.png)

Die Aufnahme startet automatisch beim Öffnen der EXE. Das Symbolmenü bietet Start, Pause/Fortsetzen, Stoppen und Speichern, Ergebnis, Öffnen der geprüften Datei, Wiederherstellung unvollständiger Sitzungen und Autostart für den aktuellen Benutzer ohne Administratorrechte.

- Grünes Dreieck: Aufnahme. Gelbe Balken: Pause. Schwarzes Quadrat: gestoppt.
- Dickerer drehender Bogen um ein festes grünes Dreieck genau in der Mitte: Verarbeitung, keine Prozentanzeige. 24 vorbereitete Bilder, jeweils 80 ms.
- Rot: Fehler oder frühere unvollständige Sitzungen im gestoppten Zustand.

Die Quelle ist das standardmäßige Windows-Ausgabegerät, nicht das Mikrofon. Stille ist kein Fehler. Nur erlaubte Inhalte aufnehmen und erforderliche Zustimmung einholen.

Vor Aufnahmebeginn prüft ein Hintergrund-Thread Schreiben, Datenträger-Flush und Umbenennen einer eigenen Testdatei sowie kurzes AAC/MKV-Kodieren und Dekodieren mit jeweils drei Sekunden Prozesslimit. Unter 256 MiB freiem Speicher wird die Aufnahme gesperrt; zwischen 256 MiB und 1 GiB wird gewarnt. Nicht messbarer Speicher erlaubt den Start nur nach erfolgreicher Ordnerprüfung mit Warnung. Die Anfangsprüfung garantiert keinen Platz für die gesamte Aufnahme.

## Speicherung und Wiederherstellung

Zunächst wird eine WAV-Sicherung mit regelmäßiger Aktualisierung und Datenträger-Flush geschrieben. Parallel entsteht AAC mit 192 kbit/s. Interne Teile werden nach dem Stoppen zu einer MKV zusammengeführt; vor dem Entfernen der WAV-Sicherung werden Dekodierung und Dauer geprüft. Die Oberfläche zeigt Verarbeitungsschritte und den geprüften Ausgabepfad. Frühere offene Sitzungen werden separat gezählt.

Bei fehlgeschlagener FFmpeg-Prüfung startet keine automatische Aufnahme. Der Menüpunkt „Начать запись только в WAV“ bietet ausdrücklich WAV ohne Encoder an. Menü und Tooltip kennzeichnen diesen Modus. Nach dem Stoppen werden geprüfte Pfade und der Ordnerbefehl angezeigt: WAV gespeichert, keine MKV erstellt. Lange Aufnahmen bleiben Teile bis 256 MiB, keine unbegrenzt große WAV. Ein atomarer Abschlussvermerk in `State/Sessions` verhindert die Einstufung abgeschlossener WAV-Sitzungen als Absturz. Bei Vermerkfehler bleibt Audio erhalten und der Status meldet einen Fehler. `State` beim Umzug mitnehmen.

Die Wiederherstellung nutzt vollständige und unvollständige WAV-Teile derselben Sitzung. Fehlende/mehrdeutige Teile, aktive Schreiber, unterschiedliche Formate und vorhandene Zieldateien werden abgelehnt. Nach erfolgreicher Prüfung werden Originale nach `Backups/RecoveredSessions` verschoben; die automatische 24-Stunden-Bereinigung betrifft diesen Ordner nicht. Bei Fehlern bleiben Originale erhalten, Arbeitsdateien können in `RecoveryWork` verbleiben.

## Start und Prüfungen

Noch kein öffentliches Binärpaket. `Build-Launcher.ps1` unter Windows PowerShell 5.1 erzeugt `Player.next.exe`. Für MKV muss ein kompatibles `tools/ffmpeg.exe` neben der EXE liegen; ausdrücklich gewähltes WAV benötigt keinen Encoder. Einen beschreibbaren Ordner verwenden; beim Umzug den gesamten Ordner mitnehmen und Autostart neu aktivieren.

`Run-Checks.ps1` führt Build, synthetische Tests, Symbolprüfungen und echte MKV-/WAV-Loopback-Tests aus. Die letzten Tests zeichnen Systemaudio im separaten Ordner `Verification` auf. Für Player-Builds sind Python und ein separates SDK nicht erforderlich.

Praktisch nur auf einem Windows-Rechner geprüft. Stromausfall, voller echter Datenträger und mehrstündiger Dauerbetrieb wurden nicht getestet. Keine absolute Datensicherheitsgarantie. Bei Wechsel/Trennung des Ausgabegeräts eine neue Sitzung beginnen. Private Aufnahmen und persönliche Pfade nicht öffentlich teilen.

[Anleitung](Guide-de.html) · [Architektur](ARCHITECTURE.md) · [Prüfungen](VERIFICATION.md) · [Änderungen](../CHANGELOG.md) · [Rechte](../RIGHTS.md) · [Drittanbieter](../THIRD_PARTY_NOTICES.md)
