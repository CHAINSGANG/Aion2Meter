using System;
using System.Windows.Media;
using Aion2Meter.Services;

namespace Aion2Meter.ViewModels;

public sealed class EventAlertRowViewModel : BindableBase
{
    private string _title = string.Empty;
    private string _countdown = string.Empty;
    private string _accentColor = string.Empty;
    private Brush _accentBrush = Brushes.White;
    private Brush _tickerBackground = Brushes.Transparent;
    public required string Key { get; init; }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string Countdown { get => _countdown; private set => SetProperty(ref _countdown, value); }
    public Brush AccentBrush { get => _accentBrush; private set => SetProperty(ref _accentBrush, value); }
    public Brush TickerBackground { get => _tickerBackground; private set => SetProperty(ref _tickerBackground, value); }

    public void Update(MeterEventAlertSnapshot alert)
    {
        Title = alert.Title;
        Countdown = alert.Countdown;
        if (string.Equals(_accentColor, alert.AccentColor, StringComparison.OrdinalIgnoreCase))
            return;

        _accentColor = alert.AccentColor;
        Color accent;
        try
        {
            accent = (Color)ColorConverter.ConvertFromString(alert.AccentColor)!;
        }
        catch (FormatException)
        {
            accent = Color.FromRgb(103, 206, 210);
        }

        var foreground = new SolidColorBrush(accent);
        foreground.Freeze();
        AccentBrush = foreground;

        var background = new LinearGradientBrush(
            Color.FromArgb(92, accent.R, accent.G, accent.B),
            Color.FromArgb(74, accent.R, accent.G, accent.B),
            0);
        background.Freeze();
        TickerBackground = background;
    }
}
