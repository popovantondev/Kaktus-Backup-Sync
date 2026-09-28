> Historical snapshot. Current behavior: [DE](HANDBUCH.de.md), [EN](MANUAL.en.md), [RU](MANUAL.ru.md), [audit](RELEASE_AUDIT.md).

> Update: two-action synchronization is now implemented; 39 tests and the WPF harness passed. Source-only changes preserve previous versions, which can be exported separately. The disabled sidebar stays dark. The audit below describes the earlier new-file-only build; current behavior is documented in [README](../README.md). Architectural limitations remain; see [rules review (RU)](PROGRAMMING_RULES_REVIEW.ru.md).

# UI and reliability audit — 2026-09-08

Scope: local C# / WPF preview; source review, synthetic regression tests and a
real WPF window harness. This is an engineering review, not an independent
security certification.

## Resolved

- Replaced the crowded single-row footer with a separate wrapping action panel
  and a dedicated status area. The file table has its own scroll area.
- German, English and Russian resources cover labels, preview actions, statuses,
  error categories and confirmation dialogs. Language choice survives restart.
- Scanning, validation before saving and copying run off the UI thread.
  Editing, language changes and task switching are disabled while an operation
  runs. Cancellation is available; copying finishes its current file before
  cancellation takes effect.
- Copy executes the displayed plan. Editing the task invalidates that plan.
  A fresh task preview and saved source/destination must agree.
- Changing source or destination resets the stored confirmation counter to five.
  Every copy still requires confirmation, including after that counter reaches zero.
- Preflight checks all new-file operations before writing; each file is checked
  again immediately before copying. Interrupted runs retain a completed-file list.
- Removed the unused prototype copier that could overwrite existing files.
  The public preview copies new files only.
- Fixed trailing-separator equality and checks for reparse points in root
  ancestors, as well as traversed entries.
- Added an original two-arrow logo, executable icon and reproducible vector
  generation script. No third-party product artwork is included.

## Verification

Core regression suite: 33 tests passing.
WPF Release build: zero errors, zero warnings.
UI harness: all three languages at 900×700 and 1180×820, visible action bounds,
language persistence, task save, async preview, busy state, confirmed synthetic
copy, JSON run report and invalidation after a path edit.
Screenshots were rendered from real WPF windows and visually inspected.
Run the harness from a separate build output if an older application is open.

## Remaining scope and limits

- This is a new-file backup preview, not bidirectional synchronization. Updating
  existing files, version recovery and scheduling are not implemented.
- Filesystem checks reduce accidental link traversal but do not lock directory
  topology against another process replacing a directory during a run. Do not
  describe them as protection against an actively hostile filesystem.
- A run is not one transaction: completed new files remain after interruption.
  The app reports completed operations; external changes during verification
  can still make the last file's state uncertain.
- Concurrent application instances do not coordinate catalog writes. Run one
  active instance per task catalog.
- Native folder pickers use the Windows language. Application dialogs and
  buttons use the selected application language.
- Actual monitor scaling beyond the tested logical window sizes, screen-reader
  usability, unplugged devices and hardware failures need separate manual tests.
