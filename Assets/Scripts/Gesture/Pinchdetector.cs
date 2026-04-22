using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace AerialNav.Gesture
{
    /// <summary>
    /// Detects index-thumb pinch gesture via MetaAimHand (XR Hands subsystem).
    /// Surfaces pinch state, strength, aim ray, and wrist velocity at pinch onset.
    /// Attach to the Locomotion GameObject alongside PinchToMoveController.
    ///
    /// Uses MIDDLE finger pinch (middle + thumb) rather than index to avoid
    /// conflicts with Meta OS system gestures (index pinch recenters viewport).
    ///
    /// Requires: Meta Hand Tracking Aim OpenXR feature enabled in
    /// Project Settings > XR Plug-in Management > OpenXR > OpenXR Feature Groups.
    /// </summary>
    public class PinchDetector : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        // Inspector
        // -----------------------------------------------------------------------

        [Header("Detection Settings")]
        [Tooltip("Pinch strength threshold to register onset. " +
                 "MetaAimHand fires middlePressed at 0.8 internally; lower for earlier detection.")]
        [SerializeField] [Range(0f, 1f)] private float pinchOnsetThreshold = 0.8f;

        [Tooltip("Pinch strength threshold below which release is registered. " +
                 "Hysteresis gap between onset and release prevents chatter.")]
        [SerializeField] [Range(0f, 1f)] private float pinchReleaseThreshold = 0.5f;

        [Header("Velocity Sampling")]
        [Tooltip("Number of frames over which wrist velocity is averaged at pinch onset. " +
                 "Higher values produce smoother onset velocity estimates.")]
        [SerializeField] [Min(1)] private int velocitySampleFrames = 5;

        [Header("Debug Visualization")]
        [Tooltip("Tint the hand mesh when a pinch is detected, confirming script efficacy.")]
        [SerializeField] private bool enableDebugVisualization = true;

        [Tooltip("Color applied to the right hand mesh when pinch is detected.")]
        [SerializeField] private Color pinchDetectedColor = new Color(0.627f, 0.125f, 0.941f, 1f);

        [Tooltip("Reference to right hand SkinnedMeshRenderer. Auto-found if null.")]
        [SerializeField] private SkinnedMeshRenderer rightHandRenderer;

        [Header("Debug Logging")]
        [SerializeField] private bool enableDebugLogging = false;

        // -----------------------------------------------------------------------
        // Public State
        // -----------------------------------------------------------------------

        /// <summary>Whether the hand is currently in a pinch.</summary>
        public bool IsPinching { get; private set; }

        /// <summary>Normalized middle-finger pinch strength this frame (0 = open, 1 = full pinch).</summary>
        public float PinchStrength { get; private set; }

        /// <summary>
        /// World-space aim ray at the moment of pinch onset.
        /// Valid only during and after a pinch; stale between strokes.
        /// </summary>
        public Ray AimRayAtOnset { get; private set; }

        /// <summary>
        /// World-space wrist velocity averaged over the frames immediately preceding pinch onset.
        /// Used by PinchToMoveController to correct the stroke arc for pre-pinch hand motion.
        /// </summary>
        public Vector3 WristVelocityAtOnset { get; private set; }

        /// <summary>Current world-space wrist position.</summary>
        public Vector3 WristPosition { get; private set; }

        // -----------------------------------------------------------------------
        // Events
        // -----------------------------------------------------------------------

        /// <summary>Fired on the frame pinch onset is detected.</summary>
        public event Action OnPinchStarted;

        /// <summary>Fired on the frame pinch release is detected.</summary>
        public event Action OnPinchReleased;

        // -----------------------------------------------------------------------
        // Private
        // -----------------------------------------------------------------------

        private XRHandSubsystem _handSubsystem;
        private MetaAimHand _metaAimHand;
        private bool _subsystemAvailable;

        // Original hand color for restoration
        private Color _originalHandColor;
        private bool _originalColorStored;

        // Rolling wrist position buffer for velocity estimation
        private Vector3[] _wristPositionBuffer;
        private float[] _wristTimestampBuffer;
        private int _bufferIndex;
        private bool _bufferFull;

        private const string LOG_TAG = "[PinchDetector]";

        // -----------------------------------------------------------------------
        // Unity Lifecycle
        // -----------------------------------------------------------------------

        private void Awake()
        {
            InitVelocityBuffer();
        }

        private void Start()
        {
            TryAcquireSubsystems();
            FindHandRenderer();
        }

        private void Update()
        {
            if (!_subsystemAvailable)
            {
                TryAcquireSubsystems();
                return;
            }

            SampleWristPosition();
            UpdatePinchState();

            if (enableDebugVisualization)
                UpdateDebugVisualization();
        }

        private void OnDisable()
        {
            RestoreHandColor();
        }

        private void OnDestroy()
        {
            RestoreHandColor();
        }

        // -----------------------------------------------------------------------
        // Subsystem Acquisition
        // -----------------------------------------------------------------------

        private void TryAcquireSubsystems()
        {
            var handSubsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(handSubsystems);

            if (handSubsystems.Count > 0 && handSubsystems[0].running)
                _handSubsystem = handSubsystems[0];

            _metaAimHand = MetaAimHand.right;

            _subsystemAvailable = _handSubsystem != null && _metaAimHand != null;

            if (_subsystemAvailable && enableDebugLogging)
                Debug.Log($"{LOG_TAG} Subsystems acquired.");
        }

        // -----------------------------------------------------------------------
        // Hand Renderer
        // -----------------------------------------------------------------------

        private void FindHandRenderer()
        {
            if (rightHandRenderer != null)
            {
                StoreOriginalColor();
                return;
            }

            var rightHand = GameObject.Find("Right Hand Tracking");
            if (rightHand != null)
                rightHandRenderer = rightHand.GetComponentInChildren<SkinnedMeshRenderer>();

            if (rightHandRenderer == null && enableDebugLogging)
                Debug.LogWarning($"{LOG_TAG} Right hand SkinnedMeshRenderer not found. " +
                                 "Assign manually in the Inspector for debug visualization.");

            StoreOriginalColor();
        }

        private void StoreOriginalColor()
        {
            if (_originalColorStored || rightHandRenderer == null) return;
            if (rightHandRenderer.material == null) return;

            _originalHandColor = rightHandRenderer.material.color;
            _originalColorStored = true;
        }

        private void UpdateDebugVisualization()
        {
            if (rightHandRenderer == null || rightHandRenderer.material == null) return;
            rightHandRenderer.material.color = IsPinching ? pinchDetectedColor : _originalHandColor;
        }

        private void RestoreHandColor()
        {
            if (!_originalColorStored) return;
            if (rightHandRenderer == null || rightHandRenderer.material == null) return;
            rightHandRenderer.material.color = _originalHandColor;
        }

        // -----------------------------------------------------------------------
        // Wrist Velocity Estimation
        // -----------------------------------------------------------------------

        private void InitVelocityBuffer()
        {
            _wristPositionBuffer = new Vector3[velocitySampleFrames];
            _wristTimestampBuffer = new float[velocitySampleFrames];
            _bufferIndex = 0;
            _bufferFull = false;
        }

        private void SampleWristPosition()
        {
            XRHandJoint wristJoint = _handSubsystem.rightHand.GetJoint(XRHandJointID.Wrist);
            if (!wristJoint.TryGetPose(out Pose wristPose)) return;

            WristPosition = wristPose.position;

            _wristPositionBuffer[_bufferIndex] = WristPosition;
            _wristTimestampBuffer[_bufferIndex] = Time.time;
            _bufferIndex = (_bufferIndex + 1) % velocitySampleFrames;
            if (_bufferIndex == 0) _bufferFull = true;
        }

        /// <summary>
        /// Computes a smoothed wrist velocity from the rolling buffer.
        /// Returns Vector3.zero if insufficient samples are available.
        /// </summary>
        private Vector3 ComputeWristVelocity()
        {
            int sampleCount = _bufferFull ? velocitySampleFrames : _bufferIndex;
            if (sampleCount < 2) return Vector3.zero;

            int newest = (_bufferIndex - 1 + velocitySampleFrames) % velocitySampleFrames;
            int oldest = _bufferFull ? _bufferIndex % velocitySampleFrames : 0;

            float dt = _wristTimestampBuffer[newest] - _wristTimestampBuffer[oldest];
            if (dt <= 0f) return Vector3.zero;

            return (_wristPositionBuffer[newest] - _wristPositionBuffer[oldest]) / dt;
        }

        // -----------------------------------------------------------------------
        // Pinch State Machine
        // -----------------------------------------------------------------------

        private void UpdatePinchState()
        {
            if (_metaAimHand == null || !_metaAimHand.added)
            {
                if (IsPinching) RegisterRelease();
                PinchStrength = 0f;
                return;
            }

            // Middle finger pinch avoids Meta OS system gesture conflicts
            PinchStrength = _metaAimHand.pinchStrengthMiddle.ReadValue();

            if (!IsPinching && PinchStrength >= pinchOnsetThreshold)
                RegisterOnset();
            else if (IsPinching && PinchStrength < pinchReleaseThreshold)
                RegisterRelease();
        }

        private void RegisterOnset()
        {
            IsPinching = true;

            Vector3 aimPosition = _metaAimHand.devicePosition.ReadValue();
            Quaternion aimRotation = _metaAimHand.deviceRotation.ReadValue();
            AimRayAtOnset = new Ray(aimPosition, aimRotation * Vector3.forward);

            WristVelocityAtOnset = ComputeWristVelocity();

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Pinch onset | strength={PinchStrength:F2} | " +
                          $"aimDir={AimRayAtOnset.direction} | wristVel={WristVelocityAtOnset.magnitude:F2}m/s");

            OnPinchStarted?.Invoke();
        }

        private void RegisterRelease()
        {
            IsPinching = false;

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Pinch released | strength={PinchStrength:F2}");

            OnPinchReleased?.Invoke();
        }

        // -----------------------------------------------------------------------
        // Public Utilities
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the current world-space aim ray from the MetaAimHand aim pose.
        /// Useful for live aim visualization; prefer AimRayAtOnset for locomotion direction.
        /// </summary>
        public Ray GetCurrentAimRay()
        {
            if (_metaAimHand == null || !_metaAimHand.added)
                return new Ray(Vector3.zero, Vector3.forward);

            Vector3 pos = _metaAimHand.devicePosition.ReadValue();
            Quaternion rot = _metaAimHand.deviceRotation.ReadValue();
            return new Ray(pos, rot * Vector3.forward);
        }

        /// <summary>Allows runtime adjustment of onset threshold for A/B tuning.</summary>
        public void SetOnsetThreshold(float threshold)
        {
            pinchOnsetThreshold = Mathf.Clamp01(threshold);
            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Onset threshold set to {pinchOnsetThreshold:F2}");
        }
    }
}