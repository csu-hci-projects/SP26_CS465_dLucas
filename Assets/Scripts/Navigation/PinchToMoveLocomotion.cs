using System.Collections.Generic;
using UnityEngine;
using AerialNav.Gesture;

namespace AerialNav.Navigation
{
    // Phase 1   — live stroke locomotion via line-of-best-fit through pinch midpoint samples.
    // Phase 1.5 — stroke end detection via displacement delta window; exponential decel to zero.
    // Phase 2   — chain multiplier: successive strokes within chainWindowSeconds accumulate a
    //             speed scalar, capped at chainMultiplierCap, decaying during idle.
    //
    // Stroke end triggers:
    //   (a) Pinch released.
    //   (b) Hand settles while pinched — cumulative midpoint displacement over the last
    //       settlementFrameWindow frames falls below settlementDisplacementThreshold.
    //
    // Chain multiplier increments on stroke END (committed stroke earns the bonus).
    // Multiplier resets to 1.0 if elapsed time since last stroke end exceeds chainWindowSeconds.
    // Multiplier decays toward 1.0 continuously during idle at chainMultiplierDecayRate/sec.

    public class PinchToMoveController : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("References")]
        [SerializeField] private PinchDetector pinchDetector;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Transform headTransform;

        [Header("Stroke Arc")]
        [Tooltip("Arc-to-speed multiplier. Primary speed dial.")]
        [SerializeField] private float arcToSpeedScale = 600f;

        [Tooltip("Speed cap before chain multiplier is applied (m/s).")]
        [SerializeField] private float maxSpeed = 4000f;

        [Tooltip("Minimum midpoint displacement from stroke origin to begin locomotion (m). Suppresses tremor.")]
        [SerializeField] private float minimumArcThreshold = 0.02f;

        [Tooltip("Minimum samples before best-fit line is computed.")]
        [SerializeField] [Min(2)] private int minimumRegressionSamples = 3;

        [Header("Stroke End Detection")]
        [Tooltip("Number of frames examined to determine if the hand has settled. " +
                 "Increase if strokes end prematurely on slow movement; " +
                 "decrease if settlement detection feels sluggish.")]
        [SerializeField] [Min(2)] private int settlementFrameWindow = 5;

        [Tooltip("Cumulative midpoint displacement (m) across the settlement window required " +
                 "to keep a stroke active. Increase if tremor sustains strokes falsely; " +
                 "decrease if slow deliberate strokes are being cut short.")]
        [SerializeField] private float settlementDisplacementThreshold = 0.005f;

        [Tooltip("Exponential smoothing time constant for deceleration toward zero (seconds). " +
                 "Increase for a longer glide; decrease for a snappier stop.")]
        [SerializeField] private float decelerationTau = 1.0f;

        [Header("Chain Multiplier")]
        [Tooltip("Maximum elapsed time (seconds) between stroke end and next stroke onset " +
                 "for the chain to remain active. Exceeding this resets multiplier to 1.0.")]
        [SerializeField] private float chainWindowSeconds = 2.0f;

        [Tooltip("Amount added to the chain multiplier per committed stroke within the window. " +
                 "Increase for more aggressive acceleration per stroke; decrease for subtler buildup.")]
        [SerializeField] private float chainMultiplierGrowthFactor = 1.32f;

        [Tooltip("Upper bound on the chain multiplier. " +
                 "Increase to allow higher top-end chained speed; decrease to cap it sooner.")]
        [SerializeField] private float chainMultiplierCap = 10.0f;

        [Tooltip("Rate at which the chain multiplier decays toward 1.0 per second during idle. " +
                 "Increase for a faster bleed; decrease to hold the multiplier longer between strokes.")]
        [SerializeField] private float chainMultiplierDecayRate = 0.5f;

        [Header("Terrain Safety")]
        [SerializeField] private float terrainFloorOffset = 2f;
        [SerializeField] private LayerMask terrainLayer = ~0;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;

        // -----------------------------------------------------------------------
        // Private — Stroke State
        // -----------------------------------------------------------------------

        private List<Vector3> _strokeSamples = new List<Vector3>();
        private Vector3 _strokeOrigin;
        private Vector3 _travelDirection;
        private bool _strokeActive;

        // -----------------------------------------------------------------------
        // Private — Velocity
        // -----------------------------------------------------------------------

        private Vector3 _currentVelocity = Vector3.zero;
        private Vector3 _targetVelocity  = Vector3.zero;

        // -----------------------------------------------------------------------
        // Private — Chain Multiplier
        // -----------------------------------------------------------------------

        private float _chainMultiplier   = 1.0f;
        private float _lastStrokeEndTime = -999f;
        private bool  _chainActive       = false;

        // -----------------------------------------------------------------------
        // Private — Settlement Detection
        // -----------------------------------------------------------------------

        private Vector3[] _settlementBuffer;
        private int  _settlementIndex;
        private bool _settlementBufferFull;

        // -----------------------------------------------------------------------
        // Const
        // -----------------------------------------------------------------------

        private const string LOG_TAG = "[PinchToMoveController]";

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void Start()
        {
            InitSettlementBuffer();
            ValidateReferences();
            SubscribeToDetector();
        }

        private void Update()
        {
            if (!ReferencesValid()) return;

            if (pinchDetector.IsPinching)
            {
                RecordSettlementSample(pinchDetector.PinchMidpointPosition);

                if (_strokeActive)
                {
                    if (IsHandSettled())
                        EndStroke("hand settled");
                    else
                        UpdateStroke();
                }
                else
                {
                    TryReinitiateStroke();
                }
            }

            DecayChainMultiplier();
            ApplyVelocity();
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
            BeginStroke();

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Pinch started | origin={_strokeOrigin} | " +
                          $"multiplier={_chainMultiplier:F2}");
        }

        private void HandlePinchReleased()
        {
            if (_strokeActive)
                EndStroke("pinch released");

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Pinch released | velocity={_currentVelocity.magnitude:F1}m/s");
        }

        // -----------------------------------------------------------------------
        // Stroke Lifecycle
        // -----------------------------------------------------------------------

        private void BeginStroke()
        {
            _strokeSamples.Clear();
            _strokeOrigin = pinchDetector.PinchMidpointPosition;
            _strokeActive = true;
            ClearSettlementBuffer();
        }

        private void EndStroke(string reason)
        {
            _strokeActive   = false;
            _targetVelocity = Vector3.zero;

            float elapsed = Time.time - _lastStrokeEndTime;

            if (elapsed <= chainWindowSeconds)
            {
                _chainMultiplier = Mathf.Min(
                    _chainMultiplier * chainMultiplierGrowthFactor,
                    chainMultiplierCap);
                _chainActive = true;
            }
            else
            {
                _chainMultiplier = 1.0f;
                _chainActive     = false;
            }

            _lastStrokeEndTime = Time.time;

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Stroke ended ({reason}) | " +
                          $"multiplier={_chainMultiplier:F2} | chain={_chainActive} | " +
                          $"coasting at {_currentVelocity.magnitude:F1}m/s");
        }

        private void TryReinitiateStroke()
        {
            float displacement = Vector3.Distance(
                pinchDetector.PinchMidpointPosition, _strokeOrigin);

            if (displacement >= minimumArcThreshold)
            {
                BeginStroke();

                if (enableDebugLogging)
                    Debug.Log($"{LOG_TAG} Stroke re-initiated mid-pinch | " +
                              $"new origin={_strokeOrigin}");
            }
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

            if (!TryFitLine(_strokeSamples, out Vector3 bestFitDirection, out float strokeSign))
                return;

            _travelDirection = bestFitDirection;

            float baseSpeed    = Mathf.Min(arc * arcToSpeedScale, maxSpeed);
            float chainedSpeed = Mathf.Min(baseSpeed * _chainMultiplier, maxSpeed * chainMultiplierCap);
            _targetVelocity    = _travelDirection * (chainedSpeed * strokeSign);
            _currentVelocity   = _targetVelocity;

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Stroke | arc={arc:F3}m | baseSpeed={baseSpeed:F1} | " +
                          $"multiplier={_chainMultiplier:F2} | chainedSpeed={chainedSpeed:F1}m/s | " +
                          $"dir={_travelDirection} | sign={strokeSign:F0}");
        }

        // -----------------------------------------------------------------------
        // Chain Multiplier Decay
        // -----------------------------------------------------------------------

        private void DecayChainMultiplier()
        {
            if (!_chainActive) return;
            if (_chainMultiplier <= 1.0f) { _chainActive = false; return; }

            _chainMultiplier = Mathf.Max(
                1.0f,
                _chainMultiplier - chainMultiplierDecayRate * Time.deltaTime);

            if (_chainMultiplier <= 1.0f)
            {
                _chainMultiplier = 1.0f;
                _chainActive     = false;
            }
        }

        // -----------------------------------------------------------------------
        // Settlement Detection
        // -----------------------------------------------------------------------

        private void InitSettlementBuffer()
        {
            _settlementBuffer     = new Vector3[settlementFrameWindow];
            _settlementIndex      = 0;
            _settlementBufferFull = false;
        }

        private void ClearSettlementBuffer()
        {
            _settlementIndex      = 0;
            _settlementBufferFull = false;
        }

        private void RecordSettlementSample(Vector3 position)
        {
            _settlementBuffer[_settlementIndex] = position;
            _settlementIndex = (_settlementIndex + 1) % settlementFrameWindow;
            if (_settlementIndex == 0) _settlementBufferFull = true;
        }

        private bool IsHandSettled()
        {
            int count = _settlementBufferFull ? settlementFrameWindow : _settlementIndex;
            if (count < 2) return false;

            float totalDisplacement = 0f;
            for (int i = 1; i < count; i++)
            {
                int curr = (_settlementIndex - i - 1 + settlementFrameWindow) % settlementFrameWindow;
                int prev = (_settlementIndex - i     + settlementFrameWindow) % settlementFrameWindow;
                totalDisplacement += Vector3.Distance(_settlementBuffer[curr], _settlementBuffer[prev]);
            }

            return totalDisplacement < settlementDisplacementThreshold;
        }

        // -----------------------------------------------------------------------
        // Line of Best Fit (PCA)
        // -----------------------------------------------------------------------

        private bool TryFitLine(List<Vector3> samples, out Vector3 direction, out float sign)
        {
            direction = Vector3.forward;
            sign      = 1f;

            Vector3 centroid = Vector3.zero;
            foreach (var s in samples) centroid += s;
            centroid /= samples.Count;

            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var s in samples)
            {
                Vector3 d = s - centroid;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }

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

            Vector3 netDisplacement = pinchDetector.PinchMidpointPosition - _strokeOrigin;
            sign = -Mathf.Sign(Vector3.Dot(netDisplacement, direction));

            return true;
        }

        // -----------------------------------------------------------------------
        // Movement
        // -----------------------------------------------------------------------

        private void ApplyVelocity()
        {
            if (!_strokeActive && _currentVelocity.sqrMagnitude > 0.001f)
            {
                float factor = 1f - Mathf.Exp(-Time.deltaTime / decelerationTau);
                _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, factor);

                if (_currentVelocity.sqrMagnitude < 0.01f)
                    _currentVelocity = Vector3.zero;
            }

            if (_currentVelocity.sqrMagnitude < 0.001f) return;
            xrOrigin.position += _currentVelocity * Time.deltaTime;
        }

        private void EnforceTerrainFloor()
        {
            Vector3 rayOrigin = xrOrigin.position + Vector3.up * 10000f;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20000f, terrainLayer))
                return;

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

        private bool ReferencesValid() =>
            pinchDetector != null && xrOrigin != null && headTransform != null;
    }
}