# Kaktus Backup & Sync 5.2 — Handbuch

[Deutsch](HANDBUCH.de.md) · [English](MANUAL.en.md) · [Русский](MANUAL.ru.md)

![Hauptfenster auf Deutsch; Ordnernamen und Dateien sind frei erfunden.](images/manual/de-main.png)

Die Sprachauswahl gilt auch für Menü und Statushinweis im Infobereich. Neue
Benachrichtigungen verwenden die aktuell gewählte Sprache. Bereits von Windows
angezeigte Benachrichtigungen behalten ihren bisherigen Text.

Fehlerhinweise und bereits sichtbare Details ändern ebenfalls die Sprache.
Der Ordner der laufenden portablen Anwendung darf nicht Teil einer Aufgabe sein:
Ihre Arbeitsdaten ändern sich während des Betriebs. Die Anwendung außerhalb der
synchronisierten Ordner aufbewahren. Für eine Kopie der Anwendung im Infobereich
beenden und die gesamte Programmmappe im Explorer kopieren.

## Aufgabe erstellen

1. **Neue Aufgabe** anklicken und einen Namen eingeben.
2. Quelle und Ziel auswählen. **Auswählen** startet im bereits eingetragenen
   Ordner. **Öffnen** öffnet ihn im Explorer. Das Ziel muss schon existieren.
3. **Prüfen** anklicken. Die Aufgabe wird nach einer erfolgreichen Prüfung
   gespeichert; auch eine leere Quelle ist erlaubt. Bei einem Fehler zeigt die
   Dateivorschau Details. Die Prüfung verändert keine Nutzdateien.
4. Den Plan lesen. **Synchronisieren** anklicken und bestätigen.

Die Richtung ist immer Quelle → Ziel. Neue Dateien werden hinzugefügt.
Ändert sich nur die Quelle, wird das Ziel nach Prüfung aktualisiert. Vorher
wird seine bisherige Version gesichert. Bestehende Dateien werden nicht gelöscht.
Eine im Ziel gelöschte Datei wird beim nächsten Lauf erneut aus der Quelle kopiert.

## Automatisch arbeiten und pausieren

Die Option **Automatisch synchronisieren alle** aktiviert eine Aufgabe. Zur Wahl
stehen 1, 5, 15, 30 und 60 Minuten. Bei gespeicherten Aufgaben werden Schalter und
Intervall sofort gespeichert. Pfade und erweiterte Einstellungen werden mit
**Prüfen** übernommen. Während der Bearbeitung oder einer offenen Vorschau haben
manuelle Aktionen Vorrang; das Hintergrundprogramm arbeitet nach dem Verbergen
des Fensters weiter.

Neue Aufgaben verlangen standardmäßig fünf erfolgreiche manuelle Bestätigungen.
Automatische Prüfungen schreiben in dieser Zeit nichts. **Sicherheit und Versionen**
zeigt den Zähler. Dort lässt sich der Schutz abschalten oder auf fünf zurücksetzen.
Anschließend **Prüfen** anklicken. Fehler, Abbrüche, Konflikte und noch belegte Dateien
verbrauchen keine Bestätigung.

**Pause** stoppt die ausgewählte Aufgabe; **Alle pausieren** pausiert alle Aufgaben
einschließlich manueller Synchronisierung, ohne deren einzelne Schalter zu ändern.
Die bereits bearbeitete Datei wird fertiggestellt. Die globale Pause bleibt nach
einem Neustart erhalten. Eine Prüfung ohne Kopieren bleibt möglich.

Schließen und Minimieren verbergen das Fenster im Infobereich. Ein Doppelklick
auf das Symbol öffnet es. **Beenden** im Symbolmenü schließt die Anwendung nach
einem kontrollierten Stopp. Drehende Pfeile zeigen Arbeit, ein grünes Häkchen einen
abgeschlossenen Lauf, Grau Pause und Gelb notwendigen Eingriff. Windows kann das
Symbol unter dem kleinen Pfeil für ausgeblendete Symbole ablegen.

