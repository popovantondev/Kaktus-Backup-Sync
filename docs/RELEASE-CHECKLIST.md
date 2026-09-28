# Release checklist · Veröffentlichungs-Checkliste · Чек-лист релиза

This is a local preparation checklist, not a release approval. Older portable and source archives at `5.2.0-preview.6` predate S10/S11. A
local portable `5.2.0-preview.7` now includes the latest mascot, approved audio,
and documentation screenshots; the source archive still needs regeneration from
a reviewed clean commit. S08's clean-Windows interactive check remains blocked,
hosted GitHub CI has not run, and local Git history contains old non-neutral
author identities. Use a reviewed clean source export in a new repository
history; do not rewrite the preserved local history.

Dies ist eine lokale Vorbereitung, keine Freigabe. Ältere Pakete `5.2.0-preview.6` enthalten nicht die späteren Änderungen S10/S11.
Ein lokales portables Paket `5.2.0-preview.7` enthält das freigegebene Maskottchen,
den erlaubten Ton und die Sprach-Screenshots; der Quelltextexport folgt nach der Prüfung. Die
interaktive Prüfung auf einem sauberen Windows-System (S08) ist blockiert, die
GitHub-CI wurde nicht ausgeführt und die lokale Git-Historie enthält ältere
Autorenangaben. Für GitHub nur einen geprüften sauberen Quelltextexport mit neuer
Historie verwenden; die lokale Historie nicht umschreiben.

Это локальная подготовка, а не разрешение на публикацию. Старые пакеты `5.2.0-preview.6` не содержат изменения S10/S11. Локальный
переносной `5.2.0-preview.7` включает утверждённый маскот, разрешённый звук и
скриншоты руководств; архив исходников нужно пересоздать после проверки. Интерактивная проверка на
чистой Windows (S08) заблокирована, GitHub CI не запускалась, в локальной
истории есть старые данные авторов. Для GitHub использовать проверенный экспорт
исходников с новой историей; локальную историю не переписывать.

## Before the first repository · Vor dem ersten Repository · Перед созданием репозитория

- [ ] Review `LICENSE` and `RIGHTS.md`: source is for viewing; an unmodified
  release binary may be used personally; source and binary redistribution are
  not permitted by the project owner.
- [x] Use the owner-approved mascot artwork in the application and public source;
  exclude its private generation metadata. The owner confirmed permission to
  redistribute the supplied startup recording; include it in source and packages.
- [ ] Export from a reviewed clean commit. Check for personal paths, runtime
  state, old Git identities, ignored files, and unexpected assets.
- [ ] Create the public repository without importing the local `.git` history.
- [ ] After upload, confirm the hosted GitHub workflow passes; local checks do
  not count as a hosted CI result.
- [x] Add matching German, English, and Russian screenshots to manuals and
  language-specific README pages. The screenshots use fictional paths and files.
- [ ] Create a repository/social preview after checking all text and images.
- [ ] Set a concise description and relevant topics. Do not label the project
  open source or select an OSI license.

- [ ] `LICENSE` und `RIGHTS.md` prüfen: Quelltext nur zur Ansicht; eine
  unveränderte Release-Datei darf privat genutzt werden; Quelltext und
  Programmdatei dürfen nicht weitergegeben werden.
- [x] Das vom Eigentümer freigegebene Maskottchen in Anwendung und Quelltext
  verwenden und private Generierungsmetadaten entfernen. Der Eigentümer hat die
  Weitergabe der bereitgestellten Startaufnahme freigegeben; sie wird mitgeliefert.
- [ ] Aus einem geprüften sauberen Commit exportieren. Persönliche Pfade,
  Laufzeitdaten, alte Git-Autoren, ignorierte Dateien und unerwartete Assets prüfen.
- [ ] Öffentliches Repository ohne lokale `.git`-Historie erstellen.
- [ ] Nach dem Upload den GitHub-Workflow prüfen; lokale Ergebnisse ersetzen
  keinen erfolgreichen CI-Lauf auf GitHub.
- [x] Sprachlich passende deutsche, englische und russische Screenshots in
  Handbücher und README-Dateien aufnehmen. Pfade und Dateinamen sind erfunden.
- [ ] Ein Repository-/Social-Preview erstellen und Texte sowie Bilder prüfen.
- [ ] Kurze Beschreibung und passende Topics setzen. Nicht als Open Source
  bezeichnen und keine OSI-Lizenz auswählen.

- [ ] Проверить `LICENSE` и `RIGHTS.md`: исходники доступны для просмотра;
  неизменённую программу из релиза можно использовать лично; владелец не
  разрешает распространять исходники или бинарный файл.
- [x] Использовать маскот, разрешённый владельцем, и убрать приватные метаданные
  генерации. Владелец подтвердил права на включение предоставленной записи запуска.
- [ ] Экспортировать проверенный чистый коммит. Проверить личные пути,
  runtime-данные, старые Git-идентификаторы, игнорируемые и неизвестные файлы.
