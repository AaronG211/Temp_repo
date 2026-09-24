using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleArena
{
    // Movement, the auto-targeting primary attack (punch when close, shoot when not),
    // the dash smash, and throwing bottles/crates.
    [RequireComponent(typeof(Collider2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3.6f;
        [SerializeField] private float acceleration = 14f;
        [SerializeField] private float bodyRadius = 0.28f;

        [Header("Punch")]
        [SerializeField] private float meleeRange = 1.05f;
        [SerializeField] private float meleeDamage = 22f;
        [SerializeField] private float meleeCooldown = 0.3f;
        [SerializeField] private float meleeKnockback = 0.35f;

        [Header("Shot")]
        [SerializeField] private ProjectileController rangedShotPrefab;
        [SerializeField] private float rangedCooldown = 0.18f;
        [SerializeField] private float rangedSpeed = 7.5f;
        [SerializeField] private float rangedDamage = 12f;
        [SerializeField] private float rangedLife = 1.5f;

        [Header("Dash")]
        [SerializeField] private float dashDistance = 1.15f;
        [SerializeField] private float dashCooldown = 2.4f;
        [SerializeField] private float dashInvincibility = 0.4f;
        [SerializeField] private float dashRadius = 1.3f;
        [SerializeField] private float dashDamage = 30f;
        [SerializeField] private float dashKnockback = 0.75f;

        [Header("Bottle (explodes)")]
        [SerializeField] private ProjectileController bottlePrefab;
        [SerializeField] private float bottleSpeed = 4.7f;
        [SerializeField] private float bottleDamage = 35f;
        [SerializeField] private float bottleLife = 0.7f;

        [Header("Crate (pierces)")]
        [SerializeField] private ProjectileController cratePrefab;
        [SerializeField] private float crateSpeed = 5.8f;
        [SerializeField] private float crateDamage = 44f;
        [SerializeField] private float crateLife = 1.6f;
        [SerializeField] private int cratePierce = 3;

        [SerializeField] private float muzzleOffset = 0.36f;

        public Vector2 Aim { get; private set; } = Vector2.right;
        public bool DashReady => dashTimer <= 0f;

        private Vector2 velocity;
        private float attackTimer, dashTimer;
        private Rect arenaBounds;
        private InputAction moveAction, attackAction, dashAction, bottleAction, crateAction;

        private void Start()
        {
            var controls = GameManager.Instance ? GameManager.Instance.Controls : null;
            if (controls == null) return;
            moveAction = controls["Move"];
            attackAction = controls["Attack"];
            dashAction = controls["Dash"];
            bottleAction = controls["Bottle"];
            crateAction = controls["Crate"];
        }

        public void ResetToStart(Rect arena)
        {
            arenaBounds = arena;
            transform.position = arena.center;
            velocity = Vector2.zero;
            attackTimer = dashTimer = 0f;
            Aim = Vector2.right;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying || moveAction == null) return;

            float dt = Time.deltaTime;
            attackTimer -= dt;
            dashTimer -= dt;

            Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            if (input.sqrMagnitude > 0.01f) Aim = input.normalized;

            // ease toward the target velocity instead of snapping
            velocity = Vector2.Lerp(velocity, input * moveSpeed, 1f - Mathf.Exp(-dt * acceleration));
            transform.position = ClampToArena((Vector2)transform.position + velocity * dt);

            if (attackAction.IsPressed() && attackTimer <= 0f) PrimaryAttack();
            if (dashAction.WasPressedThisFrame() && DashReady) Dash(input);
            if (bottleAction.WasPressedThisFrame() && gm.bottles > 0) ThrowBottle();
            if (crateAction.WasPressedThisFrame() && gm.crates > 0) ThrowCrate();
        }

        private Vector2 ClampToArena(Vector2 pos)
        {
            pos.x = Mathf.Clamp(pos.x, arenaBounds.xMin + bodyRadius, arenaBounds.xMax - bodyRadius);
            pos.y = Mathf.Clamp(pos.y, arenaBounds.yMin + bodyRadius, arenaBounds.yMax - bodyRadius);
            return pos;
        }

        private void PrimaryAttack()
        {
            var gm = GameManager.Instance;
            IDamageableEnemy target = FindNearestEnemy(out Vector2 targetPos, out float targetDist);

            if (target != null && targetDist < meleeRange)
            {
                attackTimer = meleeCooldown;
                Aim = (targetPos - (Vector2)transform.position).normalized;
                target.TakeDamage(meleeDamage, Aim * meleeKnockback);
                gm.Burst(targetPos, Palette.Gold, 12, 2.1f);
                gm.Shake(0.07f);
                gm.HitStop(0.035f);
                return;
            }

            attackTimer = rangedCooldown;
            if (target != null) Aim = (targetPos - (Vector2)transform.position).normalized;
            Fire(rangedShotPrefab, rangedSpeed, rangedDamage, rangedLife, 0);
            gm.Burst((Vector2)transform.position + Aim * muzzleOffset, Palette.Gold, 3, 0.9f);
        }

        private void Dash(Vector2 input)
        {
            var gm = GameManager.Instance;
            Vector2 dir = input.sqrMagnitude > 0.01f ? input.normalized : Aim;
            Vector2 pos = ClampToArena((Vector2)transform.position + dir * dashDistance);
            transform.position = pos;

            dashTimer = dashCooldown;
            gm.invincibleTimer = dashInvincibility;
            gm.Shake(0.09f);
            gm.Burst(pos, Palette.Red, 20, 2.6f);

            foreach (var enemy in EnemiesInRange(pos, dashRadius))
                enemy.TakeDamage(dashDamage, (enemy.Position - pos).normalized * dashKnockback);
        }

        private void ThrowBottle()
        {
            GameManager.Instance.ConsumeBottle();
            AimAtNearest();
            Fire(bottlePrefab, bottleSpeed, bottleDamage, bottleLife, 0);
        }

        private void ThrowCrate()
        {
            GameManager.Instance.ConsumeCrate();
            AimAtNearest();
            Fire(cratePrefab, crateSpeed, crateDamage, crateLife, cratePierce);
        }

        private void AimAtNearest()
        {
            if (FindNearestEnemy(out Vector2 pos, out _) != null)
                Aim = (pos - (Vector2)transform.position).normalized;
        }

        private void Fire(ProjectileController prefab, float speed, float damage, float life, int pierce)
        {
            if (!prefab) return;
            Vector2 spawnPos = (Vector2)transform.position + Aim * muzzleOffset;
            ProjectileController shot = Instantiate(prefab, spawnPos, Quaternion.identity);
            shot.Launch(Aim * speed, damage, life, pierce);
        }

        private IDamageableEnemy FindNearestEnemy(out Vector2 pos, out float dist)
        {
            IDamageableEnemy best = null;
            float bestSq = float.MaxValue;
            Vector2 me = transform.position;
            foreach (var enemy in GameManager.Instance.ActiveEnemies)
            {
                float sq = (enemy.Position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = enemy; }
            }
            pos = best != null ? best.Position : default;
            dist = best != null ? Mathf.Sqrt(bestSq) : float.MaxValue;
            return best;
        }

        private List<IDamageableEnemy> EnemiesInRange(Vector2 center, float range)
        {
            // copy first: TakeDamage can kill an enemy and remove it from the live list
            var result = new List<IDamageableEnemy>();
            foreach (var enemy in GameManager.Instance.ActiveEnemies)
                if (Vector2.Distance(center, enemy.Position) < range) result.Add(enemy);
            return result;
        }

        public void ApplyKnockback(Vector2 impulse) => velocity += impulse;
    }
}
