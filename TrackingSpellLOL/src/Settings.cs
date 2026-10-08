using System;
using System.IO;
using Newtonsoft.Json;

public class Settings
{
    public int X { get; set; } = 100;
    public int Y { get; set; } = 100;
    public int Width { get; set; } = 140;
    public int Height { get; set; } = 180;
    public int Opacity { get; set; } = 85;
    public bool ClickThrough { get; set; } = false;
    public bool PlaySoundOnFinish { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool ShowOnlyWhenInGame { get; set; } = true;

    // V2 Settings
    public string DisplayTeam { get; set; } = "Enemy Team"; // Default to Enemy Team only
    public float UIScale { get; set; } = 1.0f; // Scale factor (0.8, 1.0, 1.2, 1.4)
    public bool CompactMode { get; set; } = true;
    public bool IsLocked { get; set; } = false; // Lock HUD movement & scaling

    private static readonly string ConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "TrackingSpellLOL", "config.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var s = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(ConfigPath));
                if (s != null) return s;
            }
        }
        catch { }
        return new Settings();
    }

    public static void Save(Settings s)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(s, Formatting.Indented));
        }
        catch { }
    }
}