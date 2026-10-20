using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aion2Meter.Services;
using Aion2Meter.ViewModels;

namespace Aion2Meter;

public partial class MainWindow : Window
{
    private const double DefaultCompactWidth = 560;
    private const double MinimumCompactWidth = 340;
    private static readonly double[] OpacityLevels = [1.0, 0.72, 0.46];
    private readonly MeterViewModel _viewModel;
    private readonly MeterSettingsStore _settingsStore = new();
    private readonly DispatcherTimer _settingsSaveTimer;
    private readonly bool _openToolsOnLoad;
    private readonly bool _openPetsOnLoad;
    private readonly bool _openPetLocationOnLoad;
    private readonly bool _openPartyOnLoad;
    private readonly bool _openTimersOnLoad;
    private int _opacityLevelIndex;
    private double _windowOpacity = 1;
    private double _textOpacity = 1;
    private bool _isCompact;
    private bool _settingsReady;
    private bool _isApplyingSize;
    private int _sizeApplyVersion;
    private bool _heightUpdateQueued;
    private int _lastCompactVisibleRows = 1;
    private int _lastExpandedVisibleRows = 1;
    private bool _hasCustomCompactSize;
    private bool _hasCustomExpandedSize;
    private double _compactWidth = DefaultCompactWidth;
    private double _compactHeight = 218;
    private double _compactPreferredHeight = 218;
    private double _expandedWidth = 600;
    private double _expandedHeight = 476;
    private double _expandedPreferredHeight = 476;
    private ToolsWindow? _toolsWindow;
    private PetsWindow? _petsWindow;
    private PartyCheckWindow? _partyCheckWindow;
    private TimersWindow? _timersWindow;
    private readonly Dictionary<int, PlayerBreakdownWindow> _breakdownWindows = new();
    private GameWindowAttachment? _gameWindowAttachment;
    private TrayIconService? _trayIcon;

