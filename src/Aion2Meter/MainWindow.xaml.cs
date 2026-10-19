using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aion2Meter.ViewModels;

namespace Aion2Meter;

public partial class MainWindow : Window
{
    private const double DefaultCompactWidth = 560;
    private const double MinimumCompactWidth = 340;
    private const int DebuffSlotHeight = 34;
    private static readonly double[] OpacityLevels = [1.0, 0.72, 0.46];
    private readonly MeterViewModel _viewModel = new();
    private readonly bool _openToolsOnLoad;
    private readonly bool _openPetsOnLoad;
    private readonly bool _openPetLocationOnLoad;
    private readonly bool _openPartyOnLoad;
    private readonly bool _openTimersOnLoad;
    private int _opacityLevelIndex;
    private bool _isCompact;
    private double _expandedWidth = 600;
    private double _expandedHeight = 510;
    private ToolsWindow? _toolsWindow;
    private PetsWindow? _petsWindow;
    private PartyCheckWindow? _partyCheckWindow;
    private TimersWindow? _timersWindow;
    private readonly Dictionary<int, PlayerBreakdownWindow> _breakdownWindows = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.Players.CollectionChanged += Players_CollectionChanged;

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

        if (!Array.Exists(Environment.GetCommandLineArgs(), argument =>
                argument.Equals("--expanded", StringComparison.OrdinalIgnoreCase)))
        {
            EnterCompactMode();
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        RefreshNotificationPopupPlacement();
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
    }

    private void Window_LocationChanged(object? sender, EventArgs e) =>
        RefreshNotificationPopupPlacement();

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) =>
        RefreshNotificationPopupPlacement();

    private void Window_StateChanged(object? sender, EventArgs e) =>
        RefreshNotificationPopupPlacement();

    private void NotificationPopup_Opened(object? sender, EventArgs e) =>
        RefreshNotificationPopupPlacement();

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
        _toolsWindow?.Close();
        _petsWindow?.Close();
        _partyCheckWindow?.Close();
        _timersWindow?.Close();
        foreach (PlayerBreakdownWindow window in _breakdownWindows.Values.ToArray())
        {
            window.Close();
        }
        _breakdownWindows.Clear();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.Players.CollectionChanged -= Players_CollectionChanged;
        _viewModel.Dispose();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MeterViewModel.TargetName))
        {
            QueueCompactTargetMarquee();
        }
        else if (e.PropertyName == nameof(MeterViewModel.ShowPartyMembers) ||
                 e.PropertyName == nameof(MeterViewModel.ShowTargetDebuffs) ||
                 e.PropertyName == nameof(MeterViewModel.DisplayMode))
        {
            UpdateMeterHeight();
        }
    }

    private void Players_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        UpdateMeterHeight();

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
    }

    private void CycleOpacityButton_Click(object sender, RoutedEventArgs e)
    {
        CycleOpacity();

        if (sender is FrameworkElement element)
        {
            element.ToolTip = $"Прозрачность: уровень {_opacityLevelIndex + 1} из 3";
        }
    }

    internal void CycleOpacity()
    {
        _opacityLevelIndex = (_opacityLevelIndex + 1) % OpacityLevels.Length;
        Opacity = OpacityLevels[_opacityLevelIndex];

        string level = (_opacityLevelIndex + 1).ToString();
        var icon = (Brush)FindResource($"DmOpacity{level}");
        OpacityIcon.Background = icon;
        CompactOpacityIcon.Background = icon;
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

    private void EnterCompactMode()
    {
        _expandedWidth = ActualWidth > 0 ? ActualWidth : Width;
        _expandedHeight = ActualHeight > 0 ? ActualHeight : Height;
        DetailedLayout.Visibility = Visibility.Collapsed;
        CompactLayout.Visibility = Visibility.Visible;
        MinWidth = MinimumCompactWidth;
        MinHeight = 210;
        Width = DefaultCompactWidth;
        _isCompact = true;
        UpdateMeterHeight();
        QueueCompactTargetMarquee();
    }

    private void UpdateMeterHeight()
    {
        int visibleRows = Math.Clamp(_viewModel.Players.Count, 1, 10);
        int debuffSlotHeight = _viewModel.ShowTargetDebuffs && _viewModel.IsDamageMode ? DebuffSlotHeight : 0;
        if (_isCompact)
        {
            Height = 174 + visibleRows * 40 + debuffSlotHeight;
            return;
        }

        double requiredHeight = 220 + visibleRows * 51 + debuffSlotHeight;
        if (Height < requiredHeight)
        {
            Height = Math.Min(MaxHeight, requiredHeight);
        }
    }

    private void ExitCompactMode()
    {
        CompactLayout.Visibility = Visibility.Collapsed;
        DetailedLayout.Visibility = Visibility.Visible;
        MinWidth = 520;
        MinHeight = 430;
        Width = Math.Max(520, _expandedWidth);
        Height = Math.Max(430, _expandedHeight);
        _isCompact = false;
        UpdateMeterHeight();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
