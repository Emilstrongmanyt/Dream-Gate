# Kindling — development roadmap

Review date: 2026-09-16. Repo HEAD at write time: `6a44d9d`.
This is the **working plan** for what to build next. `docs/DESIGN.md` remains the rules/architecture source of truth. Where this file and DESIGN disagree, this file records **what the repo actually does** and the recommended lock.

Calendar **done** is still **closed alpha** (Casual, one region, 50–200 players), not store launch.

---

## 1. Where the game actually is

The vertical slice is real: Practice 1v7 on device/editor, YAML catalog Depth 1–6, deterministic sim, Ash Ring log playback, Casual 1v7 host, TestFlight identity `com.solodreams.dreamgate`.

| DESIGN phase | Status |
|---|---|
| 0 Repo & editor | **Done** |
| 1 Offline 1v7 + device gate | **Playable in editor / TestFlight path.** Confirm a full match on a phone is still the gate. |
| 2 Content + tests | **Catalog complete. Tests short** (~79 Facts, not ≥100 goldens, no combat fuzz). |
| 3 Online stack | **Partial.** Instant Casual 1v7, checkpoints, reconnect-on-live-node. Not 8-human MM. Fly/Docker WS URL is broken. |
| 4 Client production | **Early.** Stone chrome + original backdrops + CFXR. No faces, mute, weak inspect, economy labels lie. |
| 5 Closed alpha | **Not started** |
| 6 Store | **Out of scope** |

### Inventory (locked counts)

| Slice | Count |
|---|---|
| Captains | 16 |
| Shop Kindled | 48 (Depth 10/10/8/8/6/6) |
| Stall spells | 7 |
| Tokens | 7 |
| Choruses | 5 (Humanoid 25, Undead 8, Beast 8, Spirit 4, Dragon 3) |
| Captain offers | **4 every mode**, unique lobby claim |

### What is strong

- Shared `Kindling.Sim` (netstandard2.1 UPM), integer stats, seeded PCG streams, YAML + closed effect language.
- Recruit economy (GrantEmbers, Hold, Upgrade, buy-always-to-hand, Awaken/Glimpse).
- Combat algorithm (Kindle, Ward, Aegis→Venom, DrainDeaths, Afterglow, Ring damage, Berger pairings).
- IP grep + glossary in client copy (Stall, Wick, Embers, Market Depth, Ash Ring).
- Headless 8-bot matches finish with places 1–8.
- iOS TestFlight workflow exists.

### What is blocking “this is a good auto-battler”

1. **The shop is unlearnable.** Kindled have no rules text on the card or inspect (`“Text comes later.”`). Arrival / Echo / Kindle never print on the face.
2. **Presentation is color blocks.** 16 Captain portraits and 48+ faces are still the product hole.
3. **Sim bugs will poison any balance read** (Echoist, Widow edict, Glicko on bots).
4. **Online is not production-reachable** (queue returns `ws://+:PORT/...`; Fly auto-stop kills live matches).
5. **Table is mute** and economy buttons do not show cost/used state.

---

## 2. Design drift to lock (do this on paper, not as new content)

Reconcile `DESIGN.md` so it matches shipped code, then stop drifting:

| Topic | DESIGN still says | Repo does | Recommendation |
|---|---|---|---|
| Captains | §10: 12; §2: 16 | 16 | **Lock 16.** |
| Offers | Ranked always 3 (K8) | 4 every mode | **Keep 4 for Practice/Casual.** Ranked 3 only when Ranked exists. |
| Unique Captains | K19 duplicates allowed | Unique claim | **Lock unique.** |
| Glicko | Ranked only | Casual `Finish` always writes | **Casual must not write The Crown.** |
| Tutorial | 4-round scripted | Wick floor + 4 help cards | Keep overlay; do not pretend it is §15. |
| Server | `server/Kindling.Api` etc. | `tools/MatchHost` monolith | Document the monolith until alpha load requires a split. |
| URP + Input System | K9 | Built-in RP + StandaloneInputModule | Defer. Not the playability hole. |

