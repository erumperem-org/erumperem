using System.Collections.Generic;
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
    /// concedido: itens (IIITem) para o inventário primário e, se não couber,
    /// para o secundário; moedas (ICoin) para a WalletSystem.
    /// O que não couber em nenhum dos dois inventários volta para o baú.
    /// </summary>
    public sealed class ChestLootRouter : MonoBehaviour
    {
        [Header("Destino")]
        [Tooltip("Inventário preferencial: recebe os itens primeiro.")]
        [SerializeField] private InventorySystem primaryInventory;

        [Tooltip("Inventário de transbordo: recebe apenas o que não coube no primário. Opcional.")]
        [SerializeField] private InventorySystem secondaryInventory;

        [SerializeField] private WalletSystem wallet;

        private bool HasAnyInventory => primaryInventory != null || secondaryInventory != null;

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
                    case IIITem item when HasAnyInventory:
                        int remaining = amount;

                        remaining -= TryInsert(primaryInventory, item, remaining);
                        if (remaining > 0)
                            remaining -= TryInsert(secondaryInventory, item, remaining);

                        if (remaining > 0)
                        {
                            Log(LogLevel.Warning,
                                $"Inventários cheios: só coube {amount - remaining}/{amount} de '{item}' do baú '{chest.name}' - devolvendo o restante.");
                            AddLeftover(ref leftover, storageable, remaining);
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

        /// <summary>
        /// Tenta inserir no inventário informado. Retorna a quantidade inserida
        /// (0 se o inventário for nulo, for o mesmo que o primário já tentado, ou estiver cheio).
        /// </summary>
        private int TryInsert(InventorySystem target, IIITem item, int amount)
        {
            if (target == null || amount <= 0) return 0;

            // Evita tentar duas vezes no mesmo inventário se primário == secundário por engano.
            if (ReferenceEquals(target, secondaryInventory) && ReferenceEquals(target, primaryInventory))
                return 0;

            return target.AddAsMuchAsPossible(item, amount);
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