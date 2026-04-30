using System;
using UnityEngine;
using TMPro;

namespace AerialNav.Gauntlet
{
    /// <summary>
    /// Single gauntlet gate. Owns its torus mesh, sphere trigger, and billboard label.
    ///
    /// States:
    ///   Passive — no race; light pink dim
    ///   Ready   — race active; full pink, bright gold number
    ///   Missed  — flashes red/pink; player flew out of order
    ///   Passed  — flew through correctly; solid green
    ///
    /// GauntletPath is the sole authority on state transitions.
    /// This class fires OnTriggerEntered and lets the path decide what it means.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(SphereCollider))]
    public class GauntletRing : MonoBehaviour
    {
        public enum RingState { Passive, Ready, Missed, Passed }

        // ── Static Colors ─────────────────────────────────────────────────────
        private static readonly Color ColorPassive = new Color(1.00f, 0.71f, 0.76f, 0.90f);
        private static readonly Color ColorReady   = new Color(1.00f, 0.71f, 0.76f, 1.00f);
        private static readonly Color ColorFlashRed  = new Color(1.00f, 0.20f, 0.20f, 1.00f);
        private static readonly Color ColorFlashPink = new Color(1.00f, 0.71f, 0.76f, 1.00f);
        private static readonly Color ColorPassed  = new Color(0.25f, 0.85f, 0.35f, 1.00f);
        private static Camera _cachedCamera;
        private static readonly Color LabelGoldBright = new Color(1.00f, 0.84f, 0.00f, 0.75f);
        private static readonly Color LabelGoldDim    = new Color(1.00f, 0.84f, 0.00f, 0.30f);

        private static readonly int ShaderColor = Shader.PropertyToID("_Color");

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Geometry")]
        [SerializeField] private float ringDiameter = 10f;
        [SerializeField] private float tubeRadius   = 0.35f;
        [SerializeField][Range(0.3f, 0.7f)] private float triggerRadiusMultiplier = 0.52f;

        [Header("Label")]
        [SerializeField] private float labelFontSize      = 6f;
        [SerializeField] private float labelForwardOffset = 0.5f;

        // ── Public ────────────────────────────────────────────────────────────
        public event Action<GauntletRing> OnTriggerEntered;
        public RingState State { get; private set; } = RingState.Passive;
        public int       Index { get; private set; }

        // ── Private ───────────────────────────────────────────────────────────
        private MeshRenderer          _renderer;
        private MaterialPropertyBlock _mpb;
        private Transform             _billboard;
        private TextMeshPro           _label;

        private bool  _flashing;
        private float _flashTimer;
        private bool  _flashPhase;
        private const float FlashInterval = 0.25f;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _mpb      = new MaterialPropertyBlock();

            var trigger        = GetComponent<SphereCollider>();
            trigger.isTrigger  = true;
            trigger.radius     = ringDiameter * 0.5f * triggerRadiusMultiplier;

            GetComponent<MeshFilter>().mesh = RingMeshBuilder.Build(
                majorRadius:   ringDiameter * 0.5f,
                minorRadius:   tubeRadius,
                majorSegments: 48,
                minorSegments: 16);
        }

        private void Update()
        {
            if (_flashing)    TickFlash();
            if (_billboard)   BillboardToCamera();
        }

        // ── Initialization ────────────────────────────────────────────────────
        public void Initialize(int index)
        {
            Index = index;
            SpawnBillboardLabel();
            SetState(RingState.Passive);
        }

        // ── State ─────────────────────────────────────────────────────────────
        public void SetState(RingState next)
        {
            State     = next;
            _flashing = next == RingState.Missed;
            _flashTimer = 0f;
            _flashPhase = true;

            switch (next)
            {
                case RingState.Passive:
                    SetRingColor(ColorPassive);
                    SetLabelColor(LabelGoldDim);
                    break;
                case RingState.Ready:
                    SetRingColor(ColorReady);
                    SetLabelColor(LabelGoldBright);
                    break;
                case RingState.Missed:
                    SetRingColor(ColorFlashRed);
                    SetLabelColor(LabelGoldBright);
                    break;
                case RingState.Passed:
                    SetRingColor(ColorPassed);
                    SetLabelColor(LabelGoldDim);
                    break;
            }
        }

        // ── Flash Loop ────────────────────────────────────────────────────────
        private void TickFlash()
        {
            _flashTimer += Time.deltaTime;
            if (_flashTimer < FlashInterval) return;
            _flashTimer = 0f;
            _flashPhase = !_flashPhase;
            SetRingColor(_flashPhase ? ColorFlashRed : ColorFlashPink);
        }

        // ── Trigger ───────────────────────────────────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[GauntletRing] OnTriggerEnter fired by: {other.gameObject.name} tag: {other.tag}");
            if (!IsPlayerCollider(other)) return;
            OnTriggerEntered?.Invoke(this);
        }

        private static bool IsPlayerCollider(Collider col)
        {
            // Accept any collider that is part of the XR rig.
            // Checks tags first (cheap), then walks up the hierarchy (more expensive).
            if (col.CompareTag("Player") || col.CompareTag("MainCamera"))
                return true;

            // Accept any collider whose GameObject or any ancestor has XROrigin
            if (col.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null)
                return true;

            // Fallback: accept the Main Camera's collider by name
            if (col.gameObject.name == "Main Camera")
                return true;

            if (col.gameObject.name == "Camera Offset")
                return true;

            if (col.gameObject.name == "PlayerTriggerCollider")
                return true;    

            return false;
        }

        // ── Billboard Label ───────────────────────────────────────────────────
        private void SpawnBillboardLabel()
        {
            var go = new GameObject($"Label_Gate{Index + 1}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, 0f, labelForwardOffset);

            _billboard = go.transform;

            _label              = go.AddComponent<TextMeshPro>();
            _label.text         = (Index + 1).ToString();
            _label.fontSize     = labelFontSize;
            _label.alignment    = TextAlignmentOptions.Center;
            _label.fontStyle    = FontStyles.Bold;
            _label.color        = LabelGoldDim;
            _label.sortingOrder = 1;
        }

        private void BillboardToCamera()
{
    if (_cachedCamera == null)
        _cachedCamera = Camera.main ?? FindFirstObjectByType<Camera>();
    
    if (_cachedCamera != null)
        _billboard.rotation = _cachedCamera.transform.rotation;
}

        // ── Color Helpers ─────────────────────────────────────────────────────
        private void SetRingColor(Color c)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ShaderColor, c);
            _renderer.SetPropertyBlock(_mpb);
        }

        private void SetLabelColor(Color c)
        {
            if (_label != null) _label.color = c;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.6f, 0.25f);
            Gizmos.DrawWireSphere(transform.position,
                ringDiameter * 0.5f * triggerRadiusMultiplier);
        }
#endif
    }
}