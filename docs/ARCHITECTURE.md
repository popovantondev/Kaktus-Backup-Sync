# Application architecture — preview.7

The application uses the same mascot image, `Assets/KaktusMascot.png`, in local
and public builds. The public source includes its original rendered pixels with
private generation metadata removed; a missing image is an error rather than a
reason to show alternate cactus artwork.

## Deutsch

Das Fenster zeigt Daten über WPF-Bindungen und leitet Aktionen an Befehle weiter.
MainViewModel hält Bearbeitungszustand, Auswahl und Befehle. BackupWorkspace speichert
Aufgaben und koordiniert Vorschau, Berichte und Synchronisation. Diese Abläufe können
ohne ein angezeigtes Fenster geprüft werden.

TaskPoller plant automatische Läufe. Vor einem Lauf werden die Laufwerkszuordnungen
aufgelöst und geänderte Pfade gespeichert. Manuelle Befehle und Zeitplanänderungen
warten auf das Ende eines abgebrochenen Hintergrundlaufs. Eine neue Pause wird so
nicht durch eine gleichzeitig gespeicherte Pfadänderung überschrieben.

Core entscheidet über den Plan ohne Dateioperationen. Infrastructure liest, prüft
und schreibt. Eine Zwischenkopie wird erst nach Inhaltsprüfung übernommen. Fenster
behalten nur Darstellung und Windows-Anbindung. IWorkspaceDialogs grenzt Dialoge ab.

## English

| Component | Responsibility |
|---|---|
| MainWindow | Bindings, layout, focus, gestures and Windows-event forwarding |
| MainViewModel | Commands, selection, dirty draft, operation state and tray presentation |
| Editor / Preview / Status / Versions ViewModels | Fields, plan rows, translated status and version export interaction |
| BackupWorkspace | Task persistence, verified path remapping and file workflows |
| WorkspacePreferences | Local language and global pause settings |
| IWorkspaceDialogs / WorkspaceDialogs | Confirmation, folder, conflict and history UI boundary |
| TaskPoller | Due times, arrival scheduling, pause and background cancellation |
| Core | Immutable plans, comparisons, path relationships and volume identity resolution |
| Infrastructure | Scanning, hashes, staging, versions, persistent state and reports |

Preview resolves a draft, preserves comparison state only for the same bound folder,
builds a plan and persists the task. Synchronization rechecks device identity and
file content. A changed binding never silently redirects a reviewed write.
Configuration schema 2 records endpoint serials and relative directories. Legacy
state is upgraded only when its original endpoint can still be verified.

Windows transfers use [CopyFileExW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-copyfileexw)
with cancellation inside a file. Native copying retains alternate streams and
attributes; content comparison hashes the default stream. Reserved `.SyncWork`
folders have an owner marker and exclusive lock. Pending copies are verified before
the final move/replacement. A later write cleans owned abandoned pending files under
that lock. This is process-crash recovery, not a hardware durability guarantee.

## Русский

Окно больше не сохраняет задачи и не управляет копированием. Оно показывает данные
и передаёт нажатия командам MainViewModel. BackupWorkspace отвечает за сохранение и
порядок файловых операций. Создание задачи, подтверждение, пауза и ошибки проверяются
без настоящего окна; вместо диалогов тест использует простую замену.

Ручная работа и фоновый запуск не пишут одновременно. Пауза во время подготовки
останавливает запуск до копирования. Изменение частоты сначала ждёт завершения фоновой
операции и затем сохраняет актуальную задачу. Непроверенный черновик при этом остаётся
в редакторе и не попадает в конфигурацию.

Реальное питание, контроллер накопителя и злонамеренная одновременная замена каталогов
остаются за пределами автоматических проверок. Весь запуск не является одной
транзакцией: уже законченные файлы остаются после прерывания задачи.
