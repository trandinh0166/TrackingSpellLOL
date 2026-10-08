using System;

public static class TeleportCooldownResolver
{
    // Free Role Quest Teleport base CD = 390s
    public const int FreeRoleQuestBaseCd = 390;

    // Unleashed Teleport CD scales from 300s (Lvl 1) down to 210s (Lvl 18) based on champion level
    public static int CalculateUnleashedTeleportCooldown(int championLevel, bool hasCosmic, bool hasBoots)
    {
        int level = Math.Clamp(championLevel, 1, 18);
        double baseCd = 300.0 - ((300.0 - 210.0) * (level - 1) / 17.0);

        int haste = 0;
        if (hasCosmic) haste += 18;
        if (hasBoots) haste += 12;

        if (haste > 0)
        {
            baseCd = baseCd * (100.0 / (100.0 + haste));
        }

        return (int)Math.Round(baseCd);
    }
}
