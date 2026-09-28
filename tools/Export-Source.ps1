param([string]$OutputName)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputName) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/AntonsBackupManager.App/AntonsBackupManager.App.csproj')
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw 'The application project has no valid package version.' }
    $releaseVersion = $version -replace '^(\d+\.\d+)\.0(-)', '$1$2'
    $OutputName = "Kaktus-Backup-Sync-Source-$releaseVersion"
}
if ($OutputName -notmatch '^[A-Za-z0-9._-]+$' -or $OutputName -in @('.', '..')) { throw 'Use a plain output name.' }
$artifactRoot = Join-Path $projectRoot 'artifacts'
$target = Join-Path $artifactRoot $OutputName
if (Test-Path -LiteralPath $target) { throw 'Choose a new export name; existing exports are never replaced.' }
Push-Location $projectRoot
try {
    if (@(git status --porcelain).Count -ne 0) { throw 'Commit the reviewed changes before exporting.' }
    $files = @(git -c core.quotepath=false ls-files)
    if ($LASTEXITCODE -ne 0 -or $files.Count -eq 0) { throw 'Cannot read tracked source files.' }
    $excluded = '(^|/)(bin|obj|runtime|reports|artifacts|\.git)(/|$)|(^|/)(AGENTS\.md|WORK_STATUS\.md|PRIVATE_[^/]*|[^/]*MASTER_INSTRUCTIONS_PRIVATE\.md)$'
    $selected = @($files | Where-Object { $_ -notmatch $excluded })
    if (@(git ls-files --stage | Where-Object { $_ -match '^(120000|160000) ' }).Count -gt 0) { throw 'Review symlinks and submodules before export.' }
    if (($selected -join ' ').Length -gt 24000) { throw 'Source list exceeds the conservative command length limit.' }
    $archive = Join-Path $artifactRoot ($OutputName + '.zip')
    if (Test-Path -LiteralPath $archive) { throw 'Choose a new archive name.' }
    [IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
    # Read committed blobs, so cloud placeholders and local runtime data cannot leak in.
    & git archive --format=zip "--prefix=$OutputName/" "--output=$archive" HEAD -- @selected
    if ($LASTEXITCODE -ne 0) { throw 'Source archive failed.' }
    Expand-Archive -LiteralPath $archive -DestinationPath $artifactRoot
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumPath = $archive + '.sha256'
    [IO.File]::WriteAllText($checksumPath, "$hash  $([IO.Path]::GetFileName($archive))`n", [Text.UTF8Encoding]::new($false))
    [pscustomobject]@{Files=$selected.Count; Archive=$archive; Checksum=$checksumPath; SHA256=$hash}
}
finally { Pop-Location }
