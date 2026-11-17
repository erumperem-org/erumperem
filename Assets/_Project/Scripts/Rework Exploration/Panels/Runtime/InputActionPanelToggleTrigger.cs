
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.UI.Panels
{
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
                InputBindingsService.Register(_actionReference.action.actionMap);
                _actionReference.action.Enable();
                _actionReference.action.performed += HandlePerformed;
            }
            else
            {
                Debug.LogError("[InputActionPanelToggleTrigger:" + name + "] No InputActionReference assigned.", this);
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

        private void HandlePerformed(InputAction.CallbackContext context)
        {
            if (_target == null)
            {
                return;
            }

            // The game's Pause action and UI Cancel can both be Escape.
            // Do not open/close Pause behind a different active menu.
            if (_target.name.StartsWith("PausePanel", System.StringComparison.OrdinalIgnoreCase))
            {
                KeyboardNavigablePanel active = KeyboardNavigablePanel.Current;

                if (active != null && active.gameObject != _target)
                {
                    return;
                }
            }

            Trigger();
        }

        public void Trigger()
        {
            if (_target == null)
            {
                return;
            }

            SetPanelActive(!_target.activeSelf);
        }

        private void HandleOtherPanelVisibilityChanged(GameObject panel, bool isActive)
        {
            if (!isActive || panel == _target || _target == null || !_target.activeSelf)
            {
                return;
            }

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

            InputActionPanelToggleTrigger trigger = (InputActionPanelToggleTrigger)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to simulate the trigger.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Simulate Trigger"))
            {
                trigger.Trigger();
            }
        }
    }
#endif
}
