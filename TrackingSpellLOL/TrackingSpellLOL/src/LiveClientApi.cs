using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public static class LiveClientApi
{
    private static readonly HttpClientHandler _handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
        {
            // Strict security: Only trust certificate if request is specifically directed to 127.0.0.1:2999
            if (sender is HttpRequestMessage request && request.RequestUri != null)
            {
                if (request.RequestUri.Host == "127.0.0.1" && request.RequestUri.Port == 2999)
                {
                    return true;
                }
            }
            return sslPolicyErrors == SslPolicyErrors.None;
        }
    };

    private static readonly HttpClient _http = new HttpClient(_handler)
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TrackingSpellLOL", "cache");

    private static string _ddragonVersion = "14.1.1";

    public static async Task<string> GetLatestVersionAsync()
    {
        try
        {
            var json = await _http.GetStringAsync("https://ddragon.leagueoflegends.com/api/versions.json");
            var arr = JArray.Parse(json);
            _ddragonVersion = arr[0].ToString();
        }
        catch { }
        return _ddragonVersion;
    }

    public static async Task<List<EnemyPlayer>?> GetTeamDataAsync(string displayTeamSetting)
    {
        try
        {
            string url = "https://127.0.0.1:2999/liveclientdata/allgamedata";
            var response = await _http.GetStringAsync(url);
            var root = JObject.Parse(response);

            var activePlayerName = root["activePlayer"]?["summonerName"]?.ToString() ?? "";
            var allPlayers = root["allPlayers"] as JArray;
            if (allPlayers == null) return null;

            string activeTeam = "ORDER";
            foreach (var p in allPlayers)
            {
                if (p["summonerName"]?.ToString() == activePlayerName)
                {
                    activeTeam = p["team"]?.ToString() ?? "ORDER";
                    break;
                }
            }

            string targetTeam = displayTeamSetting == "Enemy Team"
                ? (activeTeam == "ORDER" ? "CHAOS" : "ORDER")
                : activeTeam;

            var resultPlayers = new List<EnemyPlayer>();
            await GetLatestVersionAsync();

            var positionOrder = new List<string> { "TOP", "JUNGLE", "MIDDLE", "BOTTOM", "UTILITY" };
            var unmapped = new List<EnemyPlayer>();

            foreach (var p in allPlayers)
            {
                string pTeam = p["team"]?.ToString() ?? "";
                if (displayTeamSetting != "Both" && pTeam != targetTeam) continue;

                var player = new EnemyPlayer
                {
                    ChampionName = p["championName"]?.ToString() ?? "UNKNOWN",
                    Position = MapPosition(p["position"]?.ToString()),
                    Level = p["level"]?.Value<int>() ?? 1
                };

                player.ChampionIcon = await GetChampionIconAsync(player.ChampionName);

                var spell1Raw = p["summonerSpells"]?["summonerSpellOne"];
                var spell2Raw = p["summonerSpells"]?["summonerSpellTwo"];

                player.Spell1 = ParseSpell(spell1Raw);
                player.Spell2 = ParseSpell(spell2Raw);

                player.Spell1.Icon = await GetSpellIconAsync(player.Spell1.Name);
                player.Spell2.Icon = await GetSpellIconAsync(player.Spell2.Name);

                // Runes & Items
                var generalRunes = p["runes"]?["generalRunes"] as JArray;
                if (generalRunes != null)
                {
                    foreach (var r in generalRunes)
                    {
                        if (r["id"]?.ToString() == "8347" || r["displayName"]?.ToString()?.Contains("Cosmic") == true)
                        {
                            player.HasCosmicInsight = true;
                        }
                    }
                }

                var items = p["items"] as JArray;
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (item["itemID"]?.Value<int>() == 3158 || item["displayName"]?.ToString()?.Contains("Ionian") == true)
                        {
                            player.HasIonianBoots = true;
                        }
                    }
                }

                resultPlayers.Add(player);
            }

            // Enforce 5-Player Slot Ordering: TOP -> JUNGLE -> MID -> AD -> SUP
            var orderedList = new List<EnemyPlayer>();
            string[] standardLanes = { "TOP", "JUNGLE", "MID", "AD", "SUP" };

            foreach (var lane in standardLanes)
            {
                var match = resultPlayers.Find(x => x.Position == lane);
                if (match != null)
                {
                    orderedList.Add(match);
                    resultPlayers.Remove(match);
                }
                else
                {
                    orderedList.Add(null!);
                }
            }

            // Fill empty slots with remaining players before inserting placeholders
            for (int i = 0; i < 5; i++)
            {
                if (orderedList[i] == null)
                {
                    if (resultPlayers.Count > 0)
                    {
                        var remaining = resultPlayers[0];
                        remaining.Position = standardLanes[i];
                        orderedList[i] = remaining;
                        resultPlayers.RemoveAt(0);
                    }
                    else
                    {
                        orderedList[i] = new EnemyPlayer { ChampionName = "UNKNOWN", Position = standardLanes[i] };
                    }
                }
            }

            return orderedList;
        }
        catch
        {
            return null;
        }
    }

    private static string MapPosition(string? rawPos)
    {
        if (string.IsNullOrEmpty(rawPos)) return "UNKNOWN";
        string u = rawPos.ToUpper();
        if (u.Contains("TOP")) return "TOP";
        if (u.Contains("JUNGLE")) return "JUNGLE";
        if (u.Contains("MID") || u.Contains("MIDDLE")) return "MID";
        if (u.Contains("BOT") || u.Contains("BOTTOM")) return "AD";
        if (u.Contains("UTILITY") || u.Contains("SUP")) return "SUP";
        return "UNKNOWN";
    }

    private static SpellInfo ParseSpell(JToken? spellToken)
    {
        var info = new SpellInfo();
        if (spellToken == null) return info;

        string displayName = spellToken["displayName"]?.ToString() ?? "";
        string rawDisplayName = spellToken["rawDisplayName"]?.ToString() ?? "";
        string rawDescription = spellToken["rawDescription"]?.ToString() ?? "";

        info.DisplayName = displayName;

        SpellDefinition def = SpellDatabase.GetSpell(displayName);
        if (def.Id == displayName || string.IsNullOrEmpty(def.Id))
        {
            def = SpellDatabase.GetSpell(rawDisplayName);
            if (def.Id == rawDisplayName || string.IsNullOrEmpty(def.Id))
            {
                def = SpellDatabase.GetSpell(rawDescription);
            }
        }

        info.Name = def.Id;
        info.BaseCooldown = def.BaseCooldown;

        return info;
    }

    public static async Task<Image?> GetChampionIconAsync(string champName)
    {
        if (string.IsNullOrEmpty(champName) || champName == "UNKNOWN") return null;
        string fileName = $"{champName}.png";
        string filePath = Path.Combine(CacheDir, "champions", fileName);

        if (File.Exists(filePath))
        {
            try { return Image.FromFile(filePath); } catch { }
        }

        try
        {
            Directory.CreateDirectory(Path.Combine(CacheDir, "champions"));
            string url = $"https://ddragon.leagueoflegends.com/cdn/{_ddragonVersion}/img/champion/{champName}.png";
            var bytes = await _http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(filePath, bytes);
            return Image.FromFile(filePath);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<Image?> GetSpellIconAsync(string spellName)
    {
        if (string.IsNullOrEmpty(spellName)) return null;
        string fileName = $"{spellName}.png";
        string filePath = Path.Combine(CacheDir, "summoner", fileName);

        if (File.Exists(filePath))
        {
            try { return Image.FromFile(filePath); } catch { }
        }

        try
        {
            Directory.CreateDirectory(Path.Combine(CacheDir, "summoner"));
            string url = $"https://ddragon.leagueoflegends.com/cdn/{_ddragonVersion}/img/spell/{spellName}.png";
            var bytes = await _http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(filePath, bytes);
            return Image.FromFile(filePath);
        }
        catch
        {
            return null;
        }
    }

    public static int CalculateCooldown(int baseCd, bool hasCosmic, bool hasBoots)
    {
        int haste = 0;
        if (hasCosmic) haste += 18;
        if (hasBoots) haste += 12;

        if (haste == 0) return baseCd;
        double cd = baseCd * (100.0 / (100.0 + haste));
        return (int)Math.Round(cd);
    }
}
