using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.UI.Panels
{
    /// <summary>
    /// Reacts to PanelVisibilityController broadcasts without any input
    /// binding of its own — closes its target panel whenever a DIFFERENT
    /// panel becomes active, in the exact same terms as
    /// InputActionPanelToggleTrigger's own listening behaviour. Exposes
    /// public Open/Close/Toggle methods for other scripts (a UI Button's
    /// onClick, InputActionButtonTrigger, etc.) to drive it, since it has
    /// no input trigger of its own.
    /// </summary>
    public sealed class PanelVisibilityListener : MonoBehaviour
    {
        [SerializeField] private GameObject _target;

        private void OnEnable() =>
            PanelVisibilityController.OnPanelVisibilityChanged += HandleOtherPanelVisibilityChanged;

        private void OnDisable() =>
            PanelVisibilityController.OnPanelVisibilityChanged -= HandleOtherPanelVisibilityChanged;

        public void Open() => SetPanelActive(true);
        public void Close() => SetPanelActive(false);

        public void Toggle()
        {
            if (_target == null) return;
            SetPanelActive(!_target.activeSelf);
        }

        private void HandleOtherPanelVisibilityChanged(GameObject panel, bool isActive)
        {
            if (!isActive) return;
            if (panel == _target) return;
            if (_target == null || !_target.activeSelf) return;

            SetPanelActive(false);
        }

        private void SetPanelActive(bool isActive)
        {
            if (_target == null) return;
            _target.SetActive(isActive);
            PanelVisibilityController.NotifyVisibilityChanged(_target, isActive);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(PanelVisibilityListener))]
    public sealed class PanelVisibilityListenerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var listener = (PanelVisibilityListener)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test Open/Close/Toggle.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open"))
                    listener.Open();

                if (GUILayout.Button("Close"))
                    listener.Close();

                if (GUILayout.Button("Toggle"))
                    listener.Toggle();
            }
        }
    }
#endif
}