using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace AerialNav.Gesture
{
    // Detects middle-thumb pinch via MetaAimHand.
    // Tracks pinch midpoint (MiddleTip/ThumbTip average) for fine-grained stroke sensing.
    // Middle finger avoids Meta OS system gesture conflicts.
    // Requires: Meta Hand Tracking Aim enabled in OpenXR Feature Groups.
    
    public class PinchDetector : MonoBehaviour
    {

        // Inspector


        [Header("Detection Settings")]
        [Tooltip("Strength threshold for pinch onset.")]
        [SerializeField] [Range(0f, 1f)] private float pinchOnsetThreshold = 0.8f;

        [Tooltip("Strength threshold for pinch release. Gap prevents chatter.")]
        [SerializeField] [Range(0f, 1f)] private float pinchReleaseThreshold = 0.5f;

        [Header("Velocity Sampling")]
        [Tooltip("Frame window for onset velocity estimation.")]
        [SerializeField] [Min(1)] private int velocitySampleFrames = 5;

        [Header("Debug Visualization")]
        [Tooltip("Tint hand mesh on pinch detection.")]
        [SerializeField] private bool enableDebugVisualization = true;

        [SerializeField] private Color pinchDetectedColor = new Color(0.627f, 0.125f, 0.941f, 1f);

        [Tooltip("Auto-found if null.")]
        [SerializeField] private SkinnedMeshRenderer rightHandRenderer;

        [Header("Debug Logging")]
        [SerializeField] private bool enableDebugLogging = false;


        // Public State


        public bool IsPinching { get; private set; }

        // normalized, 0-1
        public float PinchStrength { get; private set; }

        // midpoint between MiddleTip and ThumbTip — primary stroke tracking point
        public Vector3 PinchMidpointPosition { get; private set; }

        // wrist position retained as secondary reference
        public Vector3 WristPosition { get; private set; }

        // midpoint velocity averaged over onset window
        public Vector3 MidpointVelocityAtOnset { get; private set; }


        // Events


        public event Action OnPinchStarted;
        public event Action OnPinchReleased;


        // Private


        private XRHandSubsystem _handSubsystem;
        private MetaAimHand _metaAimHand;
        private bool _subsystemAvailable;

        private Color _originalHandColor;
        private bool _originalColorStored;

        // rolling buffer tracks midpoint, not wrist
        private Vector3[] _midpointBuffer;
        private float[] _timestampBuffer;
        private int _bufferIndex;
        private bool _bufferFull;

        private const string LOG_TAG = "[PinchDetector]";


        // Lifecycle


        private void Awake()
        {
            InitBuffer();
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

            SampleJoints();
            UpdatePinchState();

            if (enableDebugVisualization)
                UpdateDebugVisualization();
        }

        private void OnDisable() => RestoreHandColor();
        private void OnDestroy() => RestoreHandColor();


        // Subsystem Acquisition


        private void TryAcquireSubsystems()
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);

            if (subsystems.Count > 0 && subsystems[0].running)
                _handSubsystem = subsystems[0];

            _metaAimHand = MetaAimHand.right;
            _subsystemAvailable = _handSubsystem != null && _metaAimHand != null;

            if (_subsystemAvailable && enableDebugLogging)
                Debug.Log($"{LOG_TAG} Subsystems acquired.");
        }


        // Joint Sampling


        private void SampleJoints()
        {
            var hand = _handSubsystem.rightHand;

            // wrist
            if (hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wristPose))
                WristPosition = wristPose.position;

            // pinch midpoint
            bool gotMiddle = hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out Pose middlePose);
            bool gotThumb  = hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbPose);

            if (gotMiddle && gotThumb)
            {
                PinchMidpointPosition = (middlePose.position + thumbPose.position) * 0.5f;

                _midpointBuffer[_bufferIndex] = PinchMidpointPosition;
                _timestampBuffer[_bufferIndex] = Time.time;
                _bufferIndex = (_bufferIndex + 1) % velocitySampleFrames;
                if (_bufferIndex == 0) _bufferFull = true;
            }
        }


        // Velocity Estimation


        private void InitBuffer()
        {
            _midpointBuffer  = new Vector3[velocitySampleFrames];
            _timestampBuffer = new float[velocitySampleFrames];
            _bufferIndex = 0;
            _bufferFull  = false;
        }

        private Vector3 ComputeMidpointVelocity()
        {
            int count = _bufferFull ? velocitySampleFrames : _bufferIndex;
            if (count < 2) return Vector3.zero;

            int newest = (_bufferIndex - 1 + velocitySampleFrames) % velocitySampleFrames;
            int oldest = _bufferFull ? _bufferIndex % velocitySampleFrames : 0;

            float dt = _timestampBuffer[newest] - _timestampBuffer[oldest];
            if (dt <= 0f) return Vector3.zero;

            return (_midpointBuffer[newest] - _midpointBuffer[oldest]) / dt;
        }


        // Pinch State Machine


        private void UpdatePinchState()
        {
            if (_metaAimHand == null || !_metaAimHand.added)
            {
                if (IsPinching) RegisterRelease();
                PinchStrength = 0f;
                return;
            }

            PinchStrength = _metaAimHand.pinchStrengthMiddle.ReadValue();

            if (!IsPinching && PinchStrength >= pinchOnsetThreshold)
                RegisterOnset();
            else if (IsPinching && PinchStrength < pinchReleaseThreshold)
                RegisterRelease();
        }

        private void RegisterOnset()
        {
            IsPinching = true;
            MidpointVelocityAtOnset = ComputeMidpointVelocity();

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Onset | strength={PinchStrength:F2} | " +
                          $"midpoint={PinchMidpointPosition} | vel={MidpointVelocityAtOnset.magnitude:F2}m/s");

            OnPinchStarted?.Invoke();
        }

        private void RegisterRelease()
        {
            IsPinching = false;

            if (enableDebugLogging)
                Debug.Log($"{LOG_TAG} Release | strength={PinchStrength:F2}");

            OnPinchReleased?.Invoke();
        }

        // Debug Visualization

        private void FindHandRenderer()
        {
            if (rightHandRenderer != null) { StoreOriginalColor(); return; }

            var rightHand = GameObject.Find("Right Hand Tracking");
            if (rightHand != null)
                rightHandRenderer = rightHand.GetComponentInChildren<SkinnedMeshRenderer>();

            if (rightHandRenderer == null && enableDebugLogging)
                Debug.LogWarning($"{LOG_TAG} Right hand renderer not found — assign manually.");

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
            if (!_originalColorStored || rightHandRenderer == null) return;
            if (rightHandRenderer.material == null) return;
            rightHandRenderer.material.color = _originalHandColor;
        }


        // Public Utilities


        public void SetOnsetThreshold(float threshold)
        {
            pinchOnsetThreshold = Mathf.Clamp01(threshold);
        }
    }
}