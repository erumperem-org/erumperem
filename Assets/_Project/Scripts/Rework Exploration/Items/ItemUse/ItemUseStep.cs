namespace Core.Inventory.UI.ItemUse
{
    /// <summary>Where the item-use flow currently is. Cancel is valid in every step except Idle.</summary>
    public enum ItemUseStep
    {
        /// <summary>Nothing selected.</summary>
        Idle,

        /// <summary>Item selected; waiting for the player to pick which character it applies to.</summary>
        SelectingTarget,

        /// <summary>Everything needed is in the context; waiting for confirm or cancel.</summary>
        AwaitingConfirmation
    }
}
