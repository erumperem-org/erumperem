using Services.DebugUtilities;
using UnityEngine;
using Core.Economy.Currency;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Economy.Currency.Testing
{
    /// <summary>
    /// Editor-only test harness: deposits or spends N units of a reference
    /// coin on a target WalletSystem via inspector buttons. Not meant for
    /// production scenes — exists purely to exercise WalletSystem in isolation.
    /// </summary>
    public sealed class WalletTestbed : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WalletSystem _wallet;

        [Tooltip("Must implement ICoin.")]
        [SerializeField] private ScriptableObject _coinAsset;

        [Header("Amount")]
        [SerializeField] private int _amount = 1;

        private ICoin Coin => _coinAsset as ICoin;

        public void Deposit()
        {
            if (!Validate()) return;

            _wallet.Deposit(Coin, _amount);
            Log(LogLevel.Debug, $"Deposited {_amount} of '{Coin.StorageableId}'. New balance: {_wallet.GetBalance(Coin)}.");
        }

        public void Spend()
        {
            if (!Validate()) return;

            bool success = _wallet.TrySpend(Coin, _amount);

            if (success)
                Log(LogLevel.Debug, $"Spent {_amount} of '{Coin.StorageableId}'. New balance: {_wallet.GetBalance(Coin)}.");
            else
                Log(LogLevel.Warning, $"Failed to spend {_amount} of '{Coin.StorageableId}' — insufficient balance ({_wallet.GetBalance(Coin)}).");
        }

        private bool Validate()
        {
            if (_wallet == null) { Log(LogLevel.Error, "WalletSystem not assigned."); return false; }
            if (Coin == null) { Log(LogLevel.Error, "Coin asset not assigned or does not implement ICoin."); return false; }
            if (_amount <= 0) { Log(LogLevel.Error, "Amount must be greater than 0."); return false; }
            return true;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[WalletTestbed:{gameObject.name}] {msg}", LogCategory.Inventory);
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(WalletTestbed))]
    public sealed class WalletTestbedEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var testbed = (WalletTestbed)target;

            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to deposit/spend.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Deposit"))
                    testbed.Deposit();

                if (GUILayout.Button("Spend"))
                    testbed.Spend();
            }
        }
    }
#endif
}