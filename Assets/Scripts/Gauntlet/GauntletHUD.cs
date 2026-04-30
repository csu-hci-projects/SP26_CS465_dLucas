using System.Collections;
using UnityEngine;
using TMPro;

namespace AerialNav.Gauntlet
{
    /// <summary>
    /// World-space HUD that repositions itself relative to the camera every frame.
    /// Pinned to upper-right of viewport at a fixed forward distance.
    ///
    /// No Screen Space canvas needed — works correctly in XR stereo rendering.
    /// </summary>
    public class GauntletHUD : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Racing Panel")]
        [SerializeField] private GameObject      racingPanel;
        [SerializeField] private TextMeshProUGUI pathNameLabel;
        [SerializeField] private TextMeshProUGUI timerLabel;
        [SerializeField] private TextMeshProUGUI progressLabel;
        [SerializeField] private TextMeshProUGUI missedLabel;

        [Header("Results Panel")]
        [SerializeField] private GameObject      resultsPanel;
        [SerializeField] private TextMeshProUGUI resultsTitleLabel;
        [SerializeField] private TextMeshProUGUI resultsTotalLabel;

        [Header("HUD Canvas Group")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField][Range(0f, 1f)] private float racingOpacity  = 0.50f;
        [SerializeField][Range(0f, 1f)] private float resultsOpacity = 0.90f;

        [Header("Animation")]
        [SerializeField] private float resultsExpandDuration = 0.35f;
        [SerializeField] private float missedDisplayDuration = 2.5f;

        [Header("Camera Tracking")]
        [Tooltip("How far in front of the camera the HUD sits (meters).")]
        [SerializeField] private float forwardDistance = 2.0f;

        [Tooltip("Right offset from camera center (meters).")]
        [SerializeField] private float rightOffset = 0.25f;

        [Tooltip("Up offset from camera center (meters).")]
        [SerializeField] private float upOffset = -.2f;

        [Tooltip("Smoothing speed for HUD position/rotation tracking. Higher = snappier.")]
        [SerializeField] private float trackingSpeed = 8f;

        // ── Runtime ───────────────────────────────────────────────────────────
        private bool      _racing;
        private float     _raceStart;
        private Coroutine _missedClearRoutine;
        private Transform _cameraTransform;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            SetPanelActive(racingPanel,  false);
            SetPanelActive(resultsPanel, false);
            if (missedLabel != null) missedLabel.gameObject.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = racingOpacity;
        }

        private void Start()
        {
            _cameraTransform = Camera.main?.transform;

            if (_cameraTransform == null)
                Debug.LogError("[GauntletHUD] No main camera found.");
        }

        private void Update()
        {
            if (_racing && timerLabel != null)
                timerLabel.text = FormatTime(Time.time - _raceStart);

            TrackCamera();
        }

        // ── Camera Tracking ───────────────────────────────────────────────────
        private void TrackCamera()
        {
            if (_cameraTransform == null) return;

            Vector3 targetPosition = _cameraTransform.position
                + _cameraTransform.forward * forwardDistance
                + _cameraTransform.right   * rightOffset
                + _cameraTransform.up      * upOffset;

            Quaternion targetRotation = _cameraTransform.rotation;

            transform.position = Vector3.Lerp(
                transform.position, targetPosition, Time.deltaTime * trackingSpeed);

            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRotation, Time.deltaTime * trackingSpeed);
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void OnRaceStarted(string name, int totalGates)
        {
            _racing    = true;
            _raceStart = Time.time;

            SetPanelActive(resultsPanel, false);
            SetPanelActive(racingPanel,  true);

            if (canvasGroup   != null) canvasGroup.alpha    = racingOpacity;
            if (pathNameLabel != null) pathNameLabel.text   = name;
            if (timerLabel    != null) timerLabel.text      = "0.00s";
            if (progressLabel != null) progressLabel.text   = $"Gate 1 / {totalGates}";
            if (missedLabel   != null) missedLabel.gameObject.SetActive(false);
        }

        public void OnProgressChanged(int passed, int total)
        {
            if (progressLabel != null)
                progressLabel.text = $"Gate {passed} / {total}";
        }

        public void OnGateMissed(int missedIndex)
        {
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

            if (timerLabel       != null) timerLabel.text       = FormatTime(totalSeconds);
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

            var rt = resultsPanel.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector3 target = rt.localScale;
            rt.localScale  = new Vector3(target.x, 0f, target.z);

            float elapsed = 0f;
            while (elapsed < resultsExpandDuration)
            {
                elapsed      += Time.deltaTime;
                float t       = Mathf.SmoothStep(0f, 1f, elapsed / resultsExpandDuration);
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