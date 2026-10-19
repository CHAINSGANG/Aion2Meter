using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Aion2Meter.Controls;

public sealed class MarqueeTextBlock : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(MarqueeTextBlock),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnTextChanged));

    private TextBlock? _textBlock;
    private TranslateTransform? _translate;

    public MarqueeTextBlock()
    {
        Loaded += (_, _) => QueueAnimation();
        Unloaded += (_, _) => StopAnimation();
        SizeChanged += (_, _) => QueueAnimation();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _textBlock = GetTemplateChild("PART_Text") as TextBlock;
        _translate = null;
        if (_textBlock is not null)
        {
            // A transform declared inside a shared WPF template can be frozen.
            // Give every marquee its own mutable instance before animating it.
            _translate = new TranslateTransform();
            _textBlock.RenderTransform = _translate;
        }
        QueueAnimation();
    }

    private static void OnTextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is MarqueeTextBlock marquee)
        {
            marquee.QueueAnimation();
        }
    }

    private void QueueAnimation()
    {
        if (!IsLoaded)
        {
            return;
        }

        Dispatcher.BeginInvoke(RestartAnimation, DispatcherPriority.Loaded);
    }

    private void RestartAnimation()
    {
        if (_textBlock is null || _translate is null || ActualWidth <= 1)
        {
            return;
        }

        StopAnimation();
        _textBlock.Width = double.NaN;
        _textBlock.Measure(new Size(double.PositiveInfinity, Math.Max(1, ActualHeight)));
        double textWidth = Math.Ceiling(_textBlock.DesiredSize.Width) + 2;
        _textBlock.Width = textWidth;
        double overflow = textWidth - ActualWidth;
        if (overflow <= 1)
        {
            return;
        }

        TimeSpan pause = TimeSpan.FromSeconds(1.15);
        TimeSpan travel = TimeSpan.FromSeconds(Math.Max(2.2, overflow / 27d));
        TimeSpan farPause = pause + travel + pause;
        TimeSpan total = farPause + travel;
        var easing = new SineEase { EasingMode = EasingMode.EaseInOut };
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = new Duration(total),
            RepeatBehavior = RepeatBehavior.Forever
        };
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(pause)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(pause + travel), easing));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(farPause)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(total), easing));
        _translate.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void StopAnimation()
    {
        if (_translate is null)
        {
            return;
        }

        _translate.BeginAnimation(TranslateTransform.XProperty, null);
        _translate.X = 0;
    }
}
