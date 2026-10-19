using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Aion2Meter.Services;
using Aion2Meter.ViewModels;

namespace Aion2Meter;

public partial class ToolsWindow : Window
{
    private bool _syncingOpacity;

    public ToolsWindow(MeterViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        RefreshLanguageButtons();
        Loaded += (_, _) => SyncOpacitySliders();
    }

    private void DragRegion_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private MainWindow? MeterWindow => Owner as MainWindow;

    private void ToggleLayout_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.ToggleCompactMode();
        StatusText.Text = UiLanguageService.Text("LayoutChanged");
    }

    private void CycleOpacity_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.CycleOpacity();
        StatusText.Text = UiLanguageService.Text("OpacityChanged");
    }

    private void CopySummary_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.CopySummaryToClipboard();
        StatusText.Text = UiLanguageService.Text("SummaryCopied");
    }

    private void CopyScreenshot_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.CopyScreenshotToClipboard();
        StatusText.Text = UiLanguageService.Text("ScreenshotCopied");
    }

    private void OpenPets_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.OpenPets();
        StatusText.Text = UiLanguageService.Text("PetsOpened");
    }

    private void OpenPartyCheck_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.OpenPartyCheck();
        StatusText.Text = UiLanguageService.Text("PartyOpened");
    }

    private void OpenTimers_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.OpenTimers();
        StatusText.Text = UiLanguageService.Text("TimersOpened");
    }

    private void English_Click(object sender, RoutedEventArgs e) => ChangeLanguage("en");

    private void Russian_Click(object sender, RoutedEventArgs e) => ChangeLanguage("ru");

    private void ChangeLanguage(string language)
    {
        MeterWindow?.SetLanguage(language);
        RefreshLanguageButtons();
        StatusText.Text = UiLanguageService.Text("LanguageChanged");
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = UiLanguageService.Text("CheckingUpdates");
        if (MeterWindow is { } meter)
        {
            StatusText.Text = await meter.CheckForUpdatesAsync(showCurrentStatus: true);
        }
    }

    private void RefreshLanguageButtons()
    {
        Brush selected = new SolidColorBrush(Color.FromRgb(86, 71, 37));
        Brush normal = new SolidColorBrush(Color.FromRgb(32, 42, 50));
        EnglishButton.Background = UiLanguageService.IsRussian ? normal : selected;
        RussianButton.Background = UiLanguageService.IsRussian ? selected : normal;
    }

    internal void SyncOpacitySliders()
    {
        if (MeterWindow is null)
        {
            return;
        }

        _syncingOpacity = true;
        WindowOpacitySlider.Value = MeterWindow.WindowOpacityPercent;
        TextOpacitySlider.Value = MeterWindow.TextOpacityPercent;
        _syncingOpacity = false;
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingOpacity || !IsLoaded)
        {
            return;
        }

        MeterWindow?.SetCustomOpacity(WindowOpacitySlider.Value, TextOpacitySlider.Value);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
