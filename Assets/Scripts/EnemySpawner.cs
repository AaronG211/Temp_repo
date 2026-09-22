using UnityEngine;

namespace DoodleArena
{
    // Yipeng owns this file: SpawnEnemy used to create a plain Enemy object and
    // append it to a List<Enemy> with nothing ever placed in the scene.
    // SpawnEnemy/SpawnWave/SpawnBoss now call Instantiate on real prefabs.
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Enemy Prefabs")]
        [SerializeField] private EnemyController chaserPrefab;
        [SerializeField] private EnemyController shooterPrefab;
        [SerializeField] private EnemyController tankPrefab;
        [SerializeField] private BossController bossPrefab;

        [Header("Spawn Rules")]
        [SerializeField] private float minSpawnDistanceFromPlayer = 300f;

        public void ClearAll()
        {
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) Destroy(enemy.gameObject);
            foreach (var boss in FindObjectsByType<BossController>(FindObjectsSortMode.None)) Destroy(boss.gameObject);
            foreach (var shot in FindObjectsByType<ProjectileController>(FindObjectsSortMode.None)) Destroy(shot.gameObject);
        }

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
            EnemyController prefab = kind == EnemyKind.Shooter ? shooterPrefab
                : kind == EnemyKind.Tank ? tankPrefab
                : chaserPrefab;
            if (!prefab) return;
            Vector2 pos = FindSpawnPosition();
            EnemyController enemy = Instantiate(prefab, pos, Quaternion.identity, transform);
            enemy.Initialize(kind, round);
        }

        public void SpawnBoss(int round)
        {
            if (!bossPrefab || !GameManager.Instance) return;
            Vector2 pos = GameManager.Instance.arena.center;
            BossController boss = Instantiate(bossPrefab, pos, Quaternion.identity, transform);
            boss.Initialize(round);
        }

        private Vector2 FindSpawnPosition()
        {
            Rect arena = GameManager.Instance.arena;
            Vector2 playerPos = GameManager.Instance.player ? (Vector2)GameManager.Instance.player.transform.position : arena.center;
            Vector2 pos;
            int guard = 0;
            do
            {
                int edge = Random.Range(0, 4);
                pos = edge < 2
                    ? new Vector2(edge == 0 ? arena.xMin + 30f : arena.xMax - 30f, Random.Range(arena.yMin + 30f, arena.yMax - 30f))
                    : new Vector2(Random.Range(arena.xMin + 30f, arena.xMax - 30f), edge == 2 ? arena.yMin + 30f : arena.yMax - 30f);
                guard++;
            } while (Vector2.Distance(pos, playerPos) < minSpawnDistanceFromPlayer && guard < 20);
            return pos;
        }
    }
}
