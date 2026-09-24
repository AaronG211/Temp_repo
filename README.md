# Doodle Arena

A top-down 2D arena brawler made in Unity 6. Fight three waves of doodle enemies,
beat The Overseer, and do it three rounds in a row.

## Running it

1. Open the project in Unity 6 (6000.x).
2. Open `Assets/Scenes/SampleScene.unity`.
3. If the scene is empty or you changed the builder, run **Tools > Doodle Arena > Build Scene**,
   then save the scene.
4. Press Play.

## Controls

| Action | Keyboard | Gamepad |
|---|---|---|
| Move | WASD / arrow keys | Left stick / D-pad |
| Attack (punch up close, shoot otherwise) | J (hold) | X / Square |
| Dash smash | K | A / Cross |
| Throw bottle (explodes) | I | LB |
| Throw crate (pierces) | O | RB |
| Start | Enter / Space | Start |
| Restart after win/loss | R | Y / Triangle |
| Back to title | Esc | Select |

Bindings live in the `Arena` map of `Assets/InputSystem_Actions.inputactions`.

## How a run works

- Each round is 3 waves followed by the boss. Wave 1 is Chasers, wave 2 adds Shooters,
  wave 3 adds Tanks. Later rounds spawn more enemies and a tougher boss.
- The boss has three phases based on its HP: aimed spread, rotating ring, then a faster
  spread while it summons minions.
- Clearing a boss heals 30 HP and refills some bottles/crates. Beat the boss in round 3 to win.
- Enemies sometimes drop a bottle or crate.

## Code layout

| Script | What it does |
|---|---|
| `GameManager` | Run state (title, waves, rounds, win/lose), HP, score, input map |
| `PlayerController` | Movement, attacks, dash, throwing items |
| `EnemyController` | Chaser / Shooter / Tank behaviour |
| `BossController` | The Overseer's movement and attack phases |
| `ProjectileController` | Bullets, bottles and crates |
| `EnemySpawner` | Wave composition and spawn points |
| `HUDController` | Title / HUD / end screens |
| `CameraShake`, `BurstEmitter` | Screen shake and hit particles |
| `Editor/DoodleArenaSceneBuilder` | Generates the prefabs and scene objects |

World units: 1 unit = 80 px of art. Camera size is 5 (Unity default); the arena is 17 x 9 units, centred on the origin.

Character art is from Kenney's *Shape Characters* pack (CC0), see `Assets/Art/CREDITS.txt`.
