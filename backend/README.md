# Dream Gate Ranked Backend

Cloud stack for rated PvP accounts, matchmaking, stats, and match coordination.

## Components

| Component | Purpose |
|-----------|---------|
| `supabase/migrations/001_ranked_pvp.sql` | Profiles, queue, matches, history, achievements |
| `supabase/functions/matchmaking` | 30s queue, MMR widening, bot fill to 8 players |
| `supabase/functions/apply-match-result` | Server-side MMR, streaks, achievements |
| `match-server/` | Lobby coordination server (HTTP join + WebSocket scaffold) |

## Setup

### 1. Supabase project

1. Create a Supabase project.
2. Run the SQL migration in the SQL editor.
3. Deploy edge functions:

```bash
supabase functions deploy matchmaking --no-verify-jwt
supabase functions deploy apply-match-result --no-verify-jwt
```

4. Set function secrets:

```bash
supabase secrets set MATCH_SERVER_URL=https://your-match-server.example.com
```

### 2. Unity client

1. Open `Assets/Resources/BackendSettings` (create via **Dream Gate → Backend Settings** if missing).
2. Enable **Use Cloud Backend**.
3. Fill in:
   - Supabase URL
   - Supabase anon key
   - Matchmaking function URL
   - Apply match result function URL
   - Match server WebSocket/HTTP URL (optional until multi-human sim is live)

When cloud backend is disabled, the game keeps the existing local `PlayerPrefs` auth and `LocalMatchmakingService`.

### 3. Match server (optional for now)

```bash
cd backend/match-server
npm install
npm start
```

Expose port `8787` and set `MATCH_SERVER_URL` in Supabase secrets.

## Rated flow

1. Player signs in through Supabase Auth.
2. Rated lobby uses `BackendMatchmakingService`.
3. Matchmaking waits up to **30 seconds**, widening MMR range over time.
4. If fewer than 8 humans are found, remaining slots are filled with bots.
5. Match starts with shared `lobbyId`, `matchSeed`, and slot roster.
6. Client runs the match locally today; multi-human authoritative simulation will connect through `RemoteMatchClient`.
7. Match end posts to `apply-match-result` for server-side stats and achievements.

## Next step: authoritative PvP

The match server currently coordinates lobby membership only. The next milestone is to move `MatchManager` simulation server-side and stream recruit/combat state to clients through WebSocket messages.