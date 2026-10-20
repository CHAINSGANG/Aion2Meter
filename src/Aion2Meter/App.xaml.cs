using System;
using System.IO;
using System.Windows;
using Aion2Meter.Services;

namespace Aion2Meter;

public partial class App : Application
{
    private SingleInstanceGate? _instanceGate;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _instanceGate = new SingleInstanceGate(@"Local\CHAINSGANG.Aion2Meter");
            if (!_instanceGate.IsPrimary)
            {
                UiLanguageService.Apply(new MeterSettingsStore().Load().Language);
                MessageBox.Show(
                    UiLanguageService.Pick("Aion 2 DPS is already running.", "Aion 2 DPS уже запущен."),
                    "Aion 2 DPS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown();
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aion2Meter",
                "logs");
            string logPath = Path.Combine(logDirectory, $"startup-error-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            try
            {
                Directory.CreateDirectory(logDirectory);
                File.WriteAllText(logPath, exception.ToString());
            }
            catch (IOException)
            {
                logPath = UiLanguageService.Pick("could not write log", "не удалось записать журнал");
            }
            catch (UnauthorizedAccessException)
            {
                logPath = UiLanguageService.Pick("could not write log", "не удалось записать журнал");
            }
            MessageBox.Show(
                UiLanguageService.Pick(
                    $"Could not open DPS Meter.\n\n{exception.Message}\n\nLog: {logPath}",
                    $"Не удалось открыть DPS Meter.\n\n{exception.Message}\n\nЛог: {logPath}"),
                "Aion 2 DPS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceGate?.Dispose();
        base.OnExit(e);
    }
}
