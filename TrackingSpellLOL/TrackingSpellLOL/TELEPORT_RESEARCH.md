# TELEPORT & TOP ROLE QUEST RESEARCH (PATCH 2026 / SEASON 14+)

## 1. Top Role Quest Mechanics
- In Season 2024/2026, Top Laners receive a **Free Teleport** upon completing their role quest.
- This Teleport is **in addition** to their 2 starting Summoner Spells if they did not pick Teleport.
- If the Top Laner already picked Teleport as one of their 2 starting spells, completing the quest upgrades their existing Teleport to **Unleashed Teleport** instead of adding a second Teleport.

## 2. Teleport Cooldown Rules & Scaling
- **Free Teleport (Role Quest Reward)**: Base 390 seconds.
- **Unleashed Teleport Cooldown**: Scales dynamically based on **Champion Level** (Levels 1 to 18), ranging from **300 seconds down to 210 seconds** at Level 18.
- **Scaling Formula**:
  - `Cooldown = 300 - ( (300 - 210) * (Level - 1) / 17 )`
  - Level 1: 300s
  - Level 6: ~274s
  - Level 11: ~247s
  - Level 18: 210s

## 3. Live Client Data API Limitations & Quest State Detection
- The local Live Client API (`https://127.0.0.1:2999/liveclientdata/allgamedata`) provides player `level`, `summonerSpells`, and `position`.
- However, the specific Top Role Quest completion event is **NOT** exposed directly as a clean boolean field in the public `/playerlist` endpoint.
- **Compliance & Fallback Strategy**:
  - Per Riot rules: No memory reading, no DLL injection, no Vanguard bypass.
  - **Auto-Detect Strategy**: If the Top player started with Teleport as Spell 1 or Spell 2, it automatically upgrades to Unleashed Teleport when their level/game progress reaches the upgrade threshold.
  - **Manual Fallback**: Provide an option in the System Tray menu ("Unlock Top Role Quest Teleport / Toggle Top Quest TP") so the user can manually unlock the 4th Teleport icon slot on the TOP row at any time if they didn't start with Teleport.

## 4. Live API Data Usage
- `player.level`: Used for calculating dynamic level-scaled Teleport cooldown.
- `gamestats.gameTime`: Used for match duration tracking and log synchronization.
