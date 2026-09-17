using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.InputBindings
{
    /// <summary>
    /// Binds an Input Action to a target GameObject, invoking a UnityEvent
    /// when the action is performed — functionally equivalent to a UI
    /// Button's onClick, but triggered by an input binding instead of a
    /// mouse click. The action is enabled/subscribed in OnEnable and
    /// disabled/unsubscribed in OnDisable, so it never leaks a listener
    /// onto a destroyed or disabled object.
    /// </summary>
    public sealed class InputActionButtonTrigger : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The GameObject this trigger acts on. Purely contextual — the actual behaviour is whatever OnTriggered is wired to in the Inspector.")]
        [SerializeField] private GameObject _target;

        [Header("Input")]
        [SerializeField] private InputActionReference _actionReference;

        [Header("Behaviour")]
        [SerializeField] private UnityEvent _onTriggered;

        public GameObject Target => _target;

        private void OnEnable()
        {
            if (_actionReference == null || _actionReference.action == null)
            {
                Debug.LogError($"[InputActionButtonTrigger:{name}] No InputActionReference assigned.", this);
                return;
            }

            _actionReference.action.Enable();
            _actionReference.action.performed += HandlePerformed;
        }

        private void OnDisable()
        {
            if (_actionReference == null || _actionReference.action == null) return;

            _actionReference.action.performed -= HandlePerformed;
            _actionReference.action.Disable();
        }

        private void HandlePerformed(InputAction.CallbackContext context) => Trigger();

        /// <summary>
        /// Invokes the configured behaviour directly — exposed publicly so
        /// it can be triggered manually (e.g. from the editor testbed)
        /// without going through the actual input action.
        /// </summary>
        public void Trigger() => _onTriggered?.Invoke();
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(InputActionButtonTrigger))]
    public sealed class InputActionButtonTriggerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var trigger = (InputActionButtonTrigger)target;

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