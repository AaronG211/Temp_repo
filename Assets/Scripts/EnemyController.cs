using UnityEngine;

namespace DoodleArena
{
    // Yipeng owns this file: each enemy kind is now a real MonoBehaviour on a
    // prefab with a Rigidbody2D + Collider2D, spawned with Instantiate instead
    // of appended to a List<Enemy>. Enemy-vs-enemy separation, which used to be
    // a manual pairwise distance loop, is now just Unity's own 2D physics
    // resolving collisions between each enemy's solid, non-trigger collider.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyController : MonoBehaviour, IDamageableEnemy
    {
        [Header("Identity")]
        [SerializeField] private EnemyKind kind;

        [Header("Base stats (tuned per-kind at spawn time)")]
        [SerializeField] private float baseRadius = 24f;
        [SerializeField] private float contactDamage = 11f;
        [SerializeField] private float contactCooldown = .9f;
        [SerializeField] private float chaseSpeed = 120f;
        [SerializeField] private float chaseSpeedPerRound = 9f;
        [SerializeField] private float tankSpeed = 64f;
        [SerializeField] private float tankContactDamage = 18f;
        [SerializeField] private float tankContactCooldown = 1.35f;
        [SerializeField] private float shooterPreferredNear = 260f;
        [SerializeField] private float shooterPreferredFar = 390f;
        [SerializeField] private float shooterFireInterval = 1.25f;
        [SerializeField] private float shooterFireIntervalPerRound = .04f;
        [SerializeField] private ProjectileController enemyShotPrefab;

        [Header("Mini health bar (hidden until damaged, matches the original's DrawMiniHealth)")]
        [SerializeField] private GameObject healthBarRoot;
        [SerializeField] private Transform healthBarFill;
        [SerializeField] private float healthBarWidth = 48f;

        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public EnemyKind Kind => kind;
        public Vector2 Position => transform.position;

        private Rigidbody2D body;
        private CircleCollider2D bodyCollider;
        private SpriteRenderer spriteRenderer;
        private Color baseColor;
        private float attackTimer;
        private float phase;
        private float flashTimer;
        private int round;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            bodyCollider = GetComponent<CircleCollider2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer) baseColor = spriteRenderer.color;
        }

        // Called by EnemySpawner right after Instantiate.
        public void Initialize(EnemyKind enemyKind, int currentRound)
        {
            kind = enemyKind;
            round = currentRound;

            float radius = kind == EnemyKind.Tank ? baseRadius * 1.75f
                : kind == EnemyKind.Shooter ? baseRadius * 1.33f
                : baseRadius;
            MaxHp = kind == EnemyKind.Tank ? 74f + round * 8f
                : kind == EnemyKind.Shooter ? 38f + round * 4f
                : 44f + round * 5f;
            Hp = MaxHp;
            attackTimer = Random.Range(.35f, 1.1f);
            phase = Random.value * 6f;
            if (bodyCollider) bodyCollider.radius = radius;
            RefreshHealthBar();
        }

        private void OnEnable() { if (GameManager.Instance) GameManager.Instance.RegisterEnemy(this); }
        private void OnDisable() { if (GameManager.Instance) GameManager.Instance.UnregisterEnemy(this); }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            float dt = Time.deltaTime;
            attackTimer -= dt;
            phase += dt;
            flashTimer -= dt;
            if (spriteRenderer) spriteRenderer.color = flashTimer > 0f ? Color.white : baseColor;

            Vector2 playerPos = gm.player ? (Vector2)gm.player.transform.position : Position;
            Vector2 toPlayer = playerPos - Position;
            float dist = Mathf.Max(1f, toPlayer.magnitude);
            Vector2 dir = toPlayer / dist;
            float radius = bodyCollider ? bodyCollider.radius : baseRadius;

            switch (kind)
            {
                case EnemyKind.Chaser:
                    body.linearVelocity = Vector2.Lerp(body.linearVelocity, dir * (chaseSpeed + round * chaseSpeedPerRound), dt * 4f);
                    if (dist < radius + 32f && attackTimer <= 0f)
                    {
                        gm.HurtPlayer(contactDamage, dir);
                        attackTimer = contactCooldown;
                    }
                    break;

                case EnemyKind.Tank:
                    body.linearVelocity = Vector2.Lerp(body.linearVelocity, dir * tankSpeed, dt * 3f);
                    if (dist < radius + 38f && attackTimer <= 0f)
                    {
                        gm.HurtPlayer(tankContactDamage, dir);
                        attackTimer = tankContactCooldown;
                        gm.Shake(10f);
                    }
                    break;

                case EnemyKind.Shooter:
                    float desired = dist > shooterPreferredFar ? 95f : dist < shooterPreferredNear ? -100f : 0f;
                    Vector2 strafe = new Vector2(-dir.y, dir.x) * Mathf.Sin(phase * 2f) * 45f;
                    body.linearVelocity = Vector2.Lerp(body.linearVelocity, dir * desired + strafe, dt * 3f);
                    if (attackTimer <= 0f)
                    {
                        FireAt(dir, 285f + round * 8f, 9f);
                        attackTimer = shooterFireInterval - Mathf.Min(.3f, round * shooterFireIntervalPerRound);
                    }
                    break;
            }
        }

        private void FireAt(Vector2 dir, float speed, float damage)
        {
            if (!enemyShotPrefab) return;
            ProjectileController shot = Instantiate(enemyShotPrefab, Position + dir * 42f, Quaternion.identity);
            shot.Launch(dir * speed, damage, 4f, 0);
        }

        public void TakeDamage(float amount, Vector2 push)
        {
            Hp -= amount;
            body.linearVelocity += push;
            flashTimer = .1f;
            RefreshHealthBar();
            if (GameManager.Instance) GameManager.Instance.Burst(Position, new Color(.42f, .29f, .71f), 5, 145f);
            if (Hp > 0f) return;
            if (GameManager.Instance) GameManager.Instance.RegisterKill(kind, Position);
            Destroy(gameObject);
        }

        // Only shown once an enemy has taken damage, same as the original's
        // "if (e.hp < e.maxHp) DrawMiniHealth(e)" check. The fill sprite has a
        // centered pivot, so shrinking its scale also nudges its position left
        // by half the lost width, keeping the bar's left edge pinned in place
        // instead of shrinking symmetrically from both sides.
        private void RefreshHealthBar()
        {
            if (!healthBarRoot) return;
            bool show = kind != EnemyKind.Boss && Hp > 0f && Hp < MaxHp;
            healthBarRoot.SetActive(show);
            if (!show || !healthBarFill) return;
            float ratio = MaxHp > 0f ? Mathf.Clamp01(Hp / MaxHp) : 0f;
            var scale = healthBarFill.localScale;
            scale.x = healthBarWidth * ratio;
            healthBarFill.localScale = scale;
            var pos = healthBarFill.localPosition;
            pos.x = -(healthBarWidth * (1f - ratio)) * .5f;
            healthBarFill.localPosition = pos;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var projectile = other.GetComponent<ProjectileController>();
            if (projectile != null) projectile.HitEnemy(this);
        }
    }
}
