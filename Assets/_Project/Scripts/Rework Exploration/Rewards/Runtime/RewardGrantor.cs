using System.Collections.Generic;
using Core.Economy.Currency;
using Core.Inventory;
using Core.Rewards.Distribution;
using Core.Rewards.UI;
using Core.Storage;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Rewards
{
    /// <summary>
    /// Scene wiring only: builds the receivers from the Inspector references,
    /// runs the distribution (which also saves) and hands the result to the UI.
    /// Any reward source (corruption, chests, ...) just calls <see cref="Grant"/>.
    /// </summary>
    public sealed class RewardGrantor : MonoBehaviour
    {
        [Header("Coins → Wallet")]
        [SerializeField] private WalletSystem _wallet;
        [SerializeField] private WalletSaveSystem _walletSave;

        [Header("Items → Losable first, then Permanent")]
        [SerializeField] private InventorySystem _losable;
        [SerializeField] private InventorySaveSystem _losableSave;
        [SerializeField] private InventorySystem _permanent;
        [SerializeField] private InventorySaveSystem _permanentSave;

        [Header("UI")]
        [SerializeField] private RewardListView _listView;

        private RewardDistributionService _service;

        public IReadOnlyList<RewardLine> LastLines { get; private set; } = new List<RewardLine>();

        private void Awake()
        {
            _service = new RewardDistributionService(new IRewardReceiver[]
            {
                new WalletRewardReceiver(_wallet, _walletSave),
                new InventoryRewardReceiver(_losable, _losableSave, _permanent, _permanentSave)
            });
        }

        public IReadOnlyList<RewardLine> Grant(IReadOnlyDictionary<InterfaceStorageable, int> rewards)
        {
            if (_service == null) Awake();

            LastLines = _service.Distribute(rewards);

            foreach (var line in LastLines)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[RewardGrantor] {line.Storageable?.StorageableId} requested {line.Requested}, " +
                    $"accepted {line.Accepted}, rejected {line.Rejected}.",
                    LogCategory.Inventory);
            }

            if (_listView != null)
                _listView.Show(LastLines);

            return LastLines;
        }
    }
}
