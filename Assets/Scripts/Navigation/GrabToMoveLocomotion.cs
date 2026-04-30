using System.Collections.Generic;
using UnityEngine;

namespace AerialNav.Navigation
{
    // GrabToMove — fist-based stroke locomotion.
    // Replaces PinchToMove; reuses FistDetector for gesture input.
    // Stroke tracking uses right wrist position via XRHandSubsystem,
    // mirroring PinchToMove's midpoint approach but anchored to the wrist.
    //
    // Come-hither boost: strokes directed toward the head/chest receive a
    // configurable speed multiplier proportional to their alignment with the
    // head-to-wrist-origin vector. Lateral and push-away strokes are unaffected.
    //
    // Stroke lifecycle, PCA line fitting, chain multiplier, Y suppression,
    // settlement detection, and terrain floor enforcement are preserved from
    // PinchToMove Phase 1–2.5.

    public class GrabToMoveController : MonoBehaviour
    {

        // Inspector


        [Header("References")]
        [SerializeField] private FistDetector fistDetector;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform rightWristTransform;

        [Header("Stroke Arc")]
        [Tooltip("Arc-to-speed multiplier. Primary speed dial.")]
        [SerializeField] private float arcToSpeedScale = 2000f;

        [Tooltip("Speed cap before chain multiplier is applied (m/s).")]
        [SerializeField] private float maxSpeed = 4000f;

        [Tooltip("Minimum wrist displacement from stroke origin to begin locomotion (m). Suppresses tremor.")]
        [SerializeField] private float minimumArcThreshold = 0.02f;

        [Tooltip("Minimum samples before best-fit line is computed.")]
        [SerializeField] [Min(2)] private int minimumRegressionSamples = 3;

        [Header("Come-Hither Boost")]
        [Tooltip("Speed multiplier applied when stroke arc is directed fully toward the head/chest. " +
                 "Scales linearly with alignment — lateral/push-away strokes receive no boost.")]
        [SerializeField] private float comeHitherBoostMultiplier = 2.5f;

        [Header("Stroke End Detection")]
        [Tooltip("Number of frames examined to determine if the hand has settled.")]
        [SerializeField] [Min(2)] private int settlementFrameWindow = 5;

        [Tooltip("Cumulative wrist displacement (m) across the settlement window required " +
                 "to keep a stroke active.")]
        [SerializeField] private float settlementDisplacementThreshold = 0.005f;

        [Tooltip("Exponential smoothing time constant for deceleration toward zero (seconds).")]
        [SerializeField] private float decelerationTau = 0.5f;

        [Header("Chain Multiplier")]
        [Tooltip("Maximum elapsed time (seconds) between stroke end and next stroke onset " +
                 "for the chain to remain active.")]
        [SerializeField] private float chainWindowSeconds = 3.0f;

        [Tooltip("Per-stroke multiplicative growth factor applied within the chain window.")]
        [SerializeField] private float chainMultiplierGrowthFactor = 1.32f;

        [Tooltip("Upper bound on the chain multiplier.")]
        [SerializeField] private float chainMultiplierCap = 10.0f;

        [Tooltip("Rate at which the chain multiplier decays toward 1.0 per second during idle.")]
        [SerializeField] private float chainMultiplierDecayRate = 0.5f;

        [Header("Y Suppression")]
        [Tooltip("PCA residual at which Y is fully zeroed. Prevents vertical drift from arc-y strokes.")]
        [SerializeField] private float maxResidualForFullSuppression = 0.04f;

        [Tooltip("PCA residual below which Y is untouched. Preserves intentional vertical locomotion.")]
        [SerializeField] private float minResidualForNoSuppression = 0.01f;

        [Header("Terrain Safety")]
        [SerializeField] private float terrainFloorOffset = 2f;
        [SerializeField] private LayerMask terrainLayer = ~0;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;


        // Private — Fist Edge Detection


        private bool _prevFistState = false;


        // Private — Stroke State


        private List<Vector3> _strokeSamples = new List<Vector3>();
        private Vector3 _strokeOrigin;
        private Vector3 _travelDirection;
        private bool _strokeActive;


        // Private — Velocity


        private Vector3 _currentVelocity = Vector3.zero;
        private Vector3 _targetVelocity  = Vector3.zero;


        // Private — Chain Multiplier


        private float _chainMultiplier   = 1.0f;
        private float _lastStrokeEndTime = -999f;
        private bool  _chainActive       = false;


        // Private — Settlement Detection


        private Vector3[] _settlementBuffer;
        private int  _settlementIndex;
        private bool _settlementBufferFull;

        private const string LOG_TAG = "[GrabToMoveController]";


        // Lifecycle


        private void Start()
        {
            InitSettlementBuffer();
            ValidateReferences();
        }

