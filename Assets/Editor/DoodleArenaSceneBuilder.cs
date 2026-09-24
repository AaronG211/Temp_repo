using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DoodleArena.Editor
{
    // Tools > Doodle Arena > Build Scene
    // Regenerates the prefabs (player, enemies, boss, projectiles) and rebuilds the
    // scene objects (GameManager, spawner, camera, HUD canvas) with all references
    // wired up. Safe to re-run; it replaces whatever it built last time.
    public static class DoodleArenaSceneBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string SpriteFolder = "Assets/GeneratedSprites";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const float PixelsPerUnit = 100f;

        private static readonly Rect Arena = new Rect(-7.3f, -3.5f, 14.6f, 7f);

        [MenuItem("Tools/Doodle Arena/Build Scene")]
        public static void BuildScene()
        {
            AssetDatabase.Refresh();
            EnsureFolder(PrefabFolder);
            EnsureFolder(SpriteFolder);

            Sprite particleSprite = GetCircleSprite(32);
            Material particleMaterial = GetOrCreateParticleMaterial(particleSprite);

            GameObject shotPrefab   = SavePrefab(BuildProjectile("Player", ShotKind.Player, 0.08f, Palette.Gold), "Shot_Player");
            GameObject enemyShot    = SavePrefab(BuildProjectile("Enemy", ShotKind.Enemy, 0.09f, Palette.Purple), "Shot_Enemy");
            GameObject bossOrb      = SavePrefab(BuildProjectile("BossOrb", ShotKind.BossOrb, 0.13f, Palette.Red), "Shot_BossOrb");
            GameObject bottlePrefab = SavePrefab(BuildProjectile("Bottle", ShotKind.Bottle, 0.16f, Palette.Red), "Shot_Bottle");
            GameObject cratePrefab  = SavePrefab(BuildCrate(), "Shot_Crate");

            GameObject playerPrefab  = SavePrefab(BuildPlayer(shotPrefab, bottlePrefab, cratePrefab), "Player");
            GameObject chaserPrefab  = SavePrefab(BuildEnemy("Chaser", "Assets/Art/Char_Chaser.png", 0.24f, null), "Enemy_Chaser");
            GameObject shooterPrefab = SavePrefab(BuildEnemy("Shooter", "Assets/Art/Char_Shooter.png", 0.32f, enemyShot), "Enemy_Shooter");
            GameObject tankPrefab    = SavePrefab(BuildEnemy("Tank", "Assets/Art/Char_Tank.png", 0.42f, null), "Enemy_Tank");
            GameObject bossPrefab    = SavePrefab(BuildBoss(bossOrb), "Boss_TheOverseer");

            BuildSceneObjects(playerPrefab, chaserPrefab, shooterPrefab, tankPrefab, bossPrefab, particleMaterial);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Doodle Arena: scene built. Save the scene and press Play.");
        }

        // ---------- prefabs ----------

        private static GameObject BuildPlayer(GameObject shot, GameObject bottle, GameObject crate)
        {
            const float radius = 0.28f;
            var go = new GameObject("Player");
            go.AddComponent<SpriteRenderer>().sprite = LoadArt("Assets/Art/Char_Player.png");
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = radius;
            AddShadow(go, radius);

            var player = go.AddComponent<PlayerController>();
            SetRefs(player,
                ("rangedShotPrefab", shot.GetComponent<ProjectileController>()),
                ("bottlePrefab", bottle.GetComponent<ProjectileController>()),
                ("cratePrefab", crate.GetComponent<ProjectileController>()));
            return go;
        }

        private static GameObject BuildEnemy(string label, string artPath, float radius, GameObject shot)
        {
            var go = new GameObject("Enemy_" + label);
            go.AddComponent<SpriteRenderer>().sprite = LoadArt(artPath);
            AddBody(go, radius);
            AddShadow(go, radius);

            var enemy = go.AddComponent<EnemyController>();
            AddHealthBar(go, enemy, radius);
            if (shot) SetRefs(enemy, ("enemyShotPrefab", shot.GetComponent<ProjectileController>()));
            return go;
        }

        private static GameObject BuildBoss(GameObject orb)
        {
            const float radius = 0.72f;
            var go = new GameObject("Boss_TheOverseer");
            go.AddComponent<SpriteRenderer>().sprite = LoadArt("Assets/Art/Char_Boss.png");
            AddBody(go, radius);
            AddShadow(go, radius);

            var boss = go.AddComponent<BossController>();
            SetRefs(boss, ("orbPrefab", orb.GetComponent<ProjectileController>()));
            return go;
        }

        private static GameObject BuildProjectile(string label, ShotKind kind, float radius, Color color)
        {
            var go = new GameObject("Shot_" + label);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite(Mathf.RoundToInt(radius * 2f * PixelsPerUnit));
            sr.color = color;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = radius;

            var projectile = go.AddComponent<ProjectileController>();
            var so = new SerializedObject(projectile);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static GameObject BuildCrate()
        {
            const float radius = 0.23f;
            var go = BuildProjectile("Crate", ShotKind.Crate, radius, Palette.Gold);
            go.GetComponent<SpriteRenderer>().sprite = GetSquareSprite(Mathf.RoundToInt(radius * 2f * PixelsPerUnit));
            return go;
        }

        private static void AddBody(GameObject go, float radius)
        {
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            go.AddComponent<CircleCollider2D>().radius = radius;
        }

        // Soft dark blob under each character.
        private static void AddShadow(GameObject parent, float radius)
        {
            Sprite shadow = LoadArt("Assets/Art/Char_Shadow.png");
            if (!shadow) return;

            var go = new GameObject("Shadow");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(0f, -radius * 0.35f, 0.01f);
            float scale = radius * 1.7f / (shadow.bounds.size.x * 0.5f);
            go.transform.localScale = new Vector3(scale, scale * 0.55f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = shadow;
            sr.color = new Color(0f, 0f, 0f, 0.25f);
            sr.sortingOrder = -1;
        }

        // Small bar above the enemy, only visible once it has taken damage.
        private static void AddHealthBar(GameObject enemyGO, EnemyController enemy, float radius)
        {
            float width = radius * 2f;
            float height = Mathf.Max(0.04f, radius * 0.18f);

            var root = new GameObject("HealthBar");
            root.transform.SetParent(enemyGO.transform, false);
            root.transform.localPosition = new Vector3(0f, radius + 0.2f, -0.02f);
            root.SetActive(false);

            Sprite unit = GetUnitSquareSprite();
            MakeBarPiece(root.transform, "Back", unit, width, height, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.9f), 10);
            Transform fill = MakeBarPiece(root.transform, "Fill", unit, width, height, Palette.Red, 11);

            var so = new SerializedObject(enemy);
            so.FindProperty("healthBarRoot").objectReferenceValue = root;
            so.FindProperty("healthBarFill").objectReferenceValue = fill;
            so.FindProperty("healthBarWidth").floatValue = width;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform MakeBarPiece(Transform parent, string name, Sprite sprite, float width, float height, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(width, height, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go.transform;
        }

        private static GameObject SavePrefab(GameObject go, string prefabName)
        {
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, PrefabFolder + "/" + prefabName + ".prefab");
            Object.DestroyImmediate(go);
            return asset;
        }

        private static void SetRefs(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs) so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- sprites ----------

        private static Sprite LoadArt(string path)
        {
            ConfigureSpriteImporter(path, PixelsPerUnit);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning("Doodle Arena: missing art file " + path);
            return sprite;
        }

        private static Sprite GetCircleSprite(int diameter)
        {
            string path = SpriteFolder + "/Circle_" + diameter + ".png";
            if (!File.Exists(path)) WriteTexture(path, MakeCircleTexture(diameter));
            ConfigureSpriteImporter(path, PixelsPerUnit);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite GetSquareSprite(int size)
        {
            string path = SpriteFolder + "/Square_" + size + ".png";
            if (!File.Exists(path)) WriteTexture(path, MakeSolidTexture(size));
            ConfigureSpriteImporter(path, PixelsPerUnit);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // 1x1 world unit, so a transform's scale equals its size in world units.
        // Also used as the fill sprite for UI bars (Filled images need a sprite).
        private static Sprite GetUnitSquareSprite()
        {
            string path = SpriteFolder + "/Square_Unit.png";
            if (!File.Exists(path)) WriteTexture(path, MakeSolidTexture(4));
            ConfigureSpriteImporter(path, 4f);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void WriteTexture(string path, Texture2D tex)
        {
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        private static void ConfigureSpriteImporter(string path, float ppu)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite && Mathf.Approximately(importer.spritePixelsPerUnit, ppu)) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static Texture2D MakeCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half));
                    byte alpha = (byte)Mathf.Clamp((half - d) * 255f, 0, 255); // 1px soft edge
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeSolidTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Material GetOrCreateParticleMaterial(Sprite sprite)
        {
            string path = SpriteFolder + "/BurstParticleMaterial.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = sprite.texture;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- scene ----------

        private static void BuildSceneObjects(GameObject playerPrefab, GameObject chaserPrefab, GameObject shooterPrefab,
            GameObject tankPrefab, GameObject bossPrefab, Material particleMaterial)
        {
            // Rebuild from scratch each time rather than patching whatever is there;
            // half-built leftovers from an earlier failed run caused problems.
            // The camera is kept because it carries the URP template's settings.
            foreach (var name in new[] { "GameManager", "EnemySpawner", "Player", "HitBurst", "Canvas", "EventSystem", "Doodle Arena Game" })
                DestroyIfExists(name);

            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            gm.arena = Arena;

            var spawner = new GameObject("EnemySpawner").AddComponent<EnemySpawner>();
            SetRefs(spawner,
                ("chaserPrefab", chaserPrefab.GetComponent<EnemyController>()),
                ("shooterPrefab", shooterPrefab.GetComponent<EnemyController>()),
                ("tankPrefab", tankPrefab.GetComponent<EnemyController>()),
                ("bossPrefab", bossPrefab.GetComponent<BossController>()));

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.position = Arena.center;

            var burstGO = new GameObject("HitBurst");
            var ps = burstGO.AddComponent<ParticleSystem>();
            var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.sharedMaterial = particleMaterial;
            var burst = burstGO.AddComponent<BurstEmitter>();

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGO.AddComponent<Camera>();
            }
            cam.orthographic = true;
            // GameManager refits this at runtime for the actual window shape; 16:9 here
            cam.orthographicSize = Mathf.Max(Arena.height * 0.5f, Arena.width * 0.5f * 9f / 16f) + 0.4f;
            cam.transform.position = new Vector3(Arena.center.x, Arena.center.y, -10f);
            var shake = cam.GetComponent<CameraShake>();
            if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();

            HUDController hud = BuildCanvas();

            var gmSO = new SerializedObject(gm);
            gmSO.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
            gmSO.FindProperty("spawner").objectReferenceValue = spawner;
            gmSO.FindProperty("hud").objectReferenceValue = hud;
            gmSO.FindProperty("cameraShake").objectReferenceValue = shake;
            gmSO.FindProperty("burst").objectReferenceValue = burst;
            gmSO.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            gmSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DestroyIfExists(string name)
        {
            GameObject found = GameObject.Find(name);
            if (found != null) Object.DestroyImmediate(found);
        }

        // ---------- HUD ----------
        // Layout is in a 1600x900 reference canvas. Each element is anchored to the
        // part of the screen it belongs to (corner, top edge or centre), so it stays
        // put when the window isn't 16:9.

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private static HUDController BuildCanvas()
        {
            var canvasGO = new GameObject("Canvas");
            canvasGO.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            Transform root = canvasGO.transform;
            var hud = canvasGO.AddComponent<HUDController>();

            // title screen
            GameObject title = CreatePanel(root, "TitlePanel", Palette.Paper);
            CreateText(title.transform, "Title", "DOODLE ARENA", 72, Center, new Vector2(0, 190), new Vector2(1000, 100));
            CreateText(title.transform, "Subtitle", "Punch. Throw. Survive the page.", 27, Center, new Vector2(0, 110), new Vector2(880, 50));
            CreateText(title.transform, "Controls", "MOVE  WASD / ARROWS     ATTACK  J     DASH  K     ITEMS  I / O", 20, Center, new Vector2(0, -10), new Vector2(1000, 44));
            CreateButton(title.transform, "StartHint", "PRESS ENTER TO FIGHT", Center, new Vector2(0, -115));

            // in-game HUD
            GameObject game = CreatePanel(root, "GamePanel", Color.clear);
            Image vignette = CreateImage(game.transform, "LowHealthVignette", Center, Vector2.zero, Vector2.zero, new Color(Palette.Red.r, Palette.Red.g, Palette.Red.b, 0f));
            Stretch(vignette.rectTransform);
            vignette.raycastTarget = false;

            CreateImage(game.transform, "HealthBarBack", TopLeft, new Vector2(72, -72), new Vector2(360, 26), Palette.Ink);
            Image healthFill = CreateFillImage(game.transform, "HealthBarFill", TopLeft, new Vector2(72, -72), new Vector2(360, 26), Palette.Red);
            Text healthLabel = CreateText(game.transform, "HealthLabel", "HP 100", 18, TopLeft, new Vector2(72, -70), new Vector2(360, 30));
            healthLabel.color = Palette.Paper;

            Text roundWave = CreateText(game.transform, "RoundWaveText", "ROUND 1/3   WAVE 1/3", 25, TopCenter, new Vector2(0, -28), new Vector2(520, 42));
            Text combo = CreateText(game.transform, "ComboText", "x3 COMBO", 20, TopCenter, new Vector2(0, -70), new Vector2(250, 38));
            combo.color = Palette.Red;

            Text score = CreateText(game.transform, "ScoreText", "000000", 25, TopRight, new Vector2(-72, -28), new Vector2(300, 42));
            score.alignment = TextAnchor.MiddleRight;
            Text status = CreateText(game.transform, "StatusText", "K READY     I x3     O x2", 20, TopRight, new Vector2(-72, -70), new Vector2(500, 38));
            status.alignment = TextAnchor.MiddleRight;

            GameObject bossPanel = CreatePanel(game.transform, "BossHealthPanel", Color.clear);
            CreateImage(bossPanel.transform, "BossBarBack", TopCenter, new Vector2(0, -135), new Vector2(580, 25), Palette.Ink);
            Image bossFill = CreateFillImage(bossPanel.transform, "BossBarFill", TopCenter, new Vector2(0, -135), new Vector2(580, 25), Palette.Purple);
            Text bossLabel = CreateText(bossPanel.transform, "BossLabel", "THE OVERSEER", 16, TopCenter, new Vector2(0, -133), new Vector2(580, 29));
            bossLabel.color = Palette.Paper;

            // wave banner
            Image bannerBack = CreateImage(root, "BannerPanel", Center, Vector2.zero, new Vector2(790, 108), new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.94f));
            Text banner = CreateText(bannerBack.transform, "BannerText", "WAVE CLEARED", 40, Center, Vector2.zero, new Vector2(790, 108));
            banner.color = Palette.Paper;

            // end screens
            GameObject gameOver = BuildEndScreen(root, "GameOverPanel", "INKED OUT!", "The doodles got you this time.", out Text gameOverScore);
            GameObject victory = BuildEndScreen(root, "VictoryPanel", "YOU RULE THE PAGE!", "Three bosses down. The arena is yours.", out Text victoryScore);

            var so = new SerializedObject(hud);
            so.FindProperty("titlePanel").objectReferenceValue = title;
            so.FindProperty("gamePanel").objectReferenceValue = game;
            so.FindProperty("gameOverPanel").objectReferenceValue = gameOver;
            so.FindProperty("victoryPanel").objectReferenceValue = victory;
            so.FindProperty("bannerPanel").objectReferenceValue = bannerBack.gameObject;
            so.FindProperty("bossHealthPanel").objectReferenceValue = bossPanel;
            so.FindProperty("lowHealthVignette").objectReferenceValue = vignette;
            so.FindProperty("roundWaveText").objectReferenceValue = roundWave;
            so.FindProperty("scoreText").objectReferenceValue = score;
            so.FindProperty("comboText").objectReferenceValue = combo;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("healthFill").objectReferenceValue = healthFill;
            so.FindProperty("healthLabel").objectReferenceValue = healthLabel;
            so.FindProperty("bossHealthFill").objectReferenceValue = bossFill;
            so.FindProperty("bannerText").objectReferenceValue = banner;
            so.FindProperty("gameOverScoreText").objectReferenceValue = gameOverScore;
            so.FindProperty("victoryScoreText").objectReferenceValue = victoryScore;
            so.ApplyModifiedPropertiesWithoutUndo();

            hud.ShowTitle(); // leave the scene looking like it does when the game starts
            return hud;
        }

        private static GameObject BuildEndScreen(Transform root, string name, string heading, string subtitle, out Text scoreText)
        {
            var bg = Palette.Paper;
            bg.a = 0.92f;
            GameObject panel = CreatePanel(root, name, bg);
            CreateText(panel.transform, "Title", heading, 60, Center, new Vector2(0, 155), new Vector2(1080, 100));
            scoreText = CreateText(panel.transform, "Score", "FINAL SCORE  000000", 40, Center, new Vector2(0, 32), new Vector2(700, 55));
            CreateText(panel.transform, "Subtitle", subtitle, 24, Center, new Vector2(0, -46), new Vector2(700, 42));
            CreateButton(panel.transform, "RetryHint", "PRESS R TO TRY AGAIN", Center, new Vector2(0, -190));
            return panel;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            if (background.a > 0f) go.AddComponent<Image>().color = background;
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Pivot matches the anchor, so `position` is measured from that corner/edge.
        private static RectTransform Place(GameObject go, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Place(go, parent, anchor, position, size);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Palette.Ink;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Place(go, parent, anchor, position, size);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        // Filled images only respect fillAmount when they have a sprite assigned.
        private static Image CreateFillImage(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            Image img = CreateImage(parent, name, anchor, position, size, color);
            img.sprite = GetUnitSquareSprite();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            return img;
        }

        private static void CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position)
        {
            var size = new Vector2(380, 70);
            Image back = CreateImage(parent, name, anchor, position, size, Palette.Red);
            Text text = CreateText(back.transform, "Label", label, 27, Center, Vector2.zero, size);
            text.color = Palette.Paper;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
