#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.Shop.Editor
{
    [CustomEditor(typeof(ItemShopButton))]
    public sealed class ItemShopButtonEditor : UnityEditor.Editor
    {
        private int _testQuantity = 1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var button = (ItemShopButton)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Purchase Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test purchases.", MessageType.Info);
                return;
            }

            if (button.Item != null)
                EditorGUILayout.LabelField("Total cost", (button.UnitPrice * _testQuantity).ToString());

            _testQuantity = EditorGUILayout.IntField("Quantity", _testQuantity);

            if (GUILayout.Button("Try Purchase"))
            {
                bool success = button.TryPurchase(_testQuantity);
                Debug.Log(success
                    ? $"[ItemShopButton] Purchase succeeded ({_testQuantity} unit(s))."
                    : "[ItemShopButton] Purchase failed — check currency balance and inventory space.");
            }
        }
    }
}
#endif
