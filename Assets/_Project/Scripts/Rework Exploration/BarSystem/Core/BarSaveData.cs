using System;

namespace BarSystem.Core
{
    /// <summary>
    /// Represents the runtime state of a bar for save/load purposes.
    /// Deliberately simple and serializable (compatible with JsonUtility).
    /// </summary>
    [Serializable]
    public class BarSaveData
    {
        public string Id;
        public float Current;
        public float Max;

        public BarSaveData() { }

        public BarSaveData(string id, float current, float max)
        {
            Id = id;
            Current = current;
            Max = max;
        }
    }
}