using System.Reflection;
using System.Runtime.InteropServices;

namespace ClipTyper;

/// <summary>
/// The hidden host form. Never shown; it owns the tray icon and menu, the
/// global hotkeys, and the clipboard-format listener. All real work lives in
/// <see cref="Typer"/> and <see cref="ClipboardStripper"/>.
/// </summary>
public class MainForm : Form
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;

    private const int HOTKEY_TYPE = 1;
    private const int HOTKEY_QUIT = 2;
    private const int WM_HOTKEY = 0x0312;
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    private readonly NotifyIcon _tray;

    /// <summary>False when another app already owns Ctrl+Alt+V or Ctrl+Alt+Q.</summary>
    public bool HotkeysOk { get; }

    public MainForm()
    {
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Text = "ClipTyper";

        var stripItem = new ToolStripMenuItem("Strip formatting from copied text")
        {
            CheckOnClick = true,
            Checked = Settings.StripEnabled
        };
        stripItem.CheckedChanged += (_, _) =>
        {
            Settings.StripEnabled = stripItem.Checked;
            Settings.Save();
        };

        var autoClearItem = new ToolStripMenuItem("Clear clipboard after typing")
        {
            CheckOnClick = true,
            Checked = Settings.AutoClear
        };
        autoClearItem.CheckedChanged += (_, _) =>
        {
            Settings.AutoClear = autoClearItem.Checked;
            Settings.Save();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Type clipboard now (3s to click target)", null, (_, _) => TypeFromMenu());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(stripItem);
        menu.Items.Add(autoClearItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit (Ctrl+Alt+Q)", null, (_, _) => Close());

        _tray = new NotifyIcon
        {
            Text = "ClipTyper: Ctrl+Alt+V types the clipboard",
            Icon = LoadTrayIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };

        bool okType = RegisterHotKey(Handle, HOTKEY_TYPE, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.V);
        bool okQuit = RegisterHotKey(Handle, HOTKEY_QUIT, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.Q);
        HotkeysOk = okType && okQuit;
        AddClipboardFormatListener(Handle);

        Load += (_, _) => Hide();
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            Stream? stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("ClipTyper.app.ico");
            if (stream != null)
                return new Icon(stream);
        }
        catch
        {
            // Fall through to the generic icon.
        }
        return SystemIcons.Application;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE)
        {
            if (Settings.StripEnabled && !ClipboardStripper.IsWriting)
                ClipboardStripper.TryStrip();
        }
        else if (m.Msg == WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (id == HOTKEY_TYPE) TypeFromHotkey();
            else if (id == HOTKEY_QUIT) Close();
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        RemoveClipboardFormatListener(Handle);
        UnregisterHotKey(Handle, HOTKEY_TYPE);
        UnregisterHotKey(Handle, HOTKEY_QUIT);
        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosed(e);
    }

    private void Balloon(string text)
    {
        try { _tray.ShowBalloonTip(1500, "ClipTyper", text, ToolTipIcon.Info); }
        catch { /* Notifications may be off; not fatal. */ }
    }

    private static string? ReadClipboardText()
    {
        try
        {
            if (Clipboard.ContainsText())
                return Clipboard.GetText();
        }
        catch
        {
            // Clipboard momentarily locked by another app.
        }
        return null;
    }

    private void FinishTyping(string typedText, bool success, int error)
    {
        // Runs on the UI thread.
        if (!success)
        {
            Balloon($"Typing failed (SendInput error {error}). Clipboard left as-is.");
            return;
        }
        if (Settings.AutoClear)
        {
            try
            {
                if (Clipboard.ContainsText() && Clipboard.GetText() == typedText)
                    Clipboard.Clear();
            }
            catch { /* Clipboard busy; leave it. */ }
        }
        Balloon($"Typed {typedText.Length} characters.");
    }

    private void TypeFromHotkey()
    {
        string? text = ReadClipboardText();
        if (string.IsNullOrEmpty(text))
        {
            Balloon("Clipboard has no text.");
            return;
        }
        Typer.WaitForModifiersReleased();
        bool ok = Typer.TypeText(text, out int error);
        FinishTyping(text, ok, error);
    }

    private void TypeFromMenu()
    {
        string? text = ReadClipboardText();
        if (string.IsNullOrEmpty(text))
        {
            Balloon("Clipboard has no text.");
            return;
        }
        Balloon("Click the target field. Typing in 3 seconds.");
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Thread.Sleep(3000);
            bool ok = Typer.TypeText(text, out int error);
            BeginInvoke(new Action(() => FinishTyping(text, ok, error)));
        });
    }
}
