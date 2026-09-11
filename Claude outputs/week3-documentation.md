# CSE 4500 — Game State + Week 3 Documentation

## What's actually in the repo right now

The whole game currently lives in one script, `Assets/Scripts/DoodleArenaGame.cs`, attached to a single "Doodle Arena Game" object in `SampleScene.unity` (plus a Main Camera and a 2D light — that's the entire scene). `DoodleArenaBootstrap.cs` auto-spawns that object on play so there's nothing to wire up by hand. There are no sprite/image assets anywhere in the project — every visual (player, enemies, boss, bullets, HUD, particles) is drawn at runtime with `OnGUI` using solid-color circles and rectangles. That's a deliberate "doodle" art style, not a placeholder waiting on art assets.

**Core loop, as implemented:**
- Move with WASD/arrows. Movement uses velocity smoothing (lerp toward target velocity), not instant snapping.
- `J` (held): auto-targets the nearest enemy — melee punch if it's close (105 units), otherwise fires a ranged shot at it.
- `K`: a dash attack. Moves the player 115 units in the input/aim direction, gives 0.4s of invincibility, and deals AoE damage to anything within 128 units. 2.4s cooldown.
- `I`: throws a bottle (small blast radius, explodes on impact or at end of life). Limited ammo (starts at 3, capped at 5).
- `O`: throws a crate (bigger hit, pierces up to 3 enemies, damage falls off each pierce). Starts at 2, capped at 4.
- Ammo occasionally drops from killed enemies (18% chance, non-boss only).

**Enemies (3 types) + boss:**
- **Chaser** — straight-line pursuit, contact damage.
- **Shooter** — keeps mid-range distance, strafes, fires projectiles on a cooldown that shrinks as rounds go up.
- **Tank** — slow, tougher, harder contact hit, plus a screen-shake on hit.
- **Boss ("The Overseer")** — one boss, three attack phases keyed to its HP percentage (above 65%: aimed 5-shot spread; 32–65%: a 12-shot ring burst; below 32%: a tighter aimed spread *and* it starts spawning Chaser minions if there are fewer than 2 alive). Enemies also push apart from each other so they don't stack into an unreadable blob.

**Structure:** Each round is 3 waves of enemies (wave size and enemy mix scale with round number and wave number), then a boss. Clearing the boss heals the player partially, refills some ammo, and ends the run in a Victory state (there's currently no round 2+ content — one boss clear = win). There's a full state machine: Title → Playing → Between Wave → Game Over / Victory, with a working HUD (HP bar, boss HP bar, ammo counts, combo counter, score), screen shake, hit-stop on big hits, and a pulsing screen-edge warning at low HP.

**Loose end worth knowing about:** the project has a real Input System actions asset (`InputSystem_Actions.inputactions`) imported, but the game code doesn't use it — it polls `Keyboard.current` directly. Not broken, just something to be aware of if anyone starts editing input.

## Why Aaron's original Week 3 write-up needed fixing

The submitted entry said "Built the first level of the game" with a next-week goal of "find assets for enemies and bosses for level two." Two problems with that framing, based on what's actually in the repo:
1. There's no "level" concept in the code at all — it's a single arena with a round/wave spawner. "First level" undersells what's there: a full playable combat loop, three enemy behaviors, and a complete multi-phase boss, not a static level layout.
2. There's nothing to "find assets" for — the art is 100% procedural (drawn shapes, no imported sprites). Framing next steps around asset-hunting points the team at work that doesn't match the actual approach.

## Corrected Week 3 entry (Aaron Gao)

**Weekly Contributions:**
- Implemented the full player combat kit in `DoodleArenaGame.cs`: movement, auto-targeting melee/ranged attack (J), a dash-AoE attack with brief invincibility (K), and two throwable items with different behavior — blast-radius bottles (I) and piercing crates (O).
- Built three enemy behaviors (Chaser, Shooter, Tank) and a boss with three HP-gated attack phases, including minion spawning in its final phase.
- Built the round/wave spawner and the full game state machine (Title, Playing, Between Wave, Game Over, Victory).
- Drew the entire game (player, enemies, projectiles, HUD, particles) procedurally with `OnGUI` in a hand-drawn "doodle" style — no external art assets used.
- Set up `SampleScene.unity` and `DoodleArenaBootstrap.cs` so the game runs immediately on Play with no manual scene setup.

**Missed Goals:** None.

**Next Week's Goal (revised — no asset-hunting, since art is procedural):**
- Add audio (hit/attack sfx, background music).
- Build out round 2+ content — a second enemy formation/pacing and a visually/behaviorally distinct second boss, since right now one boss clear ends the run.
- Migrate input from raw `Keyboard.current` polling to the already-imported Input System actions asset.
- Run a first internal playtest and note balance issues (attack cooldowns, enemy HP/damage scaling per round) to fix before Week 4's WebGL build goes up.

## Still need from the team

The repo only shows Aaron's commits, so I can't reconstruct what you, Yipeng, and Aijia actually did this week from the files alone — tell me and I'll fold it into the same write-up before it goes in the doc.
