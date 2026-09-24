using UnityEngine;

namespace DoodleArena
{
    // Every bullet and thrown item. Kind decides who it hurts and what happens on hit:
    //   Player  - single hit
    //   Crate   - passes through a few enemies, losing damage each time
    //   Bottle  - slows down, then explodes (on impact or when its time runs out)
    //   Enemy / BossOrb - only hurt the player
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class ProjectileController : MonoBehaviour
    {
        [SerializeField] private ShotKind kind;
        [SerializeField] private float explosionRadius = 1.45f;
        [SerializeField] private float bottleDrag = 0.9f;
        [SerializeField] private float pierceDamageFalloff = 0.78f;

        private Rigidbody2D body;
        private float life;
        private float damage;
        private int pierce;
        private bool dead;

        private bool HurtsPlayer => kind == ShotKind.Enemy || kind == ShotKind.BossOrb;

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
            if (kind == ShotKind.Bottle) body.linearVelocity *= 1f - Time.deltaTime * bottleDrag;

            var gm = GameManager.Instance;
            bool outOfBounds = gm && !gm.arena.Contains(transform.position);
            if (life > 0f && !outOfBounds) return;

            if (kind == ShotKind.Bottle) Explode();
            else Remove();
        }

        // Enemy shots check for the player here. Player shots are reported from the
        // enemy's side (EnemyController/BossController call HitEnemy).
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (dead || !HurtsPlayer) return;
            if (other.GetComponent<PlayerController>() == null) return;
            if (GameManager.Instance) GameManager.Instance.HurtPlayer(damage, body.linearVelocity.normalized);
            Remove();
        }

        public void HitEnemy(IDamageableEnemy enemy)
        {
            if (dead || HurtsPlayer) return;

            if (kind == ShotKind.Bottle)
            {
                Explode();
                return;
            }

            float pushStrength = kind == ShotKind.Crate ? 0.48f : 0.16f;
            enemy.TakeDamage(damage, body.linearVelocity.normalized * pushStrength);

            if (kind == ShotKind.Crate && pierce-- > 0)
            {
                damage *= pierceDamageFalloff;
                return;
            }
            Remove();
        }

        private void Explode()
        {
            if (dead) return;
            dead = true;

            var gm = GameManager.Instance;
            if (gm)
            {
                Vector2 center = transform.position;
                gm.Shake(0.14f);
                gm.HitStop(0.045f);
                gm.Burst(center, Palette.Red, 35, 3.6f);

                // iterate backwards: a kill can remove the enemy from the list
                var enemies = gm.ActiveEnemies;
                for (int i = enemies.Count - 1; i >= 0; i--)
                {
                    var enemy = enemies[i];
                    if (Vector2.Distance(center, enemy.Position) < explosionRadius)
                        enemy.TakeDamage(damage, (enemy.Position - center).normalized * 0.8f);
                }
            }
            Destroy(gameObject);
        }

        private void Remove()
        {
            if (dead) return;
            dead = true;
            Destroy(gameObject);
        }
    }
}
