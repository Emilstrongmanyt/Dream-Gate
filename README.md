# Kindling (The Ember Exchange)

Original-IP 8-player auto-battler. Captains recruit Kindled at the Ember Exchange
and fight in the Ash Ring. Choruses in this slice: Undead, Beast, Humanoid,
Dragon, Spirit, plus stall spells. This repository currently ships the
deterministic simulation library, catalog, and a headless 8-bot match.

## Build and test

```bash
dotnet test
```

Runs `Kindling.Sim.Tests` (net8, xUnit) against `Kindling.Sim` (netstandard2.1).

## Headless 8-bot match

```bash
dotnet run --project tools/HeadlessAlpha
```

Loads `content/`, fills all eight seats with heuristic bots, prints round-by-round
Wick and place, and exits 0 when places `1..8` are assigned.

## Layout

- `sim/Kindling.Sim` — UPM `com.kindling.sim`, no UnityEngine. Includes `MatchSession` (protocol v1, snapshots, server timer). Sockets/Redis not wired yet.
- `sim/Kindling.Sim.Tests` — goldens and match-loop tests
- `content/` — YAML catalog (canonical) + JSON schemas
- `tools/HeadlessAlpha` — console runner
- `docs/DESIGN.md` — source of truth
- `docs/IP_GUARDRAILS.md` — competitor-name ban list

## Unity client (offline 1v7 alpha)

Unity **6000.5.2f1**. Placeholder Hearthstone-like cards; real art later.

1. Open `client/` in the Unity Hub (editor `6000.5.2f1`).
2. Press Play. Empty seats fill with bots. Choose a Captain, buy from the stall, Ready to fight.
   Recruit timer auto-starts combat (15s round 1, up to 60s from round 5). Ash Ring playback
   caps at 12s then the next recruit starts. Tap a card for tribe and keywords; authored names
   and effect text wait on content.

Catalog is loaded from `../content` (walk-up from `Assets`).

## Decisions locked 2026-08-24

- Name: Kindling / The Ember Exchange (this workspace folder)
- Unity: 6000.5.2f1 (installed)
- Art: Hearthstone-like presentation; import later
- Empty lobby seats filled with bots
- Landscape, English-only
- Cloud later: Fly.io
- No public replays (14-day private logs)
- Broker: silent + text
- Legal entity: later
- Casual 4th Captain offer: post-alpha

Match server / Ranked / IAP are not this slice.