    public MainWindow()
    {
        MeterSettings settings = _settingsStore.Load();
        UiLanguageService.Apply(settings.Language);
        _viewModel = new MeterViewModel();
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.Players.CollectionChanged += Players_CollectionChanged;
        _viewModel.TargetDebuffs.CollectionChanged += DynamicItems_CollectionChanged;
        _viewModel.EventTimers.CollectionChanged += DynamicItems_CollectionChanged;
        _viewModel.Pets.CollectionChanged += DynamicItems_CollectionChanged;
        _viewModel.PartyMembers.CollectionChanged += DynamicItems_CollectionChanged;
        _viewModel.RestorePreferences(
            settings.BossOnly,
            settings.ShowPartyMembers,
            settings.ShowTargetDebuffs,
            settings.DisplayMode,
            settings.PvpMode,
            settings.KeepNotificationsWhenHidden);

        _settingsSaveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _settingsSaveTimer.Tick += (_, _) =>
        {
            _settingsSaveTimer.Stop();
            SaveSettings();
        };

        _opacityLevelIndex = Math.Clamp(settings.OpacityLevel, 0, OpacityLevels.Length - 1);
        Opacity = 1;
        _windowOpacity = Math.Clamp(settings.WindowOpacity, 0.25, 1);
        _textOpacity = Math.Clamp(settings.TextOpacity, 0.25, 1);
        UpdateOpacityIcons();
        _compactWidth = Clamp(settings.CompactWidth, MinimumCompactWidth, 1800, DefaultCompactWidth);
        _compactHeight = Clamp(settings.CompactHeight, 180, MaxHeight, 218);
        _compactPreferredHeight = _compactHeight;
        _expandedWidth = Clamp(settings.ExpandedWidth, 520, 1800, 600);
        _expandedHeight = Clamp(settings.ExpandedHeight, 430, MaxHeight, 476);
        _expandedPreferredHeight = _expandedHeight;
        _hasCustomCompactSize = settings.HasCustomCompactSize;
        _hasCustomExpandedSize = settings.HasCustomExpandedSize;

        _openToolsOnLoad = Array.Exists(Environment.GetCommandLineArgs(), argument =>
            argument.Equals("--tools", StringComparison.OrdinalIgnoreCase));
        _openPetsOnLoad = Array.Exists(Environment.GetCommandLineArgs(), argument =>
            argument.Equals("--pets", StringComparison.OrdinalIgnoreCase));
        _openPetLocationOnLoad = Array.Exists(Environment.GetCommandLineArgs(), argument =>
            argument.Equals("--pet-location", StringComparison.OrdinalIgnoreCase));
        _openPartyOnLoad = Array.Exists(Environment.GetCommandLineArgs(), argument =>
            argument.Equals("--party", StringComparison.OrdinalIgnoreCase));
        _openTimersOnLoad = Array.Exists(Environment.GetCommandLineArgs(), argument =>
            argument.Equals("--timers", StringComparison.OrdinalIgnoreCase));

        bool forceExpanded = Array.Exists(Environment.GetCommandLineArgs(), argument =>
            argument.Equals("--expanded", StringComparison.OrdinalIgnoreCase));
        if (!forceExpanded && settings.IsCompact)
        {
            EnterCompactMode(captureCurrentSize: false);
        }
        else
        {
            ApplyWindowSize(_expandedWidth, _expandedHeight);
        }

        RestoreWindowPosition(settings.Left, settings.Top);
        _settingsReady = true;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _trayIcon = new TrayIconService(
            () => Dispatcher.Invoke(ShowFromTray),
            () => Dispatcher.Invoke(HideToTray),
            () => Dispatcher.InvokeAsync(async () => await CheckForUpdatesAsync(showCurrentStatus: true)),
            () => Dispatcher.Invoke(ExitApplication));
        _gameWindowAttachment = new GameWindowAttachment(this);
        _gameWindowAttachment.Start();
        _viewModel.SetOverlayVisible(IsVisible && WindowState != WindowState.Minimized);
        ApplyWindowFillOpacityToOpenWindows();
        ApplyTextOpacityToOpenWindows();
        await _viewModel.InitializeAsync();
        RefreshNotificationPopupPlacement();
        RefreshDebuffPopupPlacement();
        QueueCompactTargetMarquee();
        if (_openToolsOnLoad)
        {
            OpenTools();
        }
        if (_openPetsOnLoad)
        {
            OpenPets();
        }
        if (_openPetLocationOnLoad)
        {
            OpenPets();
            if ((_viewModel.Pets.FirstOrDefault(pet => pet.PetId == 1003) ?? _viewModel.Pets.FirstOrDefault()) is { } pet)
            {
                _petsWindow?.OpenLocations(pet);
            }
        }
        if (_openPartyOnLoad)
        {
            OpenPartyCheck();
        }
        if (_openTimersOnLoad)
        {
            OpenTimers();
        }
        await CheckForUpdatesAsync(showCurrentStatus: false);
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        RefreshNotificationPopupPlacement();
        RefreshDebuffPopupPlacement();
        QueueSettingsSave();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshNotificationPopupPlacement();
        RefreshDebuffPopupPlacement();
        if (!_settingsReady || _isApplyingSize || WindowState != WindowState.Normal)
        {
            return;
        }

        if (_isCompact)
        {
            _compactWidth = ActualWidth;
            _compactHeight = ActualHeight;
            _compactPreferredHeight = ActualHeight;
            _hasCustomCompactSize = true;
        }
        else
        {
            _expandedWidth = ActualWidth;
            _expandedHeight = ActualHeight;
            _expandedPreferredHeight = ActualHeight;
            _hasCustomExpandedSize = true;
        }
        QueueSettingsSave();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        _viewModel.SetOverlayVisible(IsVisible && WindowState != WindowState.Minimized);
        RefreshNotificationPopupPlacement();
        RefreshDebuffPopupPlacement();
    }

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        _viewModel.SetOverlayVisible(IsVisible && WindowState != WindowState.Minimized);

