# Kindling (The Ember Exchange)

Original-IP 8-player auto-battler. Captains recruit Kindled at the Ember Exchange
and fight in the Ash Ring. Choruses in this slice: Undead, Beast, Humanoid,
Dragon, Spirit, plus stall spells. This repository ships the deterministic sim,
YAML catalog, Unity Practice 1v7 client, and a Casual 1v7 match host.

Working plan: `docs/ROADMAP.md`. Rules/architecture: `docs/DESIGN.md`.

## Build and test

```bash
dotnet test
```

Runs `Kindling.Sim.Tests` (net8, xUnit) against `Kindling.Sim` (netstandard2.1).

## Headless 8-bot match

```bash
dotnet run --project tools/HeadlessAlpha
```

## Local match host (Phase 3)

```bash
dotnet run --project tools/MatchHost
```

Listens on `http://127.0.0.1:5080/` (or `PORT` in Docker/Fly).

- `POST /v1/auth/device` — anonymous account + HMAC token
- `POST /v1/queue` — 1v7 Casual (Bearer token optional, 1 req / 2s)
- `WS /v1/match?id=&seat=&token=` — protocol v1
- Checkpoints: file store, or Redis when `REDIS_URL` is set

Unity stays offline unless `KINDLING_HOST` or PlayerPrefs `kindling.host` is set (e.g. `http://127.0.0.1:5080`).

```bash
docker compose -f infra/docker-compose.yml up --build matchhost redis
```

Loads `content/`, fills all eight seats with heuristic bots, prints round-by-round
Wick and place, and exits 0 when places `1..8` are assigned.

## Layout

- `sim/Kindling.Sim` — UPM `com.kindling.sim`, no UnityEngine. Includes `MatchSession` (protocol v1, snapshots, server timer).
- `sim/Kindling.Sim.Tests` — goldens and match-loop tests
- `content/` — YAML catalog (canonical) + JSON schemas
- `tools/HeadlessAlpha` — console runner
- `docs/DESIGN.md` — rules and architecture
- `docs/ROADMAP.md` — what to build next
- `docs/IP_GUARDRAILS.md` — competitor-name ban list

## Unity client (offline 1v7 alpha)

Unity **6000.5.2f1**. Chorus color blocks until Captain/Kindled faces land.

1. Open `client/` in the Unity Hub (editor `6000.5.2f1`).
2. Press Play. Empty seats fill with bots. Choose a Captain, buy from the stall, Ready to fight.
   Recruit timer auto-starts combat (15s round 1, up to 60s from round 5). Ash Ring playback
   caps at 12s then the next recruit starts. Tap a card for Chorus and keywords.

Catalog is loaded from `../content` (walk-up from `Assets`).

## Decisions locked

- Name: Kindling / The Ember Exchange
- Unity: 6000.5.2f1
- Presentation: dusk marketplace (Stone chrome, original menu/board art; Captain/Kindled faces still pending)
- Practice and Casual fill empty seats with bots
- Landscape, English-only
- 16 Captains, 4 unique-claim offers every current mode
- Cloud: Fly.io (Casual host exists; Ranked is flagged off)
- No public replays (14-day private logs)
- Broker: silent + text
- Legal entity: later
- IAP / Ranked public: post-closed-alpha (see `docs/ROADMAP.md`)
