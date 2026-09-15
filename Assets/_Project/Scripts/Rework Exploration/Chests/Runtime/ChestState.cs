namespace Core.Chests
{
    /// <summary>
    /// Visual/interaction state of a chest, exposed purely for view
    /// consumers (animation, color, etc.) — carries no gameplay logic itself.
    /// </summary>
    public enum ChestState
    {
        Closed,
        Open
    }
}