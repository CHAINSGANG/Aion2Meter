using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Aion2Meter.ViewModels;

namespace Aion2Meter.Controls;

public partial class NotificationTicker : UserControl
{
    private const double ScrollPixelsPerSecond = 42;
    private readonly TranslateTransform _scroll = new();
    private bool _updateQueued;
    private bool _isRendering;
    private double _cycleWidth;
    private long _lastFrameTimestamp;

    public NotificationTicker()
    {
        InitializeComponent();
        Track.RenderTransform = _scroll;
        Loaded += (_, _) => StartRendering();
        Unloaded += (_, _) => StopRendering();
        SizeChanged += (_, _) => QueueTrackUpdate();
        PrimaryCycle.SizeChanged += (_, _) => QueueTrackUpdate();
        DataContextChanged += (_, _) => QueueTrackUpdate();
    }

    public int VisualAlertCount => (DataContext as MeterViewModel)?.EventAlerts.Count ?? 0;

    private void StartRendering()
    {
        if (_isRendering)
            return;

        _isRendering = true;
        _lastFrameTimestamp = Stopwatch.GetTimestamp();
        CompositionTarget.Rendering += OnRendering;
        QueueTrackUpdate();
    }

    private void StopRendering()
    {
        if (!_isRendering)
            return;

        CompositionTarget.Rendering -= OnRendering;
        _isRendering = false;
        _scroll.X = 0;
    }

    private void QueueTrackUpdate()
    {
        if (!IsLoaded || _updateQueued)
            return;

        _updateQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _updateQueued = false;
            UpdateTrack();
        }, DispatcherPriority.Loaded);
    }

    private void UpdateTrack()
    {
        if (!IsLoaded)
            return;

        double cycleWidth = PrimaryCycle.ActualWidth;
        if (cycleWidth <= 0 || Viewport.ActualWidth <= 1)
        {
            _cycleWidth = 0;
            return;
        }
        _cycleWidth = cycleWidth;

        int requiredCopies = Math.Clamp(
            (int)Math.Ceiling(Viewport.ActualWidth / cycleWidth) + 2,
            2,
            16);
        while (Track.Children.Count < requiredCopies)
        {
            var copy = new ContentPresenter
            {
                ContentTemplate = (DataTemplate)Resources["TickerCycleTemplate"]
            };
            copy.SetBinding(ContentPresenter.ContentProperty, new Binding());
            Track.Children.Add(copy);
        }
        while (Track.Children.Count > requiredCopies)
            Track.Children.RemoveAt(Track.Children.Count - 1);

        // Updating a title must not restart the marquee from its beginning.
        while (_scroll.X <= -_cycleWidth)
            _scroll.X += _cycleWidth;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = Math.Min(0.05, (now - _lastFrameTimestamp) / (double)Stopwatch.Frequency);
        _lastFrameTimestamp = now;
        if (_cycleWidth <= 0 || elapsed <= 0)
            return;

        double next = _scroll.X - elapsed * ScrollPixelsPerSecond;
        while (next <= -_cycleWidth)
            next += _cycleWidth;
        _scroll.X = next;
    }
}
