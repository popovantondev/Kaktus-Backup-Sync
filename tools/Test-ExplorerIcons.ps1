param(
    [string]$Executable,
    [string]$OutputDirectory = 'runtime/explorer-icons'
)
# Extract through the Windows shell, checking the actual executable resource.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $Executable) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/AntonsBackupManager.App/AntonsBackupManager.App.csproj')
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw 'The application project has no valid package version.' }
    $releaseVersion = $version -replace '^(\d+\.\d+)\.0(-)', '$1$2'
    $Executable = "artifacts/Kaktus-Backup-Sync-Portable-$releaseVersion/KaktusBackupSync.exe"
}
if (-not [IO.Path]::IsPathRooted($Executable)) { $Executable = Join-Path $projectRoot $Executable }
if (-not [IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $projectRoot $OutputDirectory }
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ShellIconCheck {
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern int SHDefExtractIconW(string path, int index, uint flags,
        out IntPtr large, out IntPtr small, uint size);
    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr icon);
}
'@
$exePath = (Resolve-Path -LiteralPath $Executable).Path
$assetPath = Join-Path $PSScriptRoot '../src/AntonsBackupManager.App/Assets/AppIcon.ico'
$decoder = [Windows.Media.Imaging.BitmapDecoder]::Create([Uri]([IO.Path]::GetFullPath($assetPath)),
    [Windows.Media.Imaging.BitmapCreateOptions]::None, [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
$sizes = @(16,20,24,28,30,32,36,40,48,60,64,72,80,96,128,160,192,256)
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($outputPath) | Out-Null
$sheet = [Windows.Media.DrawingVisual]::new()
$context = $sheet.RenderOpen()
try {
    $context.DrawRectangle([Windows.Media.Brushes]::White, $null, [Windows.Rect]::new(0,0,1000,620))
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $size = $sizes[$i]
        $original = @($decoder.Frames | Where-Object { $_.PixelWidth -eq $size -and $_.PixelHeight -eq $size })
        if ($original.Count -ne 1) { throw "Missing or duplicate native icon frame: ${size}px" }
        $large = [IntPtr]::Zero; $small = [IntPtr]::Zero
        try {
            $result = [ShellIconCheck]::SHDefExtractIconW($exePath,0,0,[ref]$large,[ref]$small,[uint32]$size)
            if ($result -ne 0 -or $large -eq [IntPtr]::Zero) { throw "Shell icon extraction failed at ${size}px: $result" }
            $bitmap = [Windows.Interop.Imaging]::CreateBitmapSourceFromHIcon($large, [Windows.Int32Rect]::Empty,
                [Windows.Media.Imaging.BitmapSizeOptions]::FromEmptyOptions())
            if ($bitmap.PixelWidth -ne $size -or $bitmap.PixelHeight -ne $size) { throw "Shell returned the wrong size for ${size}px." }
            $actual = [Windows.Media.Imaging.FormatConvertedBitmap]::new($bitmap, [Windows.Media.PixelFormats]::Pbgra32, $null, 0)
            $expected = [Windows.Media.Imaging.FormatConvertedBitmap]::new($original[0], [Windows.Media.PixelFormats]::Pbgra32, $null, 0)
            $actualPixels = [byte[]]::new($size * $size * 4)
            $expectedPixels = [byte[]]::new($actualPixels.Length)
            $actual.CopyPixels($actualPixels, $size * 4, 0)
            $expected.CopyPixels($expectedPixels, $size * 4, 0)
            $difference = 0L
            for ($p = 0; $p -lt $actualPixels.Length; $p++) { $difference += [Math]::Abs([int]$actualPixels[$p] - [int]$expectedPixels[$p]) }
            if ($difference / $actualPixels.Length -gt 1) { throw "Shell resampled or replaced the ${size}px frame." }
            $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
            $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
            $stream = [IO.File]::Create((Join-Path $outputPath "shell-$size.png"))
            try { $encoder.Save($stream) } finally { $stream.Dispose() }
            if ($i -lt 8) { $x = 20 + $i * 120; $y = 40 }
            elseif ($i -lt 14) { $x = 20 + ($i - 8) * 150; $y = 150 }
            else { $x = @(20,180,370,610)[$i - 14]; $y = 320 }
            $label = [Windows.Media.FormattedText]::new("${size}px", [Globalization.CultureInfo]::InvariantCulture,
                [Windows.FlowDirection]::LeftToRight, [Windows.Media.Typeface]::new('Segoe UI'), 14, [Windows.Media.Brushes]::Black, 1.0)
            $context.DrawText($label, [Windows.Point]::new($x, $y - 24))
            $context.DrawImage($bitmap, [Windows.Rect]::new($x,$y,$size,$size))
        } finally {
            if ($large -ne [IntPtr]::Zero) { [ShellIconCheck]::DestroyIcon($large) | Out-Null }
            if ($small -ne [IntPtr]::Zero) { [ShellIconCheck]::DestroyIcon($small) | Out-Null }
        }
    }
} finally { $context.Close() }
$sheetBitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(1000,620,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$sheetBitmap.Render($sheet)
$sheetEncoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$sheetEncoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($sheetBitmap))
$sheetStream = [IO.File]::Create((Join-Path $outputPath 'shell-icon-sizes.png'))
try { $sheetEncoder.Save($sheetStream) } finally { $sheetStream.Dispose() }
"PASS Windows shell extracted all 18 native icon sizes without resampling."
