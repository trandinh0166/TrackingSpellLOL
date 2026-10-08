# API Architecture

```
Riot Game Client (Local 127.0.0.1:2999)
  │ (HTTPS GET /liveclientdata/allgamedata)
  ▼
LiveClientApi
  │
  ├──────► DataDragon Version Check & Asset Downloader
  │
  ▼
GameSessionManager (Manages NOT_IN_GAME -> CONNECTING -> SYNCING -> IN_GAME)
  │
  ▼
PlayerData (EnemyPlayer / SpellInfo)
  │
  ▼
SpellDatabase (Loads data/summoner_spells.json)
  │
  ▼
CooldownEngine (Stopwatch-based Monotonic Timers)
  │
  ▼
Overlay UI (3 Columns x 5 Player Rows Win32 Transparent Form)
```
