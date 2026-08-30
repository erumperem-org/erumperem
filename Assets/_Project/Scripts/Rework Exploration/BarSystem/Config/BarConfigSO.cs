using UnityEngine;

namespace BarSystem.Config
{
    /// <summary>
    /// Design-time configuration for a bar type (default values, color).
    /// Runtime state persistence (current value) is a separate
    /// responsibility — see Persistence/IBarStateRepository.
    /// </summary>
    [CreateAssetMenu(menuName = "BarSystem/Bar Config", fileName = "NewBarConfig")]
    public class BarConfigSO : ScriptableObject
    {
        [Tooltip("Unique identifier used as the key when saving/loading this bar's state.")]
        public string Id;

        [Header("Default Values (Used When No Save Exists)")]
        public float MinDefault = 0f;
        public float MaxDefault = 100f;
        public float CurrentDefault = 100f;

        [Header("Visual")]
        public Color BarColor = Color.white;
    }
}