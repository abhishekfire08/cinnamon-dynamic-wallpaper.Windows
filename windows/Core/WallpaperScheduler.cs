using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CinnamonDynamicWallpaper.Windows.Core;

public sealed class WallpaperScheduler
{
    private const uint SetDesktopWallpaper = 0x0014;
    private const uint UpdateIniFile = 0x0001;
    private const uint BroadcastChange = 0x0002;

    public string Update(AppSettings settings, Action<string> reportStatus)
    {
        if (!Directory.Exists(settings.ImageFolder))
        {
            throw new DirectoryNotFoundException("Choose a folder containing the wallpaper images.");
        }

        var fallbackImages = Directory.EnumerateFiles(settings.ImageFolder)
            .Where(IsSupportedImage)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (fallbackImages.Length == 0)
        {
            throw new FileNotFoundException("No supported images were found in the selected folder.");
        }

        var periods = settings.UseCustomTimes
            ? ParseCustomTimes(settings.CustomPeriodStarts)
            : SolarTimes.GetPeriods(settings.Latitude, settings.Longitude, DateTime.Today);
        var currentTime = TimeOnly.FromDateTime(DateTime.Now);
        var imageIndex = periods.Length - 1;

        for (var index = 0; index < periods.Length - 1; index++)
        {
            if (currentTime >= periods[index] && currentTime < periods[index + 1])
            {
                imageIndex = index;
                break;
            }
        }

        var imageName = settings.PeriodImages.Length == 10 ? settings.PeriodImages[imageIndex] : fallbackImages[imageIndex % fallbackImages.Length];
        var imagePath = Path.IsPathRooted(imageName) ? imageName : Path.Combine(settings.ImageFolder, imageName);
        if (!File.Exists(imagePath))
        {
            imagePath = fallbackImages[imageIndex % fallbackImages.Length];
        }

        ApplyPictureAspect(settings.PictureAspect);
        if (!SystemParametersInfo(SetDesktopWallpaper, 0, imagePath, UpdateIniFile | BroadcastChange))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not set the desktop wallpaper.");
        }

        reportStatus($"Active: {Path.GetFileName(imagePath)}  |  Manual coordinates: {settings.Latitude:F2}, {settings.Longitude:F2}");
        return imagePath;
    }

    private static bool IsSupportedImage(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp";

    private static TimeOnly[] ParseCustomTimes(string[] customStarts)
    {
        if (customStarts.Length != 10 || customStarts.Any(value => !TimeOnly.TryParse(value, out _)))
        {
            throw new InvalidDataException("Enter ten valid period start times in HH:mm format.");
        }

        var times = customStarts.Select(value => TimeOnly.Parse(value)).ToArray();
        if (times[0] != TimeOnly.MinValue || times.Zip(times.Skip(1), (current, next) => current < next).Any(isIncreasing => !isIncreasing))
        {
            throw new InvalidDataException("Custom times must start at 00:00 and be in increasing order.");
        }

        return times;
    }

    private static void ApplyPictureAspect(string aspect)
    {
        var (style, tile) = aspect switch
        {
            "Center" => ("0", "0"),
            "Stretch" => ("2", "0"),
            "Fit" => ("6", "0"),
            "Fill" => ("10", "0"),
            "Span" => ("22", "0"),
            _ => ("10", "0")
        };

        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true);
        key?.SetValue("WallpaperStyle", style);
        key?.SetValue("TileWallpaper", tile);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, string value, uint flags);
}

internal static class SolarTimes
{
    public static TimeOnly[] GetPeriods(double latitude, double longitude, DateTime date)
    {
        var civilDawn = GetSunEvent(date, latitude, longitude, true, 96);
        var sunrise = GetSunEvent(date, latitude, longitude, true, 90.833);
        var morning = GetSunEvent(date, latitude, longitude, true, 89.167);
        var sunsetLight = GetSunEvent(date, latitude, longitude, false, 89.167);
        var civilDusk = GetSunEvent(date, latitude, longitude, false, 90.833);
        var night = GetSunEvent(date, latitude, longitude, false, 96);

        if (civilDawn is null || sunrise is null || morning is null || sunsetLight is null || civilDusk is null || night is null)
        {
            return [
                new TimeOnly(0, 0), new TimeOnly(5, 0), new TimeOnly(7, 0), new TimeOnly(9, 0),
                new TimeOnly(11, 0), new TimeOnly(13, 0), new TimeOnly(15, 0), new TimeOnly(17, 0),
                new TimeOnly(19, 0), new TimeOnly(21, 0)
            ];
        }

        var lightDuration = sunsetLight.Value - morning.Value;
        if (lightDuration < TimeSpan.Zero)
        {
            lightDuration += TimeSpan.FromDays(1);
        }

        return [
            new TimeOnly(0, 0),
            TimeOnly.FromDateTime(civilDawn.Value),
            TimeOnly.FromDateTime(sunrise.Value),
            TimeOnly.FromDateTime(morning.Value),
            TimeOnly.FromDateTime(morning.Value + TimeSpan.FromTicks(lightDuration.Ticks * 3 / 8)),
            TimeOnly.FromDateTime(morning.Value + TimeSpan.FromTicks(lightDuration.Ticks * 5 / 8)),
            TimeOnly.FromDateTime(morning.Value + TimeSpan.FromTicks(lightDuration.Ticks * 7 / 8)),
            TimeOnly.FromDateTime(sunsetLight.Value),
            TimeOnly.FromDateTime(civilDusk.Value),
            TimeOnly.FromDateTime(night.Value)
        ];
    }

    private static DateTime? GetSunEvent(DateTime date, double latitude, double longitude, bool sunrise, double zenith)
    {
        const double degreesToRadians = Math.PI / 180;
        var dayOfYear = date.DayOfYear;
        var longitudeHour = longitude / 15;
        var approximateTime = dayOfYear + ((sunrise ? 6 : 18) - longitudeHour) / 24;
        var meanAnomaly = (0.9856 * approximateTime) - 3.289;
        var trueLongitude = Normalize(meanAnomaly + (1.916 * Math.Sin(degreesToRadians * meanAnomaly)) +
            (0.020 * Math.Sin(2 * degreesToRadians * meanAnomaly)) + 282.634, 360);
        var rightAscension = Normalize(Math.Atan(0.91764 * Math.Tan(degreesToRadians * trueLongitude)) / degreesToRadians, 360);
        rightAscension += (Math.Floor(trueLongitude / 90) * 90) - (Math.Floor(rightAscension / 90) * 90);
        rightAscension /= 15;

        var sinDeclination = 0.39782 * Math.Sin(degreesToRadians * trueLongitude);
        var cosDeclination = Math.Cos(Math.Asin(sinDeclination));
        var cosHourAngle = (Math.Cos(degreesToRadians * zenith) -
            (sinDeclination * Math.Sin(degreesToRadians * latitude))) /
            (cosDeclination * Math.Cos(degreesToRadians * latitude));

        if (cosHourAngle is > 1 or < -1)
        {
            return null;
        }

        var hourAngle = sunrise
            ? 360 - (Math.Acos(cosHourAngle) / degreesToRadians)
            : Math.Acos(cosHourAngle) / degreesToRadians;
        hourAngle /= 15;

        var localMeanTime = hourAngle + rightAscension - (0.06571 * approximateTime) - 6.622;
        var universalTime = Normalize(localMeanTime - longitudeHour, 24);
        var utc = DateTime.SpecifyKind(date.Date.AddHours(universalTime), DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local);
    }

    private static double Normalize(double value, double range) => (value % range + range) % range;
}