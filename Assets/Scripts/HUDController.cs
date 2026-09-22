using UnityEngine;
using UnityEngine.UI;

namespace DoodleArena
{
    // Aijia owns this file: every visual that used to be a GUI.DrawTexture /
    // GUI.Label call inside OnGUI (health bar, score, banners, title/game-over/
    // victory screens) is now a real Canvas with Image / Text components that
    // this script updates instead of redrawing from scratch every frame.
    public class HUDController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject bannerPanel;
        [SerializeField] private GameObject bossHealthPanel;
        [SerializeField] private GameObject lowHealthVignette;

        [Header("Game HUD")]
        [SerializeField] private Text roundWaveText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text statusText;
        [SerializeField] private Image healthFill;
        [SerializeField] private Text healthLabel;
        [SerializeField] private Image bossHealthFill;
        [SerializeField] private Text bannerText;

        [Header("End Screens")]
        [SerializeField] private Text gameOverScoreText;
        [SerializeField] private Text victoryScoreText;

        private float bannerTimer;
        private float lowHealthPulseTimer;

        private void Awake()
        {
            if (GameManager.Instance) GameManager.Instance.hud = this;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
                if (bannerPanel && bannerTimer <= 0f) bannerPanel.SetActive(false);
            }

            bool showingRun = gm.IsPlaying || gm.State == GameState.BetweenWave;
            if (!showingRun)
            {
                if (bossHealthPanel) bossHealthPanel.SetActive(false);
                return;
            }

            if (roundWaveText) roundWaveText.text = "ROUND " + gm.round + "   WAVE " + Mathf.Min(gm.wave, 3) + "/3";
            if (scoreText) scoreText.text = gm.score.ToString("000000");
            if (healthFill) healthFill.fillAmount = Mathf.Clamp01(gm.playerHp / gm.playerMaxHp);
            if (healthLabel) healthLabel.text = "HP " + Mathf.CeilToInt(gm.playerHp);
            if (comboText)
            {
                comboText.gameObject.SetActive(gm.combo >= 3);
                comboText.text = "x" + gm.combo + " COMBO";
            }
            if (statusText && gm.player)
                statusText.text = (gm.player.SecondaryReady ? "K READY" : "K CHARGING") + "     I x" + gm.bottles + "     O x" + gm.crates;

            var boss = FindFirstObjectByType<BossController>();
            if (bossHealthPanel) bossHealthPanel.SetActive(boss != null);
            if (boss != null && bossHealthFill) bossHealthFill.fillAmount = Mathf.Clamp01(boss.Hp / boss.MaxHp);

            if (lowHealthVignette)
            {
                bool low = gm.playerHp <= 30f && gm.IsPlaying;
                lowHealthVignette.SetActive(low);
                if (low)
                {
                    lowHealthPulseTimer += Time.deltaTime * 7f;
                    var img = lowHealthVignette.GetComponent<Image>();
                    if (img)
                    {
                        var c = img.color;
                        c.a = .12f + (Mathf.Sin(lowHealthPulseTimer) + 1f) * .055f;
                        img.color = c;
                    }
                }
            }
        }

        public void ShowTitle()
        {
            SetPanel(titlePanel, true);
            SetPanel(gamePanel, false);
            SetPanel(gameOverPanel, false);
            SetPanel(victoryPanel, false);
            SetPanel(bannerPanel, false);
        }

        public void ShowGame()
        {
            SetPanel(titlePanel, false);
            SetPanel(gamePanel, true);
            SetPanel(gameOverPanel, false);
            SetPanel(victoryPanel, false);
        }

        public void ShowGameOver()
        {
            if (gameOverScoreText && GameManager.Instance) gameOverScoreText.text = "FINAL SCORE  " + GameManager.Instance.score.ToString("000000");
            SetPanel(gameOverPanel, true);
        }

        public void ShowVictory()
        {
            if (victoryScoreText && GameManager.Instance) victoryScoreText.text = "FINAL SCORE  " + GameManager.Instance.score.ToString("000000");
            SetPanel(victoryPanel, true);
        }

        public void ShowBanner(string text, float duration)
        {
            bannerTimer = duration;
            if (bannerText) bannerText.text = text;
            SetPanel(bannerPanel, true);
        }

        private static void SetPanel(GameObject panel, bool active)
        {
            if (panel) panel.SetActive(active);
        }
    }
}
