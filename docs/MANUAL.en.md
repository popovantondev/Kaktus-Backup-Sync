# Kaktus Backup & Sync 5.2 — Manual

[Deutsch](HANDBUCH.de.md) · [English](MANUAL.en.md) · [Русский](MANUAL.ru.md)

![Main window in English; folder names and files are fictional.](images/manual/en-main.png)

The language picker also updates the tray menu and tooltip. New notifications
use the selected language. Notifications already displayed by Windows retain
their previous text.

Error guidance and already visible details also change language. Do not include
the running portable app's folder in a task: its working data changes while running.
Keep the app outside synchronized folders. To copy the app itself, exit through
the tray and copy its entire folder in File Explorer.

## Create and run a task

Choose **New task**, enter a name, source and existing destination. **Browse**
starts in the folder already entered; **Open folder** opens that folder in Explorer.
**Preview** validates and saves the task, including an empty source, then displays
the plan. Errors appear with details in the preview area. Preview does not change
your source or destination files. **Synchronize** runs the reviewed plan after confirmation.

The direction is source → destination. New files are added. Source-only changes
update the destination after preserving its old version. Files are never copied
back to the source. Deleting a destination file causes it to be copied again.
The program does not delete working destination files.

## Automation, pause and tray

Enable **Automatically synchronize every** and select 1, 5, 15, 30 or 60 minutes.
For saved tasks, enable/pause and interval changes save immediately. Apply changed
paths and advanced settings with **Preview**. Editing and an open reviewed plan
take priority over background runs; hiding the window lets background work continue.

New tasks require five successful manual confirmations by default. Automatic
checks do not write until those confirmations are completed. **Safety and versions**
shows the remaining count; you can disable protection or reset it to five, then
apply the change with **Preview**. Errors and cancellation do not consume a confirmation.

**Pause task** pauses one task. **Pause all tasks** pauses all synchronization,
including manual runs, without changing individual task switches. The current
file finishes first. Global pause survives restarting; read-only preview remains
available. Closing or minimizing hides the app in the notification area. Double-click
its icon to return; use its **Exit** menu to stop safely. Rotating arrows mean work,
a green check means a completed run, gray means pause and yellow means attention.
Windows may place the icon in its hidden-icons menu. Successful background runs
are silent; repeated identical failures are deduplicated. Optional Windows startup
is available in the tray menu and starts the app hidden.

## Removable drives, conflicts and recovery

Set the folders, open **Safety and versions**, choose **Bind drive**, then **Preview**.
An arrival event for that bound removable volume triggers a run immediately, subject
to pause and confirmation protection. Source and destination bindings follow the
volume serial number when drive letters change. A missing or duplicate serial
blocks writes. Rebind after formatting. A legacy binding may need to be assigned
again if the letter changed before upgrading. The application never ejects the drive.

Different changes on both sides and files found only at the destination are
conflicts. Select appropriate conflict rows and choose **Use source**. This changes
the plan first; **Synchronize** applies it and keeps the previous destination version.
**Compare** shows both sides with size, date and hash. **Keep destination** is
remembered until either side changes; apply it with **Synchronize**. Cancel leaves
the plan unchanged. Unselected conflicts stay unchanged. A destination-only file cannot be replaced
from a missing source. There is no reverse copying to the source.

New old versions live under `.SyncVersions/<task-ID>` at the destination. The
default limit is seven per file, configurable from 1 to 100. After a successful run,
older excess versions belonging to that task are removed. **Versions** exports a
verified version as a new file without overwriting an existing one. Legacy versions
from earlier builds remain readable in the old local state and are not automatically
deleted. **History** lists manual and automatic copy runs.

## Install, portable mode and updates

Windows 10/11 x64. The portable release includes its .NET runtime and needs no
Python, SDK, Visual Studio or installer. Extract the complete
folder and run `KaktusBackupSync.exe`; keep its DLL files with it.

The ZIP already contains `portable.flag`. Tasks, language, history and cache live
in the adjacent `runtime`, created when the app is used. To move the app, exit
through the tray and copy the entire folder, including runtime, DLLs and Assets.

Earlier non-portable builds used `%LOCALAPPDATA%\AntonsBackupManager`. To preserve
old tasks, close both builds and copy that entire state into a new runtime folder
in the portable copy. Do not merge or overwrite existing state. See START-HERE.txt.

