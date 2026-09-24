using UnityEngine;

namespace DoodleArena
{
    // The Overseer. Attack pattern changes as its HP drops:
    //   above 65%  - aimed 5-shot spread
    //   32% - 65%  - rotating 12-orb ring
    //   below 32%  - faster 7-shot spread and it calls in Chaser minions
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class BossController : MonoBehaviour, IDamageableEnemy
    {
        [Header("Stats")]
        [SerializeField] private float baseHp = 420f;
        [SerializeField] private float hpPerRound = 140f;
        [SerializeField] private float moveSpeed = 0.7f;
        [SerializeField] private float enragedMoveSpeed = 1f;
        [SerializeField] private float strafeAmount = 1f;

        [Header("Attacks")]
        [SerializeField] private ProjectileController orbPrefab;
        [SerializeField] private float phaseTwoAt = 0.65f;
        [SerializeField] private float phaseThreeAt = 0.32f;
        [SerializeField] private int maxMinions = 2;

        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public EnemyKind Kind => EnemyKind.Boss;
        public Vector2 Position => transform.position;

        private Rigidbody2D body;
        private CircleCollider2D bodyCollider;
        private float attackTimer;
        private float timeAlive;
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
            attackTimer = 1f;
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
            timeAlive += dt;
            attackTimer -= dt;

            Vector2 playerPos = gm.player ? (Vector2)gm.player.transform.position : Position;
            Vector2 toPlayer = playerPos - Position;
            Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.down;
            float hpRatio = Hp / MaxHp;

            float speed = hpRatio > 0.5f ? moveSpeed : enragedMoveSpeed;
            Vector2 strafe = new Vector2(-dir.y, dir.x) * Mathf.Sin(timeAlive) * strafeAmount;
            body.linearVelocity = Vector2.Lerp(body.linearVelocity, dir * speed + strafe, dt * 2f);

            if (attackTimer > 0f) return;

            if (hpRatio > phaseTwoAt)
            {
                FireSpread(dir, 5, 13f, 4f, 12f);
                attackTimer = 1.35f;
            }
            else if (hpRatio > phaseThreeAt)
            {
                float spin = timeAlive * 20f;
                for (int i = 0; i < 12; i++) FireOrb(Rotate(Vector2.right, i * 30f + spin), 3.05f, 10f);
                attackTimer = 1.05f;
            }
            else
            {
                FireSpread(dir, 7, 11f, 4.9f, 13f);
                if (gm.ActiveMinionCount() < maxMinions) gm.RequestMinionSpawn();
                gm.Shake(0.06f);
                attackTimer = 0.82f;
            }
        }

        private void FireSpread(Vector2 dir, int count, float degreesApart, float speed, float damage)
        {
            int half = count / 2;
            for (int i = -half; i <= half; i++) FireOrb(Rotate(dir, i * degreesApart), speed, damage);
        }

        private void FireOrb(Vector2 dir, float speed, float damage)
        {
            if (!orbPrefab) return;
            Vector2 spawnPos = Position + dir * (bodyCollider.radius * 0.6f);
            ProjectileController orb = Instantiate(orbPrefab, spawnPos, Quaternion.identity);
            orb.Launch(dir * speed, damage, 4f, 0);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        public void TakeDamage(float amount, Vector2 push)
        {
            if (Hp <= 0f) return;
            Hp -= amount;
            body.linearVelocity += push;

            var gm = GameManager.Instance;
            if (gm) gm.Burst(Position, Palette.Purple, 5, 1.8f);
            if (Hp > 0f) return;
            if (gm) gm.RegisterKill(Kind, Position);
            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var projectile = other.GetComponent<ProjectileController>();
            if (projectile != null) projectile.HitEnemy(this);
        }
    }
}
