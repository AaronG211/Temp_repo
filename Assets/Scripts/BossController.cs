using UnityEngine;

namespace DoodleArena
{
    // Aaron owns this file: the boss used to be a method (UpdateBoss) reaching
    // into a plain Enemy struct-like object inside the shared list. It is now
    // its own prefab with its own phase logic living on this component.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class BossController : MonoBehaviour, IDamageableEnemy
    {
        [Header("Stats")]
        [SerializeField] private float baseHp = 420f;
        [SerializeField] private float hpPerRound = 140f;
        [SerializeField] private float radius = 72f;
        [SerializeField] private ProjectileController orbPrefab;

        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public EnemyKind Kind => EnemyKind.Boss;
        public Vector2 Position => transform.position;

        private Rigidbody2D body;
        private CircleCollider2D bodyCollider;
        private float attackTimer;
        private float phase;
        private int round;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            bodyCollider = GetComponent<CircleCollider2D>();
        }

        public void Initialize(int currentRound)
        {
            round = currentRound;
            MaxHp = baseHp + round * hpPerRound;
            Hp = MaxHp;
            if (bodyCollider) bodyCollider.radius = radius;
        }

        private void OnEnable() { if (GameManager.Instance) GameManager.Instance.RegisterEnemy(this); }
        private void OnDisable() { if (GameManager.Instance) GameManager.Instance.UnregisterEnemy(this); }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            float dt = Time.deltaTime;
            phase += dt;
            attackTimer -= dt;

            Vector2 playerPos = gm.player ? (Vector2)gm.player.transform.position : Position;
            Vector2 toPlayer = playerPos - Position;
            float dist = Mathf.Max(1f, toPlayer.magnitude);
            Vector2 dir = toPlayer / dist;
            float hpRatio = Hp / MaxHp;

            Vector2 strafe = new Vector2(-dir.y, dir.x) * Mathf.Sin(phase) * 80f;
            body.linearVelocity = Vector2.Lerp(body.linearVelocity, dir * (hpRatio > .5f ? 54f : 78f) + strafe, dt * 2f);

            if (attackTimer > 0f) return;

            if (hpRatio > .65f)
            {
                for (int i = -2; i <= 2; i++) FireOrb(Rotate(dir, i * 13f), 320f, 12f);
                attackTimer = 1.35f;
            }
            else if (hpRatio > .32f)
            {
                for (int i = 0; i < 12; i++) FireOrb(Rotate(Vector2.right, i * 30f + phase * 20f), 245f, 10f);
                attackTimer = 1.05f;
            }
            else
            {
                for (int i = -3; i <= 3; i++) FireOrb(Rotate(dir, i * 11f), 390f, 13f);
                if (gm.ActiveMinionCount(true) < 2) gm.RequestMinionSpawn();
                attackTimer = .82f;
                gm.Shake(5f);
            }
        }

        private void FireOrb(Vector2 dir, float speed, float damage)
        {
            if (!orbPrefab) return;
            ProjectileController shot = Instantiate(orbPrefab, Position + dir * 42f, Quaternion.identity);
            shot.Launch(dir * speed, damage, 4f, 0);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));
        }

        public void TakeDamage(float amount, Vector2 push)
        {
            Hp -= amount;
            body.linearVelocity += push;
            if (GameManager.Instance) GameManager.Instance.Burst(Position, new Color(.42f, .29f, .71f), 5, 145f);
            if (Hp > 0f) return;
            if (GameManager.Instance) GameManager.Instance.RegisterKill(Kind, Position);
            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var projectile = other.GetComponent<ProjectileController>();
            if (projectile != null) projectile.HitEnemy(this);
        }
    }
}
