# Build all Windows icons from the approved mascot artwork.
# Run with Windows PowerShell -STA.
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$ErrorActionPreference = 'Stop'
$assetDirectory = Join-Path $PSScriptRoot '../src/AntonsBackupManager.App/Assets'
$mascotPath = Join-Path $assetDirectory 'KaktusMascot.png'
$mascot = [Windows.Media.Imaging.BitmapFrame]::Create([Uri]([IO.Path]::GetFullPath($mascotPath)),
    [Windows.Media.Imaging.BitmapCreateOptions]::None, [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
$sizes = @(16,20,24,28,30,32,36,40,48,60,64,72,80,96,128,160,192,256)
$traySizes = @(16,20,24,32,40,48,64)

function Save-Frames([string]$path, [int[]]$frameSizes, [scriptblock]$drawFrame) {
    $frames = @()
    foreach ($size in $frameSizes) {
        $visual = [Windows.Media.DrawingVisual]::new()
        $context = $visual.RenderOpen()
        try {
            $context.PushTransform([Windows.Media.ScaleTransform]::new($size / 256.0, $size / 256.0))
            & $drawFrame $context
            $context.Pop()
        } finally { $context.Close() }
        $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
        $bitmap.Render($visual)
        $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $memory = [IO.MemoryStream]::new()
        try { $encoder.Save($memory); $frames += ,$memory.ToArray() }
        finally { $memory.Dispose() }
    }
    $writer = [IO.BinaryWriter]::new([IO.File]::Create($path))
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frameSizes.Count)
        $offset = 6 + 16 * $frameSizes.Count
        for ($i = 0; $i -lt $frameSizes.Count; $i++) {
            $dimension = if ($frameSizes[$i] -eq 256) { 0 } else { $frameSizes[$i] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose() }
}

function Draw-Mascot([Windows.Media.DrawingContext]$context) {
    $context.DrawRoundedRectangle([Windows.Media.BrushConverter]::new().ConvertFromString('#F3F6EF'), $null,
        [Windows.Rect]::new(8,8,240,240), 56, 56)
    $context.DrawImage($mascot, [Windows.Rect]::new(0,0,256,256))
}

$appIcon = [Windows.Media.DrawingVisual]::new()
$appContext = $appIcon.RenderOpen()
try { Draw-Mascot $appContext } finally { $appContext.Close() }
$appBitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(256,256,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$appBitmap.Render($appIcon)
$appEncoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$appEncoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($appBitmap))
$appPng = Join-Path $assetDirectory 'AppIcon.png'
$stream = [IO.File]::Create($appPng)
try { $appEncoder.Save($stream) } finally { $stream.Dispose() }
Save-Frames (Join-Path $assetDirectory 'AppIcon.ico') $sizes { param($context) Draw-Mascot $context }

function Write-TrayIcon([string]$name, [string]$kind, [double]$angle) {
    $draw = {
        param($context)
        $context.DrawImage($mascot, [Windows.Rect]::new(0,0,256,256))
        $badge = [Windows.Media.Brushes]::DarkGreen
        if ($kind -eq 'Paused') { $badge = [Windows.Media.BrushConverter]::new().ConvertFromString('#66776B') }
        if ($kind -eq 'Attention') { $badge = [Windows.Media.BrushConverter]::new().ConvertFromString('#94600E') }
        $center = [Windows.Point]::new(209,209)
        $context.DrawEllipse($badge, [Windows.Media.Pen]::new([Windows.Media.Brushes]::White,7), $center,43,43)
        if ($kind -eq 'Ready') {
            $pen = [Windows.Media.Pen]::new([Windows.Media.Brushes]::White,11)
            $pen.StartLineCap = $pen.EndLineCap = [Windows.Media.PenLineCap]::Round
            $context.DrawGeometry($null,$pen,[Windows.Media.Geometry]::Parse('M188,209 L203,223 L230,193'))
        } elseif ($kind -eq 'Paused') {
            $pen = [Windows.Media.Pen]::new([Windows.Media.Brushes]::White,12)
            $pen.StartLineCap = [Windows.Media.PenLineCap]::Round
            $context.DrawLine($pen,[Windows.Point]::new(199,195),[Windows.Point]::new(199,223))
            $context.DrawLine($pen,[Windows.Point]::new(219,195),[Windows.Point]::new(219,223))
        } elseif ($kind -eq 'Attention') {
            $pen = [Windows.Media.Pen]::new([Windows.Media.Brushes]::White,11)
            $pen.StartLineCap = $pen.EndLineCap = [Windows.Media.PenLineCap]::Round
            $context.DrawLine($pen,[Windows.Point]::new(209,192),[Windows.Point]::new(209,210))
            $context.DrawEllipse([Windows.Media.Brushes]::White,$null,[Windows.Point]::new(209,222),2.8,2.8)
        } else {
            $context.PushTransform([Windows.Media.RotateTransform]::new($angle,209,209))
            $upperPen = [Windows.Media.Pen]::new([Windows.Media.Brushes]::White,6)
            $lowerPen = [Windows.Media.Pen]::new([Windows.Media.BrushConverter]::new().ConvertFromString('#9ED0A1'),6)
            foreach ($pen in @($upperPen,$lowerPen)) {
                $pen.StartLineCap = $pen.EndLineCap = [Windows.Media.PenLineCap]::Round
                $pen.LineJoin = [Windows.Media.PenLineJoin]::Round
            }
            $context.DrawGeometry($null,$upperPen,[Windows.Media.Geometry]::Parse('M191,211 C191,200 199,192 210,192 L215,192 M210,186 L217,192 L210,198'))
            $context.DrawGeometry($null,$lowerPen,[Windows.Media.Geometry]::Parse('M227,207 C227,218 219,226 208,226 L203,226 M208,232 L201,226 L208,220'))
            $context.Pop()
        }
    }.GetNewClosure()
    Save-Frames (Join-Path $assetDirectory $name) $traySizes $draw
}

Write-TrayIcon 'TrayReady.ico' 'Ready' 0
Write-TrayIcon 'TrayPaused.ico' 'Paused' 0
Write-TrayIcon 'TrayAttention.ico' 'Attention' 0
0..11 | ForEach-Object { Write-TrayIcon "TraySync$_.ico" 'Sync' ($_ * 30) }

$svg = @'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1254 1254" role="img" aria-label="Kaktus Backup &amp; Sync">
  <image href="KaktusMascot.png" width="1254" height="1254"/>
</svg>
'@
[IO.File]::WriteAllText((Join-Path $assetDirectory 'KaktusIcon.svg'), $svg.Trim() + "`n", [Text.UTF8Encoding]::new($false))
Write-Output "Generated the application icon and tray states from the approved mascot at native sizes: $($sizes -join ', ')."