Erfolgreiche Hintergrundläufe erzeugen keine Meldungsfenster. Gleiche Fehler
werden nicht bei jeder Prüfung erneut gemeldet. Das Menü bietet einen freiwilligen
Start mit Windows; dann startet die Anwendung im Infobereich.

## USB, Konflikte und frühere Versionen

Beim normalen Start wächst ein kleiner Kaktus gleichmäßig in seinem Topf.
Das dauert drei Sekunden; er bleibt
danach eine Sekunde sichtbar. Ein Klick oder eine Taste
stoppt auch das leise Blätterrascheln und
überspringt die Animation. Bei deaktivierten Windows-Animationen erscheint er sofort;
beim Start im Infobereich wird keine Startgrafik gezeigt. Das Symbol verbindet einen
Kaktus mit zwei Pfeilen. Nur die Pfeile drehen sich während der Synchronisierung.

Zum Zuordnen eines angeschlossenen Wechseldatenträgers Quelle und Ziel einstellen,
**Sicherheit und Versionen → Laufwerk zuordnen** anklicken und mit **Prüfen**
speichern. Nur die zugeordnete Datenträgerkennung löst einen sofortigen Start aus.
Pause und Bestätigungsschutz gelten dabei weiter. Quelle und Ziel folgen jeweils
der Seriennummer ihres Volumes, auch bei einem neuen Laufwerkbuchstaben. Fehlt das
Volume oder melden zwei angeschlossene Volumes dieselbe Nummer, wird nicht
geschrieben. Nach Formatierung neu zuordnen. Eine alte Zuordnung muss eventuell
erneuert werden, wenn der Buchstabe bereits vor dem Update geändert wurde.
Das Programm wirft keinen Datenträger aus.

Unterschiedliche Änderungen auf beiden Seiten oder Dateien nur im Ziel werden
als Konflikte angezeigt. Die Dateien bleiben zunächst erhalten. Vorhandene
Konfliktzeilen auswählen und **Quelle übernehmen** anklicken: Zunächst ändert sich
nur der Plan. **Synchronisieren** führt ihn aus und sichert die alte Zielversion.
Mit **Vergleichen** werden beide Seiten mit Größe, Datum und Hash angezeigt.
**Ziel behalten** merkt sich die Entscheidung, bis sich eine der Seiten ändert;
mit **Synchronisieren** wird sie übernommen. **Abbrechen** lässt den Plan unverändert.
Dateien nur im Ziel können nicht aus einer fehlenden Quelle ersetzt werden.
Nicht ausgewählte Konflikte bleiben unverändert. Es gibt keine Rückkopie zur Quelle.

Neue frühere Versionen liegen im Ziel unter `.SyncVersions/<Aufgaben-ID>`.
Standardmäßig bleiben sieben Versionen je Datei; die Grenze ist zwischen 1 und 100
einstellbar. Nach einem erfolgreichen Lauf werden überschüssige ältere Versionen
dieser Aufgabe entfernt. **Versionen** stellt eine gewählte Version als neue Datei
wieder her und überschreibt keine vorhandene Datei. Alte Versionen aus früheren
Ausgaben bleiben im bisherigen lokalen Zustand lesbar und werden nicht automatisch
gelöscht. Die Verlaufsansicht enthält manuelle und automatische Kopierläufe.

## Installation und Aktualisierung

Windows 10/11, x64. Die portable Ausgabe enthält die benötigte .NET-Laufzeit.
Python, SDK, Visual Studio und eine Installation werden nicht benötigt.
Die gesamte Programmmappe entpacken; die EXE zusammen mit ihren DLLs aufbewahren.
Dann `KaktusBackupSync.exe` öffnen. Die EXE nicht einzeln verschieben.

Die ZIP enthält bereits `portable.flag`. Aufgaben, Sprache, Verlauf und Cache liegen
im Unterordner `runtime`, der beim Benutzen erstellt wird. Zum Umziehen die Anwendung
über den Infobereich beenden und die gesamte Mappe samt runtime, DLLs und Assets kopieren.

