using System;
using System.Drawing;
using System.Windows.Forms;

namespace Aion2Meter.Services;

internal sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _iconImage;
    private readonly ToolStripMenuItem _showItem;
    private readonly ToolStripMenuItem _hideItem;
    private readonly ToolStripMenuItem _updateItem;
    private readonly ToolStripMenuItem _exitItem;
    private string? _releaseUrl;

    public TrayIconService(Action show, Action hide, Action checkUpdates, Action exit)
    {
        _showItem = new ToolStripMenuItem { Text = UiLanguageService.Text("Show") };
        _hideItem = new ToolStripMenuItem { Text = UiLanguageService.Text("Hide") };
        _updateItem = new ToolStripMenuItem { Text = UiLanguageService.Text("CheckUpdates") };
        _exitItem = new ToolStripMenuItem { Text = UiLanguageService.Text("Exit") };
        _showItem.Click += (_, _) => show();
        _hideItem.Click += (_, _) => hide();
        _updateItem.Click += (_, _) => checkUpdates();
        _exitItem.Click += (_, _) => exit();

        _menu = new ContextMenuStrip();
        _menu.Items.AddRange([_showItem, _hideItem, new ToolStripSeparator(), _updateItem, new ToolStripSeparator(), _exitItem]);
        string? processPath = Environment.ProcessPath;
        _iconImage = (string.IsNullOrWhiteSpace(processPath) ? null : Icon.ExtractAssociatedIcon(processPath))
            ?? (Icon)SystemIcons.Application.Clone();
        _icon = new NotifyIcon
        {
            Text = UiLanguageService.Text("TrayText"),
            Icon = _iconImage,
            ContextMenuStrip = _menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => show();
        _icon.BalloonTipClicked += (_, _) => UpdateCheckService.OpenRelease(_releaseUrl);
        UiLanguageService.LanguageChanged += LanguageChanged;
    }

    public void ShowUpdate(Version version, string? releaseUrl)
    {
        _releaseUrl = releaseUrl;
        _icon.BalloonTipTitle = UiLanguageService.Text("UpdateTitle");
        _icon.BalloonTipText = UiLanguageService.Text("UpdateAvailable", version);
        _icon.BalloonTipIcon = ToolTipIcon.Info;
        _icon.ShowBalloonTip(8000);
    }

    private void LanguageChanged(object? sender, EventArgs e)
    {
        _showItem.Text = UiLanguageService.Text("Show");
        _hideItem.Text = UiLanguageService.Text("Hide");
        _updateItem.Text = UiLanguageService.Text("CheckUpdates");
        _exitItem.Text = UiLanguageService.Text("Exit");
    }

    public void Dispose()
    {
        UiLanguageService.LanguageChanged -= LanguageChanged;
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _iconImage.Dispose();
    }
}
