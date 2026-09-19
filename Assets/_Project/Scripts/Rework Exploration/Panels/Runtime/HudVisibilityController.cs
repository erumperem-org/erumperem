using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.UI.Panels
{
    /// <summary>
    /// Keeps the HUD active exactly when no panel is currently active.
    /// Initial state is determined by scanning the explicit _panels list in
    /// Awake (checking each one's current activeSelf) — no longer inferred
    /// purely from broadcasts, avoiding the earlier "assumes everything
    /// starts inactive" caveat.
    /// </summary>
    public sealed class HudVisibilityController : MonoBehaviour
    {
        [SerializeField] private GameObject _hud;

        [Tooltip("Every panel the HUD should account for. Scanned once in Awake to seed the initial active set.")]
        [SerializeField] private List<GameObject> _panels = new();

        private readonly HashSet<GameObject> _activePanels = new();

        public IReadOnlyCollection<GameObject> ActivePanels => _activePanels;

        private void Awake()
        {
            _activePanels.Clear();

            foreach (var panel in _panels)
            {
                if (panel != null && panel.activeSelf)
                    _activePanels.Add(panel);
            }
        }

        private void OnEnable()
        {
            PanelVisibilityController.OnPanelVisibilityChanged += HandlePanelVisibilityChanged;
            RefreshHud();
        }

        private void OnDestroy() =>
            PanelVisibilityController.OnPanelVisibilityChanged -= HandlePanelVisibilityChanged;

        private void HandlePanelVisibilityChanged(GameObject panel, bool isActive)
        {
            if (isActive) _activePanels.Add(panel);
            else _activePanels.Remove(panel);

            RefreshHud();
        }

        private void RefreshHud()
        {
            if (_hud == null) return;
            _hud.SetActive(_activePanels.Count == 0);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(HudVisibilityController))]
    public sealed class HudVisibilityControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (HudVisibilityController)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect tracked panels.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Active panels tracked", controller.ActivePanels.Count.ToString());

            foreach (var panel in controller.ActivePanels)
                EditorGUILayout.LabelField("•", panel != null ? panel.name : "<null>");
        }
    }
#endif
}