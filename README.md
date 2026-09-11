# Doodle Rumble

A self-contained 2D wave-action game built for Unity 6. The game starts automatically
from `SampleScene` and creates its visual assets at runtime, so there are no missing
sprites or prefabs to configure.

## Play

1. Open the project in Unity 6.0 or newer.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press Play.

## Controls

- `WASD` or arrow keys: Move
- `J`: Punch a nearby enemy, otherwise shoot toward the nearest enemy
- `K`: Dash smash with a short invulnerability window
- `I`: Throw an explosive bottle
- `O`: Throw a piercing crate
- `Esc`: Return to title
- `R`: Restart after a win or loss

## Game flow

The game has three escalating enemy waves followed by **The Overseer** boss. Defeat
the boss to win. Defeated enemies can restore limited-use bottle and crate throws.