    private void NotificationPopup_Opened(object? sender, EventArgs e) =>
        RefreshNotificationPopupPlacement();

    private void DebuffPopup_Opened(object? sender, EventArgs e)
    {
        RefreshDebuffPopupPlacement();
        if (DebuffPopup.Child is { } child) ApplyTextOpacity(child);
    }

    private void RefreshDebuffPopupPlacement()
    {
        if (!IsLoaded || !DebuffPopup.IsOpen || WindowState == WindowState.Minimized)
            return;

        double middle = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth / 2;
        bool meterOnLeft = Left + ActualWidth / 2 < middle;
        DebuffPopup.Placement = meterOnLeft ? PlacementMode.Right : PlacementMode.Left;
        double offset = meterOnLeft ? 6 : -6;
        DebuffPopup.HorizontalOffset = offset + 0.01;
        DebuffPopup.HorizontalOffset = offset;
        FrameworkElement anchor = _isCompact ? CompactTargetHpAnchor : DetailedTargetHpAnchor;
        if (anchor.IsVisible)
            DebuffPopup.VerticalOffset = anchor.TranslatePoint(new Point(0, 0), RootFrame).Y;
    }

    private void RefreshNotificationPopupPlacement()
    {
        if (!IsLoaded || !NotificationPopup.IsOpen || WindowState == WindowState.Minimized)
        {
            return;
        }

        // WPF Popup lives in its own HWND and otherwise remains at its previous
        // screen coordinates while the transparent meter window is dragged.
        // Nudging the placement offset forces it to follow the target immediately.
        double offset = NotificationPopup.HorizontalOffset;
        NotificationPopup.HorizontalOffset = offset + 0.01;
        NotificationPopup.HorizontalOffset = offset;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _settingsSaveTimer.Stop();
        SaveSettings();
        _toolsWindow?.Close();
        _petsWindow?.Close();
        _partyCheckWindow?.Close();
        _timersWindow?.Close();
        foreach (PlayerBreakdownWindow window in _breakdownWindows.Values.ToArray())
        {
            window.Close();
        }
        _breakdownWindows.Clear();
        _gameWindowAttachment?.Dispose();
        _trayIcon?.Dispose();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.Players.CollectionChanged -= Players_CollectionChanged;
        _viewModel.TargetDebuffs.CollectionChanged -= DynamicItems_CollectionChanged;
        _viewModel.EventTimers.CollectionChanged -= DynamicItems_CollectionChanged;
        _viewModel.Pets.CollectionChanged -= DynamicItems_CollectionChanged;
        _viewModel.PartyMembers.CollectionChanged -= DynamicItems_CollectionChanged;
        _viewModel.Dispose();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MeterViewModel.TargetName))
        {
            QueueCompactTargetMarquee();
        }
        else if (e.PropertyName == nameof(MeterViewModel.ShowPartyMembers) ||
                 e.PropertyName == nameof(MeterViewModel.DisplayMode))
        {
            QueueMeterHeightUpdate();
            QueueSettingsSave();
        }
        else if (e.PropertyName == nameof(MeterViewModel.ShowTargetDebuffs) ||
                 e.PropertyName == nameof(MeterViewModel.IsPvpMode) ||
                 e.PropertyName == nameof(MeterViewModel.KeepNotificationsWhenHidden))
        {
            QueueSettingsSave();
        }
        else if (e.PropertyName == nameof(MeterViewModel.BossOnly))
        {
            QueueSettingsSave();
        }
    }

    private void Players_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueMeterHeightUpdate();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ApplyTextOpacityToOpenWindows);
    }

    private void QueueMeterHeightUpdate()
    {
        if (_heightUpdateQueued)
        {
            return;
        }

        _heightUpdateQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _heightUpdateQueued = false;
            UpdateMeterHeight();
        }, DispatcherPriority.Loaded);
    }

    private void DynamicItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ApplyTextOpacityToOpenWindows);

    private void CompactTargetMarquee_SizeChanged(object sender, SizeChangedEventArgs e) =>
        QueueCompactTargetMarquee();

    private void QueueCompactTargetMarquee()
    {
        if (!IsLoaded)
        {
            return;
        }

        Dispatcher.BeginInvoke(RestartCompactTargetMarquee, DispatcherPriority.Loaded);
    }

    private void RestartCompactTargetMarquee()
    {
        CompactTargetNameTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        CompactTargetNameText.Measure(new Size(double.PositiveInfinity, CompactTargetMarquee.ActualHeight));

        double overflow = CompactTargetNameText.DesiredSize.Width - CompactTargetMarquee.ActualWidth;
        if (overflow <= 1)
        {
            CompactTargetNameTranslate.X = 0;
            return;
        }

        var animation = new DoubleAnimation
        {
            From = overflow / 2,
            To = -overflow / 2,
            BeginTime = TimeSpan.FromSeconds(1),
            Duration = TimeSpan.FromSeconds(Math.Max(2.5, overflow / 28)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        CompactTargetNameTranslate.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void DragRegion_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OpenTools_Click(object sender, RoutedEventArgs e) => OpenTools();

    private void OpenPartyCheck_Click(object sender, RoutedEventArgs e) => OpenPartyCheck();

    private void PlayerRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PlayerRowViewModel player })
        {
            return;
        }

        if (_breakdownWindows.TryGetValue(player.PlayerId, out PlayerBreakdownWindow? existing) && existing.IsLoaded)
        {
            existing.Activate();
            e.Handled = true;
            return;
        }

        var window = new PlayerBreakdownWindow(player)
        {
            Owner = this,
            Topmost = Topmost,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _breakdownWindows[player.PlayerId] = window;
        window.Closed += (_, _) => _breakdownWindows.Remove(player.PlayerId);
        window.Show();
        ApplyWindowFillOpacity(window);
        ApplyTextOpacity(window);
        e.Handled = true;
    }

    private void OpenTools()
    {
        if (_toolsWindow is { IsLoaded: true })
        {
            if (_toolsWindow.WindowState == WindowState.Minimized)
            {
                _toolsWindow.WindowState = WindowState.Normal;
            }

            _toolsWindow.Activate();
            return;
        }

        _toolsWindow = new ToolsWindow(_viewModel)
        {
            Owner = this,
            Topmost = Topmost,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _toolsWindow.Closed += (_, _) => _toolsWindow = null;
        _toolsWindow.Show();
        ApplyWindowFillOpacity(_toolsWindow);
        ApplyTextOpacity(_toolsWindow);
    }

    internal void OpenPets()
    {
        if (_petsWindow is { IsLoaded: true })
        {
            _petsWindow.Activate();
            return;
        }

        _petsWindow = new PetsWindow(_viewModel)
        {
            Owner = this,
            Topmost = Topmost,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _petsWindow.Closed += (_, _) => _petsWindow = null;
        _petsWindow.Show();
        ApplyWindowFillOpacity(_petsWindow);
        ApplyTextOpacity(_petsWindow);
    }

    internal void OpenPartyCheck()
    {
        if (_partyCheckWindow is { IsLoaded: true })
        {
            _partyCheckWindow.Activate();
            return;
        }

        _partyCheckWindow = new PartyCheckWindow(_viewModel)
        {
            Owner = this,
            Topmost = Topmost,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _partyCheckWindow.Closed += (_, _) => _partyCheckWindow = null;
        _partyCheckWindow.Show();
        ApplyWindowFillOpacity(_partyCheckWindow);
        ApplyTextOpacity(_partyCheckWindow);
    }

    internal void OpenTimers()
    {
        if (_timersWindow is { IsLoaded: true })
        {
            _timersWindow.Activate();
            return;
        }

        _timersWindow = new TimersWindow(_viewModel)
        {
            Owner = this,
            Topmost = Topmost,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _timersWindow.Closed += (_, _) => _timersWindow = null;
        _timersWindow.Show();
        ApplyWindowFillOpacity(_timersWindow);
        ApplyTextOpacity(_timersWindow);
    }

    internal void SetLanguage(string language)
    {
        UiLanguageService.Apply(language);
        _viewModel.RefreshLanguage();
        UpdateOpacityIcons();
        QueueSettingsSave();
    }

    internal async Task<string> CheckForUpdatesAsync(bool showCurrentStatus)
    {
        UpdateCheckResult result = await UpdateCheckService.CheckAsync();
        if (!result.Succeeded)
        {
            return UiLanguageService.Text("UpdateFailed", result.Error ?? "Unknown error");
        }

        if (result.IsUpdateAvailable && result.LatestVersion is not null)
        {
            _trayIcon?.ShowUpdate(result.LatestVersion, result.ReleaseUrl);
            return UiLanguageService.Text("UpdateAvailable", result.LatestVersion);
        }

        return showCurrentStatus
            ? UiLanguageService.Text("UpdateCurrent", result.CurrentVersion)
            : string.Empty;
    }

    private void HideToTray() => _gameWindowAttachment?.HideToTray();

    private void ShowFromTray()
    {
        if (_gameWindowAttachment is not null)
        {
            _gameWindowAttachment.ShowFromTray();
            return;
        }

        Show();
        Activate();
    }

    private void ExitApplication()
    {
        Close();
    }

    private void CycleOpacityButton_Click(object sender, RoutedEventArgs e)
    {
        CycleOpacity();

        if (sender is FrameworkElement element)
        {
            element.ToolTip = UiLanguageService.Pick(
                $"Opacity: level {_opacityLevelIndex + 1} of 3",
                $"Прозрачность: уровень {_opacityLevelIndex + 1} из 3");
        }
    }

    internal void CycleOpacity()
    {
        _opacityLevelIndex = (_opacityLevelIndex + 1) % OpacityLevels.Length;
        _windowOpacity = OpacityLevels[_opacityLevelIndex];
        UpdateOpacityIcons();
        ApplyWindowFillOpacityToOpenWindows();
        _toolsWindow?.SyncOpacitySliders();
        QueueSettingsSave();
    }

    internal double WindowOpacityPercent => _windowOpacity * 100;
    internal double TextOpacityPercent => _textOpacity * 100;

    internal void SetCustomOpacity(double windowPercent, double textPercent)
    {
        _windowOpacity = Math.Clamp(windowPercent / 100d, 0.25, 1);
        _textOpacity = Math.Clamp(textPercent / 100d, 0.25, 1);
        _opacityLevelIndex = Enumerable.Range(0, OpacityLevels.Length)
            .OrderBy(index => Math.Abs(OpacityLevels[index] - _windowOpacity))
            .First();
        UpdateOpacityIcons();
        ApplyWindowFillOpacityToOpenWindows();
        ApplyTextOpacityToOpenWindows();
        QueueSettingsSave();
    }

    private void ApplyWindowFillOpacityToOpenWindows()
    {
        ApplyWindowFillOpacity(this);
        if (_toolsWindow is not null) ApplyWindowFillOpacity(_toolsWindow);
        if (_petsWindow is not null) ApplyWindowFillOpacity(_petsWindow);
        if (_partyCheckWindow is not null) ApplyWindowFillOpacity(_partyCheckWindow);
        if (_timersWindow is not null) ApplyWindowFillOpacity(_timersWindow);
        foreach (PlayerBreakdownWindow window in _breakdownWindows.Values) ApplyWindowFillOpacity(window);
    }

    private void ApplyWindowFillOpacity(Window window)
    {
        window.Opacity = 1;
        foreach (string key in new[] { "PanelBackground", "PanelHeader", "DarkStrip" })
        {
            if (window.Resources[key] is Brush brush)
            {
                brush.Opacity = _windowOpacity;
            }
        }

        // A few auxiliary windows use a literal root fill rather than a named
        // resource. Its background fades independently while text stays crisp.
        if (!ReferenceEquals(window, this) && VisualTreeHelper.GetChildrenCount(window) > 0 &&
            VisualTreeHelper.GetChild(window, 0) is System.Windows.Controls.Border root &&
            root.Background is SolidColorBrush solid)
        {
            SolidColorBrush copy = solid.Clone();
            copy.Opacity = _windowOpacity;
            root.Background = copy;
        }
    }

    private void ApplyTextOpacityToOpenWindows()
    {
        ApplyTextOpacity(this);
        if (DebuffPopup.Child is { } debuffs) ApplyTextOpacity(debuffs);
        if (_toolsWindow is not null) ApplyTextOpacity(_toolsWindow);
        if (_petsWindow is not null) ApplyTextOpacity(_petsWindow);
        if (_partyCheckWindow is not null) ApplyTextOpacity(_partyCheckWindow);
        if (_timersWindow is not null) ApplyTextOpacity(_timersWindow);
        foreach (PlayerBreakdownWindow window in _breakdownWindows.Values) ApplyTextOpacity(window);
    }

    private void ApplyTextOpacity(DependencyObject root)
    {
        if (root is System.Windows.Controls.TextBlock text)
        {
            text.Opacity = _textOpacity;
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            ApplyTextOpacity(VisualTreeHelper.GetChild(root, index));
        }
    }

    private void ToggleCompactButton_Click(object sender, RoutedEventArgs e)
        => ToggleCompactMode();

    internal void ToggleCompactMode()
    {
        if (!_isCompact)
        {
            EnterCompactMode();
            return;
        }

        ExitCompactMode();
    }

    internal void CopySummaryToClipboard()
    {
        var lines = new List<string>
        {
            $"{_viewModel.TargetName} · {_viewModel.BattleTime}",
            $"{_viewModel.SummaryDamageLabel}: {_viewModel.CompactDamageText} · {_viewModel.SummaryDpsLabel}: {_viewModel.CompactDpsText}"
        };
        lines.AddRange(_viewModel.Players.Select(player =>
            $"{player.Rank}. {player.Nickname} — {player.DpsText} · {player.DamageText} · {player.ShareText}"));
        Clipboard.SetText(string.Join(Environment.NewLine, lines));
    }

    internal void CopyScreenshotToClipboard()
    {
        int width = Math.Max(1, (int)Math.Ceiling(ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        bitmap.Freeze();
        Clipboard.SetImage(bitmap);
    }

    private void EnterCompactMode(bool captureCurrentSize = true)
    {
        if (captureCurrentSize)
        {
            _expandedWidth = ActualWidth > 0 ? ActualWidth : Width;
            _expandedHeight = ActualHeight > 0 ? ActualHeight : Height;
        }
        DetailedLayout.Visibility = Visibility.Collapsed;
        CompactLayout.Visibility = Visibility.Visible;
        MinWidth = MinimumCompactWidth;
        MinHeight = 180;
        _isCompact = true;
        ApplyWindowSize(_compactWidth, _compactHeight);
        UpdateMeterHeight(force: !_hasCustomCompactSize);
        Dispatcher.BeginInvoke(RefreshDebuffPopupPlacement, DispatcherPriority.Loaded);
        QueueCompactTargetMarquee();
        QueueSettingsSave();
    }

    private void UpdateMeterHeight(bool force = false)
    {
        int visibleRows = Math.Clamp(_viewModel.Players.Count, 1, 10);
        if (_isCompact)
        {
            bool contentChanged = visibleRows != _lastCompactVisibleRows;
            _lastCompactVisibleRows = visibleRows;
            if (force || contentChanged)
            {
                double compactRequiredHeight = 174 + visibleRows * 40;
                double desiredHeight = Math.Max(_compactPreferredHeight, compactRequiredHeight);
                if (Math.Abs(Height - desiredHeight) > 0.5)
                {
                    ApplyWindowSize(Width, desiredHeight);
                }
            }
            return;
        }

        bool expandedContentChanged = visibleRows != _lastExpandedVisibleRows;
        _lastExpandedVisibleRows = visibleRows;
        double requiredHeight = 220 + visibleRows * 51;
        double expandedDesiredHeight = Math.Min(MaxHeight, Math.Max(_expandedPreferredHeight, requiredHeight));
        if ((force || expandedContentChanged) && Math.Abs(Height - expandedDesiredHeight) > 0.5)
        {
            ApplyWindowSize(Width, expandedDesiredHeight);
        }
    }

    private void ExitCompactMode()
    {
        CompactLayout.Visibility = Visibility.Collapsed;
        DetailedLayout.Visibility = Visibility.Visible;
        MinWidth = 520;
        MinHeight = 430;
        _isCompact = false;
        ApplyWindowSize(Math.Max(520, _expandedWidth), Math.Max(430, _expandedHeight));
        UpdateMeterHeight(force: !_hasCustomExpandedSize);
        Dispatcher.BeginInvoke(RefreshDebuffPopupPlacement, DispatcherPriority.Loaded);
        QueueSettingsSave();
    }

    private void UpdateOpacityIcons()
    {
        string level = (_opacityLevelIndex + 1).ToString();
        var icon = (Brush)FindResource($"DmOpacity{level}");
        OpacityIcon.Background = icon;
        CompactOpacityIcon.Background = icon;
    }

    private void ApplyWindowSize(double width, double height)
    {
        _isApplyingSize = true;
        int version = ++_sizeApplyVersion;
        Width = width;
        Height = height;
        if (_isCompact)
        {
            _compactWidth = width;
            _compactHeight = height;
        }
        else
        {
            _expandedWidth = width;
            _expandedHeight = height;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (version == _sizeApplyVersion)
            {
                _isApplyingSize = false;
            }
        }, DispatcherPriority.Loaded);
    }

    private void RestoreWindowPosition(double left, double top)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top))
        {
            return;
        }

        double visibleLeft = SystemParameters.VirtualScreenLeft - Width + 80;
        double visibleRight = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80;
        double visibleTop = SystemParameters.VirtualScreenTop - Height + 50;
        double visibleBottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 50;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = Math.Clamp(left, visibleLeft, visibleRight);
        Top = Math.Clamp(top, visibleTop, visibleBottom);
    }

    private void QueueSettingsSave()
    {
        if (!_settingsReady)
        {
            return;
        }

        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private void SaveSettings()
    {
        if (!_settingsReady)
        {
            return;
        }

        if (WindowState == WindowState.Normal)
        {
            if (_isCompact)
            {
                _compactWidth = ActualWidth;
                _compactHeight = ActualHeight;
            }
            else
            {
                _expandedWidth = ActualWidth;
                _expandedHeight = ActualHeight;
            }
        }

        _settingsStore.Save(new MeterSettings
        {
            Language = UiLanguageService.CurrentLanguage,
            Left = RestoreBounds.Left,
            Top = RestoreBounds.Top,
            Width = ActualWidth,
            Height = ActualHeight,
            IsCompact = _isCompact,
            CompactWidth = _compactWidth,
            CompactHeight = _compactPreferredHeight,
            ExpandedWidth = _expandedWidth,
            ExpandedHeight = _expandedPreferredHeight,
            HasCustomCompactSize = _hasCustomCompactSize,
            HasCustomExpandedSize = _hasCustomExpandedSize,
            OpacityLevel = _opacityLevelIndex,
            WindowOpacity = _windowOpacity,
            TextOpacity = _textOpacity,
            BossOnly = _viewModel.BossOnly,
            ShowPartyMembers = _viewModel.ShowPartyMembers,
            ShowTargetDebuffs = _viewModel.ShowTargetDebuffs,
            PvpMode = _viewModel.IsPvpMode,
            KeepNotificationsWhenHidden = _viewModel.KeepNotificationsWhenHidden,
            DisplayMode = _viewModel.DisplayMode
        });
    }

    private static double Clamp(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e) => HideToTray();
}
