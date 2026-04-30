using System;
using UnityEngine;
using TMPro;

namespace AerialNav.Gauntlet
{
    /// <summary>
    /// Single gauntlet gate. Owns its torus mesh, billboard label, and plane-crossing detection.
    ///
    /// Detection: each frame, projects the camera position onto the ring's plane (normal = transform.up).
    /// A passage is registered when:
    ///   (a) the camera is within the ring's world-space radius, and
    ///   (b) the dot product of (camera - center) with transform.up changes sign.
    /// Direction-agnostic.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class GauntletRing : MonoBehaviour
    {
        public enum RingState { Passive, Ready, Missed, Passed }

        private static readonly Color ColorPassive    = new Color(1.00f, 0.71f, 0.76f, 0.90f);
        private static readonly Color ColorReady      = new Color(1.00f, 0.71f, 0.76f, 1.00f);
        private static readonly Color ColorFlashRed   = new Color(1.00f, 0.20f, 0.20f, 1.00f);
        private static readonly Color ColorFlashPink  = new Color(1.00f, 0.71f, 0.76f, 1.00f);
        private static readonly Color ColorPassed     = new Color(0.25f, 0.85f, 0.35f, 1.00f);
        private static readonly Color LabelGoldBright = new Color(1.00f, 0.84f, 0.00f, 0.75f);
        private static readonly Color LabelGoldDim    = new Color(1.00f, 0.84f, 0.00f, 0.30f);
        private static readonly int   ShaderColor     = Shader.PropertyToID("_Color");

        [Header("Geometry")]
        [SerializeField] private float ringDiameter = 10f;
        [SerializeField] private float tubeRadius   = 0.35f;

        [Header("Label")]
        [SerializeField] private float labelFontSize      = 6f;
        [SerializeField] private float labelForwardOffset = 0.5f;

        public event Action<GauntletRing> OnRingPassed;
        public RingState State { get; private set; } = RingState.Passive;
        public int       Index { get; private set; }

        private MeshRenderer          _renderer;
        private MaterialPropertyBlock _mpb;
        private Transform             _billboard;
        private TextMeshPro           _label;
        private Camera                _camera;

        private float _previousDot    = 0f;
        private bool  _dotInitialized = false;

        private bool  _flashing;
        private float _flashTimer;
        private bool  _flashPhase;
        private const float FlashInterval = 0.25f;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _mpb      = new MaterialPropertyBlock();

            GetComponent<MeshFilter>().mesh = RingMeshBuilder.Build(
                majorRadius:   ringDiameter * 0.5f,
                minorRadius:   tubeRadius,
                majorSegments: 48,
                minorSegments: 16);
        }

        private void Start()
        {
            _camera = Camera.main;
        }

        private void Update()
        {
            if (_flashing)  TickFlash();
            if (_billboard) BillboardToCamera();
            CheckPlaneCrossing();
        }

        public void Initialize(int index)
        {
            Index = index;
            SpawnBillboardLabel();
            SetState(RingState.Passive);
        }

        private void CheckPlaneCrossing()
        {
            if (_camera == null) return;

            Vector3 toCamera    = _camera.transform.position - transform.position;
            Vector3 projected   = Vector3.ProjectOnPlane(toCamera, transform.up);
            float   worldRadius = ringDiameter * 0.5f * transform.lossyScale.x;

            if (projected.magnitude > worldRadius)
            {
                _dotInitialized = false;
                return;
            }

            float currentDot = Vector3.Dot(toCamera, transform.up);

            if (!_dotInitialized)
            {
                _previousDot    = currentDot;
                _dotInitialized = true;
                return;
            }

            bool crossed = (_previousDot >= 0f && currentDot < 0f)
                        || (_previousDot <  0f && currentDot >= 0f);

            if (crossed)
            {
                Debug.Log($"[GauntletRing] Gate {Index + 1} passed.");
                OnRingPassed?.Invoke(this);
            }

            _previousDot = currentDot;
        }

        public void SetState(RingState next)
        {
            State       = next;
            _flashing   = next == RingState.Missed;
            _flashTimer = 0f;
            _flashPhase = true;

            switch (next)
            {
                case RingState.Passive: SetRingColor(ColorPassive); SetLabelColor(LabelGoldDim);    break;
                case RingState.Ready:   SetRingColor(ColorReady);   SetLabelColor(LabelGoldBright); break;
                case RingState.Missed:  SetRingColor(ColorFlashRed);SetLabelColor(LabelGoldBright); break;
                case RingState.Passed:  SetRingColor(ColorPassed);  SetLabelColor(LabelGoldDim);    break;
            }
        }

        private void TickFlash()
        {
            _flashTimer += Time.deltaTime;
            if (_flashTimer < FlashInterval) return;
            _flashTimer = 0f;
            _flashPhase = !_flashPhase;
            SetRingColor(_flashPhase ? ColorFlashRed : ColorFlashPink);
        }

        private void SpawnBillboardLabel()
        {
            var go = new GameObject($"Label_Gate{Index + 1}");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, 0f, labelForwardOffset);
            _billboard          = go.transform;
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
            if (_camera != null) _billboard.rotation = _camera.transform.rotation;
        }

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
            float worldRadius = ringDiameter * 0.5f * transform.lossyScale.x;
            Gizmos.color = new Color(1f, 0.4f, 0.6f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, worldRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position,  transform.up * worldRadius);
            Gizmos.DrawRay(transform.position, -transform.up * worldRadius);
        }
#endif
    }
}