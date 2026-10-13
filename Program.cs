namespace ClipTyper;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Settings.Load();
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(true, "ClipTyper.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("ClipTyper is already running. Look for it in the system tray.",
                "ClipTyper", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new MainForm();
        if (!form.HotkeysOk)
        {
            MessageBox.Show(
                "Ctrl+Alt+V or Ctrl+Alt+Q is already in use by another app (are Clipboard Typer or ClipStrip still running?), so the typing hotkeys were not registered.",
                "ClipTyper", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Application.Run(form);
    }
}
