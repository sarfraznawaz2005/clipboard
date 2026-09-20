using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;

namespace ClipboardApp.Services;

// Asks DWM to draw the native title bar dark instead of building a custom one.
public static class DarkTitleBar
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    const int UseImmersiveDarkMode20H1 = 20; // Windows 10 20H1+ / Windows 11
    const int UseImmersiveDarkModeOld = 19;  // Windows 10 1809-1909
    const int CaptionColor = 35;             // Windows 11 22000+ only; ignored on older Windows

    // A dark gray rather than the near-black that immersive dark mode alone produces.
    static readonly Color TitleBarColor = Color.FromRgb(0x2B, 0x2F, 0x38);

    public static void Apply(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;

            int enabled = 1;
            if (DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode20H1, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, UseImmersiveDarkModeOld, ref enabled, sizeof(int));

            int colorRef = TitleBarColor.R | (TitleBarColor.G << 8) | (TitleBarColor.B << 16);
            DwmSetWindowAttribute(hwnd, CaptionColor, ref colorRef, sizeof(int));
        };
    }
}
