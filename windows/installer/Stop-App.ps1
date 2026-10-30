param(
    [Parameter(Mandatory = $true)]
    [string]$InstallPath
)

$ErrorActionPreference = "SilentlyContinue"
$normalizedInstallPath = [System.IO.Path]::GetFullPath($InstallPath).TrimEnd('\') + '\'

Get-Process -Name "CinnamonDynamicWallpaper.Windows" | Where-Object {
    $_.Path -and [System.IO.Path]::GetFullPath($_.Path).StartsWith(
        $normalizedInstallPath,
        [System.StringComparison]::OrdinalIgnoreCase)
} | Stop-Process -Force