[CmdletBinding()]
param(
    [string]$Branch = "develop",
    [string]$ExtPath = "./ext/Lidarr"
)

$ErrorActionPreference = "Stop"

function Assert-Command {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Display
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Display is required but was not found in PATH."
    }
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptRoot

try {
    Assert-Command -Name "git" -Display "git"

    if ($Branch -notmatch '^[\w./-]+$') {
        throw "Branch contains invalid characters."
    }

    if ([System.IO.Path]::IsPathRooted($ExtPath)) {
        throw "ExtPath must be a path relative to the repository root."
    }

    $extFullPath = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot $ExtPath))
    $repoRoot = [System.IO.Path]::GetFullPath($scriptRoot)
    $rootPrefix = if ($repoRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) { $repoRoot } else { $repoRoot + [System.IO.Path]::DirectorySeparatorChar }
    if (-not $extFullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "ExtPath must stay inside the repository."
    }

    $extParent = Split-Path -Parent $extFullPath
    if (-not (Test-Path $extParent)) {
        New-Item -ItemType Directory -Path $extParent -Force | Out-Null
    }

    if (-not (Test-Path (Join-Path $extFullPath "src/NzbDrone.Core/Lidarr.Core.csproj"))) {
        Write-Host "Cloning Lidarr repository (branch: $Branch)..."
        git clone --branch $Branch --depth 1 https://github.com/Lidarr/Lidarr.git $extFullPath
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to clone Lidarr repository."
        }
    }
    else {
        Write-Host "Lidarr checkout already present at $extFullPath"
    }

    $coreProject = Join-Path $extFullPath "src/NzbDrone.Core/Lidarr.Core.csproj"
    if (-not (Test-Path $coreProject)) {
        throw "Lidarr.Core.csproj was not found after clone. Expected $coreProject"
    }

    Write-Host "Lidarr is ready at $extFullPath"
    Write-Host "Next: .\\build.ps1"
}
finally {
    Pop-Location
}
