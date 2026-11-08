using System.Collections.Generic;
using UnityEngine;
using InteractionSystem;

[DisallowMultipleComponent]
public sealed class InteractableBaseOutline : MonoBehaviour
{
    public enum ActivationMode
    {
        /// <summary>Acompanha InteractableBase.AvailabilityChanged automaticamente.</summary>
        FollowAvailability,
        /// <summary>Só reage aos métodos públicos (Show, Hide, ShowSource...), ligados via UnityEvents.</summary>
        ManualOnly
    }

    private const string DefaultShaderName = "Custom/Interaction Outline";
    private const string ManualSource = "manual";
    private const string AvailabilitySource = "availability";

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [Header("Integration")]
    [SerializeField] private InteractableBase _interactable;

    [SerializeField] private ActivationMode _activationMode = ActivationMode.FollowAvailability;

    [Tooltip("Se marcado, o outline só aparece enquanto Interactable.CanInteract for true, " +
             "independente de qual evento o ativou.")]
    [SerializeField] private bool _requireCanInteract = true;

    [Header("Visual")]
    [Tooltip("Material com o shader Custom/Interaction Outline. Vazio = shader padrão.")]
    [SerializeField] private Material _outlineMaterial;

    [ColorUsage(true, true)]
    [SerializeField] private Color _outlineColor = new Color(1f, 0.82f, 0.15f, 1f);

    [Min(0f)] [SerializeField] private float _outlineWidth = 0.025f;
    [Min(0f)] [SerializeField] private float _fadeInDuration = 0.18f;
    [Min(0f)] [SerializeField] private float _fadeOutDuration = 0.22f;

    [Tooltip("Vazio = todos os MeshRenderer e SkinnedMeshRenderer dos filhos.")]
    [SerializeField] private Renderer[] _targetRenderers;

    private readonly List<RendererBinding> _bindings = new();
    private readonly HashSet<string> _activeSources = new();

    private Material _materialInstance;
    private bool _built;
    private float _currentIntensity;
    private float _targetIntensity;
    private float _fadeVelocity;
    private float _lastAppliedIntensity = -1f;

    // ---------------------------------------------------------------
    // API pública (plugável em UnityEvents do InteractableBase)
    // ---------------------------------------------------------------

    public bool IsVisible => _targetIntensity > 0f;
    public float CurrentIntensity => _currentIntensity;

    public void Show() => SetSource(ManualSource, true);
    public void Hide() => SetSource(ManualSource, false);
    public void Toggle() => SetSource(ManualSource, !_activeSources.Contains(ManualSource));
    public void SetVisible(bool visible) => SetSource(ManualSource, visible);

    /// <summary>Ativa uma fonte nomeada (ex.: "range"). O outline fica visível enquanto houver alguma fonte ativa.</summary>
    public void ShowSource(string source) => SetSource(source, true);

    /// <summary>Desativa uma fonte nomeada.</summary>
    public void HideSource(string source) => SetSource(source, false);

    /// <summary>Remove todas as fontes e esconde o outline com fade.</summary>
    public void ClearSources() => _activeSources.Clear();

    public void SetColor(Color color)
    {
        _outlineColor = color;
        _lastAppliedIntensity = -1f;
    }

    public void SetWidth(float width)
    {
        _outlineWidth = Mathf.Max(0f, width);
        ApplyMaterialProperties();
    }

    private void SetSource(string source, bool active)
    {
        if (string.IsNullOrEmpty(source)) return;

        if (active) _activeSources.Add(source);
        else _activeSources.Remove(source);
    }

    // ---------------------------------------------------------------
    // Ciclo de vida
    // ---------------------------------------------------------------

    private void Awake()
    {
        ResolveReferences();
        BuildOutline();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BuildOutline();

        if (_interactable == null) return;

        if (_activationMode == ActivationMode.FollowAvailability)
        {
            _interactable.AvailabilityChanged += HandleAvailabilityChanged;
            SetSource(AvailabilitySource, _interactable.CanInteract);
        }
    }

    private void OnDisable()
    {
        if (_interactable != null)
            _interactable.AvailabilityChanged -= HandleAvailabilityChanged;

        _activeSources.Clear();
        _targetIntensity = _currentIntensity = _fadeVelocity = 0f;
        ApplyIntensity(0f);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            Renderer outline = _bindings[i].OutlineRenderer;
            if (outline != null)
                Destroy(outline.gameObject);
        }

        _bindings.Clear();

