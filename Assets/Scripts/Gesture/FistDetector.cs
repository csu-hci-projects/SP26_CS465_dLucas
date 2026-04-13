using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Detects closed fist gesture by measuring fingertip proximity to wrist.
/// Attach to a GameObject under XR Origin.
/// </summary>
public class FistDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Distance threshold for considering a finger curled. Lower = tighter fist required.")]
    [SerializeField] private float curlThreshold = 0.05f;
    
    [Tooltip("Require all four fingers curled, or allow some tolerance?")]
    [SerializeField] private int minimumCurledFingers = 4;

    [Header("Debug Visualization")]
    [Tooltip("Enable to tint hand mesh when fist is detected.")]
    [SerializeField] private bool enableDebugVisualization = true;
    
    [Tooltip("Color to apply when fist is detected.")]
    [SerializeField] private Color fistDetectedColor = Color.green;
    
    [Tooltip("Reference to right hand mesh renderer (auto-found if null).")]
    [SerializeField] private SkinnedMeshRenderer rightHandRenderer;
    
    [Tooltip("Reference to left hand mesh renderer (auto-found if null).")]
    [SerializeField] private SkinnedMeshRenderer leftHandRenderer;

    // Subsystem reference
    private XRHandSubsystem _handSubsystem;
    private bool _subsystemAvailable;

    // Original colors for restoration
    private Color _originalRightHandColor;
    private Color _originalLeftHandColor;
    private bool _colorsStored;

    // Joint IDs for fingertips (excluding thumb for fist detection)
    private static readonly XRHandJointID[] FingertipJoints = 
    {
        XRHandJointID.IndexTip,
        XRHandJointID.MiddleTip,
        XRHandJointID.RingTip,
        XRHandJointID.LittleTip
    };

    // Public state for external access
    public bool IsRightFist { get; private set; }
    public bool IsLeftFist { get; private set; }

    private void Start()
    {
        TryGetHandSubsystem();
        FindHandRenderers();
    }

    private void Update()
    {
        if (!_subsystemAvailable)
        {
            TryGetHandSubsystem();
            return;
        }

        // Detect fists
        IsRightFist = DetectFist(_handSubsystem.rightHand);
        IsLeftFist = DetectFist(_handSubsystem.leftHand);

        // Debug visualization
        if (enableDebugVisualization)
        {
            UpdateDebugVisualization();
        }
    }

    private void TryGetHandSubsystem()
    {
        var handSubsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(handSubsystems);
        
        if (handSubsystems.Count > 0)
        {
            _handSubsystem = handSubsystems[0];
            _subsystemAvailable = _handSubsystem != null && _handSubsystem.running;
            
            if (_subsystemAvailable)
            {
                Debug.Log("[FistDetector] Hand subsystem acquired successfully.");
            }
        }
    }

    private void FindHandRenderers()
    {
        if (rightHandRenderer == null)
        {
            var rightHand = GameObject.Find("RightHand");
            if (rightHand != null)
            {
                rightHandRenderer = rightHand.GetComponent<SkinnedMeshRenderer>();
            }
        }

        if (leftHandRenderer == null)
        {
            var leftHand = GameObject.Find("LeftHand");
            if (leftHand != null)
            {
                leftHandRenderer = leftHand.GetComponent<SkinnedMeshRenderer>();
            }
        }

        StoreOriginalColors();
    }

    private void StoreOriginalColors()
    {
        if (_colorsStored) return;

        if (rightHandRenderer != null && rightHandRenderer.material != null)
        {
            _originalRightHandColor = rightHandRenderer.material.color;
        }

        if (leftHandRenderer != null && leftHandRenderer.material != null)
        {
            _originalLeftHandColor = leftHandRenderer.material.color;
        }

        _colorsStored = true;
    }

    /// <summary>
    /// Detects if the given hand is in a fist pose.
    /// </summary>
    private bool DetectFist(XRHand hand)
    {
        if (!hand.isTracked)
        {
            return false;
        }

        // Get wrist pose as reference point
        XRHandJoint wristJoint = hand.GetJoint(XRHandJointID.Wrist);
        if (!wristJoint.TryGetPose(out Pose wristPose))
        {
            return false;
        }

        int curledFingers = 0;

        foreach (XRHandJointID tipId in FingertipJoints)
        {
            XRHandJoint tipJoint = hand.GetJoint(tipId);
            
            if (!tipJoint.TryGetPose(out Pose tipPose))
            {
                continue;
            }

            // Transform tip position into wrist-local space
            Vector3 localTipPos = Quaternion.Inverse(wristPose.rotation) * (tipPose.position - wristPose.position);
            
            // For a curled finger, the tip is close to the wrist on the forward axis
            // We check the magnitude of the X component (lateral distance from wrist center)
            // Curled fingers have small X values; extended fingers have large X values
            float extension = Mathf.Abs(localTipPos.x);

            if (extension < curlThreshold)
            {
                curledFingers++;
            }
        }

        return curledFingers >= minimumCurledFingers;
    }

    private void UpdateDebugVisualization()
    {
        if (rightHandRenderer != null && rightHandRenderer.material != null)
        {
            rightHandRenderer.material.color = IsRightFist ? fistDetectedColor : _originalRightHandColor;
        }

        if (leftHandRenderer != null && leftHandRenderer.material != null)
        {
            leftHandRenderer.material.color = IsLeftFist ? fistDetectedColor : _originalLeftHandColor;
        }
    }

    private void OnDisable()
    {
        // Restore original colors when disabled
        RestoreOriginalColors();
    }

    private void OnDestroy()
    {
        RestoreOriginalColors();
    }

    private void RestoreOriginalColors()
    {
        if (!_colorsStored) return;

        if (rightHandRenderer != null && rightHandRenderer.material != null)
        {
            rightHandRenderer.material.color = _originalRightHandColor;
        }

        if (leftHandRenderer != null && leftHandRenderer.material != null)
        {
            leftHandRenderer.material.color = _originalLeftHandColor;
        }
    }

    /// <summary>
    /// Public method to check fist state for either hand.
    /// </summary>
    public bool IsFist(Handedness handedness)
    {
        return handedness == Handedness.Right ? IsRightFist : IsLeftFist;
    }

    /// <summary>
    /// Allows runtime adjustment of curl threshold for tuning.
    /// </summary>
    public void SetCurlThreshold(float threshold)
    {
        curlThreshold = threshold;
        Debug.Log($"[FistDetector] Curl threshold set to {threshold}");
    }
}