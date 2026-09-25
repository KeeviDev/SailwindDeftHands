<#
.SYNOPSIS
Builds, packages and optionally publishes a Deft Hands release.

.DESCRIPTION
Without -Publish: sets the version in Plugin.cs, AssemblyInfo.cs and thunderstore.toml,
asking which part to bump when -Version is omitted, then builds the mod and packages the
GitHub and Thunderstore zips into bin/Publish. CHANGELOG.md must already have a section
for the version.

With -Publish: builds and packages the current version, then creates the GitHub release
and uploads the package to Thunderstore. Requires a clean working tree whose HEAD is
pushed to origin/master, and a Thunderstore token in bin/thunderstore.token or the
THUNDERSTORE_TOKEN environment variable.

.PARAMETER Version
Version to release, in major.minor.patch form. Ignored with -Publish.

.PARAMETER Publish
Publishes the current version instead of setting a new one.

.PARAMETER Yes
Skips the confirmation prompt before publishing.

.EXAMPLE
.\release.ps1
.\release.ps1 -Version 1.1.0
.\release.ps1 -Publish
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$Publish,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = $PSScriptRoot
$PluginFile = Join-Path $Root 'Plugin.cs'
$AssemblyInfoFile = Join-Path $Root 'Properties\AssemblyInfo.cs'
$TomlFile = Join-Path $Root 'Thunderstore\thunderstore.toml'
$ChangelogFile = Join-Path $Root 'CHANGELOG.md'
$ProjectFile = Join-Path $Root 'DeftHands.csproj'
$BuiltDll = Join-Path $Root 'bin\Release\DeftHands.dll'
$PublishDir = Join-Path $Root 'bin\Publish'
$TokenFile = Join-Path $Root 'bin\thunderstore.token'

$GitHubRepo = 'KeeviDev/SailwindDeftHands'
$GitHubAccount = 'KeeviDev'
$ReleaseBranch = 'master'
$ThunderstorePackage = 'Keevi-DeftHands'

$VersionPatterns = [ordered]@{
    $PluginFile       = '(PLUGIN_VERSION\s*=\s*")(\d+\.\d+\.\d+)(")'
    $AssemblyInfoFile = '(Assembly(?:File)?Version\(")(\d+\.\d+\.\d+)(\.\d+"\))'
    $TomlFile         = '(?m)(^versionNumber\s*=\s*")(\d+\.\d+\.\d+)(")'
}

function Invoke-Checked {
    param([string]$FilePath, [string[]]$Arguments)

    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $FilePath @Arguments | Out-Host
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    if ($LASTEXITCODE -ne 0) {
        throw "'$FilePath $($Arguments -join ' ')' failed with exit code $LASTEXITCODE."
    }
}

function Get-FileVersions {
    param([string]$Path)

    $text = [IO.File]::ReadAllText($Path)
    $found = [regex]::Matches($text, $VersionPatterns[$Path])
    if ($found.Count -eq 0) {
        throw "No version found in $Path."
    }
    return $found | ForEach-Object { $_.Groups[2].Value }
}

function Get-CurrentVersion {
    return @(Get-FileVersions $PluginFile)[0]
}

function Assert-VersionsInSync {
    param([string]$Expected)

    foreach ($path in $VersionPatterns.Keys) {
        foreach ($found in Get-FileVersions $path) {
            if ($found -ne $Expected) {
                throw "$path has version $found, expected $Expected. Run .\release.ps1 -Version $Expected first."
            }
        }
    }
}

function Set-VersionInFile {
    param([string]$Path, [string]$NewVersion)

    $bytes = [IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [IO.File]::ReadAllText($Path)
    $updated = [regex]::Replace($text, $VersionPatterns[$Path], '${1}' + $NewVersion + '${3}')
    [IO.File]::WriteAllText($Path, $updated, (New-Object Text.UTF8Encoding($hasBom)))
}

function Read-BumpedVersion {
    param([string]$Current)

    $parts = $Current.Split('.') | ForEach-Object { [int]$_ }
    $options = [ordered]@{
        '1' = @('major', "$($parts[0] + 1).0.0")
        '2' = @('minor', "$($parts[0]).$($parts[1] + 1).0")
        '3' = @('patch', "$($parts[0]).$($parts[1]).$($parts[2] + 1)")
    }

    Write-Host "Current version: $Current"
    foreach ($key in $options.Keys) {
        Write-Host ("  {0}) {1,-5} -> {2}" -f $key, $options[$key][0], $options[$key][1])
    }

    $choice = Read-Host 'Which part to bump? [1-3]'
    if (-not $options.Contains($choice)) {
        throw 'No version part selected.'
    }
    return $options[$choice][1]
}

<#
.SYNOPSIS
Returns the body of a version's CHANGELOG.md section, in Keep a Changelog format
("## [1.2.0] - 2026-01-31"), without its heading or the link references at the end of the file.
.OUTPUTS
The section text, or $null if the changelog has no section for the version.
#>
function Get-ChangelogSection {
    param([string]$ForVersion)

    $headingPattern = '^##\s+\[' + [regex]::Escape($ForVersion) + '\]'
    $sectionEndPattern = '^##\s|^\[[^\]]+\]:\s'

    $lines = [IO.File]::ReadAllLines($ChangelogFile)
    $start = -1
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match $headingPattern) {
            $start = $i
            break
        }
    }
    if ($start -lt 0) {
        return $null
    }

    $section = New-Object Collections.Generic.List[string]
    for ($i = $start + 1; $i -lt $lines.Length -and $lines[$i] -notmatch $sectionEndPattern; $i++) {
        $section.Add($lines[$i])
    }
    return ($section -join "`n").Trim()
}

