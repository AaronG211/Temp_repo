# Doodle Arena: OnGUI/List-based to Real Unity Objects Conversion Plan

## Why this conversion is happening

The current build works, but almost none of it uses real Unity constructs. There's no scene hierarchy, no prefabs, no colliders, no Rigidbody2D, and no Canvas. Everything lives inside one MonoBehaviour's Update() and OnGUI(), with "objects" like Enemy and Shot being plain private C# classes tracked in Lists rather than actual GameObjects. This needs to move to the patterns already demonstrated in Q1 (UFOController, transform.position movement) and Q2 (RotatingShipController, Rigidbody2D + AddTorque/AddRelativeForce, MazeObstacle's OnCollisionEnter2D, SerializeField-exposed tuning values).

## Aaron: DoodleArenaGame.cs (shared core) + DoodleArenaBoss.cs

What's there now: DoodleArenaGame.cs holds all shared state as plain fields on the MonoBehaviour, playerPos, a List<Enemy>, a List<Shot>, sparks. Enemy and Shot are private sealed classes, not Unity objects. There's no scene hierarchy at all, DoodleArenaBootstrap.cs spawns one empty GameObject at runtime and everything happens inside that single component's Update()/OnGUI(). The boss (DoodleArenaBoss.cs) is just a method, UpdateBoss(Enemy boss, Vector2 direction, float dt), operating on that same plain Enemy class, no boss GameObject exists anywhere.

What needs to change: Enemy, Shot, and Spark stop being plain C# classes and become real MonoBehaviours attached to actual prefabs (EnemyController, ProjectileController, etc.), each with a SpriteRenderer + Collider2D (+ Rigidbody2D for physics-based movement, matching Q2's ship). The boss becomes its own prefab with its phase logic living on that prefab's script instead of a method reaching into a shared list. The List<Enemy>/List<Shot> bookkeeping goes away in favor of Instantiate/Destroy and Unity's own collision callbacks (OnTriggerEnter2D/OnCollisionEnter2D, like MazeObstacle.cs does for scene reload).

Aaron is also the one who needs to own the shared game-state question below, since DoodleArenaGame.cs is the thing everyone else's converted scripts will need to talk to.

## Sideesh (me): DoodleArenaPlayer.cs

What's there now: UpdatePlayer moves the player by directly adding to a Vector2 playerPos field every frame, no Transform, no Rigidbody2D. Closest existing analog is Q1's UFOController, except UFO actually moves transform.position and mine moves a private math variable that then gets drawn at that position via OnGUI. PrimaryAttack/DashAttack find targets and register hits using NearestEnemy() + manual Vector2.Distance(...) < radius checks, no colliders at all. Attack numbers (22 damage, .32f cooldown, 105 melee range, etc.) are hardcoded inline.

What needs to change: the player becomes an actual GameObject with a SpriteRenderer and Collider2D, movement switches to modifying transform.position (or a Rigidbody2D) directly instead of a separate tracked variable, and hit detection switches from NearestEnemy() + distance math to a real Collider2D (a trigger zone in front of the player for melee, or OnTriggerEnter2D on the projectile itself for ranged). All the numeric constants become [SerializeField] fields so they're editable in the Inspector, like Q2's public float speed.

The open architecture question: DashAttack and HurtPlayer both reach directly into shared fields on the partial class DoodleArenaGame, specifically enemies, arena, shake, hitStop, combo, and state. Once the player is its own MonoBehaviour on its own GameObject, it won't have direct access to those anymore. There are two ways to handle this.

Option one is a lightweight GameManager singleton that other scripts (player, enemies, HUD) hold a reference to and read/write through, e.g. GameManager.Instance.HurtPlayer(...), GameManager.Instance.Shake(9). This is the least disruptive to the existing hand-off contracts since it basically keeps DoodleArenaGame.cs as the hub everyone already expects, just accessed via a static reference instead of a partial class.

