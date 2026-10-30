namespace CinnamonDynamicWallpaper.Windows.Core;

public sealed class AppSettings
{
    public string SelectedImageSet { get; set; } = "lakeside";
    public string ImageFolder { get; set; } = string.Empty;
    public bool UseImageFolder { get; set; }
    public string[] PeriodImages { get; set; } = Enumerable.Range(0, 10).Select(index => $"{index}.jpg").ToArray();
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool UseCustomTimes { get; set; }
    public string[] CustomPeriodStarts { get; set; } = ["00:00", "05:00", "07:00", "09:00", "11:00", "13:00", "15:00", "17:00", "19:00", "21:00"];
    public string PictureAspect { get; set; } = "Fill";
    public bool StartWithWindows { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public int RefreshMinutes { get; set; } = 1;
}