using System.Collections;
using UnityEngine;
using TMPro;

namespace AerialNav.Gauntlet
{
    /// <summary>
    /// World-space HUD parented to the Camera Offset, pinned upper-right.
    ///
    /// Racing view  — small, 50% opacity: path name + live timer + gate progress
    /// Warning line — "Gate X Missed!" shown below timer, auto-clears on next valid gate
    /// Results view — expands to show total time when race ends; persists until new race
    ///
    /// Setup:
    ///   1. Create a Canvas (World Space) child of Camera Offset.
    ///   2. Set Canvas scaler Reference Resolution to 1920x1080.
    ///   3. Position canvas ~1.5m forward, ~0.6m right, ~0.3m up from Camera Offset.
    ///   4. Attach this script; wire the TMP references in Inspector.
    /// </summary>
    public class GauntletHUD : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Racing Panel (always visible during race)")]
        [SerializeField] private GameObject   racingPanel;
        [SerializeField] private TextMeshProUGUI pathNameLabel;
        [SerializeField] private TextMeshProUGUI timerLabel;
        [SerializeField] private TextMeshProUGUI progressLabel;   // "Gate 3 / 25"
        [SerializeField] private TextMeshProUGUI missedLabel;     // "Gate X Missed!"

        [Header("Results Panel (shown on finish)")]
        [SerializeField] private GameObject   resultsPanel;
        [SerializeField] private TextMeshProUGUI resultsTitleLabel;
        [SerializeField] private TextMeshProUGUI resultsTotalLabel;

        [Header("HUD Canvas Group (opacity control)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField][Range(0f, 1f)] private float racingOpacity = 0.50f;
        [SerializeField][Range(0f, 1f)] private float resultsOpacity = 0.90f;

        [Header("Animation")]
        [SerializeField] private float resultsExpandDuration = 0.35f;
        [SerializeField] private float missedDisplayDuration = 2.5f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private bool  _racing;
        private float _raceStart;
        private Coroutine _missedClearRoutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            SetPanelActive(racingPanel,  false);
            SetPanelActive(resultsPanel, false);
            if (missedLabel != null) missedLabel.gameObject.SetActive(false);
            if (canvasGroup != null)  canvasGroup.alpha = racingOpacity;
        }

        private void Update()
        {
            if (!_racing) return;
            if (timerLabel != null)
                timerLabel.text = FormatTime(Time.time - _raceStart);
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void OnRaceStarted(string name, int totalGates)
        {
            _racing    = true;
            _raceStart = Time.time;

            SetPanelActive(resultsPanel, false);
            SetPanelActive(racingPanel,  true);

            if (canvasGroup   != null) canvasGroup.alpha = racingOpacity;
            if (pathNameLabel != null) pathNameLabel.text = name;
            if (timerLabel    != null) timerLabel.text    = "0.00s";
            if (progressLabel != null) progressLabel.text = $"Gate 1 / {totalGates}";
            if (missedLabel   != null) missedLabel.gameObject.SetActive(false);
        }

        public void OnProgressChanged(int passed, int total)
        {
            if (progressLabel != null)
                progressLabel.text = $"Gate {passed} / {total}";
        }

        public void OnGateMissed(int missedIndex)
        {
            // missedIndex is 0-based; display as 1-based
            if (missedLabel == null) return;

            if (_missedClearRoutine != null)
                StopCoroutine(_missedClearRoutine);

            missedLabel.text = $"Gate {missedIndex + 1} Missed!";
            missedLabel.gameObject.SetActive(true);
            _missedClearRoutine = StartCoroutine(ClearMissedAfterDelay());
        }

        public void OnRaceFinished(float totalSeconds)
        {
            _racing = false;

            if (_missedClearRoutine != null)
            {
                StopCoroutine(_missedClearRoutine);
                if (missedLabel != null) missedLabel.gameObject.SetActive(false);
            }

            // Freeze timer display at final time
            if (timerLabel != null) timerLabel.text = FormatTime(totalSeconds);

            // Populate results
            if (resultsTitleLabel != null) resultsTitleLabel.text = "Finished!";
            if (resultsTotalLabel != null) resultsTotalLabel.text = $"Total  {FormatTime(totalSeconds)}";

            StartCoroutine(ExpandResults());
        }

        // ── Coroutines ────────────────────────────────────────────────────────
        private IEnumerator ClearMissedAfterDelay()
        {
            yield return new WaitForSeconds(missedDisplayDuration);
            if (missedLabel != null) missedLabel.gameObject.SetActive(false);
        }

        private IEnumerator ExpandResults()
        {
            SetPanelActive(resultsPanel, true);

            if (canvasGroup != null) canvasGroup.alpha = resultsOpacity;

            // Scale up from zero height
            var rt = resultsPanel.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector3 target = rt.localScale;
            rt.localScale  = new Vector3(target.x, 0f, target.z);

            float elapsed = 0f;
            while (elapsed < resultsExpandDuration)
            {
                elapsed     += Time.deltaTime;
                float t      = Mathf.SmoothStep(0f, 1f, elapsed / resultsExpandDuration);
                rt.localScale = new Vector3(target.x, t * target.y, target.z);
                yield return null;
            }

            rt.localScale = target;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static string FormatTime(float seconds)
        {
            int   m = (int)(seconds / 60f);
            float s = seconds % 60f;
            return m > 0 ? $"{m}:{s:00.00}" : $"{s:F2}s";
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }
    }
}