Option two is an event-driven approach, the player fires events (OnPlayerHit, OnDashHit, OnPlayerDamaged) that a manager script subscribes to and reacts to (updating HP, triggering shake, updating combo). This is more decoupled and more "correct" in a larger codebase, but it means Aijia's HUD and Yipeng's enemy scripts also need to know about those events rather than just reading fields off a manager, which is more coordination work for a project on this timeline.

My take: go with the GameManager singleton. It's the smaller change relative to what everyone already has, it keeps a single obvious place (GameManager.Instance) for Aijia's HUD to pull playerHp/combo from and for Yipeng's enemy scripts to call into for damage/death, and it avoids everyone needing to wire up event subscriptions under time pressure. This should get confirmed with Aaron since he owns the shared state file, but I'd propose it as the plan and let him push back if he sees a reason not to.

## Yipeng: DoodleArenaEnemies.cs

What's there now: UpdateEnemies loops a List<Enemy> every frame, switches on enemy.kind (Chaser/Shooter/Tank/Boss) to decide movement math, and does its own enemy-vs-enemy separation math via a nested double for loop checking every pair's distance. SpawnEnemy creates a plain Enemy object and adds it to the list, nothing is ever actually instantiated in the scene. FireEnemyShot does the same thing for enemy projectiles.

What needs to change: each enemy type becomes its own prefab (or one prefab with a kind field driving behavior) with Rigidbody2D + Collider2D. SpawnEnemy becomes an actual Instantiate(enemyPrefab, position, rotation) call. The manual pairwise separation loop gets replaced by Unity's physics doing collision resolution between enemy Rigidbody2Ds automatically. FireEnemyShot instantiates a real projectile prefab instead of appending to a list. All stat numbers (HP formulas, spawn timing, movement speeds) become [SerializeField] fields.

Yipeng's enemy scripts are also one of the two consumers of whatever the shared-state answer ends up being (calling into GameManager for damage/death/scoring), so this piece depends on Aaron and me settling the architecture question first.

## Aijia: DoodleArenaPresentation.cs + the HUD portion still in DoodleArenaGame.cs

What's there now: literally every visual in the game, the paper-texture background, the title screen text, the player/enemy/shot doodle-circle drawings, health bars, banners, game-over/victory screens, is a GUI.DrawTexture/GUI.Label/GUI.color call inside OnGUI, which Unity's own docs say is meant for editor tooling, not shipped gameplay UI. Screen shake is faked by nudging GUI.matrix each frame. Particles (Burst/Spark) are a hand-rolled List<Spark> drawn as little rotated rectangles, not a real particle system.

What needs to change: the entire HUD (health bars, score, round/wave label, banners, title/game-over/victory screens) becomes a Unity Canvas with real Image, Text, and Slider/fill-Image components. Screen shake becomes actually moving the Camera's transform. Burst's hand-drawn sparks become a real ParticleSystem component. Since the player/enemies/shots are becoming real SpriteRenderer-based GameObjects (per Aaron's and Yipeng's sections above), the drawing methods for those (DrawPlayer, DrawEnemy, DrawShot, DrawCircle, DrawRotated) go away entirely, that visual work moves onto the sprite assigned to each prefab instead of being redrawn procedurally every frame.

Aijia's HUD is the other consumer of the shared-state answer, since it needs to read playerHp, combo, and score from wherever those end up living (GameManager.Instance under the proposed plan).

## Sequencing / dependency order

1. Settle the GameManager singleton question with Aaron first, since three of the four people's conversions read or write through it.
2. Aaron converts DoodleArenaGame.cs's Enemy/Shot/Spark into real prefabs and stands up the GameManager singleton with the same fields/methods everyone already expects (HurtPlayer, DamageEnemy, Burst, shake, hitStop, combo, state), just exposed statically instead of via partial class.
3. Sideesh (player), Yipeng (enemies), and Aijia (presentation/HUD) can then convert in parallel against that GameManager contract, since none of their pieces depend on each other directly, only on GameManager.
