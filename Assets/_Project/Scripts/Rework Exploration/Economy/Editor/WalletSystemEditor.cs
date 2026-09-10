#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.Economy.Currency.Editor
{
    [CustomEditor(typeof(WalletSystem))]
    public sealed class WalletSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var wallet = (WalletSystem)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect balances at runtime.", MessageType.Info);
                return;
            }

            foreach (var (coin, amount) in wallet.Balances.ToList())
                EditorGUILayout.LabelField(coin.StorageableId, amount.ToString());
        }
    }
}
#endif
