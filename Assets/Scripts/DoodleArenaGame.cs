using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Random = UnityEngine.Random;

namespace DoodleArena
{
    public sealed class DoodleArenaGame : MonoBehaviour
    {
        private const float WorldW = 1600f;
        private const float WorldH = 900f;
        private readonly Rect arena = new Rect(72, 128, 1456, 700);

        private enum GameState { Title, Playing, BetweenWave, GameOver, Victory }
        private enum EnemyKind { Chaser, Shooter, Tank, Boss }
        private enum ShotKind { Player, Enemy, Bottle, Crate, BossOrb }

        private sealed class Enemy
        {
            public Vector2 pos, vel;
            public EnemyKind kind;
            public float hp, maxHp, radius, attackTimer, flash, phase;
            public bool dead;
        }

        private sealed class Shot
        {
            public Vector2 pos, vel;
            public ShotKind kind;
            public float radius, damage, life;
            public int pierce;
            public bool dead;
        }

        private sealed class Spark
        {
            public Vector2 pos, vel;
            public Color color;
            public float life, maxLife, size;
        }

        private Texture2D white, circle;
        private GUIStyle titleStyle, subtitleStyle, hudStyle, smallStyle, bigStyle, buttonStyle;
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Shot> shots = new List<Shot>();
        private readonly List<Spark> sparks = new List<Spark>();

        private GameState state = GameState.Title;
        private Vector2 playerPos, playerVel, aim = Vector2.right;
        private float playerHp, invincible, primaryCooldown, secondaryCooldown, waveTimer;
        private float bannerTimer, shake, hitStop, elapsed;
        private int round, wave, score, bottles, crates, combo;
        private int pendingChaserSpawns;
        private string banner = "";
        private bool bossSpawned;

