using UnityEngine;
using UnityEngine.UI;

namespace DoodleArena
{
    // Switches between the title / in-game / game over / victory panels and keeps
    // the in-game HUD (HP, score, combo, items, boss bar) up to date.
    public class HUDController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject bannerPanel;
        [SerializeField] private GameObject bossHealthPanel;
        [SerializeField] private Image lowHealthVignette;

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

        [SerializeField] private float lowHealthThreshold = 30f;

        private float bannerTimer;
        private float pulse;

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.unscaledDeltaTime;
                if (bannerTimer <= 0f) SetPanel(bannerPanel, false);
            }

            bool inRun = gm.IsPlaying || gm.State == GameState.BetweenWave;
            if (!inRun)
            {
                SetPanel(bossHealthPanel, false);
                return;
            }

            roundWaveText.text = "ROUND " + gm.round + "/" + gm.totalRounds + "   WAVE " + Mathf.Min(gm.wave, gm.wavesPerRound) + "/" + gm.wavesPerRound;
            scoreText.text = gm.score.ToString("000000");
            healthFill.fillAmount = Mathf.Clamp01(gm.playerHp / gm.playerMaxHp);
            healthLabel.text = "HP " + Mathf.CeilToInt(gm.playerHp);

            comboText.gameObject.SetActive(gm.combo >= 3);
            comboText.text = "x" + gm.combo + " COMBO";

            if (gm.player)
                statusText.text = (gm.player.DashReady ? "K READY" : "K CHARGING") + "     I x" + gm.bottles + "     O x" + gm.crates;

            var boss = gm.Boss;
            SetPanel(bossHealthPanel, boss != null);
            if (boss != null) bossHealthFill.fillAmount = Mathf.Clamp01(boss.Hp / boss.MaxHp);

            UpdateLowHealthPulse(gm);
        }

        private void UpdateLowHealthPulse(GameManager gm)
        {
            if (!lowHealthVignette) return;
            bool low = gm.IsPlaying && gm.playerHp <= lowHealthThreshold;
            lowHealthVignette.gameObject.SetActive(low);
            if (!low) return;

            pulse += Time.deltaTime * 7f;
            var c = lowHealthVignette.color;
            c.a = 0.12f + (Mathf.Sin(pulse) + 1f) * 0.055f;
            lowHealthVignette.color = c;
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
            gameOverScoreText.text = "FINAL SCORE  " + GameManager.Instance.score.ToString("000000");
            SetPanel(bannerPanel, false);
            SetPanel(gameOverPanel, true);
        }

        public void ShowVictory()
        {
            victoryScoreText.text = "FINAL SCORE  " + GameManager.Instance.score.ToString("000000");
            SetPanel(bannerPanel, false);
            SetPanel(victoryPanel, true);
        }

        public void ShowBanner(string text, float duration)
        {
            bannerTimer = duration;
            bannerText.text = text;
            SetPanel(bannerPanel, true);
        }

        private static void SetPanel(GameObject panel, bool active)
        {
            if (panel) panel.SetActive(active);
        }
    }
}
