using System;
using Core.CharacterStats;
using Core.Exploration.Items;
using Erumperem.Characters;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Inventory.UI.ItemUse
{
    /// <summary>
    /// State machine of the item-use flow (no visuals — see ItemUseFlowView):
    ///
    ///   click slot → [SelectingTarget] → pick character → [AwaitingConfirmation] → Confirm
    ///                      (skipped for items that need no target)
    ///   Cancel is valid at any step. Clicking another slot restarts with that item.
    ///
    /// Accumulates an <see cref="ItemUseContext"/>. On Confirm it re-checks
    /// that the item is still in the exact slot, re-checks applicability,
    /// executes the effect and removes ONE unit from the exact inventory and
    /// slot the item was selected from. If the source inventory changes
    /// under the flow (item moved/consumed elsewhere), the flow aborts.
    /// </summary>
    public sealed class ItemUseFlowController : MonoBehaviour
    {
        public ItemUseContext Context { get; } = new();
        public ItemUseStep Step { get; private set; } = ItemUseStep.Idle;

        /// <summary>Fired on every step change, including the return to Idle.</summary>
        public event Action<ItemUseStep, ItemUseContext> OnStepChanged;

        /// <summary>A target/confirmation was refused, or the flow aborted. Carries a player-facing reason.</summary>
        public event Action<string> OnRejected;

        public event Action<ItemUseResult> OnCompleted;
        public event Action OnCancelled;

        private InventorySystem _attachedSource;

        [Tooltip("Ligado: qualquer clique em slot inicia o fluxo. Desligado: só inicia via BeginWithItem (botão Usar dos painéis).")]
        [SerializeField] private bool _startOnSlotClick = false;

        private void OnEnable()
        {
            if (_startOnSlotClick)
                InventorySlotView.AnySlotClicked += HandleSlotClicked;
        }

        private void OnDisable()
        {
            InventorySlotView.AnySlotClicked -= HandleSlotClicked;
            if (Step != ItemUseStep.Idle) ResetFlow();
        }
        // ── Entry points (UI calls these) ────────────────────────────

        /// <summary>Step 1 — normally reached by clicking a slot; public for tests/other triggers.</summary>
        public void BeginWithItem(InventorySystem source, int slotIndex, IIITem item)
        {
            if (source == null || item == null) return;

            DetachSource();
            Context.SetItem(source, slotIndex, item);
            AttachSource(source);

            SetStep(Context.RequiresTarget ? ItemUseStep.SelectingTarget : ItemUseStep.AwaitingConfirmation);
        }

        /// <summary>Step 2 — the "which character" button. Returns false (and raises OnRejected) if the item can't be applied to it.</summary>
        public bool SelectTarget(AllyCharacterStatDefinition target)
        {
            if (Step == ItemUseStep.Idle || !Context.RequiresTarget || target == null) return false;

            var check = Context.TargetedItem.CanApplyTo(target);
            if (!check.CanApply)
            {
                OnRejected?.Invoke(check.Reason);
                return false;
            }

            Context.SetTarget(target);
            SetStep(ItemUseStep.AwaitingConfirmation);
            return true;
        }

        /// <summary>Goes back from confirmation to the character picker.</summary>
        public void ChangeTarget()
        {
            if (Step != ItemUseStep.AwaitingConfirmation || !Context.RequiresTarget) return;

            Context.ClearTarget();
            SetStep(ItemUseStep.SelectingTarget);
        }

        /// <summary>Step 3 — confirm. Applies the effect and consumes the item.</summary>
        public void Confirm()
        {
            if (Step != ItemUseStep.AwaitingConfirmation || !Context.IsReadyToConfirm) return;

            var source = Context.Source;
            int slotIndex = Context.SlotIndex;
            var item = Context.Item;
            var target = Context.Target;
            var targeted = Context.TargetedItem;

            if (!IsStillInSlot())
            {
                Abort("The selected item is no longer in that slot.");
                return;
            }

            if (targeted != null)
            {
                var check = targeted.CanApplyTo(target);
                if (!check.CanApply)
                {
                    Context.ClearTarget();
                    SetStep(ItemUseStep.SelectingTarget);
                    OnRejected?.Invoke(check.Reason); // after the step change so the view keeps the message
                    return;
                }
            }

            DetachSource(); // our own removal below must not abort the flow

            if (targeted != null)
                targeted.ExecuteItemEffect(target);
            else
                item.ExecuteItemEffect();

            int removed = source.TryRemoveFromSlot(slotIndex, item, 1);
            if (removed <= 0)
                Log(LogLevel.Error, $"Effect of '{item.StorageableId}' executed but the item could not be removed from slot {slotIndex} of '{source.name}'.");

            var result = new ItemUseResult(source, slotIndex, item, target);
            ResetFlow();
            OnCompleted?.Invoke(result);
        }

        /// <summary>Cancel — valid at any step.</summary>
        public void Cancel()
        {
            if (Step == ItemUseStep.Idle) return;

            ResetFlow();
            OnCancelled?.Invoke();
        }

        // ── Internals ────────────────────────────────────────────────

        private void HandleSlotClicked(InventorySlotView view)
        {
            var source = view.Owner;
            if (source == null)
            {
                Log(LogLevel.Warning, $"Slot view '{view.name}' has no owner InventorySystem — call InventorySlotView.SetOwner from the grid view.");
                return;
            }

            if (!source.TryGetSlot(view.SlotIndex, out var item, out int quantity) || item == null || quantity <= 0)
                return; // empty slot: ignore

            BeginWithItem(source, view.SlotIndex, item);
        }

        private void HandleSourceChanged(InventorySlotChangedEventArgs _)
        {
            if (Step == ItemUseStep.Idle) return;

            if (!IsStillInSlot())
                Abort("The selected item changed and is no longer available.");
        }

        private bool IsStillInSlot()
        {
            if (!Context.HasItem) return false;

            return Context.Source.TryGetSlot(Context.SlotIndex, out var current, out int quantity)
                   && quantity > 0
                   && current != null
                   && current.StorageableId == Context.Item.StorageableId;
        }

        private void Abort(string reason)
        {
            OnRejected?.Invoke(reason);
            Cancel();
        }

        private void AttachSource(InventorySystem source)
        {
            _attachedSource = source;
            _attachedSource.OnInventoryChanged += HandleSourceChanged;
        }

        private void DetachSource()
        {
            if (_attachedSource == null) return;

            _attachedSource.OnInventoryChanged -= HandleSourceChanged;
            _attachedSource = null;
        }

        private void ResetFlow()
        {
            DetachSource();
            Context.Clear();
            SetStep(ItemUseStep.Idle);
        }

        private void SetStep(ItemUseStep step)
        {
            Step = step;
            OnStepChanged?.Invoke(step, Context);
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[ItemUseFlowController:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}
