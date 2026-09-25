using System.Collections.Generic;
using Game.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Erumperem.Combat
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class EnemyCombatOutline : MonoBehaviour
    {
        public enum VisibilityMode
        {
            FollowMarkers,
            AlwaysVisible
        }

        [SerializeField] private VisibilityMode _visibilityMode = VisibilityMode.FollowMarkers;
        [SerializeField] private Material _outlineMaterial;
        [SerializeField, ColorUsage(true, true)] private Color _outlineColor = new Color(1f, 0.08f, 0.06f, 1f);
        [SerializeField, Min(0f)] private float _outlineWidth = 0.025f;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.15f;
        [SerializeField] private Renderer[] _targetRenderers;

        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
        private readonly List<RendererBinding> _bindings = new List<RendererBinding>();
        private CombatPrototypeController _combat;
        private CombatHoverFocusMarker _hoverMarker;
        private CombatEnemySelectionMarkerBinder _selectionMarkers;
        private CombatCapsuleTag _tag;
        private Material _materialInstance;
        private float _intensity;
        private float _nextResolveTime;

        private void LateUpdate()
        {
            if (_tag == null)
                _tag = GetComponentInParent<CombatCapsuleTag>();
            if (_tag == null)
            {
                _intensity = 0f;
                ApplyIntensity();
                return;
            }

            if (Time.unscaledTime >= _nextResolveTime &&
                (_combat == null || (_visibilityMode == VisibilityMode.FollowMarkers &&
                                    (_hoverMarker == null || _selectionMarkers == null))))
            {
                if (_combat == null)
                    _combat = FindFirstObjectByType<CombatPrototypeController>();
                if (_hoverMarker == null)
                    _hoverMarker = FindFirstObjectByType<CombatHoverFocusMarker>();
                if (_selectionMarkers == null)
                    _selectionMarkers = FindFirstObjectByType<CombatEnemySelectionMarkerBinder>();
                _nextResolveTime = Time.unscaledTime + 0.5f;
            }

            if (!TryGetLivingEnemy(out var combatantId))
            {
                _intensity = 0f;
                ApplyIntensity();
                return;
            }

            bool highlighted = _visibilityMode == VisibilityMode.AlwaysVisible
                || (_hoverMarker != null && _hoverMarker.IsHighlighting(combatantId))
                || (_selectionMarkers != null && _selectionMarkers.IsHighlighting(combatantId));
            if (highlighted && _materialInstance == null)
                BuildRenderers();

            _intensity = _fadeDuration <= 0f
                ? (highlighted ? 1f : 0f)
                : Mathf.MoveTowards(_intensity, highlighted ? 1f : 0f, Time.deltaTime / _fadeDuration);
            ApplyIntensity();
        }

        private bool TryGetLivingEnemy(out string combatantId)
        {
            combatantId = _tag != null ? _tag.combatantId : null;
            if (_combat == null || !_combat.isActiveAndEnabled || !_combat.IsBattleOngoing
                || string.IsNullOrEmpty(combatantId)) return false;

            var visualRoot = _combat.TryGetUnitVisualRoot(combatantId);
            if (visualRoot == null || !transform.IsChildOf(visualRoot)) return false;

            var enemy = _combat.FindCombatantById(combatantId);
            return enemy != null && enemy.Position.Side == Side.Enemies && !enemy.Health.IsDead;
        }

        private void BuildRenderers()
        {
            if (_outlineMaterial == null) return;

            _materialInstance = new Material(_outlineMaterial)
            {
                name = name + "_EnemyOutline_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };
            var sources = _targetRenderers != null && _targetRenderers.Length > 0
                ? _targetRenderers
                : GetComponentsInChildren<Renderer>(true);

            foreach (var source in sources)
            {
                if (source == null || source.GetComponentInParent<EnemyCombatOutline>() != this) continue;
                if (source.gameObject.layer == LayerMask.NameToLayer("Ignore Raycast")) continue;
                Mesh mesh = source is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : source is MeshRenderer ? source.GetComponent<MeshFilter>()?.sharedMesh : null;
                if (mesh == null) continue;

                var outlineObject = new GameObject(source.name + "_EnemyOutline_Runtime")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = source.gameObject.layer
                };
                outlineObject.transform.SetParent(source.transform, false);
                Renderer outline;
                if (source is SkinnedMeshRenderer sourceSkinned)
                {
                    var target = outlineObject.AddComponent<SkinnedMeshRenderer>();
                    target.sharedMesh = mesh;
                    target.bones = sourceSkinned.bones;
                    target.rootBone = sourceSkinned.rootBone;
                    target.localBounds = sourceSkinned.localBounds;
                    target.quality = sourceSkinned.quality;
                    target.updateWhenOffscreen = sourceSkinned.updateWhenOffscreen;
                    outline = target;
                }
                else
                {
                    outlineObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    outline = outlineObject.AddComponent<MeshRenderer>();
                }

                outline.shadowCastingMode = ShadowCastingMode.Off;
                outline.receiveShadows = false;
                outline.lightProbeUsage = LightProbeUsage.Off;
                outline.reflectionProbeUsage = ReflectionProbeUsage.Off;
                outline.renderingLayerMask = source.renderingLayerMask;
                var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < materials.Length; i++) materials[i] = _materialInstance;
                outline.sharedMaterials = materials;
                outline.enabled = false;
                _bindings.Add(new RendererBinding(source, outline));
            }
        }

        private void ApplyIntensity()
        {
            if (_materialInstance == null) return;
            var color = _outlineColor;
            color.a *= _intensity;
            _materialInstance.SetColor(ColorId, color);
            _materialInstance.SetFloat(WidthId, _outlineWidth);
            foreach (var binding in _bindings)
            {
                if (binding.Outline == null) continue;
                binding.Outline.enabled = _intensity > 0.001f && binding.Source != null
                    && binding.Source.enabled && !binding.Source.forceRenderingOff;
                if (!binding.Outline.enabled) continue;
                if (binding.Source is SkinnedMeshRenderer source && binding.Outline is SkinnedMeshRenderer target
                    && source.sharedMesh != null)
                {
                    for (int i = 0; i < source.sharedMesh.blendShapeCount; i++)
                        target.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
                }
            }
        }

        private void OnDisable()
        {
            _intensity = 0f;
            _nextResolveTime = 0f;
            ApplyIntensity();
        }

        private void OnDestroy()
        {
            foreach (var binding in _bindings)
                if (binding.Outline != null) Destroy(binding.Outline.gameObject);
            if (_materialInstance != null) Destroy(_materialInstance);
        }

        private readonly struct RendererBinding
        {
            public readonly Renderer Source;
            public readonly Renderer Outline;

            public RendererBinding(Renderer source, Renderer outline)
            {
                Source = source;
                Outline = outline;
            }
        }
    }
}
