using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CinnamonDynamicWallpaper.Windows.Core;
using Microsoft.Win32;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ToolTipIcon = System.Windows.Forms.ToolTipIcon;
using TrayIconImage = System.Drawing.Icon;
using WpfComboBox = System.Windows.Controls.ComboBox;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle;
using Line = System.Windows.Shapes.Line;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace CinnamonDynamicWallpaper.Windows;

public partial class MainWindow : Window
{
    private const string StartupValueName = "CinnamonDynamicWallpaper";
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmWindowCornerRound = 2;
    private const int DwmBackdropMainWindow = 2;
    private static readonly string[] TimelineColors = ["#00193D", "#05597F", "#54BABF", "#9EC3A1", "#FFB95E", "#FCAE4E", "#F37F73", "#B45BBC", "#7E38CE", "#00285F"];
    private readonly AppSettings _settings;
    private readonly WallpaperScheduler _scheduler = new();
    private readonly System.Windows.Threading.DispatcherTimer _timer = new();
    private readonly NotifyIcon _trayIcon;
    private readonly TrayIconImage _trayIconImage;
    private readonly ObservableCollection<PeriodImageItem> _periodItems = [];
    private IntPtr _windowHandle;
    private string _currentImageFolder = string.Empty;
    private bool? _isLightTheme;
    private bool _isInitializing = true;
    private bool _isUpdating;
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SourceInitialized += (_, _) =>
        {
            _windowHandle = new WindowInteropHelper(this).Handle;
            ApplyNativeWindowEffects();
        };
        ApplySystemTheme();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        Icon = BitmapFrame.Create(new Uri(iconPath));
        _trayIconImage = new TrayIconImage(iconPath);

        _settings = SettingsStore.Load();
        var imageSets = Directory.Exists(GetAssetsDirectory())
            ? Directory.GetDirectories(GetAssetsDirectory()).Select(Path.GetFileName).Where(name => name is not null).OrderBy(name => name).ToArray()
            : Array.Empty<string>();
        ImageSetCombo.ItemsSource = imageSets;

        if (ImageSetCombo.Items.Contains(_settings.SelectedImageSet))
        {
            ImageSetCombo.SelectedItem = _settings.SelectedImageSet;
        }
        else if (ImageSetCombo.Items.Count > 0)
        {
            ImageSetCombo.SelectedIndex = 0;
        }

        _currentImageFolder = !string.IsNullOrWhiteSpace(_settings.ImageFolder) && Directory.Exists(_settings.ImageFolder)
            ? _settings.ImageFolder
            : ImageSetCombo.SelectedItem is string imageSet
                ? Path.Combine(GetAssetsDirectory(), imageSet)
                : string.Empty;
        FolderSourceRadio.IsChecked = _settings.UseImageFolder;
        ImageSetCombo.IsEnabled = !_settings.UseImageFolder;

        var imageChoices = GetImageNames(_currentImageFolder);
        if (_settings.PeriodImages.Length != 10)
        {
            _settings.PeriodImages = Enumerable.Range(0, 10).Select(index => $"{index}.jpg").ToArray();
        }

