using UnityEngine;

namespace DoodleArena
{
    // Yipeng's piece: Chaser, Shooter, and Tank behavior; enemy separation;
    // enemy projectiles; wave progression; and enemy spawning.
    // This is a partial class — it adds these methods onto the shared
    // DoodleArenaGame defined in Aaron's DoodleArenaGame.cs.
    public sealed partial class DoodleArenaGame
    {
        private void UpdateEnemies(float dt)
        {
            foreach (var e in enemies)
            {
                if (e.dead) continue;
                e.attackTimer -= dt;
                e.phase += dt;
                Vector2 toPlayer = playerPos - e.pos;
                float dist = Mathf.Max(1, toPlayer.magnitude);
                Vector2 dir = toPlayer / dist;

                switch (e.kind)
                {
                    case EnemyKind.Chaser:
                        e.vel = Vector2.Lerp(e.vel, dir * (120 + round * 9), dt * 4);
                        if (dist < e.radius + 32 && e.attackTimer <= 0) { HurtPlayer(11, dir); e.attackTimer = .9f; }
                        break;
                    case EnemyKind.Tank:
                        e.vel = Vector2.Lerp(e.vel, dir * 64, dt * 3);
                        if (dist < e.radius + 38 && e.attackTimer <= 0) { HurtPlayer(18, dir); e.attackTimer = 1.35f; shake = 10; }
                        break;
                    case EnemyKind.Shooter:
                        float desired = dist > 390 ? 95 : dist < 260 ? -100 : 0;
                        e.vel = Vector2.Lerp(e.vel, dir * desired + new Vector2(-dir.y, dir.x) * Mathf.Sin(e.phase * 2) * 45, dt * 3);
                        if (e.attackTimer <= 0)
                        {
                            FireEnemyShot(e.pos, dir, ShotKind.Enemy, 285 + round * 8, 9);
                            e.attackTimer = 1.25f - Mathf.Min(.3f, round * .04f);
                        }
                        break;
                    case EnemyKind.Boss:
                        // NOTE: Aaron's real DoodleArenaBoss.cs takes only (boss, direction, dt) —
                        // 3 args, not the 4-arg version from the old Temp_repo reference.
                        UpdateBoss(e, dir, dt);
                        break;
                }
                e.pos += e.vel * dt;
                e.pos.x = Mathf.Clamp(e.pos.x, arena.xMin + e.radius, arena.xMax - e.radius);
                e.pos.y = Mathf.Clamp(e.pos.y, arena.yMin + e.radius, arena.yMax - e.radius);
            }

            // Keep the crowd readable instead of letting enemies collapse into one blob.
            for (int i = 0; i < enemies.Count; i++)
            for (int j = i + 1; j < enemies.Count; j++)
            {
                Enemy a = enemies[i];
                Enemy b = enemies[j];
                if (a.dead || b.dead) continue;
                Vector2 delta = b.pos - a.pos;
                float minDistance = (a.radius + b.radius) * .82f;
                if (delta.sqrMagnitude <= .001f || delta.sqrMagnitude >= minDistance * minDistance) continue;

                float overlap = minDistance - delta.magnitude;
                Vector2 correction = delta.normalized * overlap * .5f;
                a.pos -= correction;
                b.pos += correction;
            }

            // Boss minions are queued during iteration and added only after the
            // enemy update completes, avoiding collection-modified exceptions.
            while (pendingChaserSpawns > 0)
            {
                pendingChaserSpawns--;
                SpawnEnemy(EnemyKind.Chaser);
            }
        }

        private void FireEnemyShot(Vector2 pos, Vector2 dir, ShotKind kind, float speed, float damage)
        {
            shots.Add(new Shot { pos = pos + dir * 42, vel = dir * speed, kind = kind, radius = kind == ShotKind.BossOrb ? 13 : 9, damage = damage, life = 4 });
        }

        private void UpdateWaveFlow(float dt)
        {
            if (state == GameState.Playing && enemies.Count == 0)
            {
                state = GameState.BetweenWave;
                waveTimer = bossSpawned ? 3.4f : 2.2f;
                if (bossSpawned)
                {
                    banner = "ARENA CLEARED";
                    playerHp = Mathf.Min(100, playerHp + 30);
                    bottles = Mathf.Min(5, bottles + 2);
                    crates = Mathf.Min(4, crates + 1);
                }
                else banner = "WAVE CLEARED";
                bannerTimer = waveTimer;
            }
            else if (state == GameState.BetweenWave)
            {
                waveTimer -= dt;
                if (waveTimer <= 0)
                {
                    if (bossSpawned)
                    {
                        state = GameState.Victory;
                        return;
                    }
                    state = GameState.Playing;
                    NextWave();
                }
            }
        }

        private void NextWave()
        {
            wave++;
            if (wave <= 3)
            {
                banner = "ROUND " + round + "  /  WAVE " + wave;
                bannerTimer = 1.6f;
                int count = 2 + round + wave * 2;
                for (int i = 0; i < count; i++)
                {
                    EnemyKind kind = EnemyKind.Chaser;
                    if (wave >= 2 && i % 3 == 1) kind = EnemyKind.Shooter;
                    if (wave >= 3 && i % 4 == 2) kind = EnemyKind.Tank;
                    SpawnEnemy(kind);
                }
            }
            else
            {
                bossSpawned = true;
                banner = "BOSS  —  THE OVERSEER";
                bannerTimer = 2.5f;
                SpawnEnemy(EnemyKind.Boss);
            }
        }

        private void SpawnEnemy(EnemyKind kind)
        {
            float radius = kind == EnemyKind.Boss ? 72 : kind == EnemyKind.Tank ? 42 : kind == EnemyKind.Shooter ? 32 : 24;
            float hp = kind == EnemyKind.Boss ? 420 + round * 140 : kind == EnemyKind.Tank ? 74 + round * 8 : kind == EnemyKind.Shooter ? 38 + round * 4 : 44 + round * 5;
            Vector2 pos;
            do
            {
                int edge = Random.Range(0, 4);
                pos = edge < 2
                    ? new Vector2(edge == 0 ? arena.xMin + radius : arena.xMax - radius, Random.Range(arena.yMin + radius, arena.yMax - radius))
                    : new Vector2(Random.Range(arena.xMin + radius, arena.xMax - radius), edge == 2 ? arena.yMin + radius : arena.yMax - radius);
            } while (Vector2.Distance(pos, playerPos) < 300);
            enemies.Add(new Enemy { kind = kind, pos = pos, hp = hp, maxHp = hp, radius = radius, attackTimer = Random.Range(.35f, 1.1f), phase = Random.value * 6 });
        }
    }
}
