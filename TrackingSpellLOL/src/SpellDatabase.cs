using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class SpellDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public int BaseCooldown { get; set; } = 300;
    public string CooldownType { get; set; } = "fixed"; // fixed, contextual, charge
    public int ChargeCount { get; set; } = 1;
    public int RechargeTime { get; set; } = 90;
}

public static class SpellDatabase
{
    private static readonly Dictionary<string, SpellDefinition> _db = new Dictionary<string, SpellDefinition>(StringComparer.OrdinalIgnoreCase);

    static SpellDatabase()
    {
        LoadDatabase();
    }

    public static void LoadDatabase()
    {
        try
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "summoner_spells.json");
            if (!File.Exists(dbPath))
            {
                dbPath = Path.Combine("data", "summoner_spells.json");
            }

            if (File.Exists(dbPath))
            {
                var json = File.ReadAllText(dbPath);
                var list = JsonConvert.DeserializeObject<List<SpellDefinition>>(json);
                if (list != null)
                {
                    _db.Clear();
                    foreach (var item in list)
                    {
                        _db[item.Id] = item;
                        _db[item.DisplayName] = item;
                        _db[item.NormalizedName] = item;
                    }
                }
            }
        }
        catch { }
    }

    public static SpellDefinition GetSpell(string key)
    {
        if (string.IsNullOrEmpty(key)) return GetDefaultSpell("SummonerFlash");

        if (_db.TryGetValue(key, out var def)) return def;

        string lower = key.ToLower();

        if (lower.Contains("flash")) return _db.TryGetValue("SummonerFlash", out var flash) ? flash : GetDefaultSpell("SummonerFlash");
        if (lower.Contains("teleport") || lower.Contains("gate")) return _db.TryGetValue("SummonerTeleport", out var tp) ? tp : GetDefaultSpell("SummonerTeleport");
        if (lower.Contains("ignite") || lower.Contains("dot")) return _db.TryGetValue("SummonerDot", out var dot) ? dot : GetDefaultSpell("SummonerDot");
        if (lower.Contains("ghost") || lower.Contains("haste")) return _db.TryGetValue("SummonerHaste", out var g) ? g : GetDefaultSpell("SummonerHaste");
        if (lower.Contains("heal")) return _db.TryGetValue("SummonerHeal", out var h) ? h : GetDefaultSpell("SummonerHeal");
        if (lower.Contains("exhaust")) return _db.TryGetValue("SummonerExhaust", out var ex) ? ex : GetDefaultSpell("SummonerExhaust");
        if (lower.Contains("barrier")) return _db.TryGetValue("SummonerBarrier", out var b) ? b : GetDefaultSpell("SummonerBarrier");
        if (lower.Contains("cleanse") || lower.Contains("boost")) return _db.TryGetValue("SummonerBoost", out var cl) ? cl : GetDefaultSpell("SummonerBoost");
        if (lower.Contains("smite")) return _db.TryGetValue("SummonerSmite", out var sm) ? sm : GetDefaultSpell("SummonerSmite");

        foreach (var kvp in _db)
        {
            if (key.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        return GetDefaultSpell(key);
    }

    private static SpellDefinition GetDefaultSpell(string key)
    {
        return new SpellDefinition
        {
            Id = key,
            DisplayName = key,
            NormalizedName = key.ToLower(),
            BaseCooldown = key.Contains("Teleport", StringComparison.OrdinalIgnoreCase) ? 360 :
                           key.Contains("Smite", StringComparison.OrdinalIgnoreCase) ? 90 :
                           key.Contains("Dot", StringComparison.OrdinalIgnoreCase) || key.Contains("Ignite", StringComparison.OrdinalIgnoreCase) ? 180 : 300
        };
    }
}
