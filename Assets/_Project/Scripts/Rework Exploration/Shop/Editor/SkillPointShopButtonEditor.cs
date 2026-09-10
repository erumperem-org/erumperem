#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Core.Economy.Currency;

namespace Core.Shop.Editor
{
    [CustomEditor(typeof(SkillPointShopButton))]
    public sealed class SkillPointShopButtonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var button = (SkillPointShopButton)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Purchase Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test purchases.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Global tier index", button.GlobalTierIndex.ToString());
            EditorGUILayout.LabelField("Exhausted", button.IsExhausted.ToString());

            if (!button.IsExhausted && button.TryGetCurrentTier(out ICoin currency, out int price))
                EditorGUILayout.LabelField("Current price", $"{price} ({currency?.StorageableId})");

            if (GUILayout.Button("Try Purchase"))
            {
                bool success = button.TryPurchase();
                Debug.Log(success
                    ? "[SkillPointShopButton] Purchase succeeded."
                    : "[SkillPointShopButton] Purchase failed — check currency balance or exhausted state.");
            }
        }
    }
}
#endif
