using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DoodleArena
{
    // Sideesh owns this file: the player is now a real GameObject moved via
    // transform.position (matching Q1's UFOController) with a Collider2D for
    // presence, instead of a private Vector2 field redrawn every frame by OnGUI.
    // Melee/dash range checks use GameManager's live enemy registry (backed by
    // real Collider2D-bearing enemy objects) instead of NearestEnemy() + a
    // manual Vector2.Distance loop over a List<Enemy>.
    [RequireComponent(typeof(Collider2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 365f;
        [SerializeField] private float accelSharpness = 14f;
        [SerializeField] private float bodyRadius = 28f;

        [Header("Primary Attack (melee if in range, otherwise ranged)")]
        [SerializeField] private float meleeRange = 105f;
        [SerializeField] private float meleeDamage = 22f;
        [SerializeField] private float meleeCooldown = .32f;
        [SerializeField] private float meleeKnockback = 36f;
        [SerializeField] private ProjectileController rangedShotPrefab;
        [SerializeField] private float rangedCooldown = .18f;
        [SerializeField] private float rangedSpeed = 760f;
        [SerializeField] private float rangedDamage = 12f;
        [SerializeField] private float rangedLife = 1.45f;

        [Header("Dash Attack")]
        [SerializeField] private float dashDistance = 115f;
        [SerializeField] private float dashCooldown = 2.4f;
        [SerializeField] private float dashInvincibility = .4f;
        [SerializeField] private float dashRange = 128f;
        [SerializeField] private float dashDamage = 30f;
        [SerializeField] private float dashKnockback = 75f;

        [Header("Items")]
        [SerializeField] private ProjectileController bottlePrefab;
        [SerializeField] private ProjectileController cratePrefab;

        public Vector2 Aim { get; private set; } = Vector2.right;
        public bool SecondaryReady => secondaryCooldownTimer <= 0f;

        private Vector2 velocity;
        private float primaryCooldownTimer, secondaryCooldownTimer;
        private Rect arenaBounds;

        public void ResetToStart(Rect arena)
        {
            arenaBounds = arena;
            transform.position = arena.center;
            velocity = Vector2.zero;
            primaryCooldownTimer = secondaryCooldownTimer = 0f;
            Aim = Vector2.right;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            float dt = Time.deltaTime;
            primaryCooldownTimer -= dt;
            secondaryCooldownTimer -= dt;

            var kb = Keyboard.current;
            if (kb == null) return;

            Vector2 input = Vector2.zero;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x--;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x++;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y++;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y--;
            if (input.sqrMagnitude > 0f) { input.Normalize(); Aim = input; }

            velocity = Vector2.Lerp(velocity, input * moveSpeed, 1f - Mathf.Exp(-dt * accelSharpness));
            Vector2 pos = (Vector2)transform.position + velocity * dt;
            pos.x = Mathf.Clamp(pos.x, arenaBounds.xMin + bodyRadius, arenaBounds.xMax - bodyRadius);
            pos.y = Mathf.Clamp(pos.y, arenaBounds.yMin + bodyRadius, arenaBounds.yMax - bodyRadius);
            transform.position = pos;

            if (kb.jKey.isPressed && primaryCooldownTimer <= 0f) PrimaryAttack();
            if (WasPressed(kb.kKey) && secondaryCooldownTimer <= 0f) DashAttack(input);
            if (WasPressed(kb.iKey) && gm.bottles > 0) ThrowItem(bottlePrefab, true);
            if (WasPressed(kb.oKey) && gm.crates > 0) ThrowItem(cratePrefab, false);
        }

        private static bool WasPressed(KeyControl key) => key != null && key.wasPressedThisFrame;

        private void PrimaryAttack()
        {
            var gm = GameManager.Instance;
            IDamageableEnemy nearest = FindNearestEnemy(out Vector2 nearestPos, out float nearestDist);
            if (nearest != null && nearestDist < meleeRange)
            {
                primaryCooldownTimer = meleeCooldown;
                Aim = (nearestPos - (Vector2)transform.position).normalized;
                nearest.TakeDamage(meleeDamage, Aim * meleeKnockback);
                gm.Burst(nearestPos, new Color(.96f, .78f, .37f), 12, 210f);
                gm.Shake(7f); gm.HitStop(.035f);
            }
            else
            {
                primaryCooldownTimer = rangedCooldown;
                Vector2 dir = nearest != null ? (nearestPos - (Vector2)transform.position).normalized : Aim;
                Aim = dir;
                FireProjectile(rangedShotPrefab, dir, rangedSpeed, rangedDamage, rangedLife, 0);
                gm.Burst((Vector2)transform.position + dir * 36f, new Color(.96f, .78f, .37f), 3, 90f);
            }
        }

        private void DashAttack(Vector2 input)
        {
            var gm = GameManager.Instance;
            Vector2 dir = input.sqrMagnitude > 0f ? input.normalized : Aim;
            Vector2 pos = (Vector2)transform.position + dir * dashDistance;
            pos.x = Mathf.Clamp(pos.x, arenaBounds.xMin + bodyRadius, arenaBounds.xMax - bodyRadius);
            pos.y = Mathf.Clamp(pos.y, arenaBounds.yMin + bodyRadius, arenaBounds.yMax - bodyRadius);
            transform.position = pos;

            secondaryCooldownTimer = dashCooldown;
            gm.invincibleTimer = dashInvincibility;
            gm.Shake(9f);
            gm.Burst(pos, new Color(.94f, .36f, .37f), 20, 260f);

            foreach (var enemy in FindAllEnemiesInRange(pos, dashRange))
                enemy.TakeDamage(dashDamage, (enemy.Position - pos).normalized * dashKnockback);
        }

        private void ThrowItem(ProjectileController prefab, bool isBottle)
        {
            var gm = GameManager.Instance;
            IDamageableEnemy nearest = FindNearestEnemy(out Vector2 nearestPos, out _);
            Vector2 dir = nearest != null ? (nearestPos - (Vector2)transform.position).normalized : Aim;
            if (isBottle) gm.ConsumeBottle(); else gm.ConsumeCrate();
            FireProjectile(prefab, dir,
                isBottle ? 470f : 580f,
                isBottle ? 35f : 44f,
                isBottle ? .72f : 1.65f,
                isBottle ? 0 : 3);
        }

        private void FireProjectile(ProjectileController prefab, Vector2 dir, float speed, float damage, float life, int pierce)
        {
            if (!prefab) return;
            Vector2 spawnPos = (Vector2)transform.position + dir * 36f;
            ProjectileController shot = Instantiate(prefab, spawnPos, Quaternion.identity);
            shot.Launch(dir * speed, damage, life, pierce);
        }

        private IDamageableEnemy FindNearestEnemy(out Vector2 pos, out float dist)
        {
            IDamageableEnemy best = null;
            float bestSq = float.MaxValue;
            pos = default; dist = float.MaxValue;
            var enemies = GameManager.Instance.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var candidate = enemies[i];
                float sq = ((Vector2)candidate.Position - (Vector2)transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = candidate; }
            }
            if (best != null) { pos = best.Position; dist = Mathf.Sqrt(bestSq); }
            return best;
        }

        private List<IDamageableEnemy> FindAllEnemiesInRange(Vector2 center, float range)
        {
            var result = new List<IDamageableEnemy>();
            var enemies = GameManager.Instance.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
                if (Vector2.Distance(center, enemies[i].Position) < range) result.Add(enemies[i]);
            return result;
        }

        public void ApplyKnockback(Vector2 impulse) => velocity += impulse;
    }
}
