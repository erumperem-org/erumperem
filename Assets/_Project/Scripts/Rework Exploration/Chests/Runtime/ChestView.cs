using UnityEngine;
using Core.Exploration.Items;

namespace Core.Chests
{
    /// <summary>
    /// View layer for a Chest: listens to two of the chest's own events —
    /// rarity reveal (drives the chest's color via the shared
    /// RarityColorPalette) and state change (open/closed, intended to drive
    /// an Animator trigger). Animation wiring is intentionally left
    /// commented out; it will be implemented once the Animator Controller
    /// and its trigger parameter names are finalized.
    /// </summary>
    [RequireComponent(typeof(Chest))]
    public sealed class ChestView : MonoBehaviour
    {
        [SerializeField] private Chest _chest;

        [Header("Rarity Color")]
        [Tooltip("Renderer whose material color is driven by the chest's best item rarity.")]
        [SerializeField] private Renderer _renderer;

        [Tooltip("Shared, project-wide rarity → color mapping. Do not duplicate this data locally.")]
        [SerializeField] private RarityColorPalette _rarityColorPalette;

        // ── Animation (not yet implemented) ─────────────────────────
        // [Header("Animation")]
        // [SerializeField] private Animator _animator;
        // [SerializeField] private string _openTriggerName = "Open";
        // [SerializeField] private string _closeTriggerName = "Close";

        private void Reset()
        {
            _chest = GetComponent<Chest>();
            _renderer = GetComponentInChildren<Renderer>();
        }

        private void OnEnable()
        {
            if (_chest == null) _chest = GetComponent<Chest>();
            if (_chest == null) return;

            _chest.OnBestItemRarityRevealed += HandleBestItemRarityRevealed;
            _chest.OnChestStateChanged += HandleChestStateChanged;
        }

        private void OnDisable()
        {
            if (_chest == null) return;

            _chest.OnBestItemRarityRevealed -= HandleBestItemRarityRevealed;
            _chest.OnChestStateChanged -= HandleChestStateChanged;
        }

        private void HandleBestItemRarityRevealed(ItemRarity rarity)
        {
            if (_renderer == null || _rarityColorPalette == null) return;

            _renderer.material.color = _rarityColorPalette.GetColor(rarity);
        }

        private void HandleChestStateChanged(ChestState state)
        {
            // Animation wiring intentionally left commented out — to be
            // implemented once the Animator Controller and its trigger
            // parameter names are finalized.
            //
            // switch (state)
            // {
            //     case ChestState.Open:
            //         _animator.SetTrigger(_openTriggerName);
            //         break;
            //     case ChestState.Closed:
            //         _animator.SetTrigger(_closeTriggerName);
            //         break;
            // }
        }
    }
}