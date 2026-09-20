using System.Drawing;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace ClipboardApp.Services;

public sealed class TrayIcon : IDisposable
{
    readonly NotifyIcon _icon;

    public event Action? OpenRequested;
    public event Action? ClearHistoryRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Clear history", null, (_, _) => ClearHistoryRequested?.Invoke());
        menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Clipboard",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OpenRequested?.Invoke();
        };
    }

    static Icon LoadIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/app.ico");
        var info = Application.GetResourceStream(uri);
        return info is not null
            ? new Icon(info.Stream, SystemInformation.SmallIconSize)
            : SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
