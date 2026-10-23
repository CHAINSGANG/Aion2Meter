using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Aion2Meter.ViewModels;

namespace Aion2Meter;

public partial class PlayerBreakdownWindow : Window
{
    private readonly PlayerRowViewModel _player;
    private readonly PlayerBreakdownViewModel _viewModel;

    public PlayerBreakdownWindow(PlayerRowViewModel player)
    {
        InitializeComponent();
        _player = player;
        _viewModel = new PlayerBreakdownViewModel(player);
        DataContext = _viewModel;
        _player.PropertyChanged += Player_PropertyChanged;
        Closed += (_, _) => _player.PropertyChanged -= Player_PropertyChanged;
    }

    private void Player_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerRowViewModel.Passives))
            _viewModel.UpdatePassives(_player.Passives);
        else if (e.PropertyName == nameof(PlayerRowViewModel.Specialties))
            _viewModel.UpdateSpecialties(_player.Specialties);
    }

    private void DragRegion_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BreakdownTab_Checked(object sender, RoutedEventArgs e)
    {
        if (SkillsPanel is null || PassivesPanel is null || SpecialtiesPanel is null)
            return;

        bool showPassives = ReferenceEquals(sender, PassivesTab);
        bool showSpecialties = ReferenceEquals(sender, SpecialtiesTab);
        SkillsPanel.Visibility = showPassives || showSpecialties ? Visibility.Collapsed : Visibility.Visible;
        PassivesPanel.Visibility = showPassives ? Visibility.Visible : Visibility.Collapsed;
        SpecialtiesPanel.Visibility = showSpecialties ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
