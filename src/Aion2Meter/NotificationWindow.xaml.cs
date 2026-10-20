using System;
using System.Windows;
using Aion2Meter.ViewModels;

namespace Aion2Meter;

public partial class NotificationWindow : Window
{
    public NotificationWindow(MeterViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public void PlaceNear(Window meter)
    {
        Width = Math.Max(1, meter.ActualWidth);
        double height = ActualHeight > 0 ? ActualHeight : 26;
        double screenLeft = SystemParameters.VirtualScreenLeft;
        double screenTop = SystemParameters.VirtualScreenTop;
        double screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
        double screenBottom = screenTop + SystemParameters.VirtualScreenHeight;
        double desiredTop = meter.Top + meter.ActualHeight + 6;
        if (desiredTop + height > screenBottom)
            desiredTop = meter.Top - height - 6;

        Left = Math.Clamp(meter.Left, screenLeft, Math.Max(screenLeft, screenRight - Width));
        Top = Math.Clamp(desiredTop, screenTop, Math.Max(screenTop, screenBottom - height));
    }
}