Do **not** invent Kindled/Captain names to pad Dragon/Spirit. Do **not** buy tavern kits, 3D dining tables, or random hero packs.

---

## 3. Sequenced work (build in this order)

Cut order if thin: IAP → Ranked → extra catalog → tutorial rewrite → cosmetics. **Do not cut:** sim goldens, reconnect, checkpoint, authority, pool invariant, device-play gate.

### Slice A — Make Practice honest (1–2 weeks)

Highest playtest ROI. No new content.

1. **Sim correctness**
   - Echoist: `SetEchoTimesBonus` must be **+1 per Echoist**, not `bonus × boardCount`. Golden: one Echoist + one Echo death → Echo fires twice, not `1+N`.
   - Widow Ash edict: one Echo+Summon, not `GrantKeyword Echo` plus a second Echo row.
   - Ghost source: among same-round deaths pick **worst place**, not last-appended.
   - `Hold` / `Reorder` must require Recruit.
   - Stop `Glicko2.ApplyPlaces` on Practice/Casual `Finish`.
2. **Readable shop without waiting on art**
   - Author YAML `text` for Kindled (or derive a one-line from the closed effect language).
   - Print it on inspect; print Arrival / Echo / Kindle on the card face next to Ward/Venom.
   - Enlarge inspect (tap-hold or a bigger pane). Kill the string `Text comes later.`
3. **Economy HUD truth** (`OfflineMatchApp.Refresh`)
   - Upgrade shows **cost** (`Upgrade  5` / `MAX DEPTH`), not only `D#`.
   - Reroll shows **0 or 1** (Vesper free reroll).
   - Wick/Ember **numbers on the bars**.
   - Hold latched; Edict disabled after use.
4. **First-run help** that does not sit on the warband. Corner coach-mark. Tap-to-buy / tap-to-play in addition to drag.
5. **Ash Ring timing:** scale step delay so a typical log **fits in 12s at 1×**; default 1×; keep Skip.

Exit: a designer can play a Practice match and know what a stall card does, what Upgrade costs, and watch combat without a time-skip.

### Slice B — Tests the catalog already needs (parallel with A)

Phase 2 was “content complete + tests.” Content is done; tests are not.

1. Missing §6.5 goldens: Throne + Spark Bit Kindle (aura gone before first attack); Night `SummonFill` on a 7-board; Buy+Reroll checkpoint → next stall **equals** control; mutual Echo; Investor StartOfRecruit.
2. Pool identity fixture (shop copies + owned + 2×Awaken + latch-destroyed).
3. Combat fuzz: 100 seeded `Run` no throw, DrainDeaths reentry 0.
4. `tools/Catalog.Validate`: schema `additionalProperties: false`, unique ids.
5. Soak 50 headless matches in CI (today: 8).

Exit: `dotnet test` covers the bugs in Slice A and the DESIGN golden list. **No catalog expansion until this is green.**

### Slice C — Presentation (art is in progress)

Follow `docs/ASSETS.md`. Faces before shirts.

| P | Asset | Count | Notes |
|---|---|---|---|
| 1 | Captain portraits | 16 | Same size, silhouette-first, pick + right rail |
| 2 | Kindled / spell / token faces | 48 + 7 + 7 | ~130px, landscape-readable |
| 3 | UI SFX | ~12 | buy, sell, reroll, play, edict, timer, ready, hit, death, win, lose, tap |
| 4 | Changa Bold/Medium | 2 TTF | OFL already next to Stone |
| 5 | Card face template | 1 + 5 Chorus tints | After faces exist |
| 6 | Loop bed + combat sting | 2 | After SFX |

Also: Kindling app icon (iOS slots are empty); rename leftover Dream Gate entitlements when the bundle id is truly Kindling.

Bot floor (needed before any winrate read): Sell, Hold, Reorder Wards left, skip empty-board Edict.

### Slice D — Casual that survives a phone lock