        if (_materialInstance != null)
            Destroy(_materialInstance);
    }

    private void HandleAvailabilityChanged(bool available)
    {
        SetSource(AvailabilitySource, available);
    }

    private void LateUpdate()
    {
        if (!_built) return;

        _targetIntensity = ShouldShow() ? 1f : 0f;

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

        // Evita trabalho quando nada mudou (outline totalmente apagado/estável).
        if (Mathf.Approximately(_currentIntensity, _lastAppliedIntensity)
            && (_currentIntensity <= 0.001f || _currentIntensity >= 0.999f)
            && !AnySourceRendererStateChanged())
        {
            return;
        }

        ApplyIntensity(_currentIntensity);
    }

    // ---------------------------------------------------------------
    // Lógica de visibilidade
    // ---------------------------------------------------------------

    private bool ShouldShow()
    {
        if (_activeSources.Count == 0) return false;

        if (_interactable == null || !_interactable.isActiveAndEnabled)
            return false;

        if (_requireCanInteract && !_interactable.CanInteract)
            return false;

        return true;
    }

    private void ResolveReferences()
    {
        if (_interactable == null)
            _interactable = GetComponent<InteractableBase>()
                ?? GetComponentInParent<InteractableBase>();
    }

    // ---------------------------------------------------------------
    // Construção dos renderers de outline
    // ---------------------------------------------------------------

    private void BuildOutline()
    {
        if (_built) return;

        Shader shader = _outlineMaterial != null
            ? _outlineMaterial.shader
            : Shader.Find(DefaultShaderName);

        if (shader == null)
        {
            Debug.LogError(
                $"[{nameof(InteractableBaseOutline)}] Shader '{DefaultShaderName}' não encontrado em '{name}'.",
                this);
            return;
        }

        _materialInstance = _outlineMaterial != null
            ? new Material(_outlineMaterial)
            : new Material(shader);
        _materialInstance.name = $"{name}_Outline_Runtime";
        _materialInstance.hideFlags = HideFlags.HideAndDontSave;
        ApplyMaterialProperties();

        Renderer[] sources = _targetRenderers != null && _targetRenderers.Length > 0
            ? _targetRenderers
            : GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < sources.Length; i++)
        {
            Renderer source = sources[i];
            if (source == null) continue;

            // Ignora renderers de outline criados em runtime (por este ou outro script).
            if ((source.gameObject.hideFlags & HideFlags.HideAndDontSave) != 0) continue;

            Renderer outline = CreateOutlineRenderer(source);
            if (outline == null) continue;

            outline.enabled = false;
            _bindings.Add(new RendererBinding(source, outline));
        }

        _built = true;
    }

    private Renderer CreateOutlineRenderer(Renderer source)
    {
        if (source is MeshRenderer)
        {
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return null;

            GameObject go = CreateOutlineObject(source);
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

            MeshRenderer outline = go.AddComponent<MeshRenderer>();
            CopyRendererSettings(source, outline);
            outline.sharedMaterials = CreateMaterialArray(source.sharedMaterials);
            return outline;
        }

        if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
        {
            GameObject go = CreateOutlineObject(source);
            SkinnedMeshRenderer outline = go.AddComponent<SkinnedMeshRenderer>();
            outline.sharedMesh = skinned.sharedMesh;
            outline.bones = skinned.bones;
            outline.rootBone = skinned.rootBone;
            outline.localBounds = skinned.localBounds;
            outline.quality = skinned.quality;
            outline.updateWhenOffscreen = skinned.updateWhenOffscreen;
            CopyRendererSettings(source, outline);
            outline.sharedMaterials = CreateMaterialArray(source.sharedMaterials);
            return outline;
        }

        return null;
    }

    private static GameObject CreateOutlineObject(Renderer source)
    {
        GameObject go = new GameObject($"{source.name}_Outline_Runtime");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(source.transform, false);
        return go;
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
            materials[i] = _materialInstance;

        return materials;
    }

    // ---------------------------------------------------------------
    // Aplicação visual
    // ---------------------------------------------------------------

    private void ApplyMaterialProperties()
    {
        if (_materialInstance == null) return;

        if (_materialInstance.HasProperty(OutlineColorId))
            _materialInstance.SetColor(OutlineColorId, _outlineColor);

        if (_materialInstance.HasProperty(OutlineWidthId))
            _materialInstance.SetFloat(OutlineWidthId, _outlineWidth);
    }

    private void ApplyIntensity(float intensity)
    {
        _lastAppliedIntensity = intensity;

        if (_materialInstance == null) return;

        if (_materialInstance.HasProperty(OutlineColorId))
        {
            Color color = _outlineColor;
            color.a *= intensity;
            _materialInstance.SetColor(OutlineColorId, color);
        }

        bool shouldRender = intensity > 0.001f;
        for (int i = 0; i < _bindings.Count; i++)
        {
            RendererBinding binding = _bindings[i];
            if (binding.OutlineRenderer == null) continue;

            bool visible = shouldRender
                && binding.SourceRenderer != null
                && binding.SourceRenderer.enabled;

            if (binding.OutlineRenderer.enabled != visible)
                binding.OutlineRenderer.enabled = visible;
        }
    }

    /// <summary>
    /// Detecta quando o renderer de origem foi ligado/desligado por outro sistema
    /// enquanto o outline está totalmente visível, para manter os dois sincronizados.
    /// </summary>
    private bool AnySourceRendererStateChanged()
    {
        if (_currentIntensity <= 0.001f) return false;

        for (int i = 0; i < _bindings.Count; i++)
        {
            RendererBinding b = _bindings[i];
            if (b.OutlineRenderer == null) continue;

            bool expected = b.SourceRenderer != null && b.SourceRenderer.enabled;
            if (b.OutlineRenderer.enabled != expected) return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _outlineWidth = Mathf.Max(0f, _outlineWidth);
        _fadeInDuration = Mathf.Max(0f, _fadeInDuration);
        _fadeOutDuration = Mathf.Max(0f, _fadeOutDuration);

        if (Application.isPlaying)
        {
            ApplyMaterialProperties();
            _lastAppliedIntensity = -1f;
        }
    }
#endif

    private readonly struct RendererBinding
    {
        public Renderer SourceRenderer { get; }
        public Renderer OutlineRenderer { get; }

        public RendererBinding(Renderer source, Renderer outline)
        {
            SourceRenderer = source;
            OutlineRenderer = outline;
        }
    }
}