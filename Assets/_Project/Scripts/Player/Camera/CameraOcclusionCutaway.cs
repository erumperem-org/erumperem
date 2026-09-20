using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class CameraOcclusionCutaway : MonoBehaviour
{
    [SerializeField] private LayerMask _obstacleLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private Vector3 _targetOffset = new Vector3(0, 1f, 0);
    [SerializeField, Min(0.1f)] private float _radius = 1.5f;
    [SerializeField, Min(0.01f)] private float _softness = 0.4f;
    [SerializeField, Min(0)] private float _smokeSpeed = 0.6f;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.25f;
    [SerializeField, Min(0.02f)] private float _scanInterval = 0.08f;
    [SerializeField, Min(0.01f)] private float _detectionRadius = 0.35f;
    [SerializeField, Min(0)] private float _targetClearance = 0.25f;

    private static readonly int ActiveId = Shader.PropertyToID("_CameraOcclusionActive");
    private static readonly int TargetId = Shader.PropertyToID("_CameraOcclusionTarget");
    private static readonly int SettingsId = Shader.PropertyToID("_CameraOcclusionSettings");
    private static readonly int AmountId = Shader.PropertyToID("_OcclusionAmount");
    private readonly Dictionary<Renderer, Obstacle> _obstacles = new Dictionary<Renderer, Obstacle>();
    private readonly HashSet<Renderer> _visibleObstacles = new HashSet<Renderer>();
    private readonly List<Renderer> _expired = new List<Renderer>();
    private PlayableCharactersManager _manager;
    private Shader _shader;
    private float _nextScan;

    private void Awake()
    {
        _manager = GetComponent<PlayableCharactersManager>();
        _shader = Resources.Load<Shader>("CameraOcclusion/CameraOcclusionLit");
        if (_shader == null || !_shader.isSupported)
        {
            Debug.LogError("Shader de recorte da câmera ausente ou incompatível com a plataforma.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
    }

    private void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        Shader.SetGlobalFloat(ActiveId, 0);
        if (camera != Camera.main || camera.cameraType != CameraType.Game) return;
        var target = _manager != null ? _manager.Main?.Transform : null;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            RestoreAll();
            return;
        }
        Vector3 focus = target.position + _targetOffset;
        if (Time.unscaledTime >= _nextScan)
        {
            Scan(camera, target, focus);
            _nextScan = Time.unscaledTime + _scanInterval;
        }
        _expired.Clear();
        foreach (var pair in _obstacles)
        {
            var obstacle = pair.Value;
            obstacle.Amount = Mathf.MoveTowards(obstacle.Amount,
                _visibleObstacles.Contains(pair.Key) ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(_fadeDuration, 0.01f));
            foreach (var material in obstacle.Replacements)
                if (material != null && material.shader == _shader)
                    material.SetFloat(AmountId, obstacle.Amount);
            if (pair.Key == null || obstacle.Amount <= 0f) _expired.Add(pair.Key);
        }
        foreach (var renderer in _expired)
        {
            Restore(renderer, _obstacles[renderer]);
            _obstacles.Remove(renderer);
        }
        Shader.SetGlobalVector(TargetId, focus);
        Shader.SetGlobalVector(SettingsId, new Vector4(_radius, _softness, _smokeSpeed, _targetClearance));
        Shader.SetGlobalFloat(ActiveId, 1);
    }

    private void Scan(Camera camera, Transform target, Vector3 focus)
    {
        _visibleObstacles.Clear();
        Vector3 delta = focus - camera.transform.position;
        float distance = delta.magnitude - _targetClearance;
        if (distance <= 0f) return;
        foreach (var hit in Physics.SphereCastAll(camera.transform.position, _detectionRadius,
                     delta.normalized, distance, _obstacleLayers, QueryTriggerInteraction.Ignore))
            Register(hit.collider, target);
        foreach (var collider in Physics.OverlapSphere(camera.transform.position, _detectionRadius,
                     _obstacleLayers, QueryTriggerInteraction.Ignore))
            Register(collider, target);
    }

    private void Register(Collider collider, Transform target)
    {
        if (collider.transform.IsChildOf(target)
            || collider.GetComponentInParent<PlayableCharacter>() != null
            || collider.GetComponentInParent<Interactable>() != null) return;
        var lod = collider.GetComponentInParent<LODGroup>();
        if (lod != null)
        {
            foreach (var level in lod.GetLODs())
                foreach (var renderer in level.renderers) Register(renderer);
            return;
        }
        var ownRenderer = collider.GetComponent<Renderer>();
        if (ownRenderer != null) Register(ownRenderer);
        else
        {
            var parentRenderer = collider.GetComponentInParent<Renderer>();
            if (parentRenderer != null) Register(parentRenderer);
            else foreach (var renderer in collider.GetComponentsInChildren<MeshRenderer>()) Register(renderer);
        }
    }

    private void Register(Renderer renderer)
    {
        if (!(renderer is MeshRenderer) || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return;
        _visibleObstacles.Add(renderer);
        if (_obstacles.ContainsKey(renderer)) return;
        Material[] originals = renderer.sharedMaterials;
        var replacements = (Material[])originals.Clone();
        bool changed = false;
        for (int i = 0; i < originals.Length; i++)
        {
            var source = originals[i];
            if (source == null || source.shader.name != "Universal Render Pipeline/Lit"
                || source.GetFloat("_Surface") > 0.5f) continue;
            replacements[i] = new Material(source)
            {
                shader = _shader,
                name = source.name + " (Camera Cutaway)",
                hideFlags = HideFlags.HideAndDontSave
            };
            replacements[i].SetFloat(AmountId, 0f);
            changed = true;
        }
        if (!changed) return;
        renderer.sharedMaterials = replacements;
        _obstacles.Add(renderer, new Obstacle(originals, replacements));
    }

    private static void EndCamera(ScriptableRenderContext context, Camera camera)
        => Shader.SetGlobalFloat(ActiveId, 0);

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        RestoreAll();
    }

    private void RestoreAll()
    {
        Shader.SetGlobalFloat(ActiveId, 0);
        foreach (var pair in _obstacles) Restore(pair.Key, pair.Value);
        _obstacles.Clear();
        _visibleObstacles.Clear();
        _nextScan = 0f;
    }

    private void Restore(Renderer renderer, Obstacle obstacle)
    {
        if (renderer != null)
        {
            var current = renderer.sharedMaterials;
            for (int i = 0; i < current.Length && i < obstacle.Replacements.Length; i++)
                if (current[i] == obstacle.Replacements[i]) current[i] = obstacle.Originals[i];
            renderer.sharedMaterials = current;
        }
        for (int i = 0; i < obstacle.Replacements.Length; i++)
            if (obstacle.Replacements[i] != obstacle.Originals[i]) Destroy(obstacle.Replacements[i]);
    }

    private sealed class Obstacle
    {
        public readonly Material[] Originals;
        public readonly Material[] Replacements;
        public float Amount;

        public Obstacle(Material[] originals, Material[] replacements)
        {
            Originals = originals;
            Replacements = replacements;
        }
    }
}
