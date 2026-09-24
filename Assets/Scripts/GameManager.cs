using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleArena
{
    // Central game state: run flow (title -> waves -> boss -> next round), player HP,
    // score/combo, item counts and the list of live enemies. Other scripts reach it
    // through GameManager.Instance.
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Arena")]
        public Rect arena = new Rect(-8.5f, -4.5f, 17f, 9f);

        [Header("Player")]
        public PlayerController player;
        public float playerMaxHp = 100f;
        [HideInInspector] public float playerHp;
        public float invincibleTime = 0.65f;
        [HideInInspector] public float invincibleTimer;
        public float hitKnockback = 1.8f;

        [Header("Run")]
        public int totalRounds = 3;
        public int wavesPerRound = 3;
        public int round, wave, score, combo;
        public int bottles = 3, crates = 2;
        public int maxBottles = 5, maxCrates = 4;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Scene References")]
        public EnemySpawner spawner;
        public HUDController hud;
        public CameraShake cameraShake;
        public BurstEmitter burst;

        [HideInInspector] public float shake;
        [HideInInspector] public float hitStop;

        public GameState State { get; private set; } = GameState.Title;
        public bool IsPlaying => State == GameState.Playing;
        public bool IsInvincible => invincibleTimer > 0f;
        public IReadOnlyList<IDamageableEnemy> ActiveEnemies => activeEnemies;
        public InputActionMap Controls { get; private set; }
        public IDamageableEnemy Boss
        {
            get
            {
                foreach (var e in activeEnemies) if (e.Kind == EnemyKind.Boss) return e;
                return null;
            }
        }

        private const float ShakeDecay = 0.22f;
        private const float CameraSize = 5f;     // Unity's default 2D camera size
        private const float CameraMargin = 0.2f;

        private float waveTimer;
        private bool bossSpawned;
        private readonly List<IDamageableEnemy> activeEnemies = new List<IDamageableEnemy>();
        private InputAction confirmAction, restartAction, backAction;
        private Camera cam;

        private void Awake()
        {
            Instance = this;
            playerHp = playerMaxHp;

            if (inputActions == null)
            {
                Debug.LogError("GameManager: no Input Action Asset assigned. Re-run Tools > Doodle Arena > Build Scene.");
                return;
            }
            Controls = inputActions.FindActionMap("Arena", true);
            confirmAction = Controls["Confirm"];
            restartAction = Controls["Restart"];
            backAction = Controls["Back"];
        }

        private void OnEnable() => Controls?.Enable();
        private void OnDisable()
        {
            Controls?.Disable();
            Time.timeScale = 1f;
        }

        private void Start()
        {
            if (cameraShake) cam = cameraShake.GetComponent<Camera>();
            hud?.ShowTitle();
        }

        // Size 5 at 16:9; only zooms out if a narrower window would cut the arena off.
        private void FitCameraToArena()
        {
            if (!cam) return;
            float needed = Mathf.Max(arena.height * 0.5f, arena.width * 0.5f / cam.aspect) + CameraMargin;
            cam.orthographicSize = Mathf.Max(CameraSize, needed);
        }

        private void Update()
        {
            // unscaled so the hit-stop freeze below can still count itself down
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);

            FitCameraToArena();
            HandleMenuInput();

            if (hitStop > 0f)
            {
                hitStop -= dt;
                Time.timeScale = hitStop > 0f ? 0f : 1f;
                return;
            }

            shake = Mathf.Max(0f, shake - dt * ShakeDecay);
            invincibleTimer -= dt;
            if (cameraShake) cameraShake.SetShake(shake);

            if (State == GameState.BetweenWave)
            {
                waveTimer -= dt;
                if (waveTimer <= 0f) AdvanceAfterClear();
            }
        }

        private void HandleMenuInput()
        {
            if (Controls == null) return;

            switch (State)
            {
                case GameState.Title:
                    if (confirmAction.WasPressedThisFrame()) StartRun();
                    break;
                case GameState.GameOver:
                case GameState.Victory:
                    if (restartAction.WasPressedThisFrame() || confirmAction.WasPressedThisFrame()) StartRun();
                    break;
                default:
                    if (backAction.WasPressedThisFrame()) ReturnToTitle();
                    break;
            }
        }

        public void StartRun()
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
            hitStop = 0f;
            playerHp = playerMaxHp;
            round = 1; wave = 0; score = 0; combo = 0;
            bottles = 3; crates = 2;
            bossSpawned = false;
            invincibleTimer = 0f;
            activeEnemies.Clear();
            if (spawner) spawner.ClearAll();
            if (player) player.ResetToStart(arena);
            hud?.ShowGame();
            NextWave();
        }

        public void ReturnToTitle()
        {
            State = GameState.Title;
            Time.timeScale = 1f;
            hitStop = 0f;
            activeEnemies.Clear();
            if (spawner) spawner.ClearAll();
            hud?.ShowTitle();
        }

        private void AdvanceAfterClear()
        {
            if (!bossSpawned)
            {
                State = GameState.Playing;
                NextWave();
                return;
            }

            if (round >= totalRounds)
            {
                State = GameState.Victory;
                hud?.ShowVictory();
                return;
            }

            round++;
            wave = 0;
            bossSpawned = false;
            State = GameState.Playing;
            NextWave();
        }

        private void NextWave()
        {
            wave++;
            if (wave <= wavesPerRound)
            {
                hud?.ShowBanner("ROUND " + round + "  /  WAVE " + wave, 1.6f);
                if (spawner) spawner.SpawnWave(round, wave);
            }
            else
            {
                bossSpawned = true;
                hud?.ShowBanner("BOSS  -  THE OVERSEER", 2.5f);
                if (spawner) spawner.SpawnBoss(round);
            }
        }

        public void RegisterEnemy(IDamageableEnemy enemy)
        {
            if (!activeEnemies.Contains(enemy)) activeEnemies.Add(enemy);
        }

        public void UnregisterEnemy(IDamageableEnemy enemy)
        {
            activeEnemies.Remove(enemy);
            if (State != GameState.Playing || activeEnemies.Count > 0) return;

            State = GameState.BetweenWave;
            if (bossSpawned)
            {
                waveTimer = 3.4f;
                bool lastRound = round >= totalRounds;
                hud?.ShowBanner(lastRound ? "ARENA CLEARED" : "ROUND " + round + " CLEARED", waveTimer);
                playerHp = Mathf.Min(playerMaxHp, playerHp + 30f);
                bottles = Mathf.Min(maxBottles, bottles + 2);
                crates = Mathf.Min(maxCrates, crates + 1);
            }
            else
            {
                waveTimer = 2.2f;
                hud?.ShowBanner("WAVE CLEARED", waveTimer);
            }
        }

        public int ActiveMinionCount()
        {
            int count = 0;
            foreach (var e in activeEnemies) if (e.Kind != EnemyKind.Boss) count++;
            return count;
        }

        public void RequestMinionSpawn()
        {
            if (spawner) spawner.SpawnEnemy(EnemyKind.Chaser, round);
        }

        public void RegisterKill(EnemyKind kind, Vector2 pos)
        {
            bool isBoss = kind == EnemyKind.Boss;
            score += isBoss ? 2500 : kind == EnemyKind.Tank ? 250 : 100;
            combo++;
            Burst(pos, Palette.Purple, isBoss ? 80 : 22, isBoss ? 6.5f : 3.4f);
            Shake(isBoss ? 0.35f : 0.1f);

            // small chance for a regular enemy to drop an item
            if (!isBoss && Random.value < 0.18f)
            {
                if (Random.value < 0.6f) bottles = Mathf.Min(maxBottles, bottles + 1);
                else crates = Mathf.Min(maxCrates, crates + 1);
            }
        }

        public void HurtPlayer(float amount, Vector2 dir)
        {
            if (IsInvincible || !IsPlaying) return;
            playerHp -= amount;
            invincibleTimer = invincibleTime;
            combo = 0;
            Shake(0.22f);
            HitStop(0.04f);
            Burst(player ? (Vector2)player.transform.position : Vector2.zero, Palette.Blood, 20, 3.8f);
            if (player) player.ApplyKnockback(dir * hitKnockback);

            if (playerHp <= 0f)
            {
                playerHp = 0f;
                State = GameState.GameOver;
                hud?.ShowGameOver();
            }
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);
        public void HitStop(float duration) => hitStop = Mathf.Max(hitStop, duration);
        public void Burst(Vector2 pos, Color color, int count, float speed) { if (burst) burst.Emit(pos, color, count, speed); }

        public void ConsumeBottle() => bottles--;
        public void ConsumeCrate() => crates--;
    }
}
