using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.UI.Panels
{
    /// <summary>
    /// Editor-only test harness for the static PanelVisibilityController —
    /// broadcasts a test event for a chosen panel/state, without needing
    /// any real panel trigger wired up yet.
    /// </summary>
    public sealed class PanelVisibilityControllerTestbed : MonoBehaviour
    {
        [SerializeField] private GameObject _testPanel;
        [SerializeField] private bool _testIsActive;

        public void BroadcastTestEvent() =>
            PanelVisibilityController.NotifyVisibilityChanged(_testPanel, _testIsActive);
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(PanelVisibilityControllerTestbed))]
    public sealed class PanelVisibilityControllerTestbedEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var testbed = (PanelVisibilityControllerTestbed)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to broadcast a test event.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Broadcast Test Event"))
                testbed.BroadcastTestEvent();
        }
    }
#endif
}