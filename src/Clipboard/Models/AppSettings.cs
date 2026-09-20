namespace ClipboardApp.Models;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public bool StartWithWindows { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; } = true;

    // Copy-only by default (row click puts the entry back on the clipboard).
    // When enabled, a click also simulates Ctrl+V into whatever window was active before.
    public bool AutoPasteEnabled { get; set; }

    public int MaxEntries { get; set; } = 300;

    // Skips clipboard writes tagged "exclude from monitoring" - the convention password
    // managers (1Password, Bitwarden, KeePass, Windows' own credential UI) use to keep
    // copied secrets out of clipboard history tools.
    public bool IgnorePasswordManagerCopies { get; set; } = true;

    public WindowPlacement Window { get; set; } = new();
}

public sealed class WindowPlacement
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 460;
    public double Height { get; set; } = 640;
    public bool Maximized { get; set; }
}
