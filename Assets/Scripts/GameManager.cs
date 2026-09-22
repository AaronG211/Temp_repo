using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DoodleArena
{
    public enum GameState { Title, Playing, BetweenWave, GameOver, Victory }
    public enum EnemyKind { Chaser, Shooter, Tank, Boss }
    public enum ShotKind { Player, Enemy, Bottle, Crate, BossOrb }

    public interface IDamageableEnemy
    {
        float Hp { get; }
        float MaxHp { get; }
        EnemyKind Kind { get; }
        Vector2 Position { get; }
        void TakeDamage(float amount, Vector2 push);
    }

    // Aaron owns this file: the shared game-state hub every other converted
    // script (player, enemies, boss, HUD) talks to through GameManager.Instance
    // instead of reaching into a shared partial class like the old code did.
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Arena (world units, centered at the origin)")]
        public Rect arena = new Rect(-728f, -350f, 1456f, 700f);

        [Header("Player")]
        public PlayerController player;
        public float playerMaxHp = 100f;
        [HideInInspector] public float playerHp;
        public float invincibleTime = .65f;
        [HideInInspector] public float invincibleTimer;

        [Header("Run State")]
        public int round, wave, score, combo;
        public int bottles = 3, crates = 2;

        [Header("Feel")]
        [HideInInspector] public float shake;
        [HideInInspector] public float hitStop;

        [Header("Scene References")]
        public EnemySpawner spawner;
        public HUDController hud;
        public CameraShake cameraShake;
        public BurstEmitter burst;

        public GameState State { get; private set; } = GameState.Title;
        public bool IsPlaying => State == GameState.Playing;
        public bool IsInvincible => invincibleTimer > 0f;
        public IReadOnlyList<IDamageableEnemy> ActiveEnemies => activeEnemies;

        private float waveTimer;
        private bool bossSpawned;
        private readonly List<IDamageableEnemy> activeEnemies = new List<IDamageableEnemy>();

        private void Awake()
        {
            Instance = this;
            playerHp = playerMaxHp;
        }

        private void Start()
        {
            hud?.ShowTitle();
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .033f);

            HandleGlobalInput();

            if (hitStop > 0f) { hitStop -= dt; return; }

            shake = Mathf.Max(0f, shake - dt * 18f);
            invincibleTimer -= dt;
            if (cameraShake) cameraShake.SetShake(shake);

            if (State == GameState.BetweenWave)
            {
                waveTimer -= dt;
                if (waveTimer <= 0f)
                {
                    if (bossSpawned) { State = GameState.Victory; hud?.ShowVictory(); return; }
                    State = GameState.Playing;
                    NextWave();
                }
            }
        }

        private void HandleGlobalInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (State == GameState.Title)
            {
                if (Pressed(kb.enterKey) || Pressed(kb.spaceKey) || Pressed(kb.jKey)) StartRun();
            }
            else if (State == GameState.GameOver || State == GameState.Victory)
            {
                if (Pressed(kb.rKey) || Pressed(kb.enterKey)) StartRun();
            }
            else if (Pressed(kb.escapeKey))
            {
                ReturnToTitle();
            }
        }

        private static bool Pressed(KeyControl key) => key != null && key.wasPressedThisFrame;

        public void StartRun()
        {
            State = GameState.Playing;
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
            if (spawner) spawner.ClearAll();
            hud?.ShowTitle();
        }

        private void NextWave()
        {
            wave++;
            if (wave <= 3)
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
            if (State != GameState.Playing) return;
            if (activeEnemies.Count == 0)
            {
                State = GameState.BetweenWave;
                waveTimer = bossSpawned ? 3.4f : 2.2f;
                if (bossSpawned)
                {
                    hud?.ShowBanner("ARENA CLEARED", waveTimer);
                    playerHp = Mathf.Min(playerMaxHp, playerHp + 30f);
                    bottles = Mathf.Min(5, bottles + 2);
                    crates = Mathf.Min(4, crates + 1);
                }
                else hud?.ShowBanner("WAVE CLEARED", waveTimer);
            }
        }

        public int ActiveMinionCount(bool excludeBoss)
        {
            int count = 0;
            foreach (var e in activeEnemies) if (!excludeBoss || e.Kind != EnemyKind.Boss) count++;
            return count;
        }

        public void RequestMinionSpawn()
        {
            if (spawner) spawner.SpawnEnemy(EnemyKind.Chaser, round);
        }

        public void RegisterKill(EnemyKind kind, Vector2 pos)
        {
            score += kind == EnemyKind.Boss ? 2500 : kind == EnemyKind.Tank ? 250 : 100;
            combo++;
            Burst(pos, new Color(0.42f, 0.29f, 0.71f), kind == EnemyKind.Boss ? 80 : 22, kind == EnemyKind.Boss ? 520f : 270f);
            Shake(kind == EnemyKind.Boss ? 28f : 8f);
            if (kind != EnemyKind.Boss && Random.value < .18f)
            {
                if (Random.value < .58f) bottles = Mathf.Min(5, bottles + 1);
                else crates = Mathf.Min(4, crates + 1);
            }
        }

        public void HurtPlayer(float amount, Vector2 dir)
        {
            if (IsInvincible || !IsPlaying) return;
            playerHp -= amount;
            invincibleTimer = invincibleTime;
            combo = 0;
            Shake(18f); HitStop(.04f);
            Burst(player ? (Vector2)player.transform.position : Vector2.zero, new Color(.72f, .23f, .28f), 20, 300f);
            player?.ApplyKnockback(dir * 145f);
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
