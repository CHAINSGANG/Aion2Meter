using System.Windows;
using System.Windows.Input;
using Aion2Meter.ViewModels;

namespace Aion2Meter;

public partial class PlayerBreakdownWindow : Window
{
    public PlayerBreakdownWindow(PlayerRowViewModel player)
    {
        InitializeComponent();
        DataContext = new PlayerBreakdownViewModel(player);
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