Frühere nicht portable Ausgaben nutzten `%LOCALAPPDATA%\AntonsBackupManager`.
Zum Übernehmen beide Ausgaben beenden und den gesamten alten Zustand in einen
neuen runtime-Ordner der portablen Kopie kopieren. Bestehende Zustände nicht mischen
oder überschreiben. Weitere Hinweise stehen in START-HERE.txt.

Ab preview.6 speichert die Anwendung Aufgaben im Format 2 mit beiden Zuordnungen.
Format 1 wird automatisch gelesen. Ältere Ausgaben können Format 2 nicht lesen;
für eine Rückkehr die gesamte vor dem Update gesicherte runtime-Mappe verwenden.

Diese Programmmappe muss beschreibbar sein und außerhalb der gesicherten Ordner
liegen. Beim Aktualisieren die Anwendung über den Infobereich beenden, die komplette
Konfiguration samt `sync` und `reports` sichern und die neuen Programmdateien in
denselben Programmordner übernehmen. Persönliche Daten gehören nicht ins öffentliche ZIP.

Eine zweite Instanz dieser Ausgabe öffnet die erste. Ältere Ausgaben ohne diese
Sperre vorher beenden. Autostart nach dem Verschieben der Programmmappe erneut setzen.
Die drei letzten Sicherungen der Aufgabenkonfiguration liegen neben `tasks.json`.
Bei einer beschädigten Konfiguration bleibt das Speichern gesperrt. Anwendung beenden,
die beschädigte Datei separat sichern und eine geprüfte Sicherung als `tasks.json`
zurückkopieren. Nicht im laufenden Betrieb Konfigurationsdateien austauschen.

## Architektur und Entwicklung

- **Core** enthält Aufgabenmodelle, Pfadregeln und den Vergleichsplan. Es führt
  keine Dateioperationen aus und kennt WPF nicht.
- **Infrastructure** liest und prüft Dateien, schreibt sichere Kopien, verwaltet
  Versionen, Konfiguration, Berichte und Hash-Cache.
- **App** enthält WPF und die Windows-Anbindung. **MainViewModel** koordiniert
  Befehle und Anzeigezustand. **BackupWorkspace** verwaltet gespeicherte Aufgaben
  und Dateiabläufe. Der Fenstercode behandelt Darstellung, Gesten und Windows-Ereignisse.
- **Tests** prüft Regeln und echte Abläufe mit selbst erzeugten Beispieldateien.

Die Entscheidung und ihre Ausführung sind getrennt: Prüfen → unveränderlicher
Plan → Bestätigung → erneute Prüfung → temporäre Datei → geprüfte Übernahme.
Kommentare erklären schwierige Entscheidungen auf einfachem Englisch.

Mit .NET SDK 10.0.400 im Projektordner:

```powershell
dotnet test --project tests/AntonsBackupManager.Core.Tests -c Release
dotnet build tests/AntonsBackupManager.UiCheck -c Release -o runtime/ui-check
dotnet runtime/ui-check/AntonsBackupManager.UiCheck.dll runtime/ui-audit
dotnet publish src/AntonsBackupManager.App -c Release -r win-x64 --self-contained true -o artifacts/release
```

Der erste Restore benötigt die Paketquelle. `runtime`, `artifacts`, Konfiguration,
Berichte und private Arbeitsnotizen sind aus Git ausgeschlossen. Ein Commit ist
ein lokaler gespeicherter Entwicklungsstand; ein Push veröffentlicht ihn auf einem
Server. Vor einem Push Identität, Verlauf, Lizenz und Dateien gesondert prüfen.
Die Quellen sind zum Betrachten vorgesehen; es wird keine offene Lizenz erteilt.

## Datensicherheit und Grenzen

