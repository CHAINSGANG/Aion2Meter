using System.Windows;
using System.Windows.Input;
using Aion2Meter.ViewModels;

namespace Aion2Meter;

public partial class PetsWindow : Window
{
    private PetLocationsWindow? _locationsWindow;

    public PetsWindow(MeterViewModel viewModel)
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

    private void LocationsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Services.MeterPetSnapshot pet })
        {
            return;
        }

        OpenLocations(pet);
    }

    internal void OpenLocations(Services.MeterPetSnapshot pet)
    {
        _locationsWindow?.Close();
        _locationsWindow = new PetLocationsWindow(pet, (MeterViewModel)DataContext)
        {
            Owner = this,
            Topmost = Topmost,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _locationsWindow.Closed += (_, _) => _locationsWindow = null;
        _locationsWindow.Show();
    }
}
