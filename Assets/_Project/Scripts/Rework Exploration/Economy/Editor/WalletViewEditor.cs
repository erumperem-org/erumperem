#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.Economy.Currency.UI.Editor
{
    [CustomEditor(typeof(WalletView))]
    public sealed class WalletViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var view = (WalletView)target;

            var slotsProp = serializedObject.FindProperty("_displaySlots");
            for (int i = 0; i < slotsProp.arraySize; i++)
            {
                var slot = slotsProp.GetArrayElementAtIndex(i);
                var coinProp = slot.FindPropertyRelative("_coinAsset");
                var iconProp = slot.FindPropertyRelative("_icon");
                var textProp = slot.FindPropertyRelative("_amountText");

                if (coinProp.objectReferenceValue == null || iconProp.objectReferenceValue == null || textProp.objectReferenceValue == null)
                    EditorGUILayout.HelpBox($"Slot {i} is missing a reference (coin/icon/text).", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test refresh against live wallet data.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Refresh Now"))
                view.RefreshAll();
        }
    }
}
#endif