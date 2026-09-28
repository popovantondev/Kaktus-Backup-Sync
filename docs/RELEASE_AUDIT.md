# Release audit — 5.2.0-preview.7

This report covers a verified local preview. Physical power-loss testing and a
fresh Windows installation remain external checks.

## Changes and evidence

- Source and destination volumes have independent serial/relative-folder bindings.
  A unique matching volume updates a changed root; an absent or duplicate serial
  blocks writes. Legacy state upgrades only at a verifiable original endpoint.
  Real drive-letter strings are checked in domain tests; generated folder roots
  exercise arrival, path persistence and write guards without changing host devices.
- Comparison baselines and remembered conflict choices can follow the same bound
  location. Relocation tests preserve the baseline, avoiding false first-run conflicts.
- Configuration schema 2 accepts schema 1 and legacy arrays. A migration test checks
  task settings, upgraded schema and backup creation. Older builds reject schema 2.
- Windows copies check cancellation inside a file. Verified staged copies are
  committed only after content validation. Updates preserve the prior destination
  and a recovery version. Unowned reserved work folders are rejected untouched.
- Injected disk-full/device errors cover new files, updates, completed-file reports
  and retry. Tests also verify cancellation callbacks and retained alternate streams.
- A separate child process is terminated during a native staged transfer in a
  generated test tree. Both new-copy and update cases pass restart, abandoned-file
  cleanup and final-content checks. Only owned test processes are terminated.
- Case-only filename renames reuse unchanged destinations without a new version.
  Changed content and a second case change after preview invalidate the old plan.
  Case-only parent-directory changes are not implemented.
- MainWindow now handles view concerns only. MainViewModel owns commands and UI state;
  BackupWorkspace coordinates persistence and file workflows. Preferences, preview,
  status and version interactions have separate models/adapters.
- Windowless workflow checks cover task creation, confirmation, dirty-draft retention,
  invalid/corrupt state, arrival at a changed volume root, device mismatch and pause
  during asynchronous preparation. Schedule writes wait for background work to stop.
- The full WPF regression passes: task editing, asynchronous preview, confirmation,
  conflicts, history, versions, schedules, pause, five-run protection, localized
  errors, language persistence and tray behavior. Binding error tracing is empty.
  Existing two-size/three-language checks still pass; no expanded UI campaign was added.
- Startup growth, final hold, skipping and local sound decoding pass. Native icon
  assets are unchanged. Tray frames rotate and stop; language changes update menu,
  tooltip and pending notification text. Screenshots were visually inspected.

All 91 file-logic tests passed with no skips. The portable package passed normal
and tray-only startup with the bundled runtime and retained synthetic settings
after relocation. All write tests use generated data. No real drive was ejected, personal
synchronization run, host power altered or public repository published.

## Public-source preparation

Tracked source content and commit history were inspected. The public source export
omits old Git history and personal runtime/workflow files. Existing local history
is preserved. The owner confirmed permission to redistribute the supplied startup recording;
preview.7 includes it in its portable package and source export settings. The owner-selected
mascot is included in the public source with its original rendered pixels and
without private generation metadata. Older preview.6 packages contain outdated branding and must not be published.
The local preview.7 package was rebuilt with the approved mascot, recording and
language-matched manual screenshots; the clean source archive is not yet regenerated. See ASSET-NOTICES.md
and [source guidance](SOURCE-PACKAGE.md).

The Windows workflow builds and runs core, windowless orchestration and child-process
recovery checks. It does not publish. A hosted GitHub run is pending publication;
local execution is not claimed to be a GitHub CI result.

## Remaining scope and practical limits

- Clean Windows and physical USB/power interruption tests have not been performed.
  Software error injection and process termination do not reproduce those events.
- Expanded network, DPI and accessibility testing was excluded from this stage.
- A run is not one transaction: completed files remain after interruption.
- Cached metadata can delay hidden content-change detection until a full scan.
  Actual writes revalidate the default stream. Alternate streams are copied by
  Windows but are not the content comparison identity.
- Volume serials identify filesystems, not cryptographically unique physical devices.
  Duplicate connected serials fail closed. Reformatting needs a new binding.
- Hostile concurrent directory-topology changes and physical storage durability
  are outside the tested guarantees.
- Source and destination remain distinct from the portable program folder.
  Keep the complete runtime state when updating; schema 2 is not backward-readable.

Older audit files describe earlier snapshots. Use this report, the current
three-language manuals and [architecture](ARCHITECTURE.md) for current behavior.
