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

    $extFullPath = if ([System.IO.Path]::IsPathRooted($ExtPath)) { $ExtPath } else { Join-Path $scriptRoot $ExtPath }
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
