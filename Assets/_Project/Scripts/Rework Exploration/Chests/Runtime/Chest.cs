using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;
using Core.Storage;
using InteractionSystem;

namespace Core.Chests
{
    /// <summary>
    /// Baú simples: recebe uma cópia já resolvida do conteúdo (moedas/itens
    /// concretos, sem chance a avaliar) de um sistema externo, e concede
    /// tudo de uma vez quando interagido. Não usa RewardGeneratorService e
    /// não envia o conteúdo pra Wallet/Inventory - só expõe a raridade do
    /// melhor item e o estado aberto/fechado, para a view reagir.
    ///
    /// Consumo é uma condição adicional de CanInteract, por cima do lock
    /// padrão de InteractableBase: uma vez consumido, só AssignLoot() com
    /// conteúdo novo reabre o baú (chama SetAvailable(true) internamente).
    /// </summary>
    public sealed class Chest : InteractableBase
    {
        private Dictionary<InterfaceStorageable, int> _contents = new();
        private bool _consumed;

        /// <summary>Disparado na interação, com a raridade do melhor IIITem no conteúdo.</summary>
        public event Action<ItemRarity> OnBestItemRarityRevealed;

        /// <summary>
        /// Disparado sempre que o baú transiciona entre Closed e Open -
        /// puramente um sinal para a view (ex: disparar uma animação).
        /// </summary>
        public event Action<ChestState> OnChestStateChanged;

        /// <summary>Disparado quando ESTE baú concede conteúdo (snapshot, antes de limpar).</summary>
        public event Action<IReadOnlyDictionary<InterfaceStorageable, int>> OnContentGranted;

        /// <summary>Disparado quando QUALQUER baú concede conteúdo - assine uma vez só para escutar todos.</summary>
        public static event Action<Chest, IReadOnlyDictionary<InterfaceStorageable, int>> OnAnyChestContentGranted;
        public bool IsConsumed => _consumed;
        public bool HasContent => !_consumed && _contents.Count > 0;

        /// <summary>
        /// Snapshot somente-leitura do conteúdo atual, para debug/editor.
        /// Nunca mutar através disso - Chest é dono do próprio dicionário.
        /// </summary>
        public IReadOnlyDictionary<InterfaceStorageable, int> DebugContents => _contents;

        /// <summary>
        /// Consumido é uma condição adicional, permanente até AssignLoot()
        /// reabrir o baú - não usa HasContent aqui de propósito, porque um
        /// baú vazio (mas não consumido) ainda precisa poder ser interagido
        /// para cair no caminho de warning em OnInteractionStarted.
        /// </summary>
        public override bool CanInteract => base.CanInteract && !_consumed;

        /// <summary>
        /// Atribui uma NOVA cópia de conteúdo a este baú, substituindo
        /// qualquer conteúdo anterior não consumido. Sempre clona os dados
        /// recebidos - quem chama nunca deve assumir que o baú referencia o
        /// dicionário original. Também reabre o baú (Closed + disponível),
        /// já que receber loot novo sempre significa que ele pode ser
        /// aberto de novo.
        /// </summary>
        public void AssignLoot(IReadOnlyDictionary<InterfaceStorageable, int> loot)
        {
            _contents = loot != null
                ? new Dictionary<InterfaceStorageable, int>(loot)
                : new Dictionary<InterfaceStorageable, int>();

            _consumed = false;
            SetAvailable(true); // reverte o lock que TryInteract aplicou na última interação

            OnChestStateChanged?.Invoke(ChestState.Closed);
            ItemRarity? bestRarity = FindBestItemRarity();
            OnBestItemRarityRevealed?.Invoke(bestRarity ?? ItemRarity.Common);
        }

        /// <summary>
        /// Executa a interação: concede o conteúdo por inteiro (sem
        /// sorteio), consome o conteúdo (não pode ser reaberto), expõe a
        /// raridade do melhor item encontrado e sinaliza a transição para
        /// Open.
        /// </summary>
        protected override void OnInteractionStarted(IInteractionContext context)
        {
            base.OnInteractionStarted(context);

            if (_contents.Count == 0)
            {
                Log(LogLevel.Warning, "Chest interacted with while empty (no content assigned).");
                _consumed = true;
                OnChestStateChanged?.Invoke(ChestState.Open);
                return;
            }

            ItemRarity? bestRarity = FindBestItemRarity();

            // Snapshot antes de limpar - quem escuta o evento recebe o conteúdo de
            // verdade, não um dicionário já esvaziado.
            var grantedContent = new Dictionary<InterfaceStorageable, int>(_contents);

            _consumed = true;
            _contents.Clear();

            OnChestStateChanged?.Invoke(ChestState.Open);
            OnContentGranted?.Invoke(grantedContent);
            OnAnyChestContentGranted?.Invoke(this, grantedContent);

            if (bestRarity.HasValue)
            {
                OnBestItemRarityRevealed?.Invoke(bestRarity.Value);
            }
            else
            {
                Log(LogLevel.Warning, "Chest content contained no IIITem — " +
                                       "check the LootTable (README directive: avoid coin-only tables).");
            }
        }
        private ItemRarity? FindBestItemRarity()
        {
            ItemRarity? best = null;

            foreach (var storageable in _contents.Keys)
            {
                if (storageable is not IIITem item) continue;
                if (best == null || item.Rarity > best.Value)
                    best = item.Rarity;
            }

            return best;
        }

        /// <summary>
        /// Devolve conteúdo que não coube no destino (ex: inventário cheio) para
        /// este baú. Reabre o baú (Closed, disponível de novo) e recalcula a
        /// raridade do melhor item com o que sobrou. Soma ao invés de substituir,
        /// diferente de AssignLoot - aqui é sobra do que o baú já tinha, não loot novo.
        /// </summary>
        public void ReturnContent(IReadOnlyDictionary<InterfaceStorageable, int> leftover)
        {
            if (leftover == null || leftover.Count == 0) return;

            foreach (var (storageable, amount) in leftover)
            {
                if (amount <= 0) continue;
                _contents.TryGetValue(storageable, out var current);
                _contents[storageable] = current + amount;
            }

            _consumed = false;
            SetAvailable(true);

            OnChestStateChanged?.Invoke(ChestState.Closed);

            ItemRarity? bestRarity = FindBestItemRarity();
            OnBestItemRarityRevealed?.Invoke(bestRarity ?? ItemRarity.Common);
        }
        
        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[Chest:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}