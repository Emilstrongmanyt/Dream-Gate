# Dream Gate Ranked Backend

Cloud stack for rated PvP accounts, matchmaking, stats, and **authoritative match simulation**.

## Components

| Component | Purpose |
|-----------|---------|
| `supabase/migrations/001_ranked_pvp.sql` | Profiles, queue, matches, history, achievements |
| `supabase/functions/matchmaking` | 30s queue, MMR widening, bot fill to 8 players |
| `supabase/functions/apply-match-result` | Server-side MMR, streaks, achievements |
| `DreamGate.MatchServer/` | **Authoritative** .NET 8 match server (shared simulation code) |
| `match-server/` | Legacy Node lobby scaffold (optional) |

## Quick start

### 1. Supabase

```powershell
cd backend
Copy-Item .env.example .env
# Fill in SUPABASE_URL, keys, PROJECT_REF, MATCH_SERVER_URL

# Link project (one time):
supabase link --project-ref <PROJECT_REF>

# Apply schema + deploy functions:
.\scripts\setup-supabase.ps1
.\scripts\deploy-functions.ps1
```

Local dev:

```powershell
.\scripts\setup-supabase.ps1 -Local
```

### 2. Authoritative match server

```powershell
.\scripts\start-match-server.ps1
```

Runs on `http://localhost:8787` by default.

Endpoints:
- `POST /match/join` — create/join lobby, returns authoritative snapshot
- `GET /match/state?lobbyId=&playerId=` — poll match state
- `POST /match/action` — recruit-phase player action
- `POST /match/complete-combat` — advance after combat playback

Set `MATCH_SERVER_URL` in Supabase secrets and Unity `BackendSettings`.

### 3. Unity client

Open `Assets/Resources/BackendSettings`:

| Field | Example |
|-------|---------|
| Use Cloud Backend | ✓ |
| Supabase URL | `https://xyz.supabase.co` |
| Supabase Anon Key | your anon key |
| Matchmaking Function URL | `https://xyz.supabase.co/functions/v1/matchmaking` |
| Apply Match Result Function URL | `https://xyz.supabase.co/functions/v1/apply-match-result` |
| Match Server WebSocket URL | `http://localhost:8787` |

When cloud backend is disabled, the game uses local `PlayerPrefs` auth and offline matchmaking.

## Rated flow

1. Player signs in via Supabase Auth.
2. Rated lobby queues through `BackendMatchmakingService` (30s max, bot fill).
3. Match starts with shared `lobbyId`, `matchSeed`, and slot roster.
4. Client connects to **DreamGate.MatchServer** via `RemoteMatchClient`.
5. Server runs `MatchManager` authoritatively; clients send actions and receive snapshots.
6. Match end posts to `apply-match-result` for cloud stats and achievements.

## Architecture

```
Unity Client ──► Supabase Auth / Profiles / Matchmaking
      │
      └── HTTP ──► DreamGate.MatchServer (.NET)
                      └── MatchManager (shared C# simulation)
```

Practice mode stays fully local. Rated mode uses the match server when `MatchServerUrl` is configured.