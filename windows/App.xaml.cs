using System.Windows;

namespace CinnamonDynamicWallpaper.Windows;

public partial class App : System.Windows.Application
{
    public bool StartInBackground { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        StartInBackground = e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        base.OnStartup(e);
    }
}