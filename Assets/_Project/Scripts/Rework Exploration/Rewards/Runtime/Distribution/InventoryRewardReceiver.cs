using Core.Exploration.Items;
using Core.Inventory;
using Core.Storage;

namespace Core.Rewards.Distribution
{
    /// <summary>
    /// Routes items (IIITem) into the inventories:
    ///   1) losable, if the whole amount fits;
    ///   2) otherwise permanent, if the whole amount fits;
    ///   3) otherwise fills what it can (losable first, then permanent) and
    ///      reports the leftover as rejected (UI turns it red).
    /// Saves only the inventories that actually changed.
    /// </summary>
    public sealed class InventoryRewardReceiver : IRewardReceiver
    {
        private readonly InventorySystem _losable;
        private readonly InventorySaveSystem _losableSave;
        private readonly InventorySystem _permanent;
        private readonly InventorySaveSystem _permanentSave;

        private bool _losableDirty;
        private bool _permanentDirty;

        public InventoryRewardReceiver(
            InventorySystem losable, InventorySaveSystem losableSave,
            InventorySystem permanent, InventorySaveSystem permanentSave)
        {
            _losable = losable;
            _losableSave = losableSave;
            _permanent = permanent;
            _permanentSave = permanentSave;
        }

        public bool CanReceive(InterfaceStorageable storageable) => storageable is IIITem;

        public RewardLine Receive(InterfaceStorageable storageable, int amount)
        {
            if (storageable is not IIITem item || amount <= 0)
                return new RewardLine(storageable, amount, 0);

            int accepted;

            if (_losable != null && _losable.CanFit(item, amount))
                accepted = AddToLosable(item, amount);
            else if (_permanent != null && _permanent.CanFit(item, amount))
                accepted = AddToPermanent(item, amount);
            else
            {
                // Neither takes the full stack: fill what we can.
                accepted = AddToLosable(item, amount);
                if (accepted < amount)
                    accepted += AddToPermanent(item, amount - accepted);
            }

            return new RewardLine(storageable, amount, accepted);
        }

        public void Commit()
        {
            if (_losableDirty)
            {
                _losableDirty = false;
                if (_losableSave != null) _losableSave.SaveAsync();
            }

            if (_permanentDirty)
            {
                _permanentDirty = false;
                if (_permanentSave != null) _permanentSave.SaveAsync();
            }
        }

        private int AddToLosable(IIITem item, int amount)
        {
            if (_losable == null) return 0;
            int added = _losable.AddAsMuchAsPossible(item, amount);
            _losableDirty |= added > 0;
            return added;
        }

        private int AddToPermanent(IIITem item, int amount)
        {
            if (_permanent == null) return 0;
            int added = _permanent.AddAsMuchAsPossible(item, amount);
            _permanentDirty |= added > 0;
            return added;
        }
    }
}
