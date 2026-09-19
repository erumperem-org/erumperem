using BarSystem.Core;

namespace BarSystem.Persistence
{
    /// <summary>
    /// Abstraction of "where and how" a bar's runtime state is saved/loaded.
    /// Replace the implementation (JSON, PlayerPrefs, cloud, save slots, etc.)
    /// without touching Core, Behaviors, View, or specific bar implementations.
    /// </summary>
    public interface IBarStateRepository
    {
        void Save(BarSaveData data);

        /// <returns>null if there is no saved state for the id.</returns>
        BarSaveData Load(string id);

        bool HasSavedState(string id);
    }
}