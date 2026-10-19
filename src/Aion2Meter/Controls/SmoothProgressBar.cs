using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Aion2Meter.Controls;

public sealed class SmoothProgressBar : ProgressBar
{
    public static readonly DependencyProperty AnimatedValueProperty = DependencyProperty.Register(
        nameof(AnimatedValue),
        typeof(double),
        typeof(SmoothProgressBar),
        new FrameworkPropertyMetadata(0d, OnAnimatedValueChanged));

    public double AnimatedValue
    {
        get => (double)GetValue(AnimatedValueProperty);
        set => SetValue(AnimatedValueProperty, value);
    }

    private static void OnAnimatedValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not SmoothProgressBar progressBar)
        {
            return;
        }

        double target = Math.Clamp((double)args.NewValue, progressBar.Minimum, progressBar.Maximum);
        if (!progressBar.IsLoaded)
        {
            progressBar.Value = target;
            return;
        }

        var animation = new DoubleAnimation
        {
            From = progressBar.Value,
            To = target,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        progressBar.BeginAnimation(ValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
}
