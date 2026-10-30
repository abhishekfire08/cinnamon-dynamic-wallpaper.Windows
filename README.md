# Cinnamon Dynamic Wallpaper

Windows standalone port of the Cinnamon Dynamic Wallpaper project. The original Cinnamon extension remains under `5.4/`; the Windows application is a WPF/.NET 10 app under `windows/`.

## Windows app

- Ten time-of-day wallpaper periods, using manual coordinates or custom start times
- Twelve bundled wallpaper sets, plus a custom image folder
- System tray background operation and optional launch at Windows sign-in
- Windows light/dark appearance support and Mica backdrop where available
- Per-user installer with a standard uninstaller

## Build and run

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet run --project windows/CinnamonDynamicWallpaper.Windows.csproj
```

## Create the installer

Install Inno Setup 6, then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\windows\installer\Build-Installer.ps1
```

The setup executable and corresponding GPL source archive are generated in `windows/installer/output/`. The per-user install location defaults to `%LOCALAPPDATA%\Programs\Cinnamon Dynamic Wallpaper`.

## License and credits

The project is licensed under GNU GPL version 3 or (at your option) any later version. See [LICENSE](LICENSE). Wallpaper-set attribution is in [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).

Windows port and packaging: Abhishek Hembrom. Original Cinnamon project: Tobias, 2023.

## Contributing

Contributions to the Windows port are welcome. Open an issue first for larger changes, fork the repository, create a focused branch, and submit a pull request with a short description and verification steps. Build the Windows app with `dotnet build windows/CinnamonDynamicWallpaper.Windows.csproj -c Release` before submitting.

Browse [open issues](https://github.com/abhishekfire08/cinnamon-dynamic-wallpaper.Windows/issues) or [open pull requests](https://github.com/abhishekfire08/cinnamon-dynamic-wallpaper.Windows/pulls).

## Report a bug

[Create a bug report](https://github.com/abhishekfire08/cinnamon-dynamic-wallpaper.Windows/issues/new). Include your Windows version, app version, steps to reproduce, expected and actual behavior, and relevant error text. Remove personal information from logs and screenshots before posting.