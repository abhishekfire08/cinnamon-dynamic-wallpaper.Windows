$ErrorActionPreference = "Stop"

$windowsDir = Split-Path -Parent $PSScriptRoot
$repoDir = Split-Path -Parent $windowsDir
$stagingDir = Join-Path $windowsDir "publish-installer"
$outputDir = Join-Path $PSScriptRoot "output"
$issFile = Join-Path $PSScriptRoot "CinnamonDynamicWallpaper.iss"
$projectFile = Join-Path $windowsDir "CinnamonDynamicWallpaper.Windows.csproj"
$compiler = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
$sourceZip = Join-Path $outputDir "Cinnamon-Dynamic-Wallpaper-Source-1.0.0.zip"

if (-not (Test-Path $compiler)) {
    throw "Inno Setup 6 was not found at '$compiler'. Install it for the current user, then retry."
}

dotnet publish $projectFile -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o $stagingDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item (Join-Path $repoDir "LICENSE") (Join-Path $stagingDir "LICENSE.txt") -Force
Copy-Item (Join-Path $repoDir "THIRD_PARTY_NOTICES.txt") (Join-Path $stagingDir "THIRD_PARTY_NOTICES.txt") -Force
Copy-Item (Join-Path $PSScriptRoot "Stop-App.ps1") (Join-Path $stagingDir "Stop-App.ps1") -Force

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$sourcePaths = @(
    (Join-Path $repoDir "5.4"),
    (Join-Path $repoDir "po"),
    (Join-Path $repoDir "metadata.json"),
    (Join-Path $repoDir "icon.png"),
    (Join-Path $repoDir "icon.svg"),
    (Join-Path $repoDir "LICENSE"),
    (Join-Path $repoDir "THIRD_PARTY_NOTICES.md"),
    (Join-Path $repoDir "THIRD_PARTY_NOTICES.txt"),
    (Join-Path $windowsDir "Core"),
    (Join-Path $windowsDir "app.ico"),
    (Join-Path $windowsDir "App.xaml"),
    (Join-Path $windowsDir "App.xaml.cs"),
    (Join-Path $windowsDir "CinnamonDynamicWallpaper.Windows.csproj"),
    (Join-Path $windowsDir "MainWindow.xaml"),
    (Join-Path $windowsDir "MainWindow.xaml.cs"),
    (Join-Path $windowsDir "README.md"),
    (Join-Path $PSScriptRoot "CinnamonDynamicWallpaper.iss"),
    (Join-Path $PSScriptRoot "Build-Installer.ps1"),
    (Join-Path $PSScriptRoot "Stop-App.ps1")
)
Compress-Archive -Path $sourcePaths -DestinationPath $sourceZip -CompressionLevel Optimal -Force

& $compiler $issFile "/DSourceDir=$stagingDir" "/DOutputDir=$outputDir" "/DSourceZip=$sourceZip"
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

Get-ChildItem $outputDir -Filter "*.exe" | Select-Object FullName, Length