From preview.6, tasks are saved as schema 2 with both endpoint bindings. Schema 1
is read automatically. Older builds cannot read schema 2; downgrade only with a
complete runtime backup made before the update.

The application folder must be writable and
outside the folders being backed up. Before updating, exit from the tray, back up
the complete state including `sync` and `reports`, and replace program files in
the same installation folder. Never include personal state in the public ZIP.
A second instance of this build opens the first. Close older builds without the
instance lock beforehand. Reset optional startup after moving the program.

The last three dated configuration backups are kept beside `tasks.json`. If the
catalog is damaged, saving is blocked. Exit the app, preserve the damaged file
separately, and copy a verified backup back as `tasks.json`. Do not edit state files
while synchronization is running.

## Architecture and development

**Core** contains models, path rules and comparison decisions with no file I/O or
WPF. **Infrastructure** scans, hashes, safely writes files and stores versions,
configuration, history and cache. **App** contains WPF, editor/card ViewModels, tray
and Windows integration. **MainViewModel** coordinates commands and presentation;
**BackupWorkspace** owns task persistence and file workflows. Window code handles
rendering, gestures and Windows events. **Tests** uses disposable fictional data.

The flow is preview → immutable plan → confirmation → revalidation → temporary
file → verified replacement. Comments explain non-obvious decisions in simple English.

With .NET SDK 10.0.400 in the repository:

```powershell
dotnet test --project tests/AntonsBackupManager.Core.Tests -c Release
dotnet build tests/AntonsBackupManager.UiCheck -c Release -o runtime/ui-check
dotnet runtime/ui-check/AntonsBackupManager.UiCheck.dll runtime/ui-audit
dotnet publish src/AntonsBackupManager.App -c Release -r win-x64 --self-contained true -o artifacts/release
```

Initial restore requires package access. Runtime files, artifacts, private notes
and personal configuration are excluded from Git. A commit records a local code
snapshot; a push sends it to a server. Review author identity, history, license and
files before publishing. The source is available for viewing with no open-source
license granted.

## Safety and limits

OneDrive cloud metadata is accepted; links, junctions and unknown redirection tags
are rejected. Reading online-only files may require OneDrive to download them.
Source folders may overlap. Destinations and cross-task source/destination pairs
must not overlap.

Manual preview hashes all content. Automatic checks reuse known hashes when size
and modification time match, with a full scan each hour by default. Hidden changes
preserving both metadata values may only be detected by a full check. Before any
actual file replacement or copy, content is independently verified again.

A run is not one transaction. Completed copies remain after interruption and are
reported. Sharing-locked files are deferred while other files continue. History
shows their paths in amber; the next run retries them. Access denial and unavailable
drives still stop the run. Conflicts and deferred files do not consume a safety
confirmation. Tests cover injected disk-full/device errors, cancellation within a
large file and abrupt termination of an owned test process. Verified temporary
copies live in reserved `.SyncWork` folders. A subsequent write cleans abandoned
owned files under an exclusive lock. Do not place personal files there. The old
destination stays intact until verified replacement. Physical USB disconnection,
actual power loss and clean-machine installation remain untested. Expanded network,
display-scaling and accessibility checks are outside this stage. This is a preview.

An unambiguous rename matches size and SHA-256 with exactly one unchanged destination
file from the previous baseline. Preview shows both paths; synchronization renames
the destination without a new version. Duplicate-content ambiguity and changed
targets are not automatically renamed. Case-only filename changes are supported,
such as `Photo.jpg` → `photo.jpg`; parent-directory case is not changed. Another
filename change after preview requires a fresh plan. Files without a reliable match
remain new files or conflicts.

## Demonstration

![Startup screen with the approved Kaktus mascot.](images/manual/startup.png)

The owner-approved leaf-rustling recording is included in the source snapshot and
portable package. It is optional at runtime. See `ASSET-NOTICES.md` and the
[source package guide](SOURCE-PACKAGE.md).

On an ordinary launch a small cactus grows proportionally in its pot over three seconds, then
stays on screen for one second. Soft leaf rustling accompanies the growth.
Click or press a key to skip and stop the sound. Reduced Windows
animation settings are respected. Tray-only
launches show no intro. The cactus stays still in the tray while the arrows rotate.

Create two disposable folders outside the installation. Put the fictional files
from `demo/Source` in the source. Create, preview and synchronize a task. Edit a source
text file, synchronize again and restore its old version to a new file. Show global
pause and the tray. Public screenshots must use fictional paths and filenames only.
