using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Aion2Meter.Services;

internal sealed class GameWindowAttachment : IDisposable
{
    private const int GwlHwndParent = -8;
    private readonly Window _meterWindow;
    private readonly DispatcherTimer _timer;
    private nint _gameWindow;
    private bool _hiddenByGame;
    private bool _hiddenByUser;

    public GameWindowAttachment(Window meterWindow)
    {
        _meterWindow = meterWindow;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += (_, _) => Refresh();
    }

    public bool IsHiddenByUser => _hiddenByUser;

    public void Start()
    {
        Refresh();
        _timer.Start();
    }

    public void HideToTray()
    {
        _hiddenByUser = true;
        _meterWindow.Hide();
    }

    public void ShowFromTray()
    {
        _hiddenByUser = false;
        if (_gameWindow != 0 && IsIconic(_gameWindow))
        {
            ShowWindow(_gameWindow, 9);
        }
        _meterWindow.Show();
        _meterWindow.Activate();
    }

    private void Refresh()
    {
        nint current = FindGameWindow();
        if (current != _gameWindow)
        {
            _gameWindow = current;
            AttachOwner(current);
        }

        bool gameUnavailable = current == 0 || IsIconic(current) || !IsWindowVisible(current);
        if (gameUnavailable && _meterWindow.IsVisible && !_hiddenByUser)
        {
            _hiddenByGame = true;
            _meterWindow.Hide();
        }
        else if (!gameUnavailable && _hiddenByGame && !_hiddenByUser)
        {
            _hiddenByGame = false;
            _meterWindow.Show();
        }
    }

    private void AttachOwner(nint gameWindow)
    {
        nint meterHandle = new WindowInteropHelper(_meterWindow).Handle;
        if (meterHandle == 0)
        {
            return;
        }

        SetWindowLongPtr(meterHandle, GwlHwndParent, gameWindow);
        _meterWindow.Topmost = gameWindow == 0;
    }

    private static nint FindGameWindow()
    {
        foreach (Process process in Process.GetProcessesByName("AION2"))
        {
            try
            {
                if (process.MainWindowHandle != 0)
                {
                    return process.MainWindowHandle;
                }
            }
            catch
            {
                // The process may exit while the list is being inspected.
            }
            finally
            {
                process.Dispose();
            }
        }
        return 0;
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_meterWindow.IsLoaded)
        {
            AttachOwner(0);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint newLong);
}
