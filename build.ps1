[CmdletBinding()]
param(
    [string]$LidarrVersion = $(if ($env:LIDARR_VERSION) { $env:LIDARR_VERSION } else { "3.1.3.4987" })
)

$ErrorActionPreference = "Stop"
if ($LidarrVersion -notmatch '^\d+(\.\d+){1,3}$') {
    throw "LidarrVersion must be a dotted numeric version such as 3.1.3.4987"
}
$props = Join-Path $PSScriptRoot "ext\Lidarr\src\Directory.Build.props"
if (-not (Test-Path $props)) {
    throw "Lidarr source was not found at ext/Lidarr. Run .\setup-lidarr.ps1 first."
}
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$content = [System.IO.File]::ReadAllText($props)
$pinned = [regex]::Replace(
    $content,
    '<AssemblyVersion>[0-9.*]+</AssemblyVersion>',
    "<AssemblyVersion>$LidarrVersion</AssemblyVersion>")
if ($pinned -eq $content -and $content -notmatch [regex]::Escape("<AssemblyVersion>$LidarrVersion</AssemblyVersion>")) {
    throw "Could not find <AssemblyVersion> in $props"
}
[System.IO.File]::WriteAllText($props, $pinned, $utf8NoBom)

$sln = Join-Path $PSScriptRoot "ReleaseMediaFilter.sln"
$msbuildProps = @(
    "/p:TreatWarningsAsErrors=false",
    "/p:EnforceCodeStyleInBuild=false",
    "/p:WarningsNotAsErrors=NU1902"
)

dotnet build $sln -c Release --no-incremental --verbosity minimal @msbuildProps
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

dotnet test $sln -c Release --no-build --verbosity minimal @msbuildProps
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