Prüfungen erkennen OneDrive-Cloud-Metadaten und unterscheiden sie von Links.
OneDrive kann beim Lesen bisher nicht lokal gespeicherte Dateien herunterladen;
dafür muss der Anbieter verfügbar sein. Symlinks, Junctions und unbekannte
Umleitungstypen werden abgelehnt. Quellen dürfen sich überschneiden; Ziele und
Quelle-Ziel-Paare verschiedener Aufgaben dürfen sich nicht überschneiden.

Manuelle Prüfungen lesen alle Dateiinhalte. Automatische Prüfungen verwenden bekannte
Hashes bei unveränderter Größe und Änderungszeit und lesen standardmäßig stündlich
alles neu. Eine versteckte Änderung bei identischen Metadaten kann daher erst bei
der Vollprüfung auffallen. Vor einer tatsächlichen Dateiänderung wird der Inhalt
immer erneut geprüft. Der Cache ersetzt diese Schreibprüfung nicht.

Ein Lauf ist keine Gesamttransaktion: bereits kopierte Dateien bleiben nach einer
Unterbrechung erhalten. Stromausfall, volle Laufwerke, Hardwarefehler und mutwillige
Verzeichnisänderungen durch andere Prozesse sind nicht vollständig abgesichert.
Eine durch ein anderes Programm gesperrte Datei wird zurückgestellt. Andere Dateien
werden weiter bearbeitet. Die Historie zeigt den Dateipfad gelb; der nächste Lauf
versucht es erneut. Zugriffsfehler und fehlende Datenträger stoppen weiterhin den Lauf.
Geprüft wurden simulierte Fehler für vollen Speicher und fehlende Geräte, Abbruch
innerhalb einer großen Datei sowie das erzwungene Beenden eines eigenen Testprozesses.
Geprüfte Zwischenkopien liegen im reservierten Ordner `.SyncWork`. Beim nächsten
Schreiben werden eigene verwaiste Dateien unter exklusiver Sperre bereinigt.
Dort keine persönlichen Dateien ablegen. Das alte Ziel bleibt bis zum geprüften
Ersetzen erhalten. Physische USB-Unterbrechung, echter Stromausfall und ein sauberes
zweites Windows-System sind noch nicht geprüft. Erweiterte Netzwerk-, DPI- und
Zugänglichkeitsprüfungen gehören nicht zu diesem Schritt. Es bleibt eine Vorabversion.

Bei einer eindeutigen Umbenennung stimmen Größe und SHA-256 mit genau einer
unveränderten, früher gesicherten Zieldatei überein. Die Vorschau zeigt alten und
neuen Pfad. Das Ziel wird ohne neue Version umbenannt. Mehrdeutige gleiche Inhalte,
und geänderte Ziele werden nicht automatisch umbenannt. Eine Änderung nur der
Groß-/Kleinschreibung eines Dateinamens wird unterstützt, etwa `Foto.jpg` → `foto.jpg`.
Die Schreibweise übergeordneter Ordner bleibt erhalten. Eine erneute Namensänderung
nach der Vorschau verlangt einen neuen Plan. Dateien ohne sichere Zuordnung bleiben
neue Dateien oder Konflikte.

## Kurze Demonstration

![Startbildschirm mit dem freigegebenen Kaktus-Maskottchen.](images/manual/startup.png)

Die vom Eigentümer zur Weitergabe freigegebene Aufnahme ist im Quelltextpaket
und im portablen ZIP enthalten. Sie ist zur Laufzeit optional. Siehe
`ASSET-NOTICES.md` und die [Anleitung zum Quelltextpaket](SOURCE-PACKAGE.md).

Zwei leere Beispielordner außerhalb der Programmmappe anlegen. Die erfundenen
Dateien aus `demo/Source` in den Quellordner übernehmen. Eine Aufgabe erstellen,
prüfen, synchronisieren. Eine Textdatei der Quelle ändern und erneut synchronisieren.
Die vorige Fassung über **Versionen** als neue Datei wiederherstellen. Zuletzt die
globale Pause und den Tray zeigen. Für öffentliche Bilder ausschließlich fiktive
Pfade und Dateinamen verwenden.
