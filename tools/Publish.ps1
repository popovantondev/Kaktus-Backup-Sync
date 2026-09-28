param(
    [string]$DotnetPath = 'dotnet',
    [string]$OutputName
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appProject = Join-Path $projectRoot 'src/AntonsBackupManager.App/AntonsBackupManager.App.csproj'
if (-not $OutputName) {
    [xml]$project = Get-Content -LiteralPath $appProject
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw 'The application project has no valid package version.' }
    $releaseVersion = $version -replace '^(\d+\.\d+)\.0(-)', '$1$2'
    $OutputName = "Kaktus-Backup-Sync-Portable-$releaseVersion"
}
if ($OutputName -notmatch '^[A-Za-z0-9._-]+$' -or $OutputName -in @('.','..')) { throw 'Use a plain output folder name.' }
$artifactRoot = Join-Path $projectRoot 'artifacts'
$publishFolder = Join-Path $artifactRoot $OutputName
Push-Location $projectRoot
try {
    if (Test-Path -LiteralPath (Join-Path $publishFolder 'runtime')) { throw 'Output contains runtime data. Choose a fresh output folder before packaging.' }
    $runningOutput = @(Get-Process KaktusBackupSync -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $publishFolder 'KaktusBackupSync.exe') })
    if ($runningOutput.Count -gt 0) { throw 'Output is running. Choose a fresh output folder.' }
    & $DotnetPath publish $appProject -c Release -r win-x64 --self-contained true -o $publishFolder -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    [IO.File]::WriteAllText((Join-Path $publishFolder 'portable.flag'), '')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/START-HERE.txt') -Destination $publishFolder
    foreach ($notice in @('LICENSE','RIGHTS.md','THIRD_PARTY_NOTICES.md')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot $notice) -Destination $publishFolder
    }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'ASSET-NOTICES.md') -Destination $publishFolder
    Copy-Item -LiteralPath (Join-Path $projectRoot 'CHANGELOG.md') -Destination $publishFolder
    foreach ($manual in @('HANDBUCH.de.md','MANUAL.en.md','MANUAL.ru.md','SOURCE-PACKAGE.md','ARCHITECTURE.md')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot "docs/$manual") -Destination $publishFolder
    }
    $manualImageSource = Join-Path $projectRoot 'docs/images'
    $manualImageTarget = Join-Path $publishFolder 'images'
    New-Item -ItemType Directory -Path $manualImageTarget -Force | Out-Null
    Get-ChildItem -LiteralPath $manualImageSource -Force | Copy-Item -Destination $manualImageTarget -Recurse -Force
    $resolvedDotnet = (Get-Command $DotnetPath -ErrorAction Stop).Source
    $runtimeRoot = Split-Path -Parent $resolvedDotnet
    foreach ($notice in @('LICENSE.txt','ThirdPartyNotices.txt')) {
        $noticePath = Join-Path $runtimeRoot $notice
        if (-not (Test-Path -LiteralPath $noticePath)) { throw "Missing runtime notice: $notice" }
        Copy-Item -LiteralPath $noticePath -Destination $publishFolder
    }
    $archive = Join-Path $artifactRoot ($OutputName + '.zip')
    Compress-Archive -Path $publishFolder -DestinationPath $archive -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumPath = $archive + '.sha256'
    [IO.File]::WriteAllText($checksumPath, "$hash  $([IO.Path]::GetFileName($archive))`n", [Text.UTF8Encoding]::new($false))
    [pscustomobject]@{ Hash = $hash; Archive = $archive; Checksum = $checksumPath }
}
finally { Pop-Location }
