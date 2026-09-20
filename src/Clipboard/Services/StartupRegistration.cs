using Microsoft.Win32;

namespace ClipboardApp.Services;

// HKCU only - no admin rights, no HKLM, no scheduled task.
public static class StartupRegistration
{
    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "Clipboard";

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        var desired = $"\"{exePath}\" --tray";
        if (key.GetValue(ValueName) as string != desired)
            key.SetValue(ValueName, desired);
    }
}
