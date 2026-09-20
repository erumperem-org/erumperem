using UnityEngine;
using UnityEngine.Rendering;

namespace Player
{
    [DisallowMultipleComponent]
    public sealed class ExplorationClickMarker : MonoBehaviour
    {
        [SerializeField, ColorUsage(true, true)] private Color _color = new Color(2.5f, 0f, 0f, 1f);
        [SerializeField, Min(0.1f)] private float _duration = 0.65f;
        [SerializeField, Min(0.05f)] private float _radius = 0.55f;
        [SerializeField, Min(0.005f)] private float _lineWidth = 0.035f;
        [SerializeField, Min(0.01f)] private float _surfaceOffset = 0.06f;

        private GameObject _visual;
        private Material _material;
        private readonly LineRenderer[] _lines = new LineRenderer[6];
        private float _elapsed;

        public void Show(Vector3 point, Vector3 normal)
        {
            if (!enabled || !gameObject.activeInHierarchy) return;
            if (_visual == null && !CreateVisual()) return;
            normal = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            _visual.transform.SetPositionAndRotation(point + normal * _surfaceOffset,
                Quaternion.FromToRotation(Vector3.up, normal));
            _elapsed = 0f;
            _visual.SetActive(true);
            Draw(0f);
        }

        private bool CreateVisual()
        {
            var shader = Resources.Load<Shader>("ExplorationClickMarker");
            if (shader == null)
            {
                Debug.LogError("Shader do marcador de clique não encontrado em Resources.", this);
                return false;
            }
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _visual = new GameObject("Click destination pulse") { hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < _lines.Length; i++)
            {
                var part = new GameObject(i < 2 ? "Pulse ring" : "Inward rune");
                part.transform.SetParent(_visual.transform, false);
                var line = part.AddComponent<LineRenderer>();
                line.sharedMaterial = _material;
                line.useWorldSpace = false;
                line.loop = i < 2;
                line.positionCount = i < 2 ? 64 : 3;
                line.numCornerVertices = 2;
                line.numCapVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = LightProbeUsage.Off;
                _lines[i] = line;
            }
            return true;
        }

        private void Update()
        {
            if (_visual == null || !_visual.activeSelf) return;
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.1f, _duration));
            Draw(t);
            if (t >= 1f) _visual.SetActive(false);
        }

        private void Draw(float t)
        {
            float ease = 1f - (1f - t) * (1f - t);
            float fade = (1f - t) * (1f - t);
            for (int i = 0; i < _lines.Length; i++)
            {
                var line = _lines[i];
                Color tint = _color;
                tint.a *= fade * (i == 1 ? 0.3f : 1f);
                line.startColor = line.endColor = tint;
                line.widthMultiplier = _lineWidth * (i == 1 ? 2.2f : 1f) * Mathf.Lerp(1f, 0.3f, t);
                if (i < 2)
                {
                    float radius = _radius * (i == 0 ? Mathf.Lerp(0.5f, 1.15f, ease) : Mathf.Lerp(0.3f, 0.8f, ease));
                    for (int p = 0; p < 64; p++)
                    {
                        float angle = p * Mathf.PI * 2f / 64;
                        line.SetPosition(p, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
                    }
                }
                else
                {
                    var rotation = Quaternion.Euler(0, (i - 2) * 90f + 45f, 0);
                    float distance = _radius * Mathf.Lerp(1.25f, 0.3f, ease);
                    float wing = _radius * 0.2f;
                    line.SetPosition(0, rotation * new Vector3(-wing, 0, distance + wing));
                    line.SetPosition(1, rotation * new Vector3(0, 0, distance));
                    line.SetPosition(2, rotation * new Vector3(wing, 0, distance + wing));
                }
            }
        }

        private void OnDisable()
        {
            if (_visual != null) _visual.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_visual != null) Destroy(_visual);
            if (_material != null) Destroy(_material);
        }
    }
}
