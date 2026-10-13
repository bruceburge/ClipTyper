namespace ClipTyper;

/// <summary>
/// The two user preferences, persisted as plain text in
/// %AppData%\ClipTyper\settings.ini. This file is the only thing ClipTyper
/// ever writes; clipboard contents are never stored or logged.
/// </summary>
internal static class Settings
{
    /// <summary>Strip formatting from copied text. Optional feature, off by default.</summary>
    public static bool StripEnabled { get; set; }

    /// <summary>Clear the clipboard after a successful type. Off by default.</summary>
    public static bool AutoClear { get; set; }

    private static string SettingsPath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClipTyper");
            return Path.Combine(dir, "settings.ini");
        }
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return;

            foreach (string line in File.ReadAllLines(SettingsPath))
            {
                string t = line.Trim();
                if (t.Equals("StripEnabled=1", StringComparison.OrdinalIgnoreCase)) StripEnabled = true;
                else if (t.Equals("StripEnabled=0", StringComparison.OrdinalIgnoreCase)) StripEnabled = false;
                else if (t.Equals("AutoClear=1", StringComparison.OrdinalIgnoreCase)) AutoClear = true;
                else if (t.Equals("AutoClear=0", StringComparison.OrdinalIgnoreCase)) AutoClear = false;
            }
        }
        catch
        {
            // Defaults stand.
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath,
                "StripEnabled=" + (StripEnabled ? "1" : "0") + Environment.NewLine +
                "AutoClear=" + (AutoClear ? "1" : "0") + Environment.NewLine);
        }
        catch
        {
            // Non-fatal: preferences just will not persist this time.
        }
    }
}
