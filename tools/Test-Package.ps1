param(
    [string]$Archive,
    [switch]$NormalStart
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $Archive) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/AntonsBackupManager.App/AntonsBackupManager.App.csproj')
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw 'The application project has no valid package version.' }
    $releaseVersion = $version -replace '^(\d+\.\d+)\.0(-)', '$1$2'
    $Archive = "artifacts/Kaktus-Backup-Sync-Portable-$releaseVersion.zip"
}
if (-not [IO.Path]::IsPathRooted($Archive)) { $Archive = Join-Path $projectRoot $Archive }
if (Get-Process -Name KaktusBackupSync,AntonsBackupManager.App -ErrorAction SilentlyContinue) {
    throw 'Close the application before testing an isolated package.'
}
$archivePath = (Resolve-Path -LiteralPath $Archive).Path
$checksumPath = $archivePath + '.sha256'
if (-not (Test-Path -LiteralPath $checksumPath)) { throw 'Package SHA-256 sidecar is missing.' }
$checksumLine = [IO.File]::ReadAllText($checksumPath).Trim()
if ($checksumLine -notmatch '^([a-fA-F0-9]{64})\s+(.+)$' -or $Matches[2] -cne [IO.Path]::GetFileName($archivePath)) {
    throw 'Package SHA-256 sidecar has an invalid format or filename.'
}
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ine $Matches[1]) { throw 'Package SHA-256 does not match the archive.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $entries = @($zip.Entries | Where-Object { $_.Name })
    $unexpected = @($entries | Where-Object {
        $_.FullName -match '(?i)(^|/)(runtime|\.git|reports)(/|$)|MASTER_INSTRUCTIONS|WORK_STATUS|PRIVATE_|AGENTS\.md|\.pdb$' -or
        $_.FullName -match '(^/|\\|(^|/)\.\.(/|$)|:)'
    })
    if ($unexpected.Count -gt 0) { throw 'Package contains private state, development files or unsafe paths.' }
    $roots = @($entries | ForEach-Object { $_.FullName.Split('/')[0] } | Select-Object -Unique)
    if ($roots.Count -ne 1) { throw 'Expected one top-level application folder.' }
    foreach ($required in @('portable.flag','START-HERE.txt','KaktusBackupSync.exe','KaktusBackupSync.runtimeconfig.json',
        'coreclr.dll','hostfxr.dll','hostpolicy.dll','PresentationFramework.dll','ASSET-NOTICES.md',
        'LICENSE','RIGHTS.md','THIRD_PARTY_NOTICES.md','LICENSE.txt','ThirdPartyNotices.txt',
        'Assets/StartupLeaves.mp3','images/manual/de-main.png','images/manual/en-main.png','images/manual/ru-main.png','images/manual/startup.png')) {
        if (@($entries | Where-Object { $_.FullName -eq "$($roots[0])/$required" }).Count -ne 1) {
            throw "Portable package is incomplete: $required"
        }
    }
} finally { $zip.Dispose() }

