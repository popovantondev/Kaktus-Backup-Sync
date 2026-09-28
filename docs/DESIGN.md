# Kaktus Backup & Sync — visual design

The mascot greets the user at startup and in an empty task. File information and
actions remain the focus once a preview is open.

Every product surface uses the same approved mascot image in
`src/AntonsBackupManager.App/Assets/KaktusMascot.png`. The startup screen, empty
task state, application icon, Explorer icon, tray, and portfolio demo must not
draw or substitute another cactus. The source PNG retains its original pixels
and transparency; generation metadata is stripped for the public source.

| Role | Color |
|---|---|
| Sidebar / icon base | Forest green, #123B30 |
| Main action | Green, #23734F |
| Cactus / accents | Sage, #BCE49F |
| Surface | Warm light green, #F3F6EF |
| Main text | Dark green, #173B31 |

The startup illustration uses two clipped views of the same image source.
The pot stays still while a small cactus grows from 12% to full size over three
seconds. Width and height use the same animation clock, preserving proportions.
The plant remains upright throughout, with no sway or rotation.
The complete mascot then stays visible for one second. A click or key skips both
stages. Windows animation settings are respected; background startup shows no intro.

A fixed mask follows the pot opening so hidden parts of the cactus cannot appear
beside the pot. The first frame contains a tiny plant in the pot. A 3.5-second growth deadline
starts the final pause if rendering is delayed; the main window does not depend
on a rendered animation completion event. With animations disabled, only the
one-second still frame is shown.

The optional leaf-rustling recording accompanies startup. The owner confirmed
permission to redistribute `Assets/StartupLeaves.mp3`; public packages include it.
Where present, it plays at 30% volume.
The 3.12-second clip can finish naturally during the final still pause. Skipping
or closing the intro stops it immediately. Tray launches and reduced
motion are silent. Missing files or decoder failures do not prevent startup.
Playback uses the built-in [WPF media player](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/multimedia-overview).

The editor reserves a 14-pixel gap before the scrollbar. Expanding safety settings
or resetting confirmations brings the complete explanation into view after text
wrapping is measured. Tray labels and tooltips update immediately with the selected
language; notification messages resolve their translation when displayed.

The Explorer icon, title-bar icon, SVG wrapper, and tray all derive from the
approved mascot image. The tray adds a small rotating arrow badge while syncing
and a ready, paused, or attention badge in other states. Run
`tools/New-Icon.ps1` with Windows PowerShell in STA mode to regenerate all icon
sizes directly from that image.

The executable includes 18 native icon sizes, from 16 to 256 pixels, with stronger
strokes at small sizes. Each size is rendered directly from the original image.
The set includes intermediate sizes for scaled displays, following the
[Windows icon guidance](https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-construction).
Run tools/Test-ExplorerIcons.ps1 in STA mode after publishing to check Windows
shell extraction against every native frame and create a visual comparison sheet.

- Synchronizing: the approved mascot and twelve rotating-arrow frames.
- Ready: green check badge.
- Paused: gray background and pause bars.
- Attention: amber background and exclamation badge.

Color is accompanied by a symbol and localized tooltip. The full mascot is not
shrunk into the tray, and success does not trigger an extra message window.