        for (var index = 0; index < 10; index++)
        {
            var imageName = imageChoices.Contains(_settings.PeriodImages[index], StringComparer.OrdinalIgnoreCase)
                ? _settings.PeriodImages[index]
                : imageChoices.ElementAtOrDefault(index) ?? string.Empty;
            var startTime = _settings.CustomPeriodStarts.Length == 10
                ? _settings.CustomPeriodStarts[index]
                : new[] { "00:00", "05:00", "07:00", "09:00", "11:00", "13:00", "15:00", "17:00", "19:00", "21:00" }[index];
            var periodItem = new PeriodImageItem(index, startTime, imageName, imageChoices, _currentImageFolder);
            periodItem.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(PeriodImageItem.StartTime) && UseCustomTimesCheckBox.IsChecked == true)
                {
                    RefreshPeriodDisplays();
                }
            };
            _periodItems.Add(periodItem);
        }

        PeriodImagesControl.ItemsSource = _periodItems;
        PeriodTimesControl.ItemsSource = _periodItems;
        LatitudeTextBox.Text = _settings.Latitude.ToString(CultureInfo.CurrentCulture);
        LongitudeTextBox.Text = _settings.Longitude.ToString(CultureInfo.CurrentCulture);
        UseCustomTimesCheckBox.IsChecked = _settings.UseCustomTimes;
        PeriodTimesControl.IsEnabled = _settings.UseCustomTimes;
        SetComboSelection(PictureAspectCombo, _settings.PictureAspect);
        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        CloseToTrayCheckBox.IsChecked = _settings.CloseToTray;
        SetRefreshInterval(_settings.RefreshMinutes);
        _timer.Interval = TimeSpan.FromMinutes(_settings.RefreshMinutes);

        _trayIcon = new NotifyIcon
        {
            Icon = _trayIconImage,
            Text = "Cinnamon Dynamic Wallpaper",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _trayIcon.ContextMenuStrip.Items.Add("Open", null, (_, _) => ShowWindow());
        _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => ExitApplication());
        _trayIcon.DoubleClick += (_, _) => ShowWindow();

        _timer.Tick += (_, _) => RefreshWallpaper();
        _timer.Start();
        _isInitializing = false;
        RefreshPeriodDisplays();
        PeriodTimelineCanvas.SizeChanged += (_, _) => RenderPeriodTimeline();
        Loaded += (_, _) =>
        {
            if (System.Windows.Application.Current is App { StartInBackground: true })
            {
                if (!_settings.StartWithWindows)
                {
                    _settings.StartWithWindows = true;
                    StartWithWindowsCheckBox.IsChecked = true;
                    SettingsStore.Save(_settings);
                }

                Hide();
            }

            RefreshWallpaper();
        };
    }

    private static string GetAssetsDirectory() => Path.Combine(AppContext.BaseDirectory, "Assets");

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(ApplySystemTheme));
        }
    }

    private void ApplySystemTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var themeValue = key?.GetValue("AppsUseLightTheme") ?? key?.GetValue("SystemUsesLightTheme");
        var isLightTheme = themeValue is null || Convert.ToInt32(themeValue, CultureInfo.InvariantCulture) != 0;
        if (_isLightTheme == isLightTheme)
        {
            return;
        }

        _isLightTheme = isLightTheme;
        SetThemeBrush("SurfaceBrush", isLightTheme ? "#E6F3F4F4" : "#E6202020");
        SetThemeBrush("PanelBrush", isLightTheme ? "#E6FFFFFF" : "#D92B2B2B");
        SetThemeBrush("DeeperBrush", isLightTheme ? "#D9E7EAEC" : "#CC1C1C1C");
        SetThemeBrush("MutedBrush", isLightTheme ? "#606B70" : "#A8A8A8");
        SetThemeBrush("TextBrush", isLightTheme ? "#1D2326" : "#E6E6E6");
        SetThemeBrush("SecondaryTextBrush", isLightTheme ? "#465258" : "#C5C5C5");
        SetThemeBrush("BorderBrush", isLightTheme ? "#CBD1D4" : "#484848");
        SetThemeBrush("HoverBrush", isLightTheme ? "#E3E8EA" : "#454545");
        SetThemeBrush("SelectedBrush", isLightTheme ? "#D5E1E3" : "#3A3A3A");
        SetThemeBrush("TabHoverBrush", isLightTheme ? "#DDE8ECEE" : "#D9262626");
        SetThemeBrush("TabSelectedBrush", isLightTheme ? "#D9E3E8EA" : "#DD292929");
        SetThemeBrush("ImageBackdropBrush", isLightTheme ? "#DEE3E5" : "#1C1C1C");
        SetThemeBrush("AccentBrush", isLightTheme ? "#007F8B" : "#20B7C7");
        ApplyNativeWindowEffects();
    }

    private void ApplyNativeWindowEffects()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        var cornerPreference = DwmWindowCornerRound;
        var backdropType = DwmBackdropMainWindow;
        var useDarkTitleBar = _isLightTheme == true ? 0 : 1;
        SetDwmAttribute(_windowHandle, DwmwaWindowCornerPreference, cornerPreference);
        SetDwmAttribute(_windowHandle, DwmwaSystemBackdropType, backdropType);
        SetDwmAttribute(_windowHandle, DwmwaUseImmersiveDarkMode, useDarkTitleBar);
    }

    private static void SetDwmAttribute(IntPtr windowHandle, int attribute, int value)
    {
        _ = DwmSetWindowAttribute(windowHandle, attribute, ref value, sizeof(int));
    }

    private void SetThemeBrush(string key, string color)
    {
        Resources[key] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr windowHandle, int attribute, ref int value, int valueSize);

    private static string[] GetImageNames(string folder) => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp")
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray()
        : [];

    private void ImageSetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || FolderSourceRadio.IsChecked == true || ImageSetCombo.SelectedItem is not string imageSet)
        {
            return;
        }

        _currentImageFolder = Path.Combine(GetAssetsDirectory(), imageSet);
        RefreshImageChoices();
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a folder containing the images for your dynamic wallpaper.",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_currentImageFolder) ? _currentImageFolder : string.Empty
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            FolderSourceRadio.IsChecked = true;
            _currentImageFolder = dialog.SelectedPath;
            RefreshImageChoices();
        }
    }

    private void ImageSource_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || ImageSetCombo is null)
        {
            return;
        }

        var isFolderSource = FolderSourceRadio.IsChecked == true;
        ImageSetCombo.IsEnabled = !isFolderSource;
        BrowseFolderButton.Content = isFolderSource ? "Choose source folder" : "Browse folders";
        if (!isFolderSource && ImageSetCombo.SelectedItem is string imageSet)
        {
            _currentImageFolder = Path.Combine(GetAssetsDirectory(), imageSet);
        }
        RefreshImageChoices();
    }

    private void RefreshImageChoices()
    {
        var imageChoices = GetImageNames(_currentImageFolder);
        for (var index = 0; index < _periodItems.Count; index++)
        {
            _periodItems[index].SetImageFolder(_currentImageFolder);
            _periodItems[index].SetImageChoices(imageChoices);
            if (!imageChoices.Contains(_periodItems[index].ImageName, StringComparer.OrdinalIgnoreCase))
            {
                _periodItems[index].ImageName = imageChoices.ElementAtOrDefault(index) ?? string.Empty;
            }
        }

        RefreshPeriodDisplays();
    }

    private void SetComboSelection(WpfComboBox comboBox, string option)
    {
        comboBox.SelectedItem = comboBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), option, StringComparison.OrdinalIgnoreCase));
    }

    private void SetRefreshInterval(int minutes)
    {
        RefreshIntervalCombo.SelectedItem = RefreshIntervalCombo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => int.TryParse(item.Tag?.ToString(), out var value) && value == minutes)
            ?? RefreshIntervalCombo.Items.Cast<ComboBoxItem>().FirstOrDefault();
    }

    private void RefreshPeriodDisplays()
    {
        var starts = GetPeriodStarts();
        for (var index = 0; index < _periodItems.Count; index++)
        {
            var end = index == 9 ? new TimeOnly(23, 59) : starts[index + 1].AddMinutes(-1);
            _periodItems[index].TimeRange = $"{starts[index]:HH:mm} - {end:HH:mm}";
        }

        RenderPeriodTimeline(starts);
    }

    private TimeOnly[] GetPeriodStarts()
    {
        if (UseCustomTimesCheckBox.IsChecked == true && _periodItems.Count == 10 &&
            _periodItems.All(item => TimeOnly.TryParse(item.StartTime, out _)))
        {
            return _periodItems.Select(item => TimeOnly.Parse(item.StartTime)).ToArray();
        }

        return SolarTimes.GetPeriods(_settings.Latitude, _settings.Longitude, DateTime.Today);
    }

    private void RenderPeriodTimeline(TimeOnly[]? starts = null)
    {
        if (PeriodTimelineCanvas is null || PeriodTimelineCanvas.ActualWidth <= 0)
        {
            return;
        }

        starts ??= GetPeriodStarts();
        PeriodTimelineCanvas.Children.Clear();
        var width = PeriodTimelineCanvas.ActualWidth;
        var barTop = 13d;
        var barHeight = 42d;
        for (var index = 0; index < 10; index++)
        {
            var begin = starts[index].Hour * 60 + starts[index].Minute;
            var finish = index == 9 ? 1440 : starts[index + 1].Hour * 60 + starts[index + 1].Minute;
            var x = width * begin / 1440;
            var segmentWidth = width * (finish - begin) / 1440;
            PeriodTimelineCanvas.Children.Add(new Rectangle
            {
                Width = Math.Max(0, segmentWidth),
                Height = barHeight,
                Fill = (Brush)new BrushConverter().ConvertFromString(TimelineColors[index])!,
                Tag = begin
            });
            Canvas.SetLeft(PeriodTimelineCanvas.Children[^1], x);
            Canvas.SetTop(PeriodTimelineCanvas.Children[^1], barTop);
        }

        for (var hour = 0; hour <= 24; hour += 3)
        {
            var x = width * hour / 24;
            var themeText = (Brush)Resources["TextBrush"];
            var line = new Line { X1 = x, X2 = x, Y1 = barTop, Y2 = barTop + barHeight, Stroke = themeText, StrokeThickness = 1, Opacity = 0.75 };
            PeriodTimelineCanvas.Children.Add(line);
            var label = new TextBlock { Text = hour == 24 ? "24" : hour.ToString(CultureInfo.InvariantCulture), FontSize = 11, Foreground = themeText };
            PeriodTimelineCanvas.Children.Add(label);
            Canvas.SetLeft(label, Math.Min(x + 4, width - 18));
            Canvas.SetTop(label, barTop + barHeight - 15);
        }
    }

    private void ImageTab_Click(object sender, RoutedEventArgs e) => ShowPage(ImagePage, ImageTabButton);
    private void LocationTab_Click(object sender, RoutedEventArgs e) => ShowPage(LocationPage, LocationTabButton);
    private void BehaviourTab_Click(object sender, RoutedEventArgs e) => ShowPage(BehaviourPage, BehaviourTabButton);
    private void AboutTab_Click(object sender, RoutedEventArgs e) => ShowPage(AboutPage, AboutTabButton);

    private void ShowPage(UIElement page, ToggleButton selectedTab)
    {
        ImagePage.Visibility = Visibility.Collapsed;
        LocationPage.Visibility = Visibility.Collapsed;
        BehaviourPage.Visibility = Visibility.Collapsed;
        AboutPage.Visibility = Visibility.Collapsed;
        ImageTabButton.IsChecked = false;
        LocationTabButton.IsChecked = false;
        BehaviourTabButton.IsChecked = false;
        AboutTabButton.IsChecked = false;
        page.Visibility = Visibility.Visible;
        selectedTab.IsChecked = true;
    }

    private void UseCustomTimes_Changed(object sender, RoutedEventArgs e)
    {
        if (PeriodTimesControl is not null && UseCustomTimesCheckBox is not null)
        {
            PeriodTimesControl.IsEnabled = UseCustomTimesCheckBox.IsChecked == true;
            RefreshPeriodDisplays();
        }
    }

    private void PeriodTimelineCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RenderPeriodTimeline();

    private void OpenGithub_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/TobiZog/cinnamon-dynamic-wallpaper") { UseShellExecute = true });
    }

    private void OpenLicense_Click(object sender, RoutedEventArgs e) => OpenPackageDocument("LICENSE.txt");

    private void OpenNotices_Click(object sender, RoutedEventArgs e) => OpenPackageDocument("THIRD_PARTY_NOTICES.md");

    private static void OpenPackageDocument(string fileName)
    {
        Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, fileName)) { UseShellExecute = true });
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => ApplySettings(updateWallpaper: true);

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!ApplySettings(updateWallpaper: true))
        {
            return;
        }

        if (_settings.CloseToTray)
        {
            Hide();
        }
        else
        {
            ExitApplication();
        }
    }

    private bool ApplySettings(bool updateWallpaper)
    {
        if (!double.TryParse(LatitudeTextBox.Text, CultureInfo.CurrentCulture, out var latitude) || latitude is < -90 or > 90)
        {
            ShowPage(LocationPage, LocationTabButton);
            StatusTextBlock.Text = "Enter a latitude between -90 and 90.";
            return false;
        }

        if (!double.TryParse(LongitudeTextBox.Text, CultureInfo.CurrentCulture, out var longitude) || longitude is < -180 or > 180)
        {
            ShowPage(LocationPage, LocationTabButton);
            StatusTextBlock.Text = "Enter a longitude between -180 and 180.";
            return false;
        }

        if (UseCustomTimesCheckBox.IsChecked == true && !AreCustomTimesValid())
        {
            ShowPage(LocationPage, LocationTabButton);
            StatusTextBlock.Text = "Custom period times must start at 00:00 and increase in HH:mm format.";
            return false;
        }

        _settings.SelectedImageSet = ImageSetCombo.SelectedItem as string ?? _settings.SelectedImageSet;
        _settings.ImageFolder = _currentImageFolder;
        _settings.UseImageFolder = FolderSourceRadio.IsChecked == true;
        _settings.PeriodImages = _periodItems.Select(item => item.ImageName).ToArray();
        _settings.Latitude = latitude;
        _settings.Longitude = longitude;
        _settings.UseCustomTimes = UseCustomTimesCheckBox.IsChecked == true;
        _settings.CustomPeriodStarts = _periodItems.Select(item => item.StartTime).ToArray();
        _settings.PictureAspect = (PictureAspectCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Fill";
        _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _settings.CloseToTray = CloseToTrayCheckBox.IsChecked == true;
        _settings.RefreshMinutes = int.TryParse((RefreshIntervalCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var interval) ? interval : 1;

        try
        {
            SettingsStore.Save(_settings);
            ConfigureStartup(_settings.StartWithWindows);
            _timer.Interval = TimeSpan.FromMinutes(_settings.RefreshMinutes);
            StatusTextBlock.Text = "Settings applied.";
            if (updateWallpaper)
            {
                RefreshWallpaper();
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
        {
            StatusTextBlock.Text = $"Could not apply settings: {exception.Message}";
            return false;
        }
    }

    private bool AreCustomTimesValid()
    {
        if (_periodItems.Count != 10 || _periodItems.Any(item => !TimeOnly.TryParse(item.StartTime, out _)))
        {
            return false;
        }

        var times = _periodItems.Select(item => TimeOnly.Parse(item.StartTime)).ToArray();
        return times[0] == TimeOnly.MinValue && times.Zip(times.Skip(1), (current, next) => current < next).All(isIncreasing => isIncreasing);
    }

    private void RefreshWallpaper()
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;
        try
        {
            _scheduler.Update(_settings, status => StatusTextBlock.Text = status);
            SettingsStore.Save(_settings);
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = exception.Message;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private static void ConfigureStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            var executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("Could not locate the app executable.");
            key.SetValue(StartupValueName, $"\"{executablePath}\" --background");
        }
        else
        {
            key.DeleteValue(StartupValueName, false);
        }
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _timer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIconImage.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting && _settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _trayIcon.ShowBalloonTip(1800, "Still running", "The wallpaper will continue updating in the background.", ToolTipIcon.Info);
            return;
        }

        if (!_isExiting)
        {
            _isExiting = true;
            _timer.Stop();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIconImage.Dispose();
        }

        base.OnClosing(e);
        if (_isExiting)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnClosed(e);
    }
}

public sealed class PeriodImageItem : INotifyPropertyChanged
{
    private string _imageName;
    private string _timeRange = string.Empty;
    private string _imageFolder;
    private ImageSource? _preview;

    public PeriodImageItem(int index, string startTime, string imageName, string[] imageChoices, string imageFolder)
    {
        Index = index;
        PeriodLabel = $"Period {index + 1}";
        StartTime = startTime;
        _imageName = imageName;
        ImageChoices = imageChoices;
        _imageFolder = imageFolder;
        UpdatePreview();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public int Index { get; }
    public string PeriodLabel { get; }
    private string _startTime = string.Empty;
    public string StartTime
    {
        get => _startTime;
        set
        {
            if (_startTime == value)
            {
                return;
            }

            _startTime = value;
            OnPropertyChanged(nameof(StartTime));
        }
    }
    public string[] ImageChoices { get; private set; }

    public string ImageName
    {
        get => _imageName;
        set
        {
            if (_imageName == value)
            {
                return;
            }

            _imageName = value;
            OnPropertyChanged(nameof(ImageName));
            UpdatePreview();
        }
    }

    public string TimeRange
    {
        get => _timeRange;
        set
        {
            _timeRange = value;
            OnPropertyChanged(nameof(TimeRange));
        }
    }

    public ImageSource? Preview
    {
        get => _preview;
        private set
        {
            _preview = value;
            OnPropertyChanged(nameof(Preview));
        }
    }

    public void SetImageChoices(string[] choices)
    {
        ImageChoices = choices;
        OnPropertyChanged(nameof(ImageChoices));
    }

    public void SetImageFolder(string folder)
    {
        _imageFolder = folder;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var imagePath = Path.Combine(_imageFolder, ImageName);
        if (!File.Exists(imagePath))
        {
            Preview = null;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 420;
        bitmap.UriSource = new Uri(imagePath);
        bitmap.EndInit();
        bitmap.Freeze();
        Preview = bitmap;
    }

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}