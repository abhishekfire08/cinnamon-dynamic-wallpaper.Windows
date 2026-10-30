# Cinnamon Dynamic Wallpaper for Windows

This is a standalone WPF app for Windows. It reuses the image sets from `5.4/res/images/included_image_sets` and runs independently of the Cinnamon extension.

The settings window follows the Cinnamon preferences layout with Image Configuration, Location & Times, Behaviour, and About tabs. It supports manually entered coordinates, per-period image selection and previews, custom time periods, Windows wallpaper scaling, tray behavior, and start-at-sign-in.

The app follows the Windows app appearance preference and switches between dark and light palettes while it is running. On supported Windows 11 versions it also uses the native Mica backdrop and rounded window corners. Rounded controls, translucent panels, themed dropdowns, and subtle active tabs complete the Windows-style appearance.

## Build and run

Install the .NET 10 SDK, then run:

```powershell
dotnet run --project windows/CinnamonDynamicWallpaper.Windows.csproj
```

The app starts in the notification area and updates the desktop wallpaper at the configured interval. Close or minimize the window to keep it running in the tray; use the tray menu to open or exit. The settings window can enable launch at sign-in.

## Location

Enter latitude and longitude manually; the Windows app does not request or transmit location over the internet.

Settings are stored in `%APPDATA%\CinnamonDynamicWallpaper\settings.json`. A custom image folder can be selected; supported formats are JPG, PNG, and BMP. Images are selected in filename order for ten periods through the local day.

## Publish

Create a self-contained Windows x64 Release publish. The app executable is bundled, while WPF runtime files and wallpaper assets remain alongside it:

```powershell
dotnet publish windows/CinnamonDynamicWallpaper.Windows.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishDir=publish-mica\
```

The output is written to `windows/publish-mica`. Distribute the entire publish folder, including the executable, app icon, companion runtime files, and `Assets` folder containing the bundled wallpaper sets. The existing Cinnamon GTK preferences and Linux login-screen integration are not part of the Windows app.

## Installable package

Install Inno Setup 6, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\windows\installer\Build-Installer.ps1
```

The setup executable and its GPL source archive are written under `windows/installer/output`. The installer is per-user, published by Abhishek Hembrom, and defaults to `%LOCALAPPDATA%\Programs\Cinnamon Dynamic Wallpaper`. It includes every bundled image set and the GPL/attribution notices, adds a Start Menu shortcut, and registers a standard Windows uninstaller. Starting with Windows is enabled by default and can be disabled in Behaviour. A desktop shortcut is optional. Uninstall closes app instances from that install folder before removing files and its sign-in startup entry.