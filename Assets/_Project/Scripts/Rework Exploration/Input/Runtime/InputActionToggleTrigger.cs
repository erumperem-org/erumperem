using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.InputBindings
{
    /// <summary>
    /// Binds an Input Action to a target GameObject, alternating between
    /// two configured operations on each performed input. Primary intended
    /// use: toggling a panel's active state (OperationA -> SetActive(true),
    /// OperationB -> SetActive(false), wired via the Inspector like any
    /// UnityEvent), though both operations are generic and can be wired to
    /// anything. Same bind/unbind convention as InputActionButtonTrigger.
    /// </summary>
    public sealed class InputActionToggleTrigger : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The GameObject this trigger acts on. Purely contextual — the actual behaviour is whatever OperationA/OperationB are wired to in the Inspector.")]
        [SerializeField] private GameObject _target;

        [Header("Input")]
        [SerializeField] private InputActionReference _actionReference;

        [Header("Behaviour (alternates between the two on each trigger)")]
        [Tooltip("Invoked on the 1st, 3rd, 5th... trigger. Typical panel use: target.SetActive(true).")]
        [SerializeField] private UnityEvent _operationA;
        [Tooltip("Invoked on the 2nd, 4th, 6th... trigger. Typical panel use: target.SetActive(false).")]
        [SerializeField] private UnityEvent _operationB;

        [SerializeField] private bool _startOnOperationA = true;

        private bool _nextIsOperationA;

        public GameObject Target => _target;
        public bool NextIsOperationA => _nextIsOperationA;

        private void Awake() => _nextIsOperationA = _startOnOperationA;

        private void OnEnable()
        {
            if (_actionReference == null || _actionReference.action == null)
            {
                Debug.LogError($"[InputActionToggleTrigger:{name}] No InputActionReference assigned.", this);
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
        /// Invokes whichever operation is next in the alternation, then
        /// flips the internal state. Exposed publicly so it can be
        /// triggered manually (e.g. from the editor testbed) without going
        /// through the actual input action.
        /// </summary>
        public void Trigger()
        {
            if (_nextIsOperationA)
                _operationA?.Invoke();
            else
                _operationB?.Invoke();

            _nextIsOperationA = !_nextIsOperationA;
        }

        /// <summary>Resets the alternation back to its configured starting operation, without invoking anything.</summary>
        public void ResetToggleState() => _nextIsOperationA = _startOnOperationA;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(InputActionToggleTrigger))]
    public sealed class InputActionToggleTriggerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var trigger = (InputActionToggleTrigger)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to simulate the trigger.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Next operation", trigger.NextIsOperationA ? "A" : "B");

            if (GUILayout.Button("Simulate Trigger"))
                trigger.Trigger();

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset Toggle State"))
                trigger.ResetToggleState();
        }
    }
#endif
}