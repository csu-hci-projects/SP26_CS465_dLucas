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
        [SerializeField] private float speedTier0 = 10f;    // 0-10m
        [SerializeField] private float speedTier1 = 50f;    // 10-100m
        [SerializeField] private float speedTier2 = 200f;   // 100m-1km
        [SerializeField] private float speedTier3 = 1000f;  // 1km-10km
        [SerializeField] private float speedTier4 = 5000f;  // 10km+

        [Header("Deceleration")]
        [Tooltip("Rate of velocity decay when hand opens (higher = faster stop)")]
        [SerializeField] private float decelerationRate = 4f;

        [Header("Terrain Safety")]
        [Tooltip("Minimum height above terrain (meters)")]
        [SerializeField] private float terrainFloorOffset = 2f;
        
        [Tooltip("LayerMask for terrain raycasting")]
        [SerializeField] private LayerMask terrainLayer = ~0; // Default: all layers

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

            // Calculate extension (fist distance from head)
            float extension = Vector3.Distance(handPosition, headPosition);

            // Dead zone check: fist too close to chest = hover
            if (extension < minExtension)
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"{LOG_TAG} Hovering (extension {extension:F2}m < {minExtension}m)");
                }
                return; // Maintain current velocity (no acceleration, no deceleration)
            }

            // Calculate speed multiplier from extension
            float extensionNormalized = Mathf.InverseLerp(minExtension, maxExtension, extension);
            float maxSpeed = GetMaxSpeedForAltitude();
            float targetSpeed = extensionNormalized * maxSpeed;

            // Direction from wrist orientation (forward vector of the hand)
            Vector3 flyDirection = rightWristTransform.forward;

            // Set velocity
            _currentVelocity = flyDirection * targetSpeed;

            if (enableDebugLogging)
            {
                Debug.Log($"{LOG_TAG} Flying: dir={flyDirection}, speed={targetSpeed:F1}m/s, ext={extension:F2}m");
            }
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
            {
                Debug.Log($"{LOG_TAG} Decelerating: speed={_currentVelocity.magnitude:F1}m/s");
            }
        }

        private void ApplyMovement()
        {
            if (_currentVelocity.sqrMagnitude < 0.001f) return;

            xrOrigin.position += _currentVelocity * Time.deltaTime;
        }

        private void EnforceTerrainFloor()
        {
            Vector3 origin = xrOrigin.position + Vector3.up * 10000f; // Ray from high above
            
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20000f, terrainLayer))
            {
                float minAllowedY = hit.point.y + terrainFloorOffset;
                
                if (xrOrigin.position.y < minAllowedY)
                {
                    Vector3 corrected = xrOrigin.position;
                    corrected.y = minAllowedY;
                    xrOrigin.position = corrected;
                    
                    // Kill downward velocity on floor contact
                    if (_currentVelocity.y < 0)
                    {
                        _currentVelocity.y = 0;
                    }

                    if (enableDebugLogging)
                    {
                        Debug.Log($"{LOG_TAG} Terrain floor enforced at y={minAllowedY:F1}");
                    }
                }
            }
        }

        private float GetMaxSpeedForAltitude()
        {
            // Approximate altitude: distance from origin Y=0 or use head height
            // For Cesium, we use the XR origin's height above the globe anchor
            float altitude = xrOrigin.position.y;

            if (altitude < 10f) return speedTier0;
            if (altitude < 100f) return speedTier1;
            if (altitude < 1000f) return speedTier2;
            if (altitude < 10000f) return speedTier3;
            return speedTier4;
        }

        private void ValidateReferences()
        {
            if (fistDetector == null)
            {
                fistDetector = GetComponent<FistDetector>();
                if (fistDetector == null)
                {
                    Debug.LogError($"{LOG_TAG} FistDetector not found! Attach to same GameObject or assign in Inspector.");
                }
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
            {
                Debug.LogError($"{LOG_TAG} Right Wrist Transform must be assigned! Drag R_Wrist from Right Hand Tracking.");
            }
        }

        private bool ReferencesValid()
        {
            return fistDetector != null && xrOrigin != null && headTransform != null && rightWristTransform != null;
        }
    }
}