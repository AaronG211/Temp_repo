using UnityEngine;

namespace DoodleArena
{
    // Aijia's piece: particles, screen shake, hit-stop feedback, banners,
    // title/game-over/victory screens, and all the code-drawn visuals
    // (player, enemies, shots). Her health-bar work (OnGUI, DrawHud, DrawBar,
    // DrawMiniHealth) already lives in DoodleArenaGame.cs — this file adds the
    // rest of her ownership section without redefining any of that.
    //
    // IMPORTANT: this alone is not enough to see anything on screen. The
    // OnGUI() currently in DoodleArenaGame.cs only calls DrawHud(), so
    // DrawBackground/DrawTitle/DrawGame below never get called yet. See the
    // separate note on the small edit OnGUI itself needs.
    public sealed partial class DoodleArenaGame
    {
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

            DrawHud(); // already defined in DoodleArenaGame.cs
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
            if (e.kind != EnemyKind.Boss && e.hp < e.maxHp) DrawMiniHealth(e); // already defined in DoodleArenaGame.cs
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
    }
}
