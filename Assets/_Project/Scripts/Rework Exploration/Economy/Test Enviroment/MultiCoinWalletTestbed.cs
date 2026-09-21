using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Economy.Currency;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Economy.Currency.Testing
{
    /// <summary>
    /// Editor-only test harness: deposits or spends a configurable list of
    /// coin/amount pairs on a target WalletSystem via inspector buttons,
    /// either all at once or individually. Not meant for production scenes
    /// — exists purely to exercise WalletSystem in isolation.
    /// </summary>
    public sealed class MultiCoinWalletTestbed : MonoBehaviour
    {
        [Serializable]
        public sealed class CoinAmount
        {
            [Tooltip("Must implement ICoin.")]
            [SerializeField] private ScriptableObject _coinAsset;
            [SerializeField] private int _amount = 1;

            public ICoin Coin => _coinAsset as ICoin;
            public int Amount => _amount;
        }

        [Header("References")]
        [SerializeField] private WalletSystem _wallet;

        [Header("Coins")]
        [SerializeField] private List<CoinAmount> _coins = new();

        public void DepositAll()
        {
            if (_wallet == null) { Log(LogLevel.Error, "WalletSystem not assigned."); return; }

            foreach (var entry in _coins)
            {
                if (!ValidateEntry(entry)) continue;

                _wallet.Deposit(entry.Coin, entry.Amount);
                Log(LogLevel.Debug, $"Deposited {entry.Amount} of '{entry.Coin.StorageableId}'. New balance: {_wallet.GetBalance(entry.Coin)}.");
            }
        }

        public void SpendAll()
        {
            if (_wallet == null) { Log(LogLevel.Error, "WalletSystem not assigned."); return; }

            foreach (var entry in _coins)
            {
                if (!ValidateEntry(entry)) continue;

                bool success = _wallet.TrySpend(entry.Coin, entry.Amount);

                if (success)
                    Log(LogLevel.Debug, $"Spent {entry.Amount} of '{entry.Coin.StorageableId}'. New balance: {_wallet.GetBalance(entry.Coin)}.");
                else
                    Log(LogLevel.Warning, $"Failed to spend {entry.Amount} of '{entry.Coin.StorageableId}' — insufficient balance ({_wallet.GetBalance(entry.Coin)}).");
            }
        }

        public void Deposit(int index)
        {
            if (!IsValidIndex(index)) return;

            var entry = _coins[index];
            if (!ValidateEntry(entry)) return;

            _wallet.Deposit(entry.Coin, entry.Amount);
            Log(LogLevel.Debug, $"Deposited {entry.Amount} of '{entry.Coin.StorageableId}'. New balance: {_wallet.GetBalance(entry.Coin)}.");
        }

        public void Spend(int index)
        {
            if (!IsValidIndex(index)) return;

            var entry = _coins[index];
            if (!ValidateEntry(entry)) return;

            bool success = _wallet.TrySpend(entry.Coin, entry.Amount);

            if (success)
                Log(LogLevel.Debug, $"Spent {entry.Amount} of '{entry.Coin.StorageableId}'. New balance: {_wallet.GetBalance(entry.Coin)}.");
            else
                Log(LogLevel.Warning, $"Failed to spend {entry.Amount} of '{entry.Coin.StorageableId}' — insufficient balance ({_wallet.GetBalance(entry.Coin)}).");
        }

        private bool IsValidIndex(int index)
        {
            if (_wallet == null) { Log(LogLevel.Error, "WalletSystem not assigned."); return false; }
            if (index < 0 || index >= _coins.Count) { Log(LogLevel.Error, $"Invalid coin index: {index}."); return false; }
            return true;
        }

        private bool ValidateEntry(CoinAmount entry)
        {
            if (entry.Coin == null) { Log(LogLevel.Error, "Coin asset not assigned or does not implement ICoin."); return false; }
            if (entry.Amount <= 0) { Log(LogLevel.Error, $"Amount for '{entry.Coin.StorageableId}' must be greater than 0."); return false; }
            return true;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[MultiCoinWalletTestbed:{gameObject.name}] {msg}", LogCategory.Inventory);
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(MultiCoinWalletTestbed))]
    public sealed class MultiCoinWalletTestbedEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var testbed = (MultiCoinWalletTestbed)target;

            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to deposit/spend.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Batch Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Deposit All"))
                    testbed.DepositAll();

                if (GUILayout.Button("Spend All"))
                    testbed.SpendAll();
            }

            var coinsProperty = serializedObject.FindProperty("_coins");
            if (coinsProperty != null && coinsProperty.arraySize > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Per-Coin Actions", EditorStyles.boldLabel);

                for (int i = 0; i < coinsProperty.arraySize; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"[{i}]", GUILayout.Width(30));

                        if (GUILayout.Button("Deposit"))
                            testbed.Deposit(i);

                        if (GUILayout.Button("Spend"))
                            testbed.Spend(i);
                    }
                }
            }
        }
    }
#endif
}