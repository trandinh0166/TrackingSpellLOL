using System;
using System.Drawing;

public class SpellInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int BaseCooldown { get; set; } = 300;
    public Image? Icon { get; set; }
    public SpellTimer Timer { get; set; } = new SpellTimer();
}

public class EnemyPlayer
{
    public string ChampionName { get; set; } = "";
    public string Position { get; set; } = ""; // TOP, JUNGLE, MID, AD, SUP
    public int Level { get; set; } = 1;
    public Image? ChampionIcon { get; set; }
    public SpellInfo Spell1 { get; set; } = new SpellInfo();
    public SpellInfo Spell2 { get; set; } = new SpellInfo();

    // Special Top Role Quest Teleport Slot (4th Slot on TOP row)
    public bool TopQuestUnlocked { get; set; } = false;
    public SpellInfo QuestTeleport { get; set; } = new SpellInfo();

    public bool HasCosmicInsight { get; set; } = false;
    public bool HasIonianBoots { get; set; } = false;
}
