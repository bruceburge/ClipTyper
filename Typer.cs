using System.Runtime.InteropServices;

namespace ClipTyper;

/// <summary>
/// Types text into the focused window as Unicode keystrokes, using SendInput
/// with KEYEVENTF_UNICODE. Characters go in as UTF-16 code units rather than
/// physical key positions, so keyboard layout differences between the host
/// and the target (a VM console, a remote session, another machine) cannot
/// garble symbols. This is the whole reason ClipTyper exists: it reaches
/// fields that block paste.
/// </summary>
internal static class Typer
{
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;              // 1 = keyboard
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // The union must be as large as its biggest member (MOUSEINPUT, 32 bytes
    // on x64). Without it, sizeof(INPUT) comes out as 32 instead of 40 and
    // SendInput rejects every call with ERROR_INVALID_PARAMETER (87). That
    // exact bug shipped in an early build and made typing a silent no-op,
    // which is why every SendInput result below is checked.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;   // Alt

    private static bool SendUnicodeChar(char c, out int error)
    {
        // One UTF-16 code unit per call; surrogate pairs are sent as their two
        // code units in sequence, which is what KEYEVENTF_UNICODE expects.
        var down = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE } }
        };
        var up = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP } }
        };

        uint sent = SendInput(2, [down, up], Marshal.SizeOf<INPUT>());
        if (sent != 2)
        {
            error = Marshal.GetLastWin32Error();
            return false;
        }
        error = 0;
        return true;
    }

    /// <summary>
    /// Types the whole string. Returns false (with the Win32 error) on the
    /// first character Windows refuses, so the caller can report honestly
    /// instead of claiming success.
    /// </summary>
    public static bool TypeText(string text, out int error)
    {
        foreach (char c in text)
        {
            bool ok;
            if (c == '\r') continue;              // CR of CRLF: the LF sends Enter
            if (c == '\n') ok = SendUnicodeChar('\r', out error);
            else ok = SendUnicodeChar(c, out error);
            if (!ok) return false;
            Thread.Sleep(12);                      // VM consoles drop chars if flooded
        }
        error = 0;
        return true;
    }

    private static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    /// <summary>
    /// Waits until Ctrl/Alt/Shift are physically released. If a modifier is
    /// still held when typing starts, the target app reads the characters as
    /// shortcuts or AltGr symbols.
    /// </summary>
    public static void WaitForModifiersReleased()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 2000)
        {
            if (!IsKeyDown(VK_CONTROL) && !IsKeyDown(VK_MENU) && !IsKeyDown(VK_SHIFT))
                break;
            Thread.Sleep(20);
        }
        Thread.Sleep(60);
    }
}
