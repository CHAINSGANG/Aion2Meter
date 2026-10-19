using System.Windows;
using System.Windows.Input;
using Aion2Meter.ViewModels;

namespace Aion2Meter;

public partial class TimersWindow : Window
{
    public TimersWindow(MeterViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void DragRegion_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