function Get-RequiredChangelogSection {
    param([string]$ForVersion)

    $section = Get-ChangelogSection $ForVersion
    if (-not $section) {
        throw "CHANGELOG.md has no '## [$ForVersion] - YYYY-MM-DD' section. Add one first."
    }
    return $section
}

function Invoke-Build {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if (-not $msbuild) {
        throw 'MSBuild not found.'
    }

    Write-Host 'Building...'
    Invoke-Checked $msbuild @($ProjectFile, '/nologo', '/v:minimal', '/t:Rebuild', '/p:Configuration=Release')
}

function Get-Tcli {
    $command = Get-Command tcli -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $toolPath = Join-Path $env:USERPROFILE '.dotnet\tools\tcli.exe'
    if (Test-Path $toolPath) {
        return $toolPath
    }
    throw 'tcli not found. Install it with: dotnet tool install -g tcli'
}

<#
.SYNOPSIS
Packages the built mod into the GitHub and Thunderstore zips.
.OUTPUTS
A hashtable with the GitHub and Thunderstore zip paths.
#>
function New-Packages {
    param([string]$ForVersion)

    New-Item -ItemType Directory -Force $PublishDir | Out-Null

    $gitHubZip = Join-Path $PublishDir "DeftHands-$ForVersion.zip"
    Remove-Item $gitHubZip -ErrorAction SilentlyContinue
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::Open($gitHubZip, 'Create')
    try {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $BuiltDll, 'DeftHands/DeftHands.dll') | Out-Null
    }
    finally {
        $zip.Dispose()
    }

    $thunderstoreZip = Join-Path $PublishDir "$ThunderstorePackage-$ForVersion.zip"
    Remove-Item $thunderstoreZip -ErrorAction SilentlyContinue
    Invoke-Checked (Get-Tcli) @('build', '--config-path', $TomlFile, '--package-version', $ForVersion)

    return @{ GitHub = $gitHubZip; Thunderstore = $thunderstoreZip }
}

function Get-ThunderstoreToken {
    if ($env:THUNDERSTORE_TOKEN) {
        return $env:THUNDERSTORE_TOKEN
    }
    if (Test-Path $TokenFile) {
        return ([IO.File]::ReadAllText($TokenFile)).Trim()
    }
    throw "No Thunderstore token: put it in $TokenFile or the THUNDERSTORE_TOKEN environment variable."
}

function Assert-ReadyToPublish {
    param([string]$ForVersion)

    $account = (& gh api user --jq .login)
    if ($account -ne $GitHubAccount) {
        throw "gh is logged in as '$account', expected '$GitHubAccount'. Run: gh auth switch -u $GitHubAccount"
    }

    if (& git -C $Root status --porcelain) {
        throw 'The working tree has uncommitted changes. Commit and push them first.'
    }

    Invoke-Checked git @('-C', $Root, 'fetch', '-q', 'origin')
    $head = (& git -C $Root rev-parse HEAD)
    $remoteHead = (& git -C $Root rev-parse "origin/$ReleaseBranch")
    if ($head -ne $remoteHead) {
        throw "HEAD is not origin/$ReleaseBranch. Check out $ReleaseBranch and push it first."
    }

    if (& git -C $Root ls-remote --tags origin "refs/tags/v$ForVersion") {
        throw "Tag v$ForVersion already exists on origin."
    }

    Get-ThunderstoreToken | Out-Null
}

function Publish-GitHubRelease {
    param([string]$ForVersion, [string]$ZipPath, [string]$Notes)

    $notesFile = [IO.Path]::GetTempFileName()
    try {
        [IO.File]::WriteAllText($notesFile, $Notes, (New-Object Text.UTF8Encoding($false)))
        $target = (& git -C $Root rev-parse HEAD)
        Invoke-Checked gh @('release', 'create', "v$ForVersion", $ZipPath,
            '--repo', $GitHubRepo, '--target', $target,
            '--title', "Deft Hands $ForVersion", '--notes-file', $notesFile)
    }
    finally {
        Remove-Item $notesFile -ErrorAction SilentlyContinue
    }
}

function Publish-Thunderstore {
    param([string]$ZipPath)

    Invoke-Checked (Get-Tcli) @('publish', '--config-path', $TomlFile, '--file', $ZipPath, '--token', (Get-ThunderstoreToken))
}

if ($Publish) {
    $releaseVersion = Get-CurrentVersion
    Assert-VersionsInSync $releaseVersion
    $notes = Get-RequiredChangelogSection $releaseVersion
    Assert-ReadyToPublish $releaseVersion

    Invoke-Build
    $packages = New-Packages $releaseVersion

    if (-not $Yes) {
        $answer = Read-Host "Publish v$releaseVersion to GitHub ($GitHubRepo) and Thunderstore ($ThunderstorePackage)? [y/N]"
        if ($answer -notmatch '^(y|yes)$') {
            Write-Host 'Cancelled.'
            return
        }
    }

    Publish-GitHubRelease $releaseVersion $packages.GitHub $notes
    Publish-Thunderstore $packages.Thunderstore
    Write-Host "Published v$releaseVersion."
}
else {
    if (-not $Version) {
        $Version = Read-BumpedVersion (Get-CurrentVersion)
    }
    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        throw "'$Version' is not a major.minor.patch version."
    }

    Get-RequiredChangelogSection $Version | Out-Null
    foreach ($path in $VersionPatterns.Keys) {
        Set-VersionInFile $path $Version
    }

    Invoke-Build
    $packages = New-Packages $Version

    Write-Host ''
    Write-Host "Packaged v${Version}:"
    Write-Host "  $($packages.GitHub)"
    Write-Host "  $($packages.Thunderstore)"
    Write-Host "Next: review and commit the changes, push $ReleaseBranch, then run .\release.ps1 -Publish"
}
