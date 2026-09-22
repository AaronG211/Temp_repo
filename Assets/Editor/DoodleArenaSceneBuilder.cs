using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DoodleArena.Editor
{
    // Run this once (Tools > Doodle Arena > Build Scene) after opening
    // SampleScene.unity. It generates placeholder sprites, saves prefabs for
    // the player/enemies/boss/projectiles, and builds the scene hierarchy
    // (GameManager, spawner, camera rig, Canvas HUD) that the runtime scripts
    // expect, wiring every serialized reference the way a person would
    // normally do by hand in the Inspector. This is a first pass automation:
    // it has not been run inside the Unity Editor by the person who wrote it,
    // so re-run it and report any console errors so they can get fixed fast.
    public static class DoodleArenaSceneBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string SpriteFolder = "Assets/GeneratedSprites";

        [MenuItem("Tools/Doodle Arena/Build Scene (Convert To Real Objects)")]
        public static void BuildScene()
        {
            // Picks up any art files dropped into the project from outside the Editor
            // (e.g. copied in by hand or by a tool) before this script tries to load them.
            AssetDatabase.Refresh();

            EnsureFolder(PrefabFolder);
            EnsureFolder(SpriteFolder);

            Sprite particleSprite = GetCircleSprite(32);
            Material particleMaterial = GetOrCreateParticleMaterial(particleSprite);

            GameObject playerPrefab = SaveAndDestroy(BuildPlayerPrefab(), "Player");
            GameObject chaserPrefab = SaveAndDestroy(BuildEnemyPrefab("Chaser", "Assets/Art/Char_Chaser.png", 24f), "Enemy_Chaser");
            GameObject shooterPrefab = SaveAndDestroy(BuildEnemyPrefab("Shooter", "Assets/Art/Char_Shooter.png", 32f), "Enemy_Shooter");
            GameObject tankPrefab = SaveAndDestroy(BuildEnemyPrefab("Tank", "Assets/Art/Char_Tank.png", 42f), "Enemy_Tank");
            GameObject bossPrefab = SaveAndDestroy(BuildBossPrefab(), "Boss_TheOverseer");
            GameObject playerShotPrefab = SaveAndDestroy(BuildProjectilePrefab("PlayerShot", ShotKind.Player, 8, new Color(.96f, .78f, .37f)), "Shot_Player");
            GameObject enemyShotPrefab = SaveAndDestroy(BuildProjectilePrefab("EnemyShot", ShotKind.Enemy, 9, new Color(.42f, .29f, .71f)), "Shot_Enemy");
            GameObject bossOrbPrefab = SaveAndDestroy(BuildProjectilePrefab("BossOrb", ShotKind.BossOrb, 13, new Color(.94f, .36f, .37f)), "Shot_BossOrb");
            GameObject bottlePrefab = SaveAndDestroy(BuildProjectilePrefab("Bottle", ShotKind.Bottle, 16, new Color(.94f, .36f, .37f)), "Shot_Bottle");
            GameObject cratePrefab = SaveAndDestroy(BuildCratePrefab(), "Shot_Crate");

            // Wire the enemy/boss prefab assets to fire the projectile prefab assets.
            WireEnemyShot(chaserPrefab, null);
            WireEnemyShot(shooterPrefab, enemyShotPrefab);
            WireEnemyShot(tankPrefab, null);
            WireBossOrb(bossPrefab, bossOrbPrefab);
            WirePlayerShots(playerPrefab, playerShotPrefab, bottlePrefab, cratePrefab);

            BuildRuntimeScene(playerPrefab, chaserPrefab, shooterPrefab, tankPrefab, bossPrefab, particleMaterial);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[DoodleArenaSceneBuilder] Done. Save the scene (Ctrl/Cmd+S) and press Play to test.");
        }

        // ---------- prefab construction ----------

        private static GameObject BuildPlayerPrefab()
        {
            var go = new GameObject("Player");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetFixedSprite("Assets/Art/Char_Player.png");
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 28f;
            go.AddComponent<PlayerController>();
            AddShadowChild(go, 28f);
            return go;
        }

        // radius is the real gameplay collider radius (matches EnemyController's
        // per-kind math); the art file is expected to already be sized at 2x that
        // (i.e. the correct on-screen diameter), not just an arbitrary placeholder size.
        private static GameObject BuildEnemyPrefab(string label, string artPath, float radius)
        {
            var go = new GameObject("Enemy_" + label);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetFixedSprite(artPath);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = radius;
            var enemyController = go.AddComponent<EnemyController>();
            AddShadowChild(go, radius);
            AddEnemyHealthBar(go, enemyController, radius);
            return go;
        }

        // A small floating HP bar above the enemy, hidden until it takes damage
        // (EnemyController.RefreshHealthBar toggles it) - this is the world-space
        // equivalent of the original's DrawMiniHealth OnGUI call.
        private static void AddEnemyHealthBar(GameObject enemyGO, EnemyController controller, float radius)
        {
            float width = radius * 2f;
            float height = Mathf.Max(4f, radius * .18f);
            float yOffset = radius + 20f;

            var root = new GameObject("HealthBar");
            root.transform.SetParent(enemyGO.transform, false);
            root.transform.localPosition = new Vector3(0f, yOffset, -0.02f);
            root.SetActive(false);

            Sprite pixel = GetSquareSprite(1);

            var backGO = new GameObject("Back");
            backGO.transform.SetParent(root.transform, false);
            backGO.transform.localScale = new Vector3(width, height, 1f);
            var backSr = backGO.AddComponent<SpriteRenderer>();
            backSr.sprite = pixel;
            backSr.color = new Color(.15f, .13f, .17f, .9f);
            backSr.sortingOrder = 10;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(root.transform, false);
            fillGO.transform.localScale = new Vector3(width, height, 1f);
            var fillSr = fillGO.AddComponent<SpriteRenderer>();
            fillSr.sprite = pixel;
            fillSr.color = new Color(.94f, .36f, .37f, 1f);
            fillSr.sortingOrder = 11;

            var so = new SerializedObject(controller);
            so.FindProperty("healthBarRoot").objectReferenceValue = root;
            so.FindProperty("healthBarFill").objectReferenceValue = fillGO.transform;
            so.FindProperty("healthBarWidth").floatValue = width;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildBossPrefab()
        {
            var go = new GameObject("Boss_TheOverseer");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetFixedSprite("Assets/Art/Char_Boss.png");
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 72f;
            go.AddComponent<BossController>();
            AddShadowChild(go, 72f);
            return go;
        }

        // Loads a pre-made art file (already checked into Assets/Art) and makes sure its
        // sprite import settings match the project's 1-world-unit-per-pixel convention,
        // the same convention the procedurally generated projectile dots use.
        private static Sprite GetFixedSprite(string assetPath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (importer != null && importer.spritePixelsPerUnit != 1f)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 1f;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null) Debug.LogWarning("[DoodleArenaSceneBuilder] Missing art file: " + assetPath);
            return sprite;
        }

        // A soft drop shadow behind/below the character, matching the original
        // doodle-arena look (every character was drawn with an offset dark blob under it).
        private static void AddShadowChild(GameObject parent, float radius)
        {
            var shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Char_Shadow.png");
            if (!shadowSprite) return;
            var shadowGO = new GameObject("Shadow");
            shadowGO.transform.SetParent(parent.transform, false);
            shadowGO.transform.localPosition = new Vector3(0f, -radius * .35f, 0.01f);
            float scale = radius * 1.7f / (shadowSprite.rect.width * .5f);
            shadowGO.transform.localScale = new Vector3(scale, scale * .55f, 1f);
            var sr = shadowGO.AddComponent<SpriteRenderer>();
            sr.sprite = shadowSprite;
            sr.color = new Color(0f, 0f, 0f, .25f);
            sr.sortingOrder = -1;
        }

        private static GameObject BuildProjectilePrefab(string label, ShotKind kind, int radius, Color color)
        {
            var go = new GameObject("Shot_" + label);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetCircleSprite(radius * 2);
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

        private static GameObject BuildCratePrefab()
        {
            var go = BuildProjectilePrefab("Crate", ShotKind.Crate, 23, new Color(.96f, .78f, .37f));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = GetSquareSprite(46);
            return go;
        }

        private static void WireEnemyShot(GameObject enemyPrefabGO, GameObject shotPrefabGO)
        {
            var enemy = enemyPrefabGO.GetComponent<EnemyController>();
            if (!enemy || !shotPrefabGO) return;
            var so = new SerializedObject(enemy);
            so.FindProperty("enemyShotPrefab").objectReferenceValue = shotPrefabGO.GetComponent<ProjectileController>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireBossOrb(GameObject bossPrefabGO, GameObject orbPrefabGO)
        {
            var boss = bossPrefabGO.GetComponent<BossController>();
            if (!boss || !orbPrefabGO) return;
            var so = new SerializedObject(boss);
            so.FindProperty("orbPrefab").objectReferenceValue = orbPrefabGO.GetComponent<ProjectileController>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WirePlayerShots(GameObject playerPrefabGO, GameObject shotGO, GameObject bottleGO, GameObject crateGO)
        {
            var player = playerPrefabGO.GetComponent<PlayerController>();
            if (!player) return;
            var so = new SerializedObject(player);
            so.FindProperty("rangedShotPrefab").objectReferenceValue = shotGO.GetComponent<ProjectileController>();
            so.FindProperty("bottlePrefab").objectReferenceValue = bottleGO.GetComponent<ProjectileController>();
            so.FindProperty("cratePrefab").objectReferenceValue = crateGO.GetComponent<ProjectileController>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject SaveAndDestroy(GameObject go, string prefabName)
        {
            string path = PrefabFolder + "/" + prefabName + ".prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return asset;
        }

        // ---------- sprite generation ----------

        private static Sprite GetCircleSprite(int diameter)
        {
            // No in-memory cache here on purpose: this always checks the AssetDatabase
            // (the actual project state on disk) rather than trusting a static field,
            // which previously masked a stale-recompile bug by returning old data.
            string path = SpriteFolder + "/Circle_" + diameter + ".png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing) return existing;

            Texture2D tex = MakeCircleTexture(diameter);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(Path.GetFullPath(path), png);
            AssetDatabase.ImportAsset(path);
            ConfigureSpriteImporter(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite GetSquareSprite(int size)
        {
            string path = SpriteFolder + "/Square_" + size + ".png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing) return existing;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(Path.GetFullPath(path), png);
            AssetDatabase.ImportAsset(path);
            ConfigureSpriteImporter(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ConfigureSpriteImporter(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1f; // 1 world unit per pixel, matching the original pixel-space gameplay numbers
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static Texture2D MakeCircleTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
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

        private static Material GetOrCreateParticleMaterial(Sprite sprite)
        {
            string path = SpriteFolder + "/BurstParticleMaterial.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing)
            {
                // Keep it pointed at the current sprite's texture even on a re-run
                // (an earlier run may have created this against a since-replaced texture).
                existing.mainTexture = sprite.texture;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            var shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader) { mainTexture = sprite.texture };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------- scene assembly ----------

        private static void BuildRuntimeScene(GameObject playerPrefab, GameObject chaserPrefab, GameObject shooterPrefab,
            GameObject tankPrefab, GameObject bossPrefab, Material particleMaterial)
        {
            Rect arena = new Rect(-728f, -350f, 1456f, 700f);

            // A prior run of this builder can crash partway through and leave stale,
            // half-configured objects behind (e.g. a "HitBurst" with no ParticleSystem
            // yet, or a GameManager with unset fields). Reusing those via GetComponent
            // ?? AddComponent is unreliable (Unity's "fake null" semantics on
            // UnityEngine.Object make the ?? check misbehave) and lets corruption from
            // one failed run poison the next. So every managed object here is fully
            // destroyed and recreated from scratch on every run. Only Main Camera is
            // reused/reconfigured, since destroying it can lose template-provided
            // camera components/settings.
            DestroyIfExists("GameManager");
            DestroyIfExists("EnemySpawner");
            DestroyIfExists("Player");
            DestroyIfExists("HitBurst");

            var gmGO = new GameObject("GameManager");
            var gm = gmGO.AddComponent<GameManager>();
            gm.arena = arena;

            var spawnerGO = new GameObject("EnemySpawner");
            var spawner = spawnerGO.AddComponent<EnemySpawner>();
            var spawnerSO = new SerializedObject(spawner);
            spawnerSO.FindProperty("chaserPrefab").objectReferenceValue = chaserPrefab.GetComponent<EnemyController>();
            spawnerSO.FindProperty("shooterPrefab").objectReferenceValue = shooterPrefab.GetComponent<EnemyController>();
            spawnerSO.FindProperty("tankPrefab").objectReferenceValue = tankPrefab.GetComponent<EnemyController>();
            spawnerSO.FindProperty("bossPrefab").objectReferenceValue = bossPrefab.GetComponent<BossController>();
            spawnerSO.ApplyModifiedPropertiesWithoutUndo();

            var playerGO = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerGO.name = "Player";
            playerGO.transform.position = arena.center;

            var burstGO = new GameObject("HitBurst");
            var ps = burstGO.AddComponent<ParticleSystem>();
            ConfigureBurstParticleSystem(ps, particleMaterial);
            var burst = burstGO.AddComponent<BurstEmitter>();

            Camera cam = Camera.main;
            GameObject camGO;
            if (cam == null)
            {
                camGO = new GameObject("Main Camera");
                cam = camGO.AddComponent<Camera>();
                camGO.tag = "MainCamera";
            }
            else camGO = cam.gameObject;
            cam.orthographic = true;
            cam.orthographicSize = arena.height * .5f + 40f;
            camGO.transform.position = new Vector3(arena.center.x, arena.center.y, -10f);
            CameraShake camShake = camGO.GetComponent<CameraShake>();
            if (camShake == null) camShake = camGO.AddComponent<CameraShake>();

            HUDController hud = BuildCanvas(arena);

            var gmSO = new SerializedObject(gm);
            gmSO.FindProperty("player").objectReferenceValue = playerGO.GetComponent<PlayerController>();
            gmSO.FindProperty("spawner").objectReferenceValue = spawner;
            gmSO.FindProperty("hud").objectReferenceValue = hud;
            gmSO.FindProperty("cameraShake").objectReferenceValue = camShake;
            gmSO.FindProperty("burst").objectReferenceValue = burst;
            gmSO.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DestroyIfExists(string name)
        {
            GameObject found = GameObject.Find(name);
            if (found != null) Object.DestroyImmediate(found);
        }

        private static void ConfigureBurstParticleSystem(ParticleSystem ps, Material material)
        {
            // Module setup (simulationSpace, playOnAwake, emission rate, etc.) is done
            // by BurstEmitter.Awake() at runtime instead of here: touching ps.main /
            // ps.emission immediately after AddComponent<ParticleSystem>() inside an
            // editor script throws "Do not create your own module instances" in this
            // Unity version. Assigning the renderer's material is a plain object
            // reference assignment, so that part is safe to do here.
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = material;
            }
        }

        private static HUDController BuildCanvas(Rect arena)
        {
            // Same reasoning as BuildRuntimeScene: never reuse a Canvas/EventSystem
            // that might be left over from an earlier crashed run. Build fresh every time.
            DestroyIfExists("Canvas");
            var existingEventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (existingEventSystem != null) Object.DestroyImmediate(existingEventSystem.gameObject);

            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            canvasGO.AddComponent<GraphicRaycaster>();

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();

            var hud = canvasGO.AddComponent<HUDController>();
            var hudSO = new SerializedObject(hud);

            GameObject titlePanel = CreatePanel(canvasGO.transform, "TitlePanel", new Color(.96f, .93f, .87f, 1f));
            CreateText(titlePanel.transform, "Subtitle", "Punch. Throw. Survive the page.", 27, new Rect(360, 130, 880, 50));
            CreateText(titlePanel.transform, "Controls", "MOVE  WASD / ARROWS     ATTACK  J     DASH  K     ITEMS  I / O", 20, new Rect(300, 430, 1000, 44));
            CreateButtonLikeText(titlePanel.transform, "StartHint", "PRESS ENTER TO FIGHT", new Rect(610, 530, 380, 70));

            GameObject gamePanel = CreatePanel(canvasGO.transform, "GamePanel", new Color(0, 0, 0, 0));
            Text roundWaveText = CreateText(gamePanel.transform, "RoundWaveText", "ROUND 1   WAVE 1/3", 25, new Rect(560, 28, 480, 42));
            Text scoreText = CreateText(gamePanel.transform, "ScoreText", "000000", 25, new Rect(1235, 28, 292, 42));
            Image healthBack = CreateImage(gamePanel.transform, "HealthBarBack", new Rect(72, 72, 360, 26), new Color(.15f, .13f, .17f));
            Image healthFill = CreateFillImage(gamePanel.transform, "HealthBarFill", new Rect(72, 72, 360, 26), new Color(.94f, .36f, .37f));
            Text healthLabel = CreateText(gamePanel.transform, "HealthLabel", "HP 100", 18, new Rect(72, 70, 360, 30));
            Text statusText = CreateText(gamePanel.transform, "StatusText", "K READY     I x3     O x2", 20, new Rect(1020, 68, 507, 38));
            Text comboText = CreateText(gamePanel.transform, "ComboText", "x3 COMBO", 20, new Rect(675, 66, 250, 38));
            comboText.color = new Color(.94f, .36f, .37f);

            GameObject bossPanel = CreatePanel(gamePanel.transform, "BossHealthPanel", new Color(0, 0, 0, 0));
            CreateImage(bossPanel.transform, "BossBarBack", new Rect(510, 135, 580, 25), new Color(.15f, .13f, .17f));
            Image bossFill = CreateFillImage(bossPanel.transform, "BossBarFill", new Rect(510, 135, 580, 25), new Color(.42f, .29f, .71f));
            CreateText(bossPanel.transform, "BossLabel", "THE OVERSEER", 16, new Rect(510, 133, 580, 29));

            GameObject lowHealthGO = CreateImage(gamePanel.transform, "LowHealthVignette", new Rect(0, 0, 1600, 900), new Color(.94f, .36f, .37f, 0f)).gameObject;

            GameObject bannerPanel = CreatePanel(canvasGO.transform, "BannerPanel", new Color(.15f, .13f, .17f, .94f));
            SetRect(bannerPanel.GetComponent<RectTransform>(), new Rect(405, 380, 790, 108));
            Text bannerText = CreateText(bannerPanel.transform, "BannerText", "WAVE CLEARED", 40, new Rect(0, 0, 790, 108));
            bannerText.color = new Color(.96f, .93f, .87f);

            GameObject gameOverPanel = CreatePanel(canvasGO.transform, "GameOverPanel", new Color(.96f, .93f, .87f, .92f));
            CreateText(gameOverPanel.transform, "Title", "INKED OUT!", 60, new Rect(260, 245, 1080, 100));
            Text gameOverScore = CreateText(gameOverPanel.transform, "Score", "FINAL SCORE  000000", 40, new Rect(450, 390, 700, 55));
            CreateText(gameOverPanel.transform, "Subtitle", "The doodles got you this time.", 24, new Rect(470, 475, 660, 42));
            CreateButtonLikeText(gameOverPanel.transform, "RetryHint", "PRESS R TO TRY AGAIN", new Rect(610, 605, 380, 70));

            GameObject victoryPanel = CreatePanel(canvasGO.transform, "VictoryPanel", new Color(.96f, .93f, .87f, .92f));
            CreateText(victoryPanel.transform, "Title", "YOU RULE THE PAGE!", 60, new Rect(260, 245, 1080, 100));
            Text victoryScore = CreateText(victoryPanel.transform, "Score", "FINAL SCORE  000000", 40, new Rect(450, 390, 700, 55));
            CreateText(victoryPanel.transform, "Subtitle", "Three bosses down. The arena is yours.", 24, new Rect(470, 475, 660, 42));
            CreateButtonLikeText(victoryPanel.transform, "RetryHint", "PRESS R TO TRY AGAIN", new Rect(610, 605, 380, 70));

            hudSO.FindProperty("titlePanel").objectReferenceValue = titlePanel;
            hudSO.FindProperty("gamePanel").objectReferenceValue = gamePanel;
            hudSO.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
            hudSO.FindProperty("victoryPanel").objectReferenceValue = victoryPanel;
            hudSO.FindProperty("bannerPanel").objectReferenceValue = bannerPanel;
            hudSO.FindProperty("bossHealthPanel").objectReferenceValue = bossPanel;
            hudSO.FindProperty("lowHealthVignette").objectReferenceValue = lowHealthGO;
            hudSO.FindProperty("roundWaveText").objectReferenceValue = roundWaveText;
            hudSO.FindProperty("scoreText").objectReferenceValue = scoreText;
            hudSO.FindProperty("comboText").objectReferenceValue = comboText;
            hudSO.FindProperty("statusText").objectReferenceValue = statusText;
            hudSO.FindProperty("healthFill").objectReferenceValue = healthFill;
            hudSO.FindProperty("healthLabel").objectReferenceValue = healthLabel;
            hudSO.FindProperty("bossHealthFill").objectReferenceValue = bossFill;
            hudSO.FindProperty("bannerText").objectReferenceValue = bannerText;
            hudSO.FindProperty("gameOverScoreText").objectReferenceValue = gameOverScore;
            hudSO.FindProperty("victoryScoreText").objectReferenceValue = victoryScore;
            hudSO.ApplyModifiedPropertiesWithoutUndo();

            healthBack.gameObject.SetActive(true);

            // Leave the scene in the same resting state the game itself starts in
            // (title screen only) instead of all panels sitting active at once.
            // ShowTitle() is the same public method the game calls at runtime;
            // calling it here just means the Scene view isn't misleading before Play.
            hud.ShowTitle();

            return hud;
        }

        // ---------- small UI helpers (Rects are top-left-origin, in a 1600x900 reference canvas) ----------

        private static GameObject CreatePanel(Transform parent, string name, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            if (background.a > 0f)
            {
                var img = go.AddComponent<Image>();
                img.color = background;
            }
            return go;
        }

        private static RectTransform SetRect(RectTransform rt, Rect r)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(r.x, -r.y);
            rt.sizeDelta = new Vector2(r.width, r.height);
            return rt;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, Rect rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), rect);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.15f, .13f, .17f);
            return text;
        }

        private static Text CreateButtonLikeText(Transform parent, string name, string content, Rect rect)
        {
            var back = CreateImage(parent, name + "Back", rect, new Color(.94f, .36f, .37f));
            var text = CreateText(back.transform, name + "Label", content, 27, new Rect(0, 0, rect.width, rect.height));
            text.color = new Color(.96f, .93f, .87f);
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Rect rect, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), rect);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Image CreateFillImage(Transform parent, string name, Rect rect, Color color)
        {
            Image img = CreateImage(parent, name, rect, color);
            // Image.Type.Filled only computes a partial mesh when it has an actual
            // Sprite assigned - with sprite == null, Unity's Image.OnPopulateMesh
            // falls back to always drawing the full rect regardless of fillAmount,
            // which is why the health bar's length wasn't visually responding to
            // damage even though fillAmount was being set correctly every frame.
            img.sprite = GetSquareSprite(1);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            img.fillAmount = 1f;
            return img;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
