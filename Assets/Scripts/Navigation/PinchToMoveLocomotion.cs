using System.Collections.Generic;
using UnityEngine;
using AerialNav.Gesture;

namespace AerialNav.Navigation
{
    // Phase 1 — live stroke locomotion via line-of-best-fit through pinch midpoint samples.
    // Locomotion begins immediately on pinch + movement, updates every frame mid-stroke.
    // Release freezes final direction vector and zeroes velocity (deceleration in Phase 2).
    // Stroke sign determined by regression slope — come-hither = forward, push-away = reverse.
    public class PinchToMoveController : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("References")]
        [SerializeField] private PinchDetector pinchDetector;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Transform headTransform;

        [Header("Phase 1 — Stroke Arc")]
        [Tooltip("Arc-to-speed multiplier. Primary speed dial for Phase 1.")]
        [SerializeField] private float arcToSpeedScale = 500f;

        [Tooltip("Speed cap regardless of arc (m/s).")]
        [SerializeField] private float maxSpeed = 4000f;

        [Tooltip("Minimum midpoint displacement to begin locomotion (m). Suppresses tremor.")]
        [SerializeField] private float minimumArcThreshold = 0.02f;

        [Tooltip("Minimum samples before best-fit line is computed.")]
        [SerializeField] [Min(2)] private int minimumRegressionSamples = 3;

        [Header("Terrain Safety")]
        [SerializeField] private float terrainFloorOffset = 2f;
        [SerializeField] private LayerMask terrainLayer = ~0;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;

        // -----------------------------------------------------------------------
        // Private
        // -----------------------------------------------------------------------

        // midpoint samples accumulated during active stroke
        private List<Vector3> _strokeSamples = new List<Vector3>();

        private Vector3 _strokeOrigin;
        private Vector3 _travelDirection;
        private bool _strokeActive;
        private Vector3 _currentVelocity = Vector3.zero;

        private const string LOG_TAG = "[PinchToMoveController]";

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Start()
        {
            ValidateReferences();
            SubscribeToDetector();
        }

        private void Update()
        {
            if (!ReferencesValid()) return;

            if (_strokeActive)
                UpdateStroke();

            ApplyMovement();
            EnforceTerrainFloor();
        }

        private void OnDestroy() => UnsubscribeFromDetector();

        // -----------------------------------------------------------------------
        // Detector Events
        // -----------------------------------------------------------------------

        private void SubscribeToDetector()
        {
            if (pinchDetector == null) return;
            pinchDetector.OnPinchStarted  += HandlePinchStarted;
            pinchDetector.OnPinchReleased += HandlePinchReleased;
        }

        private void UnsubscribeFromDetector()
        {
            if (pinchDetector == null) return;
            pinchDetector.OnPinchStarted  -= HandlePinchStarted;
            pinchDetector.OnPinchReleased -= HandlePinchReleased;
        }

        private void HandlePinchStarted()
        {
            _strokeSamples.Clear();
            _strokeOrigin   = pinchDetector.PinchMidpointPosition;
            _strokeActive   = true;
            _currentVelocity = Vector3.zero;

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Stroke started | origin={_strokeOrigin}");
        }

        private void HandlePinchReleased()
        {
            if (!_strokeActive) return;
            _strokeActive    = false;
            _currentVelocity = Vector3.zero;

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Stroke released | final dir={_travelDirection}");
        }

        // -----------------------------------------------------------------------
        // Live Stroke Update
        // -----------------------------------------------------------------------

        private void UpdateStroke()
        {
            _strokeSamples.Add(pinchDetector.PinchMidpointPosition);

            float arc = Vector3.Distance(pinchDetector.PinchMidpointPosition, _strokeOrigin);
            if (arc < minimumArcThreshold) return;
            if (_strokeSamples.Count < minimumRegressionSamples) return;

            Vector3 bestFitDirection;
            float   strokeSign;
            if (!TryFitLine(_strokeSamples, out bestFitDirection, out strokeSign)) return;

            _travelDirection = bestFitDirection;

            float speed      = Mathf.Min(arc * arcToSpeedScale, maxSpeed);
            _currentVelocity = _travelDirection * (speed * strokeSign);

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Live | arc={arc:F3}m | dir={_travelDirection} | " +
                          $"sign={strokeSign} | speed={speed:F1}m/s");
        }

        // -----------------------------------------------------------------------
        // Line of Best Fit
        // -----------------------------------------------------------------------

        // fits a line through accumulated samples via PCA on the centroid-centered cloud.
        // returns principal axis as direction, and sign derived from net displacement vs axis.
        private bool TryFitLine(List<Vector3> samples, out Vector3 direction, out float sign)
        {
            direction = Vector3.forward;
            sign      = 1f;

            // centroid
            Vector3 centroid = Vector3.zero;
            foreach (var s in samples) centroid += s;
            centroid /= samples.Count;

            // covariance matrix (symmetric 3x3, upper triangle)
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var s in samples)
            {
                Vector3 d = s - centroid;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }

            // dominant axis via power iteration (3 iterations sufficient for hand motion)
            Vector3 axis = (samples[samples.Count - 1] - samples[0]).normalized;
            if (axis.sqrMagnitude < 0.0001f) return false;

            for (int i = 0; i < 3; i++)
            {
                float nx = xx * axis.x + xy * axis.y + xz * axis.z;
                float ny = xy * axis.x + yy * axis.y + yz * axis.z;
                float nz = xz * axis.x + yz * axis.y + zz * axis.z;
                axis = new Vector3(nx, ny, nz).normalized;
                if (axis.sqrMagnitude < 0.0001f) return false;
            }

            direction = axis;

            // sign: net displacement from origin to current midpoint vs axis
            // come-hither moves midpoint toward body — opposite to initial reach direction
            Vector3 netDisplacement = pinchDetector.PinchMidpointPosition - _strokeOrigin;
            sign = -Mathf.Sign(Vector3.Dot(netDisplacement, direction));

            return true;
        }

        // -----------------------------------------------------------------------
        // Movement
        // -----------------------------------------------------------------------

        private void ApplyMovement()
        {
            if (_currentVelocity.sqrMagnitude < 0.001f) return;
            xrOrigin.position += _currentVelocity * Time.deltaTime;
        }

        private void EnforceTerrainFloor()
        {
            Vector3 rayOrigin = xrOrigin.position + Vector3.up * 10000f;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20000f, terrainLayer)) return;

            float minY = hit.point.y + terrainFloorOffset;
            if (xrOrigin.position.y < minY)
            {
                Vector3 corrected = xrOrigin.position;
                corrected.y = minY;
                xrOrigin.position = corrected;
                if (_currentVelocity.y < 0f) _currentVelocity.y = 0f;
            }
        }

        // -----------------------------------------------------------------------
        // Reference Validation
        // -----------------------------------------------------------------------

        private void ValidateReferences()
        {
            if (pinchDetector == null)
            {
                pinchDetector = GetComponent<PinchDetector>();
                if (pinchDetector == null)
                    Debug.LogError($"{LOG_TAG} PinchDetector not found on this GameObject.");
            }

            if (xrOrigin == null)
            {
                var found = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (found != null)
                {
                    xrOrigin = found.transform;
                    Debug.Log($"{LOG_TAG} Auto-assigned XR Origin: {xrOrigin.name}");
                }
                else Debug.LogError($"{LOG_TAG} XR Origin not found.");
            }

            if (headTransform == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    headTransform = cam.transform;
                    Debug.Log($"{LOG_TAG} Auto-assigned Head Transform: {headTransform.name}");
                }
                else Debug.LogError($"{LOG_TAG} Main Camera not found.");
            }
        }

        private bool ReferencesValid()
        {
            return pinchDetector != null && xrOrigin != null && headTransform != null;
        }
    }
}