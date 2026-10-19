using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aion2Meter.Controls;

public sealed class SmoothStackPanel : StackPanel
{
    private readonly Dictionary<UIElement, double> _positions = new();

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Size result = base.ArrangeOverride(arrangeSize);
        var currentChildren = InternalChildren.Cast<UIElement>().ToHashSet();

        foreach (UIElement child in currentChildren)
        {
            double newY = VisualTreeHelper.GetOffset(child).Y;
            if (_positions.TryGetValue(child, out double oldY))
            {
                // A new ranking update can arrive before the previous animation ends.
                // Continue from the element's current on-screen position instead of
                // snapping back to the previous layout slot.
                TranslateTransform transform = EnsureTransform(child);
                double layoutDelta = oldY - newY;
                if (Math.Abs(layoutDelta) > 0.5)
                {
                    double delta = oldY + transform.Y - newY;
                    AnimateToNewPosition(child, transform, delta);
                }
            }

            _positions[child] = newY;
        }

        foreach (UIElement removed in _positions.Keys.Where(child => !currentChildren.Contains(child)).ToArray())
        {
            _positions.Remove(removed);
        }

        return result;
    }

    private static TranslateTransform EnsureTransform(UIElement child)
    {
        if (child.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            child.RenderTransform = transform;
        }

        return transform;
    }

    private static void AnimateToNewPosition(UIElement child, TranslateTransform transform, double delta)
    {
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        transform.Y = delta;
        Panel.SetZIndex(child, delta > 0 ? 2 : 1);
        var animation = new DoubleAnimation
        {
            From = delta,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) =>
        {
            transform.Y = 0;
            Panel.SetZIndex(child, 0);
        };
        transform.BeginAnimation(TranslateTransform.YProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }
}
