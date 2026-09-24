using UnityEngine;

namespace DoodleArena
{
    // Spawns waves along the arena edges (never right on top of the player)
    // and the boss in the middle.
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private EnemyController chaserPrefab;
        [SerializeField] private EnemyController shooterPrefab;
        [SerializeField] private EnemyController tankPrefab;
        [SerializeField] private BossController bossPrefab;

        [Header("Spawning")]
        [SerializeField] private float minDistanceFromPlayer = 3.75f;
        [SerializeField] private float edgeInset = 0.4f;

        public void ClearAll()
        {
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) Destroy(enemy.gameObject);
            foreach (var boss in FindObjectsByType<BossController>(FindObjectsSortMode.None)) Destroy(boss.gameObject);
            foreach (var shot in FindObjectsByType<ProjectileController>(FindObjectsSortMode.None)) Destroy(shot.gameObject);
        }

        // Wave 1 is all chasers, wave 2 mixes in shooters, wave 3 adds tanks.
        // Later rounds just add more bodies.
        public void SpawnWave(int round, int wave)
        {
            int count = 2 + round + wave * 2;
            for (int i = 0; i < count; i++)
            {
                EnemyKind kind = EnemyKind.Chaser;
                if (wave >= 2 && i % 3 == 1) kind = EnemyKind.Shooter;
                if (wave >= 3 && i % 4 == 2) kind = EnemyKind.Tank;
                SpawnEnemy(kind, round);
            }
        }

        public void SpawnEnemy(EnemyKind kind, int round)
        {
            EnemyController prefab = kind switch
            {
                EnemyKind.Shooter => shooterPrefab,
                EnemyKind.Tank => tankPrefab,
                _ => chaserPrefab,
            };
            if (!prefab) return;
            EnemyController enemy = Instantiate(prefab, PickSpawnPoint(), Quaternion.identity, transform);
            enemy.Initialize(kind, round);
        }

        public void SpawnBoss(int round)
        {
            if (!bossPrefab || !GameManager.Instance) return;
            BossController boss = Instantiate(bossPrefab, GameManager.Instance.arena.center, Quaternion.identity, transform);
            boss.Initialize(round);
        }

        private Vector2 PickSpawnPoint()
        {
            var gm = GameManager.Instance;
            Rect area = gm.arena;
            area.xMin += edgeInset; area.xMax -= edgeInset;
            area.yMin += edgeInset; area.yMax -= edgeInset;
            Vector2 playerPos = gm.player ? (Vector2)gm.player.transform.position : area.center;

            Vector2 pos = area.center;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                pos = RandomPointOnEdge(area);
                if (Vector2.Distance(pos, playerPos) >= minDistanceFromPlayer) break;
            }
            return pos;
        }

        private static Vector2 RandomPointOnEdge(Rect r)
        {
            switch (Random.Range(0, 4))
            {
                case 0:  return new Vector2(r.xMin, Random.Range(r.yMin, r.yMax));
                case 1:  return new Vector2(r.xMax, Random.Range(r.yMin, r.yMax));
                case 2:  return new Vector2(Random.Range(r.xMin, r.xMax), r.yMin);
                default: return new Vector2(Random.Range(r.xMin, r.xMax), r.yMax);
            }
        }
    }
}
