#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// Visualizador de gizmos em forma de anel para o editor Unity.
    /// Útil para depurar visualmente radii de percepção e patrulha dos inimigos.
    /// Renderiza apenas quando o objeto está selecionado no editor (OnDrawGizmosSelected).
    /// </summary>
    public class CircleGizmo : MonoBehaviour
    {
        // ── Aparência do anel ──────────────────────────────────────────────────────

        [Header("Configuração do Anel")]
        [Tooltip("Nome exibido na label flutuante no editor.")]
        [SerializeField] private string _circleName = "Detection Radius";

        [Tooltip("Raio externo do anel visualizado.")]
        [SerializeField] private float _outerRadius = 5f;

        [Tooltip("Raio interno do anel (0 = disco sólido sem buraco).")]
        [SerializeField] private float _innerRadius = 2f;

        [Tooltip("Cor base do anel (borda sempre opaca; preenchimento usa solidAlpha).")]
        [SerializeField] private Color _circleColor = Color.green;

        // ── Orientação ─────────────────────────────────────────────────────────────

        [Header("Orientação")]
        [Tooltip("Normal do plano em que o anel é desenhado. Vector3.up = plano horizontal.")]
        [SerializeField] private Vector3 _normal = Vector3.up;

        // ── Preenchimento sólido ───────────────────────────────────────────────────

        [Header("Preenchimento")]
        [Tooltip("Se verdadeiro, desenha o anel com preenchimento semitransparente.")]
        [SerializeField] private bool _drawSolid = true;

        [Range(0f, 1f)]
        [Tooltip("Opacidade do preenchimento sólido do anel.")]
        [SerializeField] private float _solidAlpha = 0.15f;

        // ── Label ──────────────────────────────────────────────────────────────────

        [Header("Label")]
        [Tooltip("Ângulo (em graus) que determina a posição angular da label ao redor do anel.")]
        [SerializeField] private float _labelAngle = 0f;

        [Tooltip("Deslocamento radial da label além do raio externo.")]
        [SerializeField] private float _labelOffset = 0.25f;

        // ── Qualidade de renderização ──────────────────────────────────────────────

        [Header("Qualidade")]
        [Tooltip("Número de segmentos do polígono que aproxima o círculo. Mais segmentos = mais suave.")]
        [SerializeField] private int _segments = 64;

#if UNITY_EDITOR
        // Reutilizado entre frames para evitar alocação de GUIStyle a cada OnDrawGizmosSelected
        private GUIStyle _cachedLabelStyle;

        private void OnDrawGizmosSelected()
        {
            _normal.Normalize();

            Vector3 center = transform.position;

            // Garante que o raio interno não ultrapasse o externo
            _innerRadius = Mathf.Clamp(_innerRadius, 0f, _outerRadius);

            Color borderColor = new Color(_circleColor.r, _circleColor.g, _circleColor.b, 1f);

            // ── Borda externa ──────────────────────────────────────────────────────
            Handles.color = borderColor;
            Handles.DrawWireDisc(center, _normal, _outerRadius);

            // ── Borda interna (se houver) ──────────────────────────────────────────
            if (_innerRadius > 0f)
                Handles.DrawWireDisc(center, _normal, _innerRadius);

            // ── Preenchimento do anel ──────────────────────────────────────────────
            if (_drawSolid)
            {
                Color solidColor = new Color(_circleColor.r, _circleColor.g, _circleColor.b, _solidAlpha);
                Handles.color = solidColor;
                DrawRing(center, _normal, _innerRadius, _outerRadius, _segments);
            }

            // ── Label flutuante ────────────────────────────────────────────────────

            // Calcula a posição da label a partir do ângulo configurado
            float   angleRad    = _labelAngle * Mathf.Deg2Rad;
            Vector3 localOffset = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad))
                                  * (_outerRadius + _labelOffset);

            Quaternion rotation      = Quaternion.FromToRotation(Vector3.up, _normal);
            Vector3    labelPosition = center + (rotation * localOffset);

            // Cria o estilo da label uma única vez e o reutiliza
            if (_cachedLabelStyle == null)
            {
                _cachedLabelStyle = new GUIStyle
                {
                    fontStyle = FontStyle.Bold,
                    fontSize  = 12,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            _cachedLabelStyle.normal.textColor = borderColor;

            string label = $"{_circleName}\nInner: {_innerRadius:F1}\nOuter: {_outerRadius:F1}";
            Handles.Label(labelPosition, label, _cachedLabelStyle);
        }

        /// <summary>
        /// Desenha um anel preenchido com quads entre <paramref name="innerRadius"/> e
        /// <paramref name="outerRadius"/> usando <see cref="Handles.DrawAAConvexPolygon"/>.
        /// </summary>
        private void DrawRing(
            Vector3 center,
            Vector3 normal,
            float   innerRadius,
            float   outerRadius,
            int     segments)
        {
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);
            Vector3[]  vertices = new Vector3[segments * 2];

            // Pré-calcula os vértices internos e externos de cada segmento
            for (int i = 0; i < segments; i++)
            {
                float   angle = ((float)i / segments) * Mathf.PI * 2f;
                Vector3 dir   = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                vertices[i * 2]     = center + rotation * (dir * innerRadius);
                vertices[i * 2 + 1] = center + rotation * (dir * outerRadius);
            }

            // Desenha cada quad do anel conectando segmentos adjacentes
            for (int i = 0; i < segments; i++)
            {
                int current = i * 2;
                int next    = ((i + 1) % segments) * 2;

                Handles.DrawAAConvexPolygon(
                    vertices[current],
                    vertices[next],
                    vertices[next + 1],
                    vertices[current + 1]
                );
            }
        }
#endif
    }
}
