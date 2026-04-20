using UnityEngine;

namespace AerialNav.Navigation
{
    // Viltrumite-style flight locomotion. Attach to Locomotion alongside FistDetector.
    // Gesture contract:
    //   Right fist < minExtension  -> hover brake
    //   Right fist > minExtension  -> fly, speed scaled by arm extension
    //   Open right hand            -> decelerate to rest
    //   Both fists at full ext.    -> 2x speed boost
    public class ViltrumiteController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FistDetector fistDetector;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform rightWristTransform;
        [SerializeField] private Transform leftWristTransform;

        [Header("Extension Thresholds")]
        [Tooltip("Wrist-to-head distance for hover dead zone (m). Calibrate via A/B test.")]
        [SerializeField] private float minExtension = 0.25f;

        [Tooltip("Wrist-to-head distance for full speed (m). Calibrate via A/B test.")]
        [SerializeField] private float maxExtension = 0.7f;

        [Header("Speed")]
        [Tooltip("Speed cap at full extension, no boost (m/s).")]
        [SerializeField] private float maxSpeed = 4000f;

        [Tooltip("Multiplier when both fists are at full extension.")]
        [SerializeField] private float dualFistBoostMultiplier = 2f;

        [Tooltip("Normalized extension threshold for boost. < 1 adds tolerance for arm reach variance.")]
        [SerializeField] private float dualFistBoostThreshold = 0.92f;

        [Header("Cinematic Motion")]
        [Tooltip("Acceleration time constant (s). Higher = weightier ramp-up.")]
        [SerializeField] private float accelerationTau = 1.6f;

        [Tooltip("Deceleration lerp coefficient. Lower = longer coast.")]
        [SerializeField] private float decelerationRate = 2.5f;

        [Header("Terrain Safety")]
        [Tooltip("Minimum height above terrain (m).")]
        [SerializeField] private float terrainFloorOffset = 2f;

        [Tooltip("LayerMask for terrain raycasting.")]
        [SerializeField] private LayerMask terrainLayer = ~0;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;

        private Vector3 _currentVelocity = Vector3.zero;
        private const string LOG_TAG = "[ViltrumiteController]";

        private void Start()
        {
            ValidateReferences();
        }

        private void Update()
        {
            if (!ReferencesValid()) return;

            if (fistDetector.IsRightFist)
            {
                float extension = Vector3.Distance(rightWristTransform.position, headTransform.position);

                if (extension < minExtension)
                    HoverBrake();
                else
                    Fly(extension);
            }
            else
            {
                Decelerate();
            }

            ApplyMovement();
            EnforceTerrainFloor();
        }

        // Fist at chest level — active deceleration to hover
        private void HoverBrake()
        {
            if (_currentVelocity.sqrMagnitude < 0.01f)
            {
                _currentVelocity = Vector3.zero;
                return;
            }

            _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, decelerationRate * Time.deltaTime);

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} [HOVER-BRAKE] speed={_currentVelocity.magnitude:F1}m/s");
        }

        // Fist extended — accelerate in wrist-forward direction
        private void Fly(float extension)
        {
            // Clamp01 handles overshoot beyond maxExtension; required for boost evaluation
            float extensionNormalized = Mathf.Clamp01(Mathf.InverseLerp(minExtension, maxExtension, extension));
            bool isDualBoostActive = IsDualFistBoostActive(extensionNormalized);

            float speed = extensionNormalized * maxSpeed;
            if (isDualBoostActive)
                speed *= dualFistBoostMultiplier;

            Vector3 targetVelocity = rightWristTransform.forward * speed;

            // Exponential smoothing: alpha = 1 - e^(-dt/tau)
            float alpha = 1f - Mathf.Exp(-Time.deltaTime / accelerationTau);
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, alpha);

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} speed={_currentVelocity.magnitude:F1}m/s | boost={isDualBoostActive} | ext={extension:F2}m | extNorm={extensionNormalized:F2}");
        }

        // Open hand — decelerate to rest
        private void Decelerate()
        {
            if (_currentVelocity.sqrMagnitude < 0.01f)
            {
                _currentVelocity = Vector3.zero;
                return;
            }

            _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, decelerationRate * Time.deltaTime);

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} [DECEL] speed={_currentVelocity.magnitude:F1}m/s");
        }

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

                    if (_currentVelocity.y < 0)
                        _currentVelocity.y = 0;
                }
            }
        }

        // Both fists closed and at or beyond dualFistBoostThreshold; rightExtensionNormalized pre-clamped
        private bool IsDualFistBoostActive(float rightExtensionNormalized)
        {
            if (!fistDetector.IsLeftFist || !fistDetector.IsRightFist) return false;
            if (rightExtensionNormalized < dualFistBoostThreshold) return false;

            float leftExtension = Vector3.Distance(leftWristTransform.position, headTransform.position);
            float leftExtensionNormalized = Mathf.Clamp01(Mathf.InverseLerp(minExtension, maxExtension, leftExtension));

            return leftExtensionNormalized >= dualFistBoostThreshold;
        }

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
                else Debug.LogError($"{LOG_TAG} XR Origin not found!");
            }

            if (headTransform == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    headTransform = cam.transform;
                    Debug.Log($"{LOG_TAG} Auto-assigned Head Transform: {headTransform.name}");
                }
                else Debug.LogError($"{LOG_TAG} Main Camera not found!");
            }

            if (rightWristTransform == null)
                Debug.LogError($"{LOG_TAG} Right Wrist Transform must be assigned.");

            if (leftWristTransform == null)
                Debug.LogError($"{LOG_TAG} Left Wrist Transform must be assigned.");
        }

        private bool ReferencesValid()
        {
            return fistDetector != null
                && xrOrigin != null
                && headTransform != null
                && rightWristTransform != null
                && leftWristTransform != null;
        }
    }
}