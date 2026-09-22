# Doodle Arena real-object conversion — what was done

This is a first pass, done end-to-end from one machine so all four of you can
pull whatever piece you need into your own repo. It has NOT been opened in
the Unity Editor yet by whoever ran this (no Unity available where it ran) —
open it, run the one setup step below, and report any console errors so they
can get fixed fast. Treat this as a strong starting point, not a guaranteed
zero-error drop-in.

## New files in Assets/Scripts

- GameManager.cs (Aaron) — replaces DoodleArenaGame.cs's shared state. Singleton
  via GameManager.Instance, holds arena bounds, run state (title/playing/wave
  flow/game over/victory), score/combo/items, and the HurtPlayer/RegisterKill/
  Shake/HitStop/Burst methods everything else calls into.
- PlayerController.cs (Sideesh) — real GameObject moved via transform.position,
  melee/ranged/dash/item logic, all tunables as [SerializeField] fields.
- EnemyController.cs + EnemySpawner.cs (Yipeng) — Chaser/Shooter/Tank behavior
  as one prefab-driven MonoBehaviour, spawning via Instantiate instead of a
  List<Enemy>. Enemy-vs-enemy separation is now just Unity's own 2D physics
  (Rigidbody2D + solid Collider2D) instead of the old manual pairwise loop.
- BossController.cs (Aaron) — the boss is its own prefab/component now instead
  of a method reaching into a shared Enemy object.
- ProjectileController.cs (shared) — replaces Shot: real Rigidbody2D + trigger
  Collider2D, used by player shots, enemy shots, boss orbs, bottles, crates.
- HUDController.cs + CameraShake.cs + BurstEmitter.cs (Aijia) — replaces every
  OnGUI draw call with a real Canvas (Text/Image/fill-Image), moves the actual
  camera transform for shake, and drives a real ParticleSystem for hit sparks.

DoodleArenaGame.cs and DoodleArenaBootstrap.cs were left in place but emptied
out to stub comments (not deleted, so their .meta files don't go orphaned) —
everything they used to do now lives in the files above.

## One-time setup step (do this first)

1. Open this project in Unity 6 and open Assets/Scenes/SampleScene.unity.
2. Run **Tools > Doodle Arena > Build Scene (Convert To Real Objects)**.
   This generates placeholder circle/square sprites, saves prefabs for the
   player/enemies/boss/projectiles under Assets/Prefabs, and builds the scene
   hierarchy (GameManager, EnemySpawner, Player, Main Camera + CameraShake,
   a HitBurst ParticleSystem, and a full Canvas HUD) wiring every serialized
   reference the way you'd normally drag-and-drop in the Inspector.
3. Save the scene (Ctrl/Cmd+S), then press Play.
4. It's safe to re-run the menu item — it reuses existing prefabs/sprites
   instead of duplicating them, and re-wires the scene objects if you already
   have a Player/GameManager/etc. in the scene.

## Known rough edges to expect

- All shapes are placeholder circles (and a square for the crate) generated
  in code — no hand-drawn sprites yet. Swap SpriteRenderer.sprite on each
  prefab once real art exists.
- The visual layout (HUD Rects, banner size, title screen) is a direct port
  of the original OnGUI pixel positions into a 1600x900 reference Canvas, but
  hasn't been eyeballed in the Editor — expect to nudge a few Rects.
- Bottle/crate shots are circles here instead of the original's rotated
  rectangle sprites; cosmetic only, doesn't affect gameplay logic.
- If the InputSystemUIInputModule the builder adds to EventSystem causes an
  error, your project's Player Settings > Active Input Handling may need to
  be set to "Input System Package (New)" or "Both".