Do this before inviting anyone to Queue.

1. Queue `ws` URL from public host (`wss://…`), never `ws://+:PORT/`.
2. Fly: Redis + Postgres + `KINDLING_PEPPER`; **`min_machines_running = 1`**; disable auto-stop for alpha.
3. Checkpoint must restore captain offers, glimpse, ratings, combatSeq. Golden: Buy+Reroll → new process → next stall identical.
4. Opaque resume tokens (not `{matchId}-{seat}`).
5. Corrupt/missing blob → match abort, **no** rating write, `match_node_crash_abort`.
6. Net client: WebSocket continuation frames, send queue, do not show a local loop before Welcome.
7. Hide hub “Wick rating” until it is real Glicko on Ranked.

Exit: lock the phone mid-recruit, reopen, stall and timer match the server.

### Slice E — Closed alpha (Phase 5)

Only after A–D.

- One region, Casual 1v7 (bots fill empty seats; wait-then-bots if you add a real queue).
- Telemetry that can detect Chorus dominance: `chorus_winrate`, `triple_rate`, `grant_embers`, `zero_action_recruits`, crash abort (today: process-local counters only).
- Live config: `atk`/`hp`/`disabled` only. Never hot-patch `copy_limit`.
- Auth: token expiry; turn off `/v1/cosmetics/debug` and IAP sandbox on Fly.
- English-only, landscape, Broker silent+text.

**50–200 players. This is MVP done.**

### Slice F — Explicitly later (do not start now)

- Ranked / The Crown: 8 humans, no bots, Glicko only, 3 captain offers, placement games.
- Dragon/Spirit roster to 10–12 (design pass, not invented names).
- Extra tribes, Duos, Relics/Vows, Charm.
- URP, Input System, TMP, Addressables remote, Kestrel split, Android game-ci.
- IAP / battle pass cosmetics.
- Public replays, voice Broker, localization.

---

## 4. Known defects to burn down in Slice A/B

Priority inside the sim (verified in code):

1. `CombatSim.ApplyAuras` writes `EchoTimesBonus` onto **every** board unit; `SumEchoBonus` sums them → Echoist is `amount × N` (`CombatSim.cs`, `EffectHooks.cs`).
2. `cap_widow` edict is two Echo-related actions (`GrantKeyword Echo` + `GiveEchoSummon`).
3. `MatchSession.Finish` always applies Glicko, including 1v7 bots.
4. Ghost source uses elimination-list order, not worst place in that round.
5. Inspect cannot explain Kindled (`HsUi.MechanicalLine` → `"Text comes later."`).
6. Upgrade/Reroll captions do not show live cost.
7. Help overlay re-opens on the warband until skipped.
8. Combat playback defaults to 2× and hard-skips at 12s instead of fitting the log.
9. `README.md` still says sockets/Redis unwired and “Hearthstone-like cards” (docs only; client/content IP grep is clean — still fix the README).

---

## 5. How we will know it got better

| Gate | Measure |
|---|---|
| Practice honest | New player can buy a stall card and state its effect from the HUD |
| Sim | New goldens red→green; Echoist/Widow/ghost fixtures; `dotnet test` on CI |
| Presentation | Portraits on pick + rail; Kindled faces at 7-wide still readable on a phone |
| Casual | Reconnect after iOS suspend; Fly match survives 20 minutes |
| Alpha | 50 completed matches, no silent rating writes, Chorus winrate visible |

---

## 6. Immediate next PR (when we leave this doc)

1. Echoist + Widow goldens and fixes.
2. Gate Glicko behind Ranked (Casual/Practice never write μ).
3. Kindled `text` + inspect + Arrival/Echo/Kindle on the face.
4. Upgrade cost / Reroll 0·1 / bar numbers / Edict used.
5. Help overlay off the warband; tap-to-act.
6. README + DESIGN drift table in this file copied into DESIGN Key Decisions.

Art (portraits/faces) continues in parallel and binds as soon as a same-size set exists. Do not block Slice A on art.
