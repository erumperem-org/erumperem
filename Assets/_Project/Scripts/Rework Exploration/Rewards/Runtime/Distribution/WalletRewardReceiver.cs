using Core.Economy.Currency;
using Core.Storage;

namespace Core.Rewards.Distribution
{
    /// <summary>Routes coins (ICoin) into the WalletSystem and saves the wallet.</summary>
    public sealed class WalletRewardReceiver : IRewardReceiver
    {
        private readonly WalletSystem _wallet;
        private readonly WalletSaveSystem _walletSave;
        private bool _dirty;

        public WalletRewardReceiver(WalletSystem wallet, WalletSaveSystem walletSave)
        {
            _wallet = wallet;
            _walletSave = walletSave;
        }

        public bool CanReceive(InterfaceStorageable storageable) => storageable is ICoin;

        public RewardLine Receive(InterfaceStorageable storageable, int amount)
        {
            if (_wallet == null || storageable is not ICoin coin || amount <= 0)
                return new RewardLine(storageable, amount, 0);

            _wallet.Deposit(coin, amount); // wallets have no capacity limit by design
            _dirty = true;
            return new RewardLine(storageable, amount, amount);
        }

        public void Commit()
        {
            if (!_dirty) return;
            _dirty = false;

            if (_walletSave != null)
                _walletSave.SaveAsync();
        }
    }
}
