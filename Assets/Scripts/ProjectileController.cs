using UnityEngine;

namespace DoodleArena
{
    // Shared between Aaron's and Yipeng's pieces: Shot used to be a plain C#
    // class appended to a List<Shot> and moved/collided by hand every frame in
    // DoodleArenaGame.cs. It is now a real prefab with a Rigidbody2D driving
    // its motion and a trigger Collider2D generating real collision callbacks.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class ProjectileController : MonoBehaviour
    {
        [Tooltip("Set per-prefab: which kind of shot this prefab represents.")]
        [SerializeField] private ShotKind kind;
        [SerializeField] private float explosionRadius = 145f;
        [SerializeField] private float bottleDragPerSecond = .9f;

        private Rigidbody2D body;
        private float life;
        private float damage;
        private int pierce;
        private bool dead;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
        }

        public void Launch(Vector2 velocity, float damageAmount, float lifeSeconds, int pierceCount)
        {
            body.linearVelocity = velocity;
            damage = damageAmount;
            life = lifeSeconds;
            pierce = pierceCount;
            dead = false;
        }

        private void Update()
        {
            life -= Time.deltaTime;
            if (kind == ShotKind.Bottle) body.linearVelocity *= 1f - Time.deltaTime * bottleDragPerSecond;

            Rect arena = GameManager.Instance ? GameManager.Instance.arena : new Rect(-100000f, -100000f, 200000f, 200000f);
            bool outOfBounds = !arena.Contains(transform.position);
            if (life <= 0f || outOfBounds)
            {
                if (kind == ShotKind.Bottle) Explode();
                else SafeDestroy();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (dead) return;
            if (kind == ShotKind.Enemy || kind == ShotKind.BossOrb)
            {
                var player = other.GetComponent<PlayerController>();
                if (player == null) return;
                if (GameManager.Instance) GameManager.Instance.HurtPlayer(damage, body.linearVelocity.normalized);
                SafeDestroy();
            }
            // Player/Bottle/Crate shots hitting enemies are reported from the
            // enemy side (EnemyController/BossController.OnTriggerEnter2D calls
            // HitEnemy below), so there's nothing else to do here.
        }

        public void HitEnemy(IDamageableEnemy enemy)
        {
            if (dead) return;
            if (kind == ShotKind.Enemy || kind == ShotKind.BossOrb) return; // enemy shots don't hurt enemies

            if (kind == ShotKind.Bottle) { Explode(); return; }

            Vector2 push = body.linearVelocity.normalized * (kind == ShotKind.Crate ? 48f : 16f);
            enemy.TakeDamage(damage, push);

            if (kind == ShotKind.Crate && pierce-- > 0)
            {
                damage *= .78f;
                return; // keep flying, can still hit another enemy
            }
            SafeDestroy();
        }

        private void Explode()
        {
            if (dead) return;
            dead = true;
            if (GameManager.Instance)
            {
                GameManager.Instance.Shake(14f);
                GameManager.Instance.HitStop(.045f);
                GameManager.Instance.Burst(transform.position, new Color(.94f, .36f, .37f), 35, 360f);
                var enemies = GameManager.Instance.ActiveEnemies;
                for (int i = enemies.Count - 1; i >= 0; i--)
                {
                    var enemy = enemies[i];
                    if (Vector2.Distance(transform.position, enemy.Position) < explosionRadius)
                        enemy.TakeDamage(damage, (enemy.Position - (Vector2)transform.position).normalized * 80f);
                }
            }
            Destroy(gameObject);
        }

        private void SafeDestroy()
        {
            if (dead) return;
            dead = true;
            Destroy(gameObject);
        }
    }
}