$smokeParent = [IO.Path]::GetFullPath((Join-Path $env:TEMP 'AntonsBackupPackageCheck'))
$smokeRoot = [IO.Path]::GetFullPath((Join-Path $smokeParent ([Guid]::NewGuid().ToString('N'))))
if (-not $smokeRoot.StartsWith($smokeParent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Invalid smoke directory.'
}
$safePrefix = $smokeRoot + [IO.Path]::DirectorySeparatorChar

function Assert-TestPath([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($safePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Test operation escaped its generated directory.'
    }
}

function Test-Launch([string]$AppFolder, [bool]$ShowMainWindow, [string]$ExpectedLanguage) {
    Assert-TestPath $AppFolder
    $executable = Join-Path $AppFolder 'KaktusBackupSync.exe'
    $runtime = Join-Path $AppFolder 'runtime'
    $process = $null
    try {
        $options = [Diagnostics.ProcessStartInfo]::new($executable)
        # Deliberately launch from another directory, without development tools on PATH.
        $options.WorkingDirectory = $smokeRoot
        $options.UseShellExecute = $false
        $options.CreateNoWindow = $true
        $options.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        if (-not $ShowMainWindow) { $options.Arguments = '--tray' }
        $options.EnvironmentVariables['PATH'] = "$env:SystemRoot\System32;$env:SystemRoot"
        $options.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $smokeRoot 'no-installed-runtime'
        $options.EnvironmentVariables['DOTNET_ROOT_X64'] = $options.EnvironmentVariables['DOTNET_ROOT']
        $options.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
        $process = [Diagnostics.Process]::Start($options)
        $deadline = [DateTime]::UtcNow.AddSeconds(25)
        do {
            Start-Sleep -Milliseconds 250
            $process.Refresh()
            if ($process.HasExited) { throw 'Packaged application exited during startup.' }
            $languageFile = Join-Path $runtime 'language.txt'
        } until ((Test-Path -LiteralPath $languageFile) -or [DateTime]::UtcNow -gt $deadline)
        if (-not (Test-Path -LiteralPath $languageFile)) { throw 'Portable settings were not created beside the EXE.' }
        if ([IO.File]::ReadAllText($languageFile).Trim() -ne $ExpectedLanguage) { throw 'Portable language was not retained.' }
        Start-Sleep -Seconds 2
        $process.Refresh()
        if ($ShowMainWindow) {
            $deadline = [DateTime]::UtcNow.AddSeconds(10)
            while (-not $process.HasExited -and $process.MainWindowTitle -notlike 'Kaktus Backup & Sync*' -and [DateTime]::UtcNow -lt $deadline) {
                Start-Sleep -Milliseconds 250
                $process.Refresh()
            }
            if ($process.HasExited -or $process.MainWindowTitle -notlike 'Kaktus Backup & Sync*') { throw 'Main window did not appear.' }
        }
        elseif ($process.HasExited -or $process.MainWindowHandle -ne 0) { throw 'Tray startup failed or displayed a window.' }
        foreach ($moduleName in @('coreclr.dll','hostpolicy.dll','PresentationNative_cor3.dll')) {
            $module = @($process.Modules | Where-Object { $_.ModuleName -ieq $moduleName })
            if ($module.Count -ne 1 -or $module[0].FileName -ine (Join-Path $AppFolder $moduleName)) {
                throw "Runtime module did not load from the portable folder: $moduleName"
            }
        }
        "PASS portable launch (normal=$ShowMainWindow), local settings and bundled runtime."
    } finally {
        if ($process) {
            $process.Refresh()
            if (-not $process.HasExited) {
                if ($process.Path -ine $executable) { throw 'Refusing to stop a process outside this test.' }
                Stop-Process -Id $process.Id
                if (-not $process.WaitForExit(10000)) { throw 'Test process did not exit.' }
            }
            $process.Dispose()
        }
    }
}

try {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $smokeRoot
    $appFolder = Join-Path $smokeRoot $roots[0]
    Assert-TestPath $appFolder
    if (Test-Path -LiteralPath (Join-Path $appFolder 'runtime')) { throw 'Clean archive already contains user state.' }
    $config = Get-Content -LiteralPath (Join-Path $appFolder 'KaktusBackupSync.runtimeconfig.json') -Raw | ConvertFrom-Json
    if ($config.runtimeOptions.framework -or $config.runtimeOptions.frameworks -or
        @($config.runtimeOptions.includedFrameworks | Where-Object { $_.name -in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App') }).Count -ne 2) {
        throw 'Package depends on an installed runtime.'
    }
    # The portable flag must come from the archive itself, never from the test.
    Test-Launch $appFolder ([bool]$NormalStart) 'de'
    $runtime = Join-Path $appFolder 'runtime'
    [IO.File]::WriteAllText((Join-Path $runtime 'tasks.json'), '[]')
    [IO.File]::WriteAllText((Join-Path $runtime 'language.txt'), 'ru')
    [IO.File]::WriteAllText((Join-Path $runtime 'all-paused.flag'), '')
    [IO.File]::WriteAllText((Join-Path $runtime 'portable-check.txt'), 'Synthetic settings move with the application.')
    $movedFolder = Join-Path $smokeRoot 'Moved application with spaces'
    Assert-TestPath $appFolder
    Assert-TestPath $movedFolder
    Move-Item -LiteralPath $appFolder -Destination $movedFolder
    Test-Launch $movedFolder (-not [bool]$NormalStart) 'ru'
    $movedRuntime = Join-Path $movedFolder 'runtime'
    if ([IO.File]::ReadAllText((Join-Path $movedRuntime 'tasks.json')) -ne '[]' -or
        [IO.File]::ReadAllText((Join-Path $movedRuntime 'portable-check.txt')) -ne 'Synthetic settings move with the application.' -or
        -not (Test-Path -LiteralPath (Join-Path $movedRuntime 'all-paused.flag'))) { throw 'Portable state changed after relocation.' }
    if (Test-Path -LiteralPath (Join-Path $smokeRoot 'runtime')) { throw 'Application stored settings in the working directory.' }
    'PASS archive privacy, first portable launch and relocation with retained settings.'
} finally {
    # Remove only the verified GUID directory created by this invocation.
    $resolvedRoot = [IO.Path]::GetFullPath($smokeRoot)
    if ($resolvedRoot.StartsWith($smokeParent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedRoot) -match '^[a-f0-9]{32}$' -and (Test-Path -LiteralPath $resolvedRoot)) {
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
