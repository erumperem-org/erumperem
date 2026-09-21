using System.Collections.Generic;
using Core.Chests;
using Core.Economy.Currency;
using Core.Exploration.Items;
using Core.Inventory;
using Core.Storage;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Chests
{
    /// <summary>
    /// Escuta a abertura de QUALQUER Chest da cena (via Chest.OnAnyChestContentGranted
    /// - evento estático, então funciona para baús instanciados/destruídos
    /// em runtime, sem precisar re-varrer a cena) e roteia o conteúdo
    /// concedido: itens (IIITem) para um InventorySystem específico, moedas
    /// (ICoin) para uma WalletSystem específica.
    /// </summary>
    public sealed class ChestLootRouter : MonoBehaviour
    {
        [Header("Destino")]
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private WalletSystem wallet;

        private void OnEnable()
        {
            Chest.OnAnyChestContentGranted += HandleChestContentGranted;
        }

        private void OnDisable()
        {
            Chest.OnAnyChestContentGranted -= HandleChestContentGranted;
        }

        private void HandleChestContentGranted(Chest chest, IReadOnlyDictionary<InterfaceStorageable, int> content)
        {
            Dictionary<InterfaceStorageable, int> leftover = null;

            foreach (var (storageable, amount) in content)
            {
                switch (storageable)
                {
                    case IIITem item when inventory != null:
                        int added = inventory.AddAsMuchAsPossible(item, amount);
                        int itemLeftover = amount - added;
                        if (itemLeftover > 0)
                        {
                            Log(LogLevel.Warning,
                                $"Inventário cheio: só coube {added}/{amount} de '{item}' do baú '{chest.name}' - devolvendo o restante.");
                            AddLeftover(ref leftover, storageable, itemLeftover);
                        }
                        break;

                    case ICoin coin when wallet != null:
                        wallet.Deposit(coin, amount);
                        break;

                    default:
                        Log(LogLevel.Warning,
                            $"Storageable não roteado (tipo desconhecido ou destino não atribuído) do baú '{chest.name}' - devolvendo: {storageable}.");
                        AddLeftover(ref leftover, storageable, amount);
                        break;
                }
            }

            if (leftover != null)
            {
                chest.ReturnContent(leftover);
            }
        }

        private static void AddLeftover(ref Dictionary<InterfaceStorageable, int> leftover, InterfaceStorageable storageable, int amount)
        {
            leftover ??= new Dictionary<InterfaceStorageable, int>();
            leftover.TryGetValue(storageable, out var current);
            leftover[storageable] = current + amount;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[ChestLootRouter:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}