        private readonly Color ink = Hex("25222B");
        private readonly Color paperColor = Hex("F5EEDF");
        private readonly Color coral = Hex("F05D5E");
        private readonly Color coralDark = Hex("B73B48");
        private readonly Color violet = Hex("6C4AB6");
        private readonly Color yellow = Hex("F6C85F");

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color c);
            return c;
        }

        private void Awake()
        {
            Application.targetFrameRate = 120;
            white = MakeWhite();
            circle = MakeCircle(96);
            BuildStyles();
        }

        private void OnDestroy()
        {
            if (white) Destroy(white);
            if (circle) Destroy(circle);
        }

        private static Texture2D MakeWhite()
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Ink Pixel" };
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            return t;
        }

        private static Texture2D MakeCircle(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Doodle Circle" };
            var pixels = new Color32[size * size];
            float half = size * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(half, half));
                byte a = (byte)Mathf.Clamp((half - d) * 255f, 0, 255);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            t.SetPixels32(pixels);
            t.Apply();
            return t;
        }

        private void BuildStyles()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleStyle = Style(font, 92, FontStyle.Bold, ink, TextAnchor.MiddleCenter);
            subtitleStyle = Style(font, 27, FontStyle.Normal, ink, TextAnchor.MiddleCenter);
            hudStyle = Style(font, 25, FontStyle.Bold, ink, TextAnchor.MiddleLeft);
            smallStyle = Style(font, 19, FontStyle.Bold, ink, TextAnchor.MiddleCenter);
            bigStyle = Style(font, 54, FontStyle.Bold, ink, TextAnchor.MiddleCenter);
            buttonStyle = Style(font, 27, FontStyle.Bold, ink, TextAnchor.MiddleCenter);
        }

        private static GUIStyle Style(Font font, int size, FontStyle weight, Color color, TextAnchor align)
        {
            return new GUIStyle
            {
                font = font, fontSize = size, fontStyle = weight, normal = { textColor = color },
                alignment = align, wordWrap = false, clipping = TextClipping.Overflow
            };
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .033f);
            elapsed += dt;
            if (hitStop > 0) { hitStop -= dt; UpdateSparks(dt * .25f); return; }

            var kb = Keyboard.current;
            if (kb == null) return;

            if (state == GameState.Title)
            {
                if (Pressed(kb.enterKey) || Pressed(kb.spaceKey) || Pressed(kb.jKey)) StartRun();
                UpdateSparks(dt);
                return;
            }
            if (state == GameState.GameOver || state == GameState.Victory)
            {
                if (Pressed(kb.rKey) || Pressed(kb.enterKey)) StartRun();
                UpdateSparks(dt);
                return;
            }

            if (Pressed(kb.escapeKey)) { state = GameState.Title; return; }
            UpdateTimers(dt);
            UpdatePlayer(kb, dt);
            UpdateEnemies(dt);
            UpdateShots(dt);
            UpdateSparks(dt);
            ResolveCollisions();
            RemoveDead();
            UpdateWaveFlow(dt);
        }

        private static bool Pressed(KeyControl key) => key != null && key.wasPressedThisFrame;

        private void StartRun()
        {
            state = GameState.Playing;
            playerPos = new Vector2(WorldW * .5f, WorldH * .55f);
            playerVel = Vector2.zero;
            playerHp = 100;
            round = 1; wave = 0; score = 0; combo = 0;
            bottles = 3; crates = 2;
            bossSpawned = false;
            pendingChaserSpawns = 0;
            enemies.Clear(); shots.Clear(); sparks.Clear();
            primaryCooldown = secondaryCooldown = invincible = 0;
            NextWave();
        }

        private void UpdateTimers(float dt)
        {
            invincible -= dt;
            primaryCooldown -= dt;
            secondaryCooldown -= dt;
            bannerTimer -= dt;
            shake = Mathf.Max(0, shake - dt * 18f);
            foreach (var e in enemies) e.flash -= dt;
        }

        private void UpdatePlayer(Keyboard kb, float dt)
        {
            Vector2 input = Vector2.zero;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x--;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x++;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y--;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y++;
            if (input.sqrMagnitude > 0)
            {
                input.Normalize();
                aim = input;
            }
            playerVel = Vector2.Lerp(playerVel, input * 365f, 1f - Mathf.Exp(-dt * 14f));
            playerPos += playerVel * dt;
            playerPos.x = Mathf.Clamp(playerPos.x, arena.xMin + 28, arena.xMax - 28);
            playerPos.y = Mathf.Clamp(playerPos.y, arena.yMin + 28, arena.yMax - 28);

            if (kb.jKey.isPressed && primaryCooldown <= 0) PrimaryAttack();
            if (Pressed(kb.kKey) && secondaryCooldown <= 0) DashAttack(input);
            if (Pressed(kb.iKey) && bottles > 0) ThrowItem(ShotKind.Bottle);
            if (Pressed(kb.oKey) && crates > 0) ThrowItem(ShotKind.Crate);
        }

        private void PrimaryAttack()
        {
            Enemy near = NearestEnemy();
            if (near != null && Vector2.Distance(playerPos, near.pos) < 105)
            {
                primaryCooldown = .32f;
                aim = (near.pos - playerPos).normalized;
                DamageEnemy(near, 22, aim * 36);
                Burst(near.pos, yellow, 12, 210);
                shake = 7; hitStop = .035f;
            }
            else
            {
                primaryCooldown = .18f;
                Vector2 dir = near != null ? (near.pos - playerPos).normalized : aim;
                aim = dir;
                shots.Add(new Shot { pos = playerPos + dir * 35, vel = dir * 760, kind = ShotKind.Player, radius = 8, damage = 12, life = 1.45f });
                Burst(playerPos + dir * 36, yellow, 3, 90);
            }
        }

        private void DashAttack(Vector2 input)
        {
            Vector2 dir = input.sqrMagnitude > 0 ? input : aim;
            playerPos += dir.normalized * 115;
            playerPos.x = Mathf.Clamp(playerPos.x, arena.xMin + 28, arena.xMax - 28);
            playerPos.y = Mathf.Clamp(playerPos.y, arena.yMin + 28, arena.yMax - 28);
            secondaryCooldown = 2.4f;
            invincible = .4f;
            shake = 9;
            Burst(playerPos, coral, 20, 260);
            foreach (var e in enemies)
                if (!e.dead && Vector2.Distance(playerPos, e.pos) < 128) DamageEnemy(e, 30, (e.pos - playerPos).normalized * 75);
        }

        private void ThrowItem(ShotKind kind)
        {
            Vector2 dir = NearestEnemy() is Enemy e ? (e.pos - playerPos).normalized : aim;
            if (kind == ShotKind.Bottle) bottles--; else crates--;
            shots.Add(new Shot
            {
                pos = playerPos + dir * 38, vel = dir * (kind == ShotKind.Bottle ? 470 : 580), kind = kind,
                radius = kind == ShotKind.Bottle ? 16 : 23, damage = kind == ShotKind.Bottle ? 35 : 44,
                life = kind == ShotKind.Bottle ? .72f : 1.65f, pierce = kind == ShotKind.Crate ? 3 : 0
            });
        }

        private Enemy NearestEnemy()
        {
            Enemy best = null;
            float bestDist = float.MaxValue;
            foreach (var e in enemies)
            {
                if (e.dead) continue;
                float d = (e.pos - playerPos).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = e; }
            }
            return best;
        }

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
                        UpdateBoss(e, dir, dist, dt);
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

        private void UpdateBoss(Enemy e, Vector2 dir, float dist, float dt)
        {
            float hpRatio = e.hp / e.maxHp;
            e.vel = Vector2.Lerp(e.vel, dir * (hpRatio > .5f ? 54 : 78) + new Vector2(-dir.y, dir.x) * Mathf.Sin(e.phase) * 80, dt * 2);
            if (e.attackTimer > 0) return;

            if (hpRatio > .65f)
            {
                for (int i = -2; i <= 2; i++) FireEnemyShot(e.pos, Rotate(dir, i * 13), ShotKind.BossOrb, 320, 12);
                e.attackTimer = 1.35f;
            }
            else if (hpRatio > .32f)
            {
                for (int i = 0; i < 12; i++) FireEnemyShot(e.pos, Rotate(Vector2.right, i * 30 + e.phase * 20), ShotKind.BossOrb, 245, 10);
                e.attackTimer = 1.05f;
            }
            else
            {
                for (int i = -3; i <= 3; i++) FireEnemyShot(e.pos, Rotate(dir, i * 11), ShotKind.BossOrb, 390, 13);
                int minionCount = enemies.FindAll(x => !x.dead && x.kind != EnemyKind.Boss).Count + pendingChaserSpawns;
                if (minionCount < 2) pendingChaserSpawns++;
                e.attackTimer = .82f;
                shake = 5;
            }
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));
        }

        private void FireEnemyShot(Vector2 pos, Vector2 dir, ShotKind kind, float speed, float damage)
        {
            shots.Add(new Shot { pos = pos + dir * 42, vel = dir * speed, kind = kind, radius = kind == ShotKind.BossOrb ? 13 : 9, damage = damage, life = 4 });
        }

        private void UpdateShots(float dt)
        {
            foreach (var s in shots)
            {
                if (s.dead) continue;
                s.pos += s.vel * dt;
                s.life -= dt;
                if (s.kind == ShotKind.Bottle) s.vel *= 1f - dt * .9f;
                if (s.life <= 0 || !arena.Contains(s.pos))
                {
                    if (s.kind == ShotKind.Bottle) ExplodeBottle(s);
                    else s.dead = true;
                }
            }
        }

        private void ResolveCollisions()
        {
            foreach (var s in shots)
            {
                if (s.dead) continue;
                if (s.kind == ShotKind.Enemy || s.kind == ShotKind.BossOrb)
                {
                    if (Vector2.Distance(s.pos, playerPos) < s.radius + 25)
                    {
                        HurtPlayer(s.damage, s.vel.normalized); s.dead = true;
                    }
                    continue;
                }

                foreach (var e in enemies)
                {
                    if (e.dead || Vector2.Distance(s.pos, e.pos) >= s.radius + e.radius) continue;
                    if (s.kind == ShotKind.Bottle) { ExplodeBottle(s); break; }
                    DamageEnemy(e, s.damage, s.vel.normalized * (s.kind == ShotKind.Crate ? 48 : 16));
                    if (s.kind == ShotKind.Crate && s.pierce-- > 0)
                    {
                        s.damage *= .78f;
                        s.pos += s.vel.normalized * (e.radius + s.radius + 5);
                    }
                    else { s.dead = true; break; }
                }
            }
        }

        private void ExplodeBottle(Shot s)
        {
            if (s.dead) return;
            s.dead = true;
            shake = 14; hitStop = .045f;
            Burst(s.pos, coral, 35, 360);
            foreach (var e in enemies)
                if (!e.dead && Vector2.Distance(s.pos, e.pos) < 145) DamageEnemy(e, s.damage, (e.pos - s.pos).normalized * 80);
        }

        private void DamageEnemy(Enemy e, float amount, Vector2 push)
        {
            if (e.dead) return;
            e.hp -= amount;
            e.vel += push;
            e.flash = .1f;
            Burst(e.pos, violet, 5, 145);
            if (e.hp > 0) return;
            e.dead = true;
            score += e.kind == EnemyKind.Boss ? 2500 : e.kind == EnemyKind.Tank ? 250 : 100;
            combo++;
            Burst(e.pos, violet, e.kind == EnemyKind.Boss ? 80 : 22, e.kind == EnemyKind.Boss ? 520 : 270);
            shake = e.kind == EnemyKind.Boss ? 28 : 8;
            if (Random.value < .18f && e.kind != EnemyKind.Boss)
            {
                if (Random.value < .58f) bottles = Mathf.Min(5, bottles + 1); else crates = Mathf.Min(4, crates + 1);
            }
        }

        private void HurtPlayer(float amount, Vector2 dir)
        {
            if (invincible > 0 || state != GameState.Playing) return;
            playerHp -= amount;
            playerVel += dir * 145;
            invincible = .65f;
            combo = 0;
            shake = 18; hitStop = .04f;
            Burst(playerPos, coralDark, 20, 300);
            if (playerHp <= 0)
            {
                playerHp = 0; state = GameState.GameOver; bannerTimer = 0;
            }
        }

        private void RemoveDead()
        {
            enemies.RemoveAll(e => e.dead);
            shots.RemoveAll(s => s.dead);
            if (sparks.Count > 260) sparks.RemoveRange(0, sparks.Count - 260);
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

        private void Burst(Vector2 pos, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 v = Random.insideUnitCircle.normalized * Random.Range(speed * .25f, speed);
                float life = Random.Range(.18f, .55f);
                sparks.Add(new Spark { pos = pos, vel = v, color = color, life = life, maxLife = life, size = Random.Range(5, 15) });
            }
        }

        private void UpdateSparks(float dt)
        {
            foreach (var p in sparks) { p.pos += p.vel * dt; p.vel *= 1 - dt * 4; p.life -= dt; }
            sparks.RemoveAll(p => p.life <= 0);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float scale = Mathf.Min(Screen.width / WorldW, Screen.height / WorldH);
            Vector2 offset = new Vector2((Screen.width - WorldW * scale) * .5f, (Screen.height - WorldH * scale) * .5f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);
            DrawBackground();
            if (state == GameState.Title) DrawTitle();
            else DrawGame();
            GUI.matrix = old;
        }

        private void DrawBackground()
        {
            GUI.color = paperColor;
            GUI.DrawTexture(new Rect(0, 0, WorldW, WorldH), white);
            GUI.color = Color.white;
        }

        private void DrawTitle()
        {
            GUI.Label(new Rect(360, 205, 880, 50), "Punch. Throw. Survive the page.", subtitleStyle);
            DrawPlayer(new Vector2(800, 355), Vector2.right, false, 1.55f);

            var controls = new GUIStyle(smallStyle) { fontSize = 22 };
            GUI.Label(new Rect(300, 505, 1000, 44), "MOVE  WASD / ARROWS     ATTACK  J     DASH  K     ITEMS  I / O", controls);
            DrawButton(new Rect(610, 605, 380, 70), "PRESS ENTER TO FIGHT", 0);
        }

        private void DrawGame()
        {
            Vector2 shakeOffset = shake > 0 ? Random.insideUnitCircle * shake : Vector2.zero;
            Matrix4x4 before = GUI.matrix;
            GUI.matrix *= Matrix4x4.Translate(shakeOffset);

            DrawArena();
            foreach (var s in shots) DrawShot(s);
            foreach (var e in enemies) DrawEnemy(e);
            bool blink = invincible > 0 && Mathf.FloorToInt(elapsed * 18) % 2 == 0;
            if (!blink) DrawPlayer(playerPos, aim, secondaryCooldown <= 0, 1);
            foreach (var p in sparks) DrawSpark(p);
            GUI.matrix = before;

            DrawHud();
            DrawLowHealthWarning();
            if (bannerTimer > 0) DrawBanner();
            if (state == GameState.GameOver) DrawEnd(false);
            if (state == GameState.Victory) DrawEnd(true);
        }

        private void DrawArena()
        {
            GUI.color = new Color(1, 1, 1, .48f);
            GUI.DrawTexture(arena, white);
            GUI.color = ink;
            DrawOutline(arena, 4);
            GUI.color = Color.white;
        }

        private void DrawHud()
        {
            GUI.Label(new Rect(560, 28, 480, 42), "ROUND " + round + "   WAVE " + Mathf.Min(wave, 3) + "/3",
                new GUIStyle(hudStyle) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(1235, 32, 292, 42), score.ToString("000000"), new GUIStyle(hudStyle) { alignment = TextAnchor.MiddleRight });
            DrawBar(new Rect(72, 72, 360, 26), playerHp / 100f, coral, "HP " + Mathf.CeilToInt(playerHp));
            float dashFill = Mathf.Clamp01(1 - secondaryCooldown / 2.4f);
            string status = (dashFill >= 1 ? "K READY" : "K CHARGING") + "     I ×" + bottles + "     O ×" + crates;
            GUI.Label(new Rect(1020, 68, 507, 38), status, new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleRight, fontSize = 21 });
            if (combo >= 3) GUI.Label(new Rect(675, 66, 250, 38), "×" + combo + " COMBO", new GUIStyle(smallStyle) { fontSize = 21, normal = { textColor = coral } });

            Enemy boss = enemies.Find(e => e.kind == EnemyKind.Boss);
            if (boss != null) DrawBar(new Rect(510, 135, 580, 25), boss.hp / boss.maxHp, violet, "THE OVERSEER");
        }

        private void DrawLowHealthWarning()
        {
            if (playerHp > 30 || state != GameState.Playing) return;
            float pulse = .12f + (Mathf.Sin(elapsed * 7f) + 1f) * .055f;
            GUI.color = new Color(coral.r, coral.g, coral.b, pulse);
            GUI.DrawTexture(new Rect(0, 0, WorldW, 18), white);
            GUI.DrawTexture(new Rect(0, WorldH - 18, WorldW, 18), white);
            GUI.DrawTexture(new Rect(0, 0, 18, WorldH), white);
            GUI.DrawTexture(new Rect(WorldW - 18, 0, 18, WorldH), white);
            GUI.color = Color.white;
        }

        private void DrawBar(Rect rect, float fill, Color color, string label)
        {
            GUI.color = ink; GUI.DrawTexture(new Rect(rect.x - 4, rect.y - 4, rect.width + 8, rect.height + 8), white);
            GUI.color = paperColor; GUI.DrawTexture(rect, white);
            GUI.color = color; GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), white);
            GUI.color = Color.white; GUI.Label(new Rect(rect.x, rect.y - 3, rect.width, rect.height + 4), label, smallStyle);
        }

        private void DrawBanner()
        {
            float t = Mathf.Clamp01(bannerTimer * 4);
            Rect r = new Rect(405, 380, 790, 108);
            GUI.color = new Color(ink.r, ink.g, ink.b, .94f * t);
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
            var style = new GUIStyle(bigStyle) { normal = { textColor = paperColor } };
            GUI.Label(r, banner, style);
        }

        private void DrawEnd(bool won)
        {
            GUI.color = new Color(paperColor.r, paperColor.g, paperColor.b, .92f);
            GUI.DrawTexture(new Rect(0, 0, WorldW, WorldH), white);
            GUI.Label(new Rect(260, 245, 1080, 100), won ? "YOU RULE THE PAGE!" : "INKED OUT!", titleStyle);
            GUI.Label(new Rect(450, 390, 700, 55), "FINAL SCORE  " + score.ToString("000000"), bigStyle);
            GUI.Label(new Rect(470, 475, 660, 42), won ? "Three bosses down. The arena is yours." : "The doodles got you this time.", subtitleStyle);
            DrawButton(new Rect(610, 605, 380, 70), "PRESS R TO TRY AGAIN", 0);
            GUI.Label(new Rect(540, 695, 520, 40), "ESC returns to the title screen", smallStyle);
        }

        private void DrawPlayer(Vector2 pos, Vector2 dir, bool ready, float scale)
        {
            DrawCircle(pos + new Vector2(5, 26) * scale, 34 * scale, new Color(ink.r, ink.g, ink.b, .13f));
            DrawCircle(pos, 31 * scale, ink);
            DrawCircle(pos, 26 * scale, coral);
            Vector2 fist = pos + dir * 34 * scale;
            DrawCircle(fist, 13 * scale, ink);
            DrawCircle(fist, 9 * scale, ready ? yellow : coralDark);
            Vector2 eyeBase = pos + dir * 10 * scale + new Vector2(-dir.y, dir.x) * 7 * scale;
            DrawCircle(eyeBase, 4 * scale, paperColor);
            DrawCircle(eyeBase + dir * 1.5f, 2 * scale, ink);
            eyeBase = pos + dir * 10 * scale - new Vector2(-dir.y, dir.x) * 7 * scale;
            DrawCircle(eyeBase, 4 * scale, paperColor);
            DrawCircle(eyeBase + dir * 1.5f, 2 * scale, ink);
        }

        private void DrawEnemy(Enemy e)
        {
            Color main = e.flash > 0 ? Color.white : violet;
            DrawCircle(e.pos + new Vector2(5, e.radius * .72f), e.radius * 1.05f, new Color(ink.r, ink.g, ink.b, .12f));
            DrawCircle(e.pos, e.radius + 5, ink);
            DrawCircle(e.pos, e.radius, main);

            Vector2 dir = (playerPos - e.pos).normalized;
            float eye = Mathf.Clamp(e.radius * .18f, 6, 13);
            DrawCircle(e.pos + dir * e.radius * .36f + new Vector2(-dir.y, dir.x) * e.radius * .24f, eye, paperColor);
            DrawCircle(e.pos + dir * e.radius * .36f - new Vector2(-dir.y, dir.x) * e.radius * .24f, eye, paperColor);
            DrawCircle(e.pos + dir * (e.radius * .36f + 2) + new Vector2(-dir.y, dir.x) * e.radius * .24f, eye * .42f, ink);
            DrawCircle(e.pos + dir * (e.radius * .36f + 2) - new Vector2(-dir.y, dir.x) * e.radius * .24f, eye * .42f, ink);
            if (e.kind != EnemyKind.Boss && e.hp < e.maxHp) DrawMiniHealth(e);
        }

        private void DrawMiniHealth(Enemy e)
        {
            Rect r = new Rect(e.pos.x - e.radius, e.pos.y - e.radius - 18, e.radius * 2, 7);
            GUI.color = ink; GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), white);
            GUI.color = coral; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(e.hp / e.maxHp), r.height), white);
            GUI.color = Color.white;
        }

        private void DrawShot(Shot s)
        {
            if (s.kind == ShotKind.Crate)
            {
                DrawRotated(Centered(s.pos, s.radius * 2, s.radius * 2), yellow, elapsed * 420);
                DrawCircle(s.pos, 5, ink);
            }
            else if (s.kind == ShotKind.Bottle)
            {
                DrawRotated(Centered(s.pos, 20, 34), coral, Mathf.Atan2(s.vel.y, s.vel.x) * Mathf.Rad2Deg + 90 + elapsed * 180);
            }
            else
            {
                Color c = s.kind == ShotKind.Player ? yellow : s.kind == ShotKind.BossOrb ? coral : violet;
                Vector2 trailDir = s.vel.sqrMagnitude > 0 ? -s.vel.normalized : Vector2.left;
                float trailLength = s.kind == ShotKind.BossOrb ? 26 : 19;
                DrawRotated(Centered(s.pos + trailDir * trailLength * .5f, trailLength, Mathf.Max(4, s.radius * .65f)),
                    new Color(c.r, c.g, c.b, .55f), Mathf.Atan2(s.vel.y, s.vel.x) * Mathf.Rad2Deg);
                DrawCircle(s.pos, s.radius + 4, ink);
                DrawCircle(s.pos, s.radius, c);
            }
        }

        private void DrawSpark(Spark p)
        {
            Color c = p.color; c.a = Mathf.Clamp01(p.life / p.maxLife);
            DrawRotated(Centered(p.pos, p.size * 2.2f, Mathf.Max(2, p.size * .35f)), c, Mathf.Atan2(p.vel.y, p.vel.x) * Mathf.Rad2Deg);
        }

        private void DrawButton(Rect r, string text, float offsetY)
        {
            r.y += offsetY;
            GUI.color = ink; GUI.DrawTexture(new Rect(r.x + 7, r.y + 7, r.width, r.height), white);
            GUI.color = coral; GUI.DrawTexture(r, white);
            GUI.color = Color.white;
            GUI.Label(r, text, buttonStyle);
        }

        private void DrawCircle(Vector2 center, float radius, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(Centered(center, radius * 2, radius * 2), circle);
            GUI.color = Color.white;
        }

        private void DrawRotated(Rect rect, Color color, float angle)
        {
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, rect.center);
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = Color.white;
            GUI.matrix = old;
        }

        private void DrawOutline(Rect r, float w)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), white);
            GUI.DrawTexture(new Rect(r.x, r.yMax - w, r.width, w), white);
            GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), white);
            GUI.DrawTexture(new Rect(r.xMax - w, r.y, w, r.height), white);
        }

        private static Rect Centered(Vector2 c, float w, float h) => new Rect(c.x - w * .5f, c.y - h * .5f, w, h);
    }
}
