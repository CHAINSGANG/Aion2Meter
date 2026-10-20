using System;
using System.Windows.Media;
using Aion2Meter.Services;

namespace Aion2Meter.ViewModels;

public sealed class EventAlertRowViewModel : BindableBase
{
    private string _title = string.Empty;
    private string _countdown = string.Empty;
    private Brush _background = Brushes.Transparent;
    private Brush _border = Brushes.Transparent;
    private string _backgroundColor = string.Empty;
    private string _borderColor = string.Empty;

    public required string Key { get; init; }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string Countdown { get => _countdown; private set => SetProperty(ref _countdown, value); }
    public Brush Background { get => _background; private set => SetProperty(ref _background, value); }
    public Brush Border { get => _border; private set => SetProperty(ref _border, value); }

    public void Update(MeterEventAlertSnapshot alert)
    {
        Title = alert.Title;
        Countdown = alert.Countdown;
        if (_backgroundColor != alert.BackgroundColor)
        {
            _backgroundColor = alert.BackgroundColor;
            Color color = (Color)ColorConverter.ConvertFromString(alert.BackgroundColor);
            var brush = new LinearGradientBrush(
                Color.FromArgb(color.A, color.R, color.G, color.B),
                Color.FromArgb(color.A, (byte)(color.R * 0.55), (byte)(color.G * 0.55), (byte)(color.B * 0.55)),
                0);
            brush.Freeze();
            Background = brush;
        }
        if (_borderColor != alert.BorderColor)
        {
            _borderColor = alert.BorderColor;
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(alert.BorderColor));
            brush.Freeze();
            Border = brush;
        }
    }
}
