using UnityEngine;
using AerialNav.Gesture;

namespace AerialNav.Navigation
{
    /// <summary>
    /// Phase 1 — Stroke arc locomotion.
    /// Pinch onset latches stroke origin and aim direction.
    /// Pinch release computes arc displacement and applies an instantaneous
    /// velocity impulse in the latched aim direction. Stroke velocity and
    /// chain multiplier are not yet factored (Phase 2, Phase 3).
    /// Start/stop is abrupt by design for Phase 1 testability.
    ///
    /// Attach to the Locomotion GameObject alongside PinchDetector.
    /// </summary>
    public class PinchToMoveController : MonoBehaviour
    {
        
        // Inspector

        [Header("References")]
        [SerializeField] private PinchDetector pinchDetector;
        [SerializeField] private Transform xrOrigin;

        [Header("Phase 1 — Stroke Arc")]
        [Tooltip("Scales arc displacement (meters) to velocity (m/s). " +
                 "Tune this first — it is the primary speed dial for Phase 1.")]
        [SerializeField] private float arcToSpeedScale = 500f;

        [Tooltip("Speed cap regardless of arc magnitude (m/s).")]
        [SerializeField] private float maxSpeed = 4000f;

        [Tooltip("Minimum arc displacement (m) to register as an intentional stroke. " +
                 "Filters out accidental micro-movements on pinch release.")]
        [SerializeField] private float minimumArcThreshold = 0.05f;

        [Header("Terrain Safety")]
        [Tooltip("Minimum height above terrain (m).")]
        [SerializeField] private float terrainFloorOffset = 2f;

        [Tooltip("LayerMask for terrain raycasting.")]
        [SerializeField] private LayerMask terrainLayer = ~0;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;

        // Private

        private Vector3 _strokeOrigin;
        private Vector3 _travelDirection;
        private bool _strokeActive;

        private Vector3 _currentVelocity = Vector3.zero;

        private const string LOG_TAG = "[PinchToMoveController]";

        // Unity Lifecycle

        private void Start()
        {
            ValidateReferences();
            SubscribeToDetector();
        }

        private void Update()
        {
            if (!ReferencesValid()) return;

            ApplyMovement();
            EnforceTerrainFloor();
        }

        private void OnDestroy()
        {
            UnsubscribeFromDetector();
        }

        // Detector Events

        private void SubscribeToDetector()
        {
            if (pinchDetector == null) return;
            pinchDetector.OnPinchStarted += HandlePinchStarted;
            pinchDetector.OnPinchReleased += HandlePinchReleased;
        }

        private void UnsubscribeFromDetector()
        {
            if (pinchDetector == null) return;
            pinchDetector.OnPinchStarted -= HandlePinchStarted;
            pinchDetector.OnPinchReleased -= HandlePinchReleased;
        }

        private void HandlePinchStarted()
        {
            // Latch stroke origin and travel direction at pinch onset
            _strokeOrigin = pinchDetector.WristPosition;
            _travelDirection = pinchDetector.AimRayAtOnset.direction;
            _strokeActive = true;

            // Phase 1: stop dead on new stroke onset so each stroke is
            // independently evaluable during testing
            _currentVelocity = Vector3.zero;

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Stroke started | origin={_strokeOrigin} | dir={_travelDirection}");
        }

        private void HandlePinchReleased()
        {
            if (!_strokeActive) return;
            _strokeActive = false;

            CommitStroke();
        }

        // Stroke Commit

        private void CommitStroke()
        {
            Vector3 strokeVector = pinchDetector.WristPosition - _strokeOrigin;
            float arc = strokeVector.magnitude;

            if (arc < minimumArcThreshold)
            {
                // Arc too small — treat as accidental, halt movement
                _currentVelocity = Vector3.zero;

                if (enableDebugLogging)
                    Debug.Log($"{LOG_TAG} Stroke rejected | arc={arc:F3}m below threshold={minimumArcThreshold:F3}m");

                return;
            }

            // Determine sign: come-hither (wrist moving toward body) = positive travel,
            // push-away (wrist moving away from body) = negative (reverse) travel.
            // Dot the stroke vector against the aim direction to determine intent.
            float strokeSign = -Mathf.Sign(Vector3.Dot(strokeVector, _travelDirection));

            float speed = Mathf.Min(arc * arcToSpeedScale, maxSpeed);
            _currentVelocity = _travelDirection * (speed * strokeSign);

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Stroke committed | arc={arc:F3}m | sign={strokeSign} | " +
                          $"speed={speed:F1}m/s | vel={_currentVelocity}");
        }
        // Movement

        private void ApplyMovement()
        {
            if (_currentVelocity.sqrMagnitude < 0.001f) return;
            xrOrigin.position += _currentVelocity * Time.deltaTime;
        }

        private void EnforceTerrainFloor()
        {
            Vector3 origin = xrOrigin.position + Vector3.up * 10000f;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20000f, terrainLayer))
            {
                float minAllowedY = hit.point.y + terrainFloorOffset;

                if (xrOrigin.position.y < minAllowedY)
                {
                    Vector3 corrected = xrOrigin.position;
                    corrected.y = minAllowedY;
                    xrOrigin.position = corrected;

                    if (_currentVelocity.y < 0f)
                        _currentVelocity.y = 0f;
                }
            }
        }

        // Reference Validation
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
                else
                {
                    Debug.LogError($"{LOG_TAG} XR Origin not found.");
                }
            }
        }

        private bool ReferencesValid()
        {
            return pinchDetector != null && xrOrigin != null;
        }
    }
}