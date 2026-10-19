using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Aion2Meter.ViewModels;

namespace Aion2Meter;

public partial class ToolsWindow : Window
{
    public ToolsWindow(MeterViewModel viewModel)
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

    private MainWindow? MeterWindow => Owner as MainWindow;

    private void ToggleLayout_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.ToggleCompactMode();
        StatusText.Text = "Размер оверлея переключён";
    }

    private void CycleOpacity_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.CycleOpacity();
        StatusText.Text = "Переключён следующий из трёх уровней прозрачности";
    }

    private void CopySummary_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.CopySummaryToClipboard();
        StatusText.Text = "Сводка боя скопирована в буфер обмена";
    }

    private void CopyScreenshot_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.CopyScreenshotToClipboard();
        StatusText.Text = "Снимок DPS-окна скопирован в буфер обмена";
    }

    private void OpenPets_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.OpenPets();
        StatusText.Text = "Открыта страница питомцев";
    }

    private void OpenPartyCheck_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.OpenPartyCheck();
        StatusText.Text = "Открыта проверка состава группы";
    }

    private void OpenTimers_Click(object sender, RoutedEventArgs e)
    {
        MeterWindow?.OpenTimers();
        StatusText.Text = "Открыт календарь игровых событий";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