        private void Update()
        {
            if (!ReferencesValid()) return;

            HandleFistEdges();

            if (fistDetector.IsRightFist)
            {
                RecordSettlementSample(rightWristTransform.position);

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


        // Fist Edge Detection


        // Detects onset and release edges from FistDetector's boolean state,
        // mirroring the event-driven pattern from PinchDetector.
        private void HandleFistEdges()
        {
            bool currentFist = fistDetector.IsRightFist;

            if (currentFist && !_prevFistState)
                HandleGrabStarted();
            else if (!currentFist && _prevFistState)
                HandleGrabReleased();

            _prevFistState = currentFist;
        }

        private void HandleGrabStarted()
        {
            BeginStroke();

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Grab started | origin={_strokeOrigin} | " +
                          $"multiplier={_chainMultiplier:F2}");
        }

        private void HandleGrabReleased()
        {
            if (_strokeActive)
                EndStroke("grab released");

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Grab released | velocity={_currentVelocity.magnitude:F1}m/s");
        }


        // Stroke Lifecycle


        private void BeginStroke()
        {
            _strokeSamples.Clear();
            _strokeOrigin = rightWristTransform.position;
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
                rightWristTransform.position, _strokeOrigin);

            if (displacement >= minimumArcThreshold)
            {
                BeginStroke();

                if (enableDebugLogging)
                    Debug.Log($"{LOG_TAG} Stroke re-initiated mid-grab | " +
                              $"new origin={_strokeOrigin}");
            }
        }


        // Live Stroke Update


        private void UpdateStroke()
        {
            _strokeSamples.Add(rightWristTransform.position);

            float arc = Vector3.Distance(rightWristTransform.position, _strokeOrigin);
            if (arc < minimumArcThreshold) return;
            if (_strokeSamples.Count < minimumRegressionSamples) return;

            if (!TryFitLine(_strokeSamples, out Vector3 bestFitDirection, out float strokeSign, out float residual))
                return;

            _travelDirection = ApplyYSuppression(bestFitDirection, residual);

            // Come-hither boost: dot net displacement against stroke-origin-to-head vector.
            // Fully toward head = alignment 1.0 = full boost multiplier.
            // Lateral or push-away = alignment ~0 = no boost.
            Vector3 towardBody        = (headTransform.position - _strokeOrigin).normalized;
            Vector3 netDisplacement   = (rightWristTransform.position - _strokeOrigin).normalized;
            float comeHitherAlignment = Mathf.Clamp01(Vector3.Dot(netDisplacement, towardBody));
            float speedBoost          = Mathf.Lerp(1f, comeHitherBoostMultiplier, comeHitherAlignment);

            float baseSpeed    = Mathf.Min(arc * arcToSpeedScale * speedBoost, maxSpeed);
            float chainedSpeed = Mathf.Min(baseSpeed * _chainMultiplier, maxSpeed * chainMultiplierCap);
            _targetVelocity    = _travelDirection * (chainedSpeed * strokeSign);
            _currentVelocity   = _targetVelocity;

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Stroke | arc={arc:F3}m | residual={residual:F4} | " +
                          $"alignment={comeHitherAlignment:F2} | boost={speedBoost:F2} | " +
                          $"baseSpeed={baseSpeed:F1} | multiplier={_chainMultiplier:F2} | " +
                          $"chainedSpeed={chainedSpeed:F1}m/s | dir={_travelDirection}");
        }


        // Y Suppression


        private Vector3 ApplyYSuppression(Vector3 direction, float residual)
        {
            float suppressionWeight = Mathf.InverseLerp(
                minResidualForNoSuppression,
                maxResidualForFullSuppression,
                residual);

            Vector3 suppressed = new Vector3(
                direction.x,
                Mathf.Lerp(direction.y, 0f, suppressionWeight),
                direction.z);

            if (suppressed.sqrMagnitude < 0.0001f)
                return direction;

            return suppressed.normalized;
        }


        // Line of Best Fit (PCA)


        private bool TryFitLine(List<Vector3> samples, out Vector3 direction, out float sign, out float residual)
        {
            direction = Vector3.forward;
            sign      = 1f;
            residual  = 0f;

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

            float totalResidual = 0f;
            foreach (var s in samples)
            {
                Vector3 d         = s - centroid;
                Vector3 projected = Vector3.Dot(d, axis) * axis;
                Vector3 perp      = d - projected;
                totalResidual    += perp.magnitude;
            }
            residual = totalResidual / samples.Count;

            Vector3 netDisplacement = rightWristTransform.position - _strokeOrigin;
            sign = -Mathf.Sign(Vector3.Dot(netDisplacement, direction));

            return true;
        }


        // Chain Multiplier Decay


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


        // Settlement Detection


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


        // Movement


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


        // Reference Validation


        private void ValidateReferences()
        {
            if (fistDetector == null)
            {
                fistDetector = GetComponent<FistDetector>();
                if (fistDetector == null)
                    Debug.LogError($"{LOG_TAG} FistDetector not found on this GameObject.");
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

            if (rightWristTransform == null)
                Debug.LogError($"{LOG_TAG} Right Wrist Transform must be assigned in Inspector.");
        }

        private bool ReferencesValid() =>
            fistDetector != null
            && xrOrigin != null
            && headTransform != null
            && rightWristTransform != null;
    }
}