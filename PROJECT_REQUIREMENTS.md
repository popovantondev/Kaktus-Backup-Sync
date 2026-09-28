# Produktanforderungen — Kaktus Backup & Sync 5.2

Die Anwendung sichert Ordner einseitig von der Quelle ins Ziel. Deutsch ist Standard;
Oberfläche und Dokumentation sind zusätzlich auf Englisch und Russisch verfügbar.

## Invarianten

- Keine Rückkopie zur Quelle und keine automatische Löschung von Arbeitsdateien.
- Nicht erreichbare Ordner sind Fehler, keine leeren Ordner.
- Vor Dateiänderungen Plan erneut prüfen, temporär schreiben, Inhalt verifizieren.
- Vor Aktualisierungen eine frühere Version im Ziel erhalten.
- Konflikte ohne ausdrückliche Entscheidung nicht überschreiben.
- Neue Aufgaben verlangen fünf manuelle erfolgreiche Bestätigungen, sofern der
  Nutzer den Schutz nicht ausdrücklich deaktiviert.
- Aufgaben und gesamte Synchronisierung können pausiert werden.
- Persönlicher Laufzeitzustand gehört nicht ins öffentliche Repository.

## Anforderungen

Aufgaben erstellen, bearbeiten, prüfen, ausführen, entfernen; frühere Versionen
anzeigen und separat wiederherstellen. Sieben Versionen pro Datei standardmäßig,
konfigurierbar. Konfiguration versionieren und drei datierte Sicherungen erhalten.
Automatische Intervalle ab einer Minute, voller Inhaltsabgleich standardmäßig jede
Stunde. Zugeordnete Wechseldatenträger bei Ankunft erkennen. Unauffälliger Betrieb im
Infobereich. Eine Instanz, verständliche Zustände, Verlauf und Fehlerdetails.

## Qualität und verbleibende Arbeit

Core enthält reine Modelle/Regeln, Infrastructure die Dateioperationen. Die
Hauptkoordination liegt in `MainViewModel` und `BackupWorkspace`; `MainWindow.xaml.cs`
enthält Ansichtsbindung, Fensteraktionen, Layout und Weiterleitung von Windows-
Nachrichten. `MainViewModel` bündelt weiterhin viele Abläufe. Eine feinere Aufteilung
bleibt offen; die gesamte Architektur gilt daher nicht als vollständig modularisiert.
Tests müssen Störungen und synthetische Abläufe abdecken. Vor Veröffentlichung:
Datenschutz, Lizenz, Git-Metadaten und sauberes Windows prüfen. Der aktuelle Stand
ist eine Vorabversion, keine Behauptung über den Abschluss aller Anforderungen.

Siehe [Handbuch](docs/HANDBUCH.de.md), [Audit](docs/RELEASE_AUDIT.md) und
[Anforderungstabelle](docs/REQUIREMENTS_STATUS.ru.md).
