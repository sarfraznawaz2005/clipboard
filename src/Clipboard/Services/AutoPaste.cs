using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ClipboardApp.Services;

// Simulates Ctrl+V into whichever window was focused before our popup opened. Only used
// when the user opts into AppSettings.AutoPasteEnabled - the default is copy-only.
public static class AutoPaste
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    const uint INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const ushort VK_SHIFT = 0x10;
    const ushort VK_CONTROL = 0x11;
    const ushort VK_MENU = 0x12;
    const ushort VK_V = 0x56;

    // INPUT is a native union (of MOUSEINPUT/KEYBDINPUT/HARDWAREINPUT) - a struct that only
    // declares the KEYBDINPUT fields inline comes out smaller than the real native INPUT
    // (32 bytes vs the 40 Windows expects on x64). SendInput then rejects the whole call with
    // ERROR_INVALID_PARAMETER (87) and sends nothing, which is exactly what was happening.
    // FieldOffset(0) on the union member reproduces the real layout and size.
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    public static IntPtr CaptureForegroundWindow() => GetForegroundWindow();

    public static void RestoreAndPaste(IntPtr targetWindow)
    {
        Log($"start: target={Describe(targetWindow)} currentForeground={Describe(GetForegroundWindow())}");

        if (targetWindow != IntPtr.Zero)
        {
            bool ok = EnsureForeground(targetWindow);
            Log($"EnsureForeground={ok} foregroundNow={Describe(GetForegroundWindow())}");
        }

        // The user was still physically holding Ctrl+Shift when this fires - release every
        // modifier first so our injected Ctrl+V can't combine with a still-held Shift/Alt.
        ReleaseModifierKeys();

        var inputs = new[]
        {
            Key(VK_CONTROL, down: true),
            Key(VK_V, down: true),
            Key(VK_V, down: false),
            Key(VK_CONTROL, down: false),
        };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        var err = sent != inputs.Length ? Marshal.GetLastWin32Error() : 0;
        Log($"SendInput sent={sent}/{inputs.Length} lastError={err} foregroundAfter={Describe(GetForegroundWindow())}");
    }

    // Tries the clean way (AttachThreadInput) first; if the target still isn't foreground
    // after that, falls back to a harmless Alt tap, which resets Windows' focus-stealing-
    // prevention timeout even when AttachThreadInput alone doesn't take.
    static bool EnsureForeground(IntPtr target)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (GetForegroundWindow() == target) return true;

            uint targetThread = GetWindowThreadProcessId(target, IntPtr.Zero);
            uint currentThread = GetCurrentThreadId();
            bool attached = targetThread != 0 && targetThread != currentThread &&
                AttachThreadInput(currentThread, targetThread, true);
            try
            {
                SetForegroundWindow(target);
                BringWindowToTop(target);
            }
            finally
            {
                if (attached) AttachThreadInput(currentThread, targetThread, false);
            }

            Thread.Sleep(40);
            if (GetForegroundWindow() == target) return true;

            if (attempt == 0) TapAlt();
        }

        return GetForegroundWindow() == target;
    }

    static void TapAlt()
    {
        var inputs = new[] { Key(VK_MENU, down: true), Key(VK_MENU, down: false) };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        Thread.Sleep(20);
    }

    static void ReleaseModifierKeys()
    {
        var inputs = new[] { Key(VK_SHIFT, down: false), Key(VK_CONTROL, down: false), Key(VK_MENU, down: false) };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    static INPUT Key(ushort vk, bool down) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = down ? 0u : KEYEVENTF_KEYUP } },
    };

    static string Describe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "<none>";
        var title = new StringBuilder(256);
        GetWindowText(hwnd, title, title.Capacity);
        var cls = new StringBuilder(256);
        GetClassName(hwnd, cls, cls.Capacity);
        return $"[{hwnd}] class='{cls}' title='{title}'";
    }

    static void Log(string message)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipboard", "crash.log");
            File.AppendAllText(path, $"{DateTime.Now:O}  AutoPaste: {message}\n");
        }
        catch { /* best effort */ }
    }
}
