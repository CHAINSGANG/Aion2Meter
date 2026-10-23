using System;

namespace Aion2Meter.ViewModels;

public sealed class DebuffTimerViewModel : BindableBase
{
    private const string FallbackIconPath = "/Aion2Meter;component/Assets/Textures/DebuffDefenseDown.png";
    private string _name = string.Empty;
    private string _iconPath = FallbackIconPath;
    private string _remainingText = string.Empty;
    private string _stacksText = string.Empty;
    private double _remainingPercent;
    private bool _isOtherPlayer;

    public int SkillCode { get; init; }
    public int OwnerId { get; init; }
    public string Name { get => _name; private set => SetProperty(ref _name, value); }
    public string IconPath { get => _iconPath; private set => SetProperty(ref _iconPath, value); }
    public string RemainingText { get => _remainingText; private set => SetProperty(ref _remainingText, value); }
    public string StacksText { get => _stacksText; private set => SetProperty(ref _stacksText, value); }
    public double RemainingPercent { get => _remainingPercent; private set => SetProperty(ref _remainingPercent, value); }
    public bool IsOtherPlayer { get => _isOtherPlayer; private set => SetProperty(ref _isOtherPlayer, value); }

    public void Update(Services.MeterDebuffSnapshot snapshot, string? iconPath)
    {
        Name = snapshot.OwnerId > 0
            ? $"{snapshot.Name}\n{Services.UiLanguageService.Pick("Applied by", "Наложил")}: {snapshot.OwnerName ?? $"#{snapshot.OwnerId}"}"
            : snapshot.Name;
        IconPath = string.IsNullOrWhiteSpace(iconPath) ? FallbackIconPath : iconPath;
        IsOtherPlayer = snapshot.IsOtherPlayer;
        RemainingText = FormatRemaining(snapshot.RemainingMs);
        StacksText = snapshot.Stacks > 1 ? $"×{snapshot.Stacks}" : string.Empty;
        RemainingPercent = snapshot.DurationMs > 0
            ? Math.Clamp(snapshot.RemainingMs * 100d / snapshot.DurationMs, 0d, 100d)
            : 0;
    }

    private static string FormatRemaining(long milliseconds)
    {
        double seconds = Math.Max(0, milliseconds / 1000d);
        if (seconds >= 60)
        {
            int totalSeconds = (int)Math.Ceiling(seconds);
            return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
        }

        return $"{seconds:0.0}{Services.UiLanguageService.Pick("s", "с")}";
    }
}
