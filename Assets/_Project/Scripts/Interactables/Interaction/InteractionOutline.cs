using System;
using System.Collections.Generic;
using DetectionSystem.Core;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InteractionOutline : MonoBehaviour
{
    public enum VisibilityMode
    {
        Automatic,
        AlwaysWhileInRange,
        HideAfterFirstInteraction
    }

    private const string DefaultShaderName = "Custom/Interaction Outline";
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [Header("Ligação")]
    [SerializeField] private Interactable _interactable;
    [SerializeField] private DetectionReceiver _detectionReceiver;
    [SerializeField] private VisibilityMode _visibilityMode = VisibilityMode.Automatic;

    [Tooltip("Quando marcado, somente detectores cujo GameObject possui a tag Player acionam o outline.")]
    [SerializeField] private bool _onlyPlayerDetectors = true;

    [Tooltip("Deixe vazio para aceitar qualquer shape do detector do player. Use os labels da detecção para restringir o range.")]
    [SerializeField] private List<string> _acceptedShapeLabels = new List<string>
    {
        "InteractableDetectionArea",
        "CharactersDetectionArea"
    };

    [Header("Visual")]
    [Tooltip("Material com o shader Custom/Interaction Outline. Se vazio, o shader padrão é criado automaticamente.")]
    [SerializeField] private Material _outlineMaterial;
    [ColorUsage(true, true)]
    [SerializeField] private Color _outlineColor = new Color(1f, 0.82f, 0.15f, 1f);
    [Min(0f)]
    [SerializeField] private float _outlineWidth = 0.025f;
    [Min(0f)]
    [SerializeField] private float _fadeInDuration = 0.18f;
    [Min(0f)]
    [SerializeField] private float _fadeOutDuration = 0.22f;

    [Tooltip("Renderers específicos. Vazio = todos os MeshRenderer e SkinnedMeshRenderer dos filhos.")]
    [SerializeField] private Renderer[] _targetRenderers;

    private readonly List<RendererBinding> _rendererBindings = new List<RendererBinding>();
    private readonly Dictionary<DetectionContact, int> _detectionContacts = new Dictionary<DetectionContact, int>();

    private DetectionReceiver _boundReceiver;
    private Material _outlineMaterialInstance;
    private float _currentIntensity;
    private float _targetIntensity;
    private float _fadeVelocity;
    private bool _started;
    private bool _pointerHovered;

    public void SetPointerHovered(bool hovered) => _pointerHovered = hovered;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        _started = true;
        ResolveReferences();
        BuildOutlineRenderers();
        BindDetectionReceiver();
        RefreshTargetIntensity();
    }

    private void OnEnable()
    {
        if (!_started) return;
        ResolveReferences();
        BindDetectionReceiver();
    }

    private void OnDisable()
    {
        UnbindDetectionReceiver();
        _detectionContacts.Clear();
        _pointerHovered = false;
        _targetIntensity = 0f;
        _currentIntensity = 0f;
        _fadeVelocity = 0f;
        ApplyIntensity(0f);
    }

    private void LateUpdate()
    {
        if (!_started) return;

        if (_interactable != null && !_interactable.CanShowInteractionFeedback)
        {
            _targetIntensity = _currentIntensity = _fadeVelocity = 0f;
            ApplyIntensity(0f);
            return;
        }

        RefreshTargetIntensity();

        float duration = _targetIntensity > _currentIntensity
            ? _fadeInDuration
            : _fadeOutDuration;

        if (duration <= 0f)
        {
            _currentIntensity = _targetIntensity;
            _fadeVelocity = 0f;
        }
        else
        {
            _currentIntensity = Mathf.SmoothDamp(
                _currentIntensity,
                _targetIntensity,
                ref _fadeVelocity,
                duration,
                Mathf.Infinity,
                Time.deltaTime);

            if (Mathf.Abs(_currentIntensity - _targetIntensity) < 0.001f)
            {
                _currentIntensity = _targetIntensity;
                _fadeVelocity = 0f;
            }
        }

        ApplyIntensity(_currentIntensity);
    }

    private void OnDestroy()
    {
        UnbindDetectionReceiver();

        for (int i = 0; i < _rendererBindings.Count; i++)
        {
            if (_rendererBindings[i].OutlineRenderer != null)
            {
                GameObject outlineObject = _rendererBindings[i].OutlineRenderer.gameObject;
                if (outlineObject != gameObject)
                    Destroy(outlineObject);
                else
                    Destroy(_rendererBindings[i].OutlineRenderer);
            }
        }

        if (_outlineMaterialInstance != null)
            Destroy(_outlineMaterialInstance);
    }

    private void BindDetectionReceiver()
    {
        DetectionReceiver receiver = ResolveDetectionReceiver();
        if (receiver == _boundReceiver) return;

        UnbindDetectionReceiver();
        _boundReceiver = receiver;

        if (_boundReceiver == null) return;

        _boundReceiver.OnEnter += HandleDetectionEnter;
        _boundReceiver.OnExit += HandleDetectionExit;
    }

    private void UnbindDetectionReceiver()
    {
        if (_boundReceiver == null) return;

        _boundReceiver.OnEnter -= HandleDetectionEnter;
        _boundReceiver.OnExit -= HandleDetectionExit;
        _boundReceiver = null;
    }

    public void RegisterPlayerProximity(Detector detector, string shapeLabel, int shapeIndex)
    {
        if (!AcceptsDetection(detector, shapeLabel)) return;

        DetectionContact contact = new DetectionContact(detector, shapeLabel, shapeIndex);
        _detectionContacts.TryGetValue(contact, out int count);
        _detectionContacts[contact] = count + 1;
    }

    public void UnregisterPlayerProximity(Detector detector, string shapeLabel, int shapeIndex)
    {
        if (!AcceptsDetection(detector, shapeLabel)) return;

        DetectionContact contact = new DetectionContact(detector, shapeLabel, shapeIndex);
        if (!_detectionContacts.TryGetValue(contact, out int count)) return;

        if (count <= 1)
            _detectionContacts.Remove(contact);
        else
            _detectionContacts[contact] = count - 1;
    }

    private void HandleDetectionEnter(Detector detector, string shapeLabel, int shapeIndex)
    {
        RegisterPlayerProximity(detector, shapeLabel, shapeIndex);
    }

    private void HandleDetectionExit(Detector detector, string shapeLabel, int shapeIndex)
    {
        UnregisterPlayerProximity(detector, shapeLabel, shapeIndex);
    }

    private bool AcceptsDetection(Detector detector, string shapeLabel)
    {
        if (_onlyPlayerDetectors
            && (detector == null || !detector.gameObject.CompareTag("Player")))
        {
            return false;
        }

        if (_acceptedShapeLabels == null || _acceptedShapeLabels.Count == 0)
            return true;

        for (int i = 0; i < _acceptedShapeLabels.Count; i++)
        {
            if (string.Equals(_acceptedShapeLabels[i], shapeLabel, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void ResolveReferences()
    {
        Interactable localInteractable = GetComponent<Interactable>()
            ?? GetComponentInParent<Interactable>();
        if (localInteractable != null)
        {
            _interactable = localInteractable;
            _detectionReceiver = localInteractable.GetComponent<DetectionReceiver>();
            return;
        }

        if (_detectionReceiver == null)
            _detectionReceiver = _interactable != null
                ? _interactable.Receiver
                : GetComponent<DetectionReceiver>() ?? GetComponentInParent<DetectionReceiver>();
    }

    private DetectionReceiver ResolveDetectionReceiver()
    {
        ResolveReferences();

        return _detectionReceiver != null
            ? _detectionReceiver
            : _interactable != null
                ? _interactable.Receiver
                : null;
    }

    private void BuildOutlineRenderers()
    {
        if (_rendererBindings.Count > 0) return;

        Shader shader = _outlineMaterial != null
            ? _outlineMaterial.shader
            : Shader.Find(DefaultShaderName);

        if (shader == null)
        {
            Debug.LogError(
                $"[{nameof(InteractionOutline)}] Shader '{DefaultShaderName}' não encontrado em '{name}'.",
                this);
            return;
        }

        _outlineMaterialInstance = _outlineMaterial != null
            ? new Material(_outlineMaterial)
            : new Material(shader);
        _outlineMaterialInstance.name = $"{name}_InteractionOutline_Runtime";
        _outlineMaterialInstance.hideFlags = HideFlags.HideAndDontSave;
        ApplyMaterialProperties();

        Renderer[] sources = GetSourceRenderers();
        for (int i = 0; i < sources.Length; i++)
        {
            Renderer source = sources[i];
            if (source == null) continue;

            Renderer outline = CreateOutlineRenderer(source);
            if (outline == null) continue;

            _rendererBindings.Add(new RendererBinding(source, outline));
            outline.enabled = false;
        }
    }

    private Renderer[] GetSourceRenderers()
    {
        if (_targetRenderers != null && _targetRenderers.Length > 0)
        {
            return _targetRenderers;
        }

        return GetComponentsInChildren<Renderer>(true);
    }

    private Renderer CreateOutlineRenderer(Renderer source)
    {
        if (source is MeshRenderer)
        {
            MeshFilter meshFilter = source.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) return null;

            GameObject outlineObject = CreateOutlineObject(source);
            MeshFilter outlineMeshFilter = outlineObject.AddComponent<MeshFilter>();
            outlineMeshFilter.sharedMesh = meshFilter.sharedMesh;

            MeshRenderer outline = outlineObject.AddComponent<MeshRenderer>();
            CopyRendererSettings(source, outline);
            outline.sharedMaterials = CreateMaterialArray(source.sharedMaterials);
            return outline;
        }

        if (source is SkinnedMeshRenderer sourceSkinned
            && sourceSkinned.sharedMesh != null)
        {
            GameObject outlineObject = CreateOutlineObject(source);
            SkinnedMeshRenderer outline = outlineObject.AddComponent<SkinnedMeshRenderer>();
            outline.sharedMesh = sourceSkinned.sharedMesh;
            outline.bones = sourceSkinned.bones;
            outline.rootBone = sourceSkinned.rootBone;
            outline.localBounds = sourceSkinned.localBounds;
            outline.quality = sourceSkinned.quality;
            outline.updateWhenOffscreen = sourceSkinned.updateWhenOffscreen;
            CopyRendererSettings(source, outline);
            outline.sharedMaterials = CreateMaterialArray(source.sharedMaterials);
            return outline;
        }

        return null;
    }

    private static GameObject CreateOutlineObject(Renderer source)
    {
        GameObject outlineObject = new GameObject($"{source.name}_InteractionOutline_Runtime");
        outlineObject.hideFlags = HideFlags.HideAndDontSave;
        outlineObject.transform.SetParent(source.transform, false);
        return outlineObject;
    }

    private static void CopyRendererSettings(Renderer source, Renderer destination)
    {
        destination.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        destination.receiveShadows = false;
        destination.lightProbeUsage = source.lightProbeUsage;
        destination.reflectionProbeUsage = source.reflectionProbeUsage;
        destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
        destination.renderingLayerMask = source.renderingLayerMask;
    }

    private Material[] CreateMaterialArray(Material[] sourceMaterials)
    {
        int count = sourceMaterials == null || sourceMaterials.Length == 0
            ? 1
            : sourceMaterials.Length;

        Material[] materials = new Material[count];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = _outlineMaterialInstance;

        return materials;
    }

    private void ApplyMaterialProperties()
    {
        if (_outlineMaterialInstance == null) return;

        if (_outlineMaterialInstance.HasProperty(OutlineColorId))
            _outlineMaterialInstance.SetColor(OutlineColorId, _outlineColor);

        if (_outlineMaterialInstance.HasProperty(OutlineWidthId))
            _outlineMaterialInstance.SetFloat(OutlineWidthId, _outlineWidth);
    }

    private void RefreshTargetIntensity()
    {
        bool isInRange = _pointerHovered || _detectionContacts.Count > 0;
        bool shouldShow = isInRange && ShouldShowOutline();
        _targetIntensity = shouldShow ? 1f : 0f;
    }

    private bool ShouldShowOutline()
    {
        if (_interactable == null || !_interactable.isActiveAndEnabled)
            return false;

        if (!_interactable.CanShowInteractionFeedback) return false;

        if (gameObject.CompareTag("Player") || _interactable.gameObject.CompareTag("Player"))
            return false;

        switch (_visibilityMode)
        {
            case VisibilityMode.AlwaysWhileInRange:
                return true;

            case VisibilityMode.HideAfterFirstInteraction:
            case VisibilityMode.Automatic:
            default:
                return _interactable.CanInteract;
        }
    }

    private void ApplyIntensity(float intensity)
    {
        if (_outlineMaterialInstance == null) return;

        if (_outlineMaterialInstance.HasProperty(OutlineColorId))
        {
            Color color = _outlineColor;
            color.a *= intensity;
            _outlineMaterialInstance.SetColor(OutlineColorId, color);
        }

        bool shouldRender = intensity > 0.001f;
        for (int i = 0; i < _rendererBindings.Count; i++)
        {
            RendererBinding binding = _rendererBindings[i];
            if (binding.OutlineRenderer == null) continue;

            binding.OutlineRenderer.enabled = shouldRender
                && binding.SourceRenderer != null
                && binding.SourceRenderer.enabled;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
        _outlineWidth = Mathf.Max(0f, _outlineWidth);
        _fadeInDuration = Mathf.Max(0f, _fadeInDuration);
        _fadeOutDuration = Mathf.Max(0f, _fadeOutDuration);

        if (Application.isPlaying)
            ApplyMaterialProperties();
    }
#endif

    private readonly struct RendererBinding
    {
        public Renderer SourceRenderer { get; }
        public Renderer OutlineRenderer { get; }

        public RendererBinding(Renderer sourceRenderer, Renderer outlineRenderer)
        {
            SourceRenderer = sourceRenderer;
            OutlineRenderer = outlineRenderer;
        }
    }

    private readonly struct DetectionContact : IEquatable<DetectionContact>
    {
        private readonly Detector _detector;
        private readonly string _shapeLabel;
        private readonly int _shapeIndex;

        public DetectionContact(Detector detector, string shapeLabel, int shapeIndex)
        {
            _detector = detector;
            _shapeLabel = shapeLabel;
            _shapeIndex = shapeIndex;
        }

        public bool Equals(DetectionContact other) =>
            ReferenceEquals(_detector, other._detector)
            && _shapeIndex == other._shapeIndex
            && string.Equals(_shapeLabel, other._shapeLabel, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is DetectionContact other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _detector != null ? _detector.GetInstanceID() : 0;
                hash = (hash * 397) ^ _shapeIndex;
                hash = (hash * 397) ^ (_shapeLabel != null ? StringComparer.Ordinal.GetHashCode(_shapeLabel) : 0);
                return hash;
            }
        }
    }
}
