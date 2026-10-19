using System;
using System.IO;
using System.Windows;

namespace Aion2Meter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            string logPath = Path.Combine(Path.GetTempPath(), "Aion2Meter-startup-error.txt");
            File.WriteAllText(logPath, exception.ToString());
            MessageBox.Show(
                $"Не удалось открыть DPS Meter.\n\n{exception.Message}\n\nЛог: {logPath}",
                "Aion II Meter",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}
