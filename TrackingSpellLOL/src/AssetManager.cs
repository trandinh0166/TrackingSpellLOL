using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Drawing;
using Newtonsoft.Json.Linq;

public static class AssetManager
{
    private static readonly string BasePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "TrackingSpellLOL", "assets", "flash");

    private static readonly string IconPath = Path.Combine(BasePath, "SummonerFlash.png");
    private static readonly HttpClient _http = new();

    public static async Task<Image?> GetFlashIconAsync()
    {
        if (File.Exists(IconPath))
            return Image.FromFile(IconPath);

        try
        {
            var versionsJson = await _http.GetStringAsync(
                "https://ddragon.leagueoflegends.com/api/versions.json");
            var latestVersion = JArray.Parse(versionsJson)[0].ToString();

            var iconUrl = $"https://ddragon.leagueoflegends.com/cdn/{latestVersion}/img/spell/SummonerFlash.png";

            var bytes = await _http.GetByteArrayAsync(iconUrl);
            Directory.CreateDirectory(BasePath);
            await File.WriteAllBytesAsync(IconPath, bytes);
            return Image.FromFile(IconPath);
        }
        catch
        {
            return null; // UI will fall back to text "FLASH"
        }
    }
}