using System;

namespace BarSystem.Core
{
    /// <summary>
    /// State of a bar: current value, limits, and change notifications.
    /// Does not depend on UnityEngine — can be instantiated and tested in pure C#,
    /// outside the Unity Editor/Player.
    /// </summary>
    public class BarModel
    {
        public string Id { get; }
        public float Min { get; private set; }
        public float Max { get; private set; }
        public float Current { get; private set; }

        /// <summary>
        /// Normalized value between 0 and 1, ready to feed any view.
        /// </summary>
        public float Normalized => Max <= Min ? 0f : (Current - Min) / (Max - Min);

        public event Action<float> OnValueChanged;
        public event Action<float> OnMaxChanged;
        public event Action OnReachedMin;
        public event Action OnReachedMax;

        public BarModel(string id, float min, float max, float current)
        {
            Id = id;
            Min = min;
            Max = max;
            Current = ClampToRange(current);
        }

        /// <param name="keepNormalized">
        /// If true, adjusts the current value to maintain the same proportion (0-1)
        /// when changing the maximum (e.g., an "upgrade" that increases maximum health
        /// while maintaining the same health percentage).
        /// </param>
        public void SetMax(float newMax, bool keepNormalized = false)
        {
            float previousNormalized = Normalized;
            Max = newMax;

            float target = keepNormalized
                ? Min + previousNormalized * (Max - Min)
                : Current;

            OnMaxChanged?.Invoke(Max);
            SetCurrent(target);
        }

        public void SetCurrent(float value)
        {
            float clamped = ClampToRange(value);
            if (Math.Abs(clamped - Current) < float.Epsilon)
                return;

            Current = clamped;
            OnValueChanged?.Invoke(Current);

            if (Current <= Min) OnReachedMin?.Invoke();
            if (Current >= Max) OnReachedMax?.Invoke();
        }

        /// <summary>
        /// Adds (or subtracts, if negative) a value to the current value, with clamping.
        /// </summary>
        public void ApplyDelta(float delta) => SetCurrent(Current + delta);

        private float ClampToRange(float value)
        {
            if (value < Min) return Min;
            if (value > Max) return Max;
            return value;
        }
    }
}