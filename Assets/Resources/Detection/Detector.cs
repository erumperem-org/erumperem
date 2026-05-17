using System.Collections.Generic;
using UnityEngine;
using DetectionSystem.Core;
using Services.DebugUtilities.Console;

namespace DetectionSystem.Core
{
    [RequireComponent(typeof(DetectionComponent))]
    public class Detector : MonoBehaviour
    {
        [Header("Filter")]
        [SerializeField] private List<LayerMask> layerMasks = new List<LayerMask> { ~0 };
        [SerializeField] private List<string> filterTags = new List<string>();

        [Header("Timing")]
        [SerializeField, Min(0f)] private float checkInterval = 0f;

        private DetectionComponent _detection;
        private float _timer;

        // Colliders registrados explicitamente — sem FindObjectsByType por frame
        [SerializeField] private List<Collider> _tracked = new List<Collider>();

        // Estado: chave struct evita boxing do ValueTuple
        private readonly Dictionary<StateKey, bool> _state = new Dictionary<StateKey, bool>();

        // ── Struct key sem boxing ──────────────────────────────────────────
        private readonly struct StateKey : System.IEquatable<StateKey>
        {
            public readonly int ColliderId;
            public readonly int ShapeIndex;
            public StateKey(int colliderId, int shapeIndex)
            {
                ColliderId = colliderId;
                ShapeIndex = shapeIndex;
            }
            public bool Equals(StateKey o) => ColliderId == o.ColliderId && ShapeIndex == o.ShapeIndex;
            public override bool Equals(object o) => o is StateKey k && Equals(k);
            public override int GetHashCode() => ColliderId * 397 ^ ShapeIndex;
        }

        void Awake()
        {
            _detection = GetComponent<DetectionComponent>();
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = checkInterval;

            Scan();
        }

        // ── Registro de colliders ──────────────────────────────────────────

        /// <summary>
        /// Registra um collider para ser monitorado.
        /// Chame isto quando um objeto relevante for criado/ativado na cena.
        /// </summary>
        public void Register(Collider col)
        {
            if (col != null && !_tracked.Contains(col))
                _tracked.Add(col);
        }

        /// <summary>Remove um collider do monitoramento.</summary>
        public void Unregister(Collider col)
        {
            _tracked.Remove(col);

            // Limpa estado órfão
            int id = col.GetInstanceID();
            for (int i = _detection.Shapes.Count - 1; i >= 0; i--)
                _state.Remove(new StateKey(id, i));
        }

        // ── Scan (zero alloc) ──────────────────────────────────────────────

        private void Scan()
        {
            var shapes = _detection.Shapes;
            int shapeCount = shapes.Count;

            for (int c = _tracked.Count - 1; c >= 0; c--)
            {
                Collider col = _tracked[c];

                // Remove colliders destruídos sem gerar alloc (null-check em Unity é override de ==)
                if (col == null) { _tracked.RemoveAt(c); continue; }
                if (!PassesFilter(col)) continue;

                Vector3 point = col.bounds.center;
                int colId = col.GetInstanceID();
                var receiver = col.GetComponent<DetectionReceiver>();

                for (int i = 0; i < shapeCount; i++)
                {
                    var entry = shapes[i];
                    if (!entry.enabled) continue;

                    var key = new StateKey(colId, i);
                    bool wasIn = _state.TryGetValue(key, out bool prev) && prev;
                    // Testa shape individualmente — sem alocar lista de hits
                    bool isIn = entry.Contains(
                        _detection.transform.position,
                        _detection.transform.rotation,
                        point);

                    if (isIn && !wasIn)
                    {
                        LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.World, $"[Detection] ENTER  {col.name} → '{entry.label}' ({entry.shapeType})");
                        receiver?.NotifyEnter(this, entry.label, i);
                    }
                    else if (!isIn && wasIn)
                    {
                        Debug.Log($"[Detection] EXIT   {col.name} → '{entry.label}' ({entry.shapeType})");
                        receiver?.NotifyExit(this, entry.label, i);
                    }

                    _state[key] = isIn;
                }
            }
        }

        // ── Filter ─────────────────────────────────────────────────────────

        private bool PassesFilter(Collider col)
        {
            int objLayer = 1 << col.gameObject.layer;
            bool layerOk = false;
            for (int i = 0; i < layerMasks.Count; i++)
                if ((objLayer & (int)layerMasks[i]) != 0) { layerOk = true; break; }
            if (!layerOk) return false;

            if (filterTags.Count == 0) return true;
            for (int i = 0; i < filterTags.Count; i++)
                if (!string.IsNullOrEmpty(filterTags[i]) && col.CompareTag(filterTags[i])) return true;

            return false;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            for (int c = 0; c < _tracked.Count; c++)
            {
                Collider col = _tracked[c];
                if (col == null || !PassesFilter(col)) continue;

                bool hit = _detection.Contains(col.bounds.center);
                Gizmos.color = hit ? Color.yellow : Color.red;
                Gizmos.DrawSphere(col.bounds.center, 0.12f);
            }
        }
#endif
    }
}
