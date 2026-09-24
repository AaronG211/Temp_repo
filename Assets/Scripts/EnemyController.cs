using UnityEngine;

namespace DoodleArena
{
    // One script for all three regular enemy types; Initialize() picks the stats.
    //   Chaser  - runs straight at the player
    //   Shooter - keeps its distance, strafes and fires
    //   Tank    - slow, lots of HP, hits hard
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyController : MonoBehaviour, IDamageableEnemy
    {
        [SerializeField] private EnemyKind kind;
        [SerializeField] private float baseRadius = 0.24f;

        [Header("Chaser")]
        [SerializeField] private float chaseSpeed = 1.2f;
        [SerializeField] private float chaseSpeedPerRound = 0.1f;
        [SerializeField] private float contactDamage = 11f;
        [SerializeField] private float contactCooldown = 0.9f;

        [Header("Tank")]
        [SerializeField] private float tankSpeed = 0.65f;
        [SerializeField] private float tankContactDamage = 18f;
        [SerializeField] private float tankContactCooldown = 1.35f;

        [Header("Shooter")]
        [SerializeField] private ProjectileController enemyShotPrefab;
        [SerializeField] private float shooterMinDistance = 2.6f;
        [SerializeField] private float shooterMaxDistance = 3.9f;
        [SerializeField] private float shooterMoveSpeed = 1f;
        [SerializeField] private float shooterFireInterval = 1.25f;
        [SerializeField] private float shotSpeed = 2.85f;
        [SerializeField] private float shotDamage = 9f;

        [Header("Health Bar")]
        [SerializeField] private GameObject healthBarRoot;
        [SerializeField] private Transform healthBarFill;
        [SerializeField] private float healthBarWidth = 0.48f;

        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public EnemyKind Kind => kind;
        public Vector2 Position => transform.position;

        private const float ContactReach = 0.32f;

        private Rigidbody2D body;
        private CircleCollider2D bodyCollider;
        private SpriteRenderer spriteRenderer;
        private Color baseColor;
        private float attackTimer;
        private float wobble;
        private float flashTimer;
        private int round;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            bodyCollider = GetComponent<CircleCollider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer) baseColor = spriteRenderer.color;
        }

        public void Initialize(EnemyKind enemyKind, int currentRound)
        {
            kind = enemyKind;
            round = currentRound;

            switch (kind)
            {
                case EnemyKind.Tank:    MaxHp = 75f + round * 8f; bodyCollider.radius = baseRadius * 1.75f; break;
                case EnemyKind.Shooter: MaxHp = 38f + round * 4f; bodyCollider.radius = baseRadius * 1.33f; break;
                default:                MaxHp = 44f + round * 5f; bodyCollider.radius = baseRadius; break;
            }
            Hp = MaxHp;

            // stagger first attacks so a fresh wave doesn't all fire on the same frame
            attackTimer = Random.Range(0.35f, 1.1f);
            wobble = Random.value * 6f;
            RefreshHealthBar();
        }

        private void OnEnable() { if (GameManager.Instance) GameManager.Instance.RegisterEnemy(this); }
        private void OnDisable() { if (GameManager.Instance) GameManager.Instance.UnregisterEnemy(this); }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            float dt = Time.deltaTime;
            attackTimer -= dt;
            wobble += dt;
            flashTimer -= dt;
            if (spriteRenderer) spriteRenderer.color = flashTimer > 0f ? Color.white : baseColor;

            Vector2 playerPos = gm.player ? (Vector2)gm.player.transform.position : Position;
            Vector2 toPlayer = playerPos - Position;
            float dist = Mathf.Max(0.01f, toPlayer.magnitude);
            Vector2 dir = toPlayer / dist;
            float reach = bodyCollider.radius + ContactReach;

            switch (kind)
            {
                case EnemyKind.Chaser:
                    MoveToward(dir * (chaseSpeed + round * chaseSpeedPerRound), 4f, dt);
                    if (dist < reach && attackTimer <= 0f)
                    {
                        gm.HurtPlayer(contactDamage, dir);
                        attackTimer = contactCooldown;
                    }
                    break;

                case EnemyKind.Tank:
                    MoveToward(dir * tankSpeed, 3f, dt);
                    if (dist < reach && attackTimer <= 0f)
                    {
                        gm.HurtPlayer(tankContactDamage, dir);
                        gm.Shake(0.1f);
                        attackTimer = tankContactCooldown;
                    }
                    break;

                case EnemyKind.Shooter:
                    // back off if too close, close in if too far, strafe side to side
                    float approach = dist > shooterMaxDistance ? 1f : dist < shooterMinDistance ? -1f : 0f;
                    Vector2 strafe = new Vector2(-dir.y, dir.x) * Mathf.Sin(wobble * 2f) * 0.45f;
                    MoveToward(dir * approach * shooterMoveSpeed + strafe, 3f, dt);
                    if (attackTimer <= 0f)
                    {
                        Fire(dir);
                        attackTimer = shooterFireInterval - Mathf.Min(0.3f, round * 0.05f);
                    }
                    break;
            }
        }

        private void MoveToward(Vector2 targetVelocity, float responsiveness, float dt)
        {
            body.linearVelocity = Vector2.Lerp(body.linearVelocity, targetVelocity, dt * responsiveness);
        }

        private void Fire(Vector2 dir)
        {
            if (!enemyShotPrefab) return;
            Vector2 spawnPos = Position + dir * (bodyCollider.radius + 0.1f);
            ProjectileController shot = Instantiate(enemyShotPrefab, spawnPos, Quaternion.identity);
            shot.Launch(dir * (shotSpeed + round * 0.08f), shotDamage, 4f, 0);
        }

        public void TakeDamage(float amount, Vector2 push)
        {
            if (Hp <= 0f) return;
            Hp -= amount;
            body.linearVelocity += push;
            flashTimer = 0.1f;
            RefreshHealthBar();

            var gm = GameManager.Instance;
            if (gm) gm.Burst(Position, Palette.Purple, 5, 1.45f);
            if (Hp > 0f) return;
            if (gm) gm.RegisterKill(kind, Position);
            Destroy(gameObject);
        }

        // Hidden at full HP. The fill sprite is centred, so when it shrinks we slide it
        // left by half the missing width to keep the bar's left edge in place.
        private void RefreshHealthBar()
        {
            if (!healthBarRoot) return;
            bool show = Hp > 0f && Hp < MaxHp;
            healthBarRoot.SetActive(show);
            if (!show || !healthBarFill) return;

            float ratio = Mathf.Clamp01(Hp / MaxHp);
            var scale = healthBarFill.localScale;
            scale.x = healthBarWidth * ratio;
            healthBarFill.localScale = scale;

            var pos = healthBarFill.localPosition;
            pos.x = -healthBarWidth * (1f - ratio) * 0.5f;
            healthBarFill.localPosition = pos;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var projectile = other.GetComponent<ProjectileController>();
            if (projectile != null) projectile.HitEnemy(this);
        }
    }
}
