[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'

function Assert-PackageMetadata {
    param([string] $Directory)

    foreach ($entry in Get-ChildItem -LiteralPath $Directory -Force) {
        # Unity does not import hidden entries or folders such as Documentation~.
        if ($entry.Name.StartsWith('.') -or $entry.Name.EndsWith('~') -or $entry.Extension -eq '.meta') {
            continue
        }

        if (-not (Test-Path -LiteralPath ($entry.FullName + '.meta') -PathType Leaf)) {
            throw "Missing Unity metadata: $($entry.FullName).meta. Add it to the package before publishing."
        }

        if ($entry.PSIsContainer) {
            Assert-PackageMetadata -Directory $entry.FullName
        }
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/DraasGames.Unity.Analyzers/DraasGames.Unity.Analyzers.csproj'
$packageRoot = Join-Path $repoRoot 'package'
$artifactRoot = Join-Path $repoRoot 'artifacts'
$sourceDll = Join-Path $repoRoot "src/DraasGames.Unity.Analyzers/bin/$Configuration/netstandard2.0/DraasGames.Unity.Analyzers.dll"
$packageDll = Join-Path $packageRoot 'Analyzers/DraasGames.Unity.Analyzers.dll'
$manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'package.json') -Raw | ConvertFrom-Json

if (-not $SkipBuild) {
    & dotnet build $project --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Analyzer build failed ($LASTEXITCODE)." }
}
if (-not (Test-Path -LiteralPath $sourceDll -PathType Leaf)) {
    throw "Built analyzer DLL not found: $sourceDll"
}

# The checked-in DLL makes the package usable through a Git URL without consumer build steps.
Copy-Item -LiteralPath $sourceDll -Destination $packageDll -Force
Assert-PackageMetadata -Directory $packageRoot
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$archive = Join-Path $artifactRoot "$($manifest.name)-$($manifest.version).tgz"

# Standard UPM tarball, with package/ at its root. No npm installation is required.
& tar -czf $archive -C $repoRoot package
if ($LASTEXITCODE -ne 0) { throw "UPM tarball creation failed ($LASTEXITCODE)." }
$archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
$dllHash = (Get-FileHash -LiteralPath $packageDll -Algorithm SHA256).Hash
Write-Output "UPM archive: $archive"
Write-Output "Archive SHA256: $archiveHash"
Write-Output "Analyzer SHA256: $dllHash"
