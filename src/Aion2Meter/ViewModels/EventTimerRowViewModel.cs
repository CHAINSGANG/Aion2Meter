using System;
using System.Windows.Media;
using Aion2Meter.Services;

namespace Aion2Meter.ViewModels;

public sealed class EventTimerRowViewModel : BindableBase
{
    private string _kind = string.Empty;
    private string _title = string.Empty;
    private string _detail = string.Empty;
    private string _timeText = string.Empty;
    private string _countdownText = string.Empty;
    private bool _isActive;
    private Brush _accentBrush = Brushes.Cyan;

    public required string Key { get; init; }
    public string Kind { get => _kind; private set => SetProperty(ref _kind, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string Detail { get => _detail; private set => SetProperty(ref _detail, value); }
    public string TimeText { get => _timeText; private set => SetProperty(ref _timeText, value); }
    public string CountdownText { get => _countdownText; private set => SetProperty(ref _countdownText, value); }
    public bool IsActive { get => _isActive; private set => SetProperty(ref _isActive, value); }
    public Brush AccentBrush { get => _accentBrush; private set => SetProperty(ref _accentBrush, value); }

    public void Update(MeterEventTimerSnapshot snapshot)
    {
        Kind = snapshot.Kind;
        Title = snapshot.Title;
        Detail = snapshot.Detail;
        TimeText = snapshot.LocalStart.ToString("dd.MM · HH:mm");
        CountdownText = snapshot.IsActive ? UiLanguageService.Pick("NOW", "СЕЙЧАС") : EventScheduleService.Countdown(snapshot.TimeUntil);
        IsActive = snapshot.IsActive;
        AccentBrush = MakeBrush(snapshot.AccentColor);
    }

    private static Brush MakeBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
