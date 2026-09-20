using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ClipboardApp.Services;

// Registers the app-wide Ctrl+Shift+V shortcut via a hidden message-only window - this is
// the only reliable way to receive WM_HOTKEY without an on-screen window.
public sealed class GlobalHotkey : IDisposable
{
    const int WM_HOTKEY = 0x0312;
    const int HotkeyId = 0xC1E9;
    const uint MOD_CONTROL = 0x0002;
    const uint MOD_SHIFT = 0x0004;
    const uint VK_V = 0x56;
    static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    HwndSource? _source;

    public event Action? Pressed;

    /// <returns>false if Ctrl+Shift+V is already claimed by another application.</returns>
    public bool Register()
    {
        var parameters = new HwndSourceParameters("ClipboardGlobalHotkey")
        {
            WindowStyle = 0,
            Width = 0,
            Height = 0,
            ParentWindow = HWND_MESSAGE,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        return RegisterHotKey(_source.Handle, HotkeyId, MOD_CONTROL | MOD_SHIFT, VK_V);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is null) return;
        UnregisterHotKey(_source.Handle, HotkeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
