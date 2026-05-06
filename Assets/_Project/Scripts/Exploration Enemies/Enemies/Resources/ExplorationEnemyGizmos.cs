#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Core.Exploration.Enemies
{
    [RequireComponent(typeof(ExplorationEnemyController))]
    public class ExplorationEnemyGizmos : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Chase")]
        [SerializeField] private bool showChaseRadius = true;
        [SerializeField] private Color chaseRadiusColor = new Color(1f, 0.3f, 0.3f, 0.15f);
        [SerializeField] private Color chaseRadiusOutlineColor = new Color(1f, 0.3f, 0.3f, 0.8f);
        private float chaseRadius = 500;

        [Header("Stopping Distance")]
        [SerializeField] private bool showStoppingDistance = true;
        [SerializeField] private Color stoppingColor = new Color(1f, 0.85f, 0f, 0.1f);
        [SerializeField] private Color stoppingOutlineColor = new Color(1f, 0.85f, 0f, 0.8f);

        private UnityEngine.AI.NavMeshAgent agent;

        private void OnValidate()
        {
            agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        }

        private void OnDrawGizmosSelected()
        {
            chaseRadius = this.GetComponent<ExplorationEnemyController>().data.chasingData.chaseRadius;
            DrawRadius(
                showChaseRadius,
                chaseRadius,
                chaseRadiusColor,
                chaseRadiusOutlineColor,
                "Chase"
            );

            if (agent == null) agent = GetComponent<UnityEngine.AI.NavMeshAgent>();

            DrawRadius(
                showStoppingDistance,
                agent != null ? agent.stoppingDistance : 0f,
                stoppingColor,
                stoppingOutlineColor,
                "Stop"
            );
        }

        private void DrawRadius(bool enabled, float radius, Color fill, Color outline, string label)
        {
            if (!enabled || radius <= 0f) return;

            Vector3 origin = transform.position;

            Handles.color = fill;
            Handles.DrawSolidDisc(origin, Vector3.up, radius);

            Handles.color = outline;
            Handles.DrawWireDisc(origin, Vector3.up, radius);

            GUIStyle style = new GUIStyle
            {
                normal = { textColor = outline },
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };

            Handles.Label(origin + Vector3.forward * radius + Vector3.up * 0.3f, $"{label} ({radius:F0})", style);
        }
#endif
    }
}