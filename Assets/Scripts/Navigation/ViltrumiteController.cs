using UnityEngine;

namespace AerialNav.Navigation
{
    /// <summary>
    /// Viltrumite-style flight locomotion.
    /// Right fist = fly in hand direction. Open hand = decelerate to hover.
    /// Dual fully-extended fists = 2x speed boost.
    /// Attach to Locomotion GameObject alongside FistDetector.
    /// </summary>
    public class ViltrumiteController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FistDetector fistDetector;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform rightWristTransform;
        [SerializeField] private Transform leftWristTransform;

        [Header("Extension Thresholds")]
        [Tooltip("Minimum wrist-to-head distance to register movement (meters)")]
        [SerializeField] private float minExtension = 0.25f;

        [Tooltip("Distance at which full speed is reached (meters)")]
        [SerializeField] private float maxExtension = 0.7f;

        [Header("Speed")]
        [Tooltip("Universal speed cap (m/s)")]
        [SerializeField] private float maxSpeed = 4000f;

        [Tooltip("Speed multiplier when both fists are fully extended simultaneously")]
        [SerializeField] private float dualFistBoostMultiplier = 2f;

        [Header("Cinematic Motion")]
        [Tooltip("Acceleration time constant (seconds). Higher = slower, weightier ramp-up.")]
        [SerializeField] private float accelerationTau = 1.6f;

        [Tooltip("Deceleration rate when hand opens. Lower = longer coast.")]
        [SerializeField] private float decelerationRate = 2.5f;

        [Header("Terrain Safety")]
        [Tooltip("Minimum height above terrain (meters)")]
        [SerializeField] private float terrainFloorOffset = 2f;

        [Tooltip("LayerMask for terrain raycasting")]
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
                Fly();
            else
                Decelerate();

            ApplyMovement();
            EnforceTerrainFloor();
        }

        private void Fly()
        {
            float extension = Vector3.Distance(rightWristTransform.position, headTransform.position);

            if (extension < minExtension) return;

            float extensionNormalized = Mathf.InverseLerp(minExtension, maxExtension, extension);
            bool isDualBoostActive = IsDualFistBoostActive(extensionNormalized);

            float speed = extensionNormalized * maxSpeed;
            if (isDualBoostActive)
                speed *= dualFistBoostMultiplier;

            Vector3 targetVelocity = rightWristTransform.forward * speed;

            // Exponential smoothing: alpha = 1 - e^(-dt/tau)
            // At tau=1.6s, reaches ~95% of target speed in ~4.8s — weighty, cinematic.
            float alpha = 1f - Mathf.Exp(-Time.deltaTime / accelerationTau);
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, alpha);

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} speed={_currentVelocity.magnitude:F1}m/s | boost={isDualBoostActive} | ext={extension:F2}m");
        }

        private void Decelerate()
        {
            if (_currentVelocity.sqrMagnitude < 0.01f)
            {
                _currentVelocity = Vector3.zero;
                return;
            }

            _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, decelerationRate * Time.deltaTime);
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

        /// <summary>
        /// Boost is active when both fists are closed AND both are fully extended.
        /// </summary>
        private bool IsDualFistBoostActive(float rightExtensionNormalized)
        {
            if (!fistDetector.IsLeftFist || !fistDetector.IsRightFist) return false;
            if (rightExtensionNormalized < 1f) return false;

            float leftExtension = Vector3.Distance(leftWristTransform.position, headTransform.position);
            float leftExtensionNormalized = Mathf.InverseLerp(minExtension, maxExtension, leftExtension);

            return leftExtensionNormalized >= 1f;
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
                Debug.LogError($"{LOG_TAG} Left Wrist Transform must be assigned (required for dual-fist boost).");
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