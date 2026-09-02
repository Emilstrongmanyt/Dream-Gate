# Kindling — asset board

Catalog today: **16 Captains**, **48 Kindled**, **7 stall spells**, **7 tokens**, **5 Choruses**. UI chrome is Layer Lab The Stone. Combat/recruit VFX is Cartoon FX Remaster Free (Jean Moreno). Menu and table backdrops are original dusk-market paintings in `Resources/Bg` (`menu`, `board`). Card and Captain art is still pending. No audio yet.

## Do not buy yet

Generic RPG icon packs, lucky-box/gem/joystick chrome from Stone demos, extra card-back packs, competitor lookalikes, 3D dining-table furniture, and tavern/inn environment kits. They fight the dusk-market identity and do not make 7-wide cards readable. Menu and board backdrops are original; do not replace them with a store tavern.

## Need next (in this order)

| Priority | What | Count | How | Why |
|---|---|---|---|---|
| 1 | Captain portraits | 16, same size, silhouette-first | Original (commission or in-house). Not a random hero pack. | Pick screen and the right-rail identity. |
| 2 | Kindled faces | 48 shop + 7 spells + 7 tokens | Original. One pose, high-contrast, landscape-readable at ~130px. | Color blocks are the biggest UX hole. |
| 3 | UI SFX | ~12 one-shots | Small UI pack or original: buy, sell, reroll, play, edict, timer tick, ready, hit, death, win, lose, tap. | Table is mute. |
| 4 | Changa Bold/Medium TTF | 2 files | Google Fonts (OFL), already licensed next to Stone. | Matches the kit; still on Unity’s built-in font. |
| 5 | Card face template | 1 frame + 5 Chorus tints | Custom. Shirts wait until faces exist. | Faces matter more than backs. |
| 6 | Loop bed | 1 low dusk-market bed + combat sting | Original, no lyrics. | After SFX. |

## Later

Addressable remote catalog for art, extra CFXR variants if the free set feels thin, app icon refresh if Kindling replaces Dream Gate in the store listing.

Combat VFX mapping (Cartoon FX Remaster Free), spawned on the control they belong to and scaled to that rect (native CFXR sizes vary ~0.5–8, so we do not use a shared 0.45 scale):

| Beat | Prefab key | Lands on |
|---|---|---|
| Market Depth upgrade | flash + aura | Upgrade button **and** the top Depth chip (`D#`) |
| Reroll | flash | Reroll button |
| Buy | flash | Stall card |
| Play Kindled / spell | smoke / spark | Destination warband card |
| Sell | poof | The card being sold |
| Awaken | flash + fire | The awakened warband card |
| Edict | spark | Target card, or captain rail if untargeted |
| Attack / hit / death | slash / hit / poof | Combat card |
| Venom / Kindle / Afterglow / summon | venom / fire / smoke | Combat card |

Magic Aura and Poison Cloud are ground-plane (XZ) prefabs; VfxPlayer tilts them into the canvas so the UI camera does not see an edge-on sliver. Canvas is Screen Space Camera so particles can sit on cards.

## Rule

New art must be original-IP, dusk marketplace (brass, coal, lanterns). No innkeeper, tavern, or lookalike card frames. Do not invent new unit or Captain names to fill a pack.