- [ ] Создать публичный репозиторий без локальной истории `.git`.
- [ ] После загрузки проверить GitHub workflow: локальная проверка не заменяет
  успешный запуск CI на GitHub.
- [x] Добавить немецкие, английские и русские снимки интерфейса в руководства
  и соответствующие README. Пути и названия файлов вымышлены.
- [ ] Создать обложку репозитория и проверить тексты и изображения.
- [ ] Указать краткое описание и подходящие темы. Не называть проект open source
  и не выбирать лицензию OSI.

## Before each release · Vor jedem Release · Перед каждым релизом

- [ ] Review and accept pending S10/S11 changes before packaging.
- [ ] Resolve S08 or explain its limitation in the release notes.
- [ ] Update version, changelog, manuals, and package names together.
- [ ] Build a fresh public self-contained Windows x64 ZIP including the owner-approved
  optional startup recording.
- [ ] Run relevant tests, package privacy checks, relocation checks, and the
  interactive smoke check. Record exactly what ran.
- [ ] Confirm `LICENSE`, `RIGHTS.md`, `ASSET-NOTICES.md`, `LICENSE.txt`, and
  `ThirdPartyNotices.txt` are included and match the bundled .NET runtime.
- [ ] Generate and independently verify a SHA-256 sidecar for every download.
- [ ] Mark a preview as a pre-release. Do not call it stable while external
  clean-Windows or device-failure checks remain open.
- [ ] Publish only after the owner explicitly authorizes the upload/release.

- [ ] Ausstehende Änderungen S10/S11 vor dem Paketbau prüfen und annehmen.
- [ ] S08 lösen oder die Einschränkung in den Release-Hinweisen erklären.
- [ ] Version, Änderungsprotokoll, Handbücher und Paketnamen gemeinsam aktualisieren.
- [ ] Ein neues portables Windows-x64-ZIP mit der vom Eigentümer freigegebenen
  optionalen Startaufnahme erstellen.
- [ ] Passende Tests, Datenschutzprüfung des Pakets, Umzugsprüfung und manuellen
  Start-Smoke-Test ausführen. Genau notieren, was geprüft wurde.
- [ ] Prüfen, dass `LICENSE`, `RIGHTS.md`, `ASSET-NOTICES.md`, `LICENSE.txt` und
  `ThirdPartyNotices.txt` enthalten sind und zur .NET-Laufzeit passen.
- [ ] Für jeden Download einen SHA-256-Wert erzeugen und unabhängig prüfen.
- [ ] Eine Preview als Vorabversion kennzeichnen. Solange externe Windows- oder
  Geräteprüfungen fehlen, nicht als stabile Version bezeichnen.
- [ ] Erst veröffentlichen, wenn der Eigentümer den Upload/Release ausdrücklich
  freigegeben hat.

- [ ] Оценить и принять незакрытые изменения S10/S11 перед сборкой.
- [ ] Разблокировать S08 или описать ограничение в заметках к выпуску.
- [ ] Согласованно обновить версию, список изменений, инструкции и имена пакетов.
- [ ] Собрать новый переносимый ZIP для Windows x64 с разрешённой владельцем
  дополнительной записью запуска.
- [ ] Выполнить подходящие тесты, проверку пакета на приватные данные, перенос
  папки и интерактивную проверку запуска. Записать, что именно проверено.
- [ ] Проверить наличие `LICENSE`, `RIGHTS.md`, `ASSET-NOTICES.md`, `LICENSE.txt`
  и `ThirdPartyNotices.txt` и соответствие файлов версии .NET.
- [ ] Создать и отдельно перепроверить SHA-256 для каждого файла загрузки.
- [ ] Пометить preview как предварительный выпуск. Не называть его стабильным,
  пока не выполнены внешние проверки Windows и устройства.
- [ ] Публиковать только после явного разрешения владельца.

## Suggested GitHub metadata · Vorschlag für GitHub · Настройки GitHub

- Description: `Portable Windows folder backup and sync · Deutsch / Русский / English`.
- Topics: `backup`, `file-sync`, `windows`, `wpf`, `csharp`, `portable-app`.
- Default branch: `main`; enable Issues. Use the approved mascot artwork; never
  include user paths or task data.

- Beschreibung: `Portable Windows-Ordnersicherung und Synchronisierung · Deutsch / Русский / English`.
- Topics: `backup`, `file-sync`, `windows`, `wpf`, `csharp`, `portable-app`.
- Standardbranch `main`; Issues aktivieren. Das freigegebene Maskottchen nutzen,
  niemals private Pfade oder Aufgabendaten.

- Описание: `Переносное резервное копирование и синхронизация папок Windows · Deutsch / Русский / English`.
- Темы: `backup`, `file-sync`, `windows`, `wpf`, `csharp`, `portable-app`.
- Основная ветка `main`; включить Issues. Для обложки использовать утверждённый
  маскот, не добавлять личные пути и данные задач.
