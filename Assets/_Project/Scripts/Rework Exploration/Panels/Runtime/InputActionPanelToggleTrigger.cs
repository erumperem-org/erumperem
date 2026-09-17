using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.UI.Panels
{
    /// <summary>
    /// Input-driven panel toggle: alternates its target panel's active
    /// state on each performed input, broadcasting every change through
    /// the central, static PanelVisibilityController instead of firing a
    /// local UnityEvent. Also listens to that same broadcast: whenever a
    /// DIFFERENT panel becomes active, this one closes its own panel if it
    /// is currently open — giving an accordion-style "only one panel open
    /// at a time" behaviour for free, with no direct references between
    /// panel scripts.
    /// </summary>
    public sealed class InputActionPanelToggleTrigger : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private GameObject _target;

        [Header("Input")]
        [SerializeField] private InputActionReference _actionReference;

        private void OnEnable()
        {
            if (_actionReference != null && _actionReference.action != null)
            {
                _actionReference.action.Enable();
                _actionReference.action.performed += HandlePerformed;
            }
            else
            {
                Debug.LogError($"[InputActionPanelToggleTrigger:{name}] No InputActionReference assigned.", this);
            }

            PanelVisibilityController.OnPanelVisibilityChanged += HandleOtherPanelVisibilityChanged;
        }

        private void OnDisable()
        {
            if (_actionReference != null && _actionReference.action != null)
            {
                _actionReference.action.performed -= HandlePerformed;
                _actionReference.action.Disable();
            }

            PanelVisibilityController.OnPanelVisibilityChanged -= HandleOtherPanelVisibilityChanged;
        }

        private void HandlePerformed(InputAction.CallbackContext context) => Trigger();

        /// <summary>Toggles the target panel and broadcasts the change. Exposed for manual/editor testing.</summary>
        public void Trigger()
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
            _target.SetActive(isActive);
            PanelVisibilityController.NotifyVisibilityChanged(_target, isActive);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(InputActionPanelToggleTrigger))]
    public sealed class InputActionPanelToggleTriggerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var trigger = (InputActionPanelToggleTrigger)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to simulate the trigger.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Simulate Trigger"))
                trigger.Trigger();
        }
    }
#endif
}