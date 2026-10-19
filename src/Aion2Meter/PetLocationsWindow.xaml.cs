using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Aion2Meter.Services;
using Aion2Meter.ViewModels;

namespace Aion2Meter;

public partial class PetLocationsWindow : Window
{
    private readonly PetLocationWindowModel _model;
    private readonly MeterViewModel _meter;
    private readonly DispatcherTimer _navigationTimer;

    public PetLocationsWindow(MeterPetSnapshot pet, MeterViewModel meter)
    {
        InitializeComponent();
        _meter = meter;
        PetLocationCatalog catalog = PetLocationCatalog.Default;
        _model = new PetLocationWindowModel(pet, catalog.Resolve(pet.PetId), catalog.MapTileRoot);
        DataContext = _model;
        _navigationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _navigationTimer.Tick += NavigationTimer_Tick;
        _navigationTimer.Start();
        Closed += (_, _) => _navigationTimer.Stop();
        UpdateNavigation();
    }

    private void DragRegion_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MapSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: PetMapLocation selected })
        {
            _model.SelectedMap = selected;
            UpdateNavigation();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void NavigationTimer_Tick(object? sender, EventArgs e) => UpdateNavigation();

    private void UpdateNavigation()
    {
        _model.UpdateNavigation(_meter.GetNavigationPosition());
    }
}

internal sealed class PetLocationWindowModel : INotifyPropertyChanged
{
    private readonly string _mapTileRoot;
    private PetMapLocation? _selectedMap;
    private BitmapSource? _mapPreview;
    private MeterNavigationPosition? _navigationPosition;
    private string _navigationText = T("Waiting for character position", "Ожидаем позицию персонажа");
    private string _navigationDetail = T("Use a skill or teleport", "Примените навык или сделайте телепорт");

    public PetLocationWindowModel(MeterPetSnapshot pet, PetLocationSnapshot snapshot, string mapTileRoot)
    {
        Pet = pet;
        Snapshot = snapshot;
        _mapTileRoot = mapTileRoot;
        SelectedMap = snapshot.Maps.Count > 0 ? snapshot.Maps[0] : null;
    }

    public MeterPetSnapshot Pet { get; }
    public PetLocationSnapshot Snapshot { get; }

    public PetMapLocation? SelectedMap
    {
        get => _selectedMap;
        set
        {
            if (ReferenceEquals(_selectedMap, value))
            {
                return;
            }

            _selectedMap = value;
            RefreshMapPreview();
            OnPropertyChanged();
        }
    }

    public BitmapSource? MapPreview
    {
        get => _mapPreview;
        private set
        {
            _mapPreview = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasMapPreview));
        }
    }

    public bool HasMapPreview => MapPreview is not null;

    public string NavigationText
    {
        get => _navigationText;
        private set
        {
            if (_navigationText == value)
            {
                return;
            }
            _navigationText = value;
            OnPropertyChanged();
        }
    }

    public string NavigationDetail
    {
        get => _navigationDetail;
        private set
        {
            if (_navigationDetail == value)
            {
                return;
            }
            _navigationDetail = value;
            OnPropertyChanged();
        }
    }

    public void UpdateNavigation(MeterNavigationPosition? position)
    {
        bool positionChanged = position != _navigationPosition;
        _navigationPosition = position;
        if (position is null)
        {
            NavigationText = T("WAITING FOR CHARACTER POSITION", "ОЖИДАЕМ ПОЗИЦИЮ ПЕРСОНАЖА");
            NavigationDetail = T("Use a skill or teleport", "Примените навык или сделайте телепорт");
            if (positionChanged)
            {
                RefreshMapPreview();
            }
            return;
        }

        PetMapLocation? sameZone = Snapshot.Maps.FirstOrDefault(map => map.Definition?.MatchesZone(position.ZoneId) == true);
        if (sameZone is not null && SelectedMap?.Definition?.MatchesZone(position.ZoneId) != true)
        {
            SelectedMap = sameZone;
        }

        if (SelectedMap?.Definition?.MatchesZone(position.ZoneId) != true)
        {
            NavigationText = T("YOU ARE IN ANOTHER LOCATION", "ВЫ СЕЙЧАС В ДРУГОЙ ЛОКАЦИИ");
            NavigationDetail = T("Select a point in the current zone or travel to the listed one", "Выберите точку в текущей зоне или перейдите в указанную");
            if (positionChanged)
            {
                RefreshMapPreview();
            }
            return;
        }

        PetSpawnPoint? nearest = SelectedMap.Points.MinBy(point =>
        {
            double dx = point.X - position.X;
            double dy = point.Y - position.Y;
            return dx * dx + dy * dy;
        });
        if (nearest is null)
        {
            NavigationText = T("NO POINTS FOUND", "ТОЧКИ НЕ НАЙДЕНЫ");
            NavigationDetail = string.Empty;
            return;
        }

        double distance = Math.Sqrt(
            (nearest.X - position.X) * (nearest.X - position.X) +
            (nearest.Y - position.Y) * (nearest.Y - position.Y)) / 100d;
        NavigationText = T($"NEAREST POINT  {distance:0} m", $"ДО БЛИЖАЙШЕЙ ТОЧКИ  {distance:0} м");
        NavigationDetail = T("Blue point — you · gold point — target mob", "Голубая точка — вы · золотая — нужный моб");
        if (positionChanged)
        {
            RefreshMapPreview();
        }
    }

    private void RefreshMapPreview()
    {
        MeterNavigationPosition? player = SelectedMap?.Definition?.MatchesZone(_navigationPosition?.ZoneId ?? 0) == true
            ? _navigationPosition
            : null;
        MapPreview = SelectedMap is null ? null : PetMapRenderer.Render(SelectedMap, _mapTileRoot, player);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string T(string english, string russian) => UiLanguageService.Pick(english, russian);
}
