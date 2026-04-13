using UnityEngine;

namespace AerialNav.Navigation
{
    /// <summary>
    /// Viltrumite-style flight locomotion.
    /// Fist = fly in hand direction, open hand = decelerate to hover.
    /// Attach to Locomotion GameObject alongside FistDetector.
    /// </summary>
    public class ViltrumiteController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FistDetector fistDetector;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform rightWristTransform;

        [Header("Speed Settings")]
        [Tooltip("Minimum extension from chest to register movement (meters)")]
        [SerializeField] private float minExtension = 0.25f;

        [Tooltip("Maximum extension for full speed (meters)")]
        [SerializeField] private float maxExtension = 0.7f;

        [Header("Altitude Speed Tiers (m/s)")]
        [SerializeField] private float speedTier0 = 40f;
        [SerializeField] private float speedTier1 = 200f;
        [SerializeField] private float speedTier2 = 800f;
        [SerializeField] private float speedTier3 = 4000f;
        [SerializeField] private float speedTier4 = 20000f;

        [Header("Cinematic Motion")]
        [Tooltip("Time constant (seconds) for velocity smoothing. ~0.85s gives 95% of target speed in ~2.5s.")]
        [SerializeField] private float accelerationTau = 0.85f;

        [Tooltip("Rate of velocity decay when hand opens.")]
        [SerializeField] private float decelerationRate = 4f;

        [Header("Terrain Safety")]
        [Tooltip("Minimum height above terrain (meters)")]
        [SerializeField] private float terrainFloorOffset = 2f;

        [Tooltip("LayerMask for terrain raycasting")]
        [SerializeField] private LayerMask terrainLayer = ~0;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;

        private Vector3 _currentVelocity = Vector3.zero;
        private float _spawnAltitude = 0f;
        private const string LOG_TAG = "[ViltrumiteController]";

        private void Start()
        {
            ValidateReferences();
            _spawnAltitude = xrOrigin != null ? xrOrigin.position.y : 0f;
        }

        private void Update()
        {
            if (!ReferencesValid()) return;

            if (fistDetector.IsRightFist)
            {
                Fly();
            }
            else
            {
                Decelerate();
            }

            ApplyMovement();
            EnforceTerrainFloor();
        }

        private void Fly()
        {
            Vector3 handPosition = rightWristTransform.position;
            Vector3 headPosition = headTransform.position;

            float extension = Vector3.Distance(handPosition, headPosition);

            if (extension < minExtension)
            {
                if (enableDebugLogging)
                    Debug.Log($"{LOG_TAG} Hovering (extension {extension:F2}m < {minExtension}m)");
                return;
            }

            float extensionNormalized = Mathf.InverseLerp(minExtension, maxExtension, extension);
            float maxSpeed = GetMaxSpeedForAltitude();
            float targetSpeed = extensionNormalized * maxSpeed;

            Vector3 flyDirection = rightWristTransform.forward;
            Vector3 targetVelocity = flyDirection * targetSpeed;

            // Exponential smoothing toward target velocity for cinematic acceleration.
            // alpha = 1 - e^(-dt/tau): at tau=0.85s, reaches ~95% of target in ~2.5s.
            float alpha = 1f - Mathf.Exp(-Time.deltaTime / accelerationTau);
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, alpha);

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Flying: dir={flyDirection}, speed={_currentVelocity.magnitude:F1}m/s, target={targetSpeed:F1}m/s, ext={extension:F2}m");
        }

        private void Decelerate()
        {
            if (_currentVelocity.sqrMagnitude < 0.01f)
            {
                _currentVelocity = Vector3.zero;
                return;
            }

            _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, decelerationRate * Time.deltaTime);

            if (enableDebugLogging && _currentVelocity.sqrMagnitude > 0.1f)
                Debug.Log($"{LOG_TAG} Decelerating: speed={_currentVelocity.magnitude:F1}m/s");
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

                    if (enableDebugLogging)
                        Debug.Log($"{LOG_TAG} Terrain floor enforced at y={minAllowedY:F1}");
                }
            }
        }

        private float GetMaxSpeedForAltitude()
        {
            float relativeAltitude = xrOrigin.position.y - _spawnAltitude;

            if (relativeAltitude < 5f)    return speedTier0;
            if (relativeAltitude < 50f)   return speedTier1;
            if (relativeAltitude < 500f)  return speedTier2;
            if (relativeAltitude < 5000f) return speedTier3;
            return speedTier4;
        }

        private void ValidateReferences()
        {
            if (fistDetector == null)
            {
                fistDetector = GetComponent<FistDetector>();
                if (fistDetector == null)
                    Debug.LogError($"{LOG_TAG} FistDetector not found! Attach to same GameObject or assign in Inspector.");
            }

            if (xrOrigin == null)
            {
                var xrOriginComponent = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
                if (xrOriginComponent != null)
                {
                    xrOrigin = xrOriginComponent.transform;
                    Debug.Log($"{LOG_TAG} Auto-assigned XR Origin: {xrOrigin.name}");
                }
                else
                {
                    Debug.LogError($"{LOG_TAG} XR Origin not found!");
                }
            }

            if (headTransform == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    headTransform = cam.transform;
                    Debug.Log($"{LOG_TAG} Auto-assigned Head Transform: {headTransform.name}");
                }
                else
                {
                    Debug.LogError($"{LOG_TAG} Main Camera not found for head tracking!");
                }
            }

            if (rightWristTransform == null)
                Debug.LogError($"{LOG_TAG} Right Wrist Transform must be assigned! Drag R_Wrist from Right Hand Tracking.");
        }

        private bool ReferencesValid()
        {
            return fistDetector != null && xrOrigin != null && headTransform != null && rightWristTransform != null;
        }
    }
}