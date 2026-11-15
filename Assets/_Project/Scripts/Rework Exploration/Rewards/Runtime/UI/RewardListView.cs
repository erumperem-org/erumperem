using System.Collections.Generic;
using Core.Rewards.Distribution;
using UnityEngine;

namespace Core.Rewards.UI
{
    /// <summary>Spawns one RewardView per RewardLine. Replaces the previous list on every Show.</summary>
    public sealed class RewardListView : MonoBehaviour
    {
        [SerializeField] private RewardView _rewardViewPrefab;
        [SerializeField] private Transform _container;

        private readonly List<RewardView> _spawned = new();

        public void Show(IReadOnlyList<RewardLine> lines)
        {
            Clear();

            if (_rewardViewPrefab == null || _container == null || lines == null) return;

            foreach (var line in lines)
            {
                var view = Instantiate(_rewardViewPrefab, _container);
                view.Show(line);
                _spawned.Add(view);
            }
        }

        public void Clear()
        {
            foreach (var view in _spawned)
                if (view != null) Destroy(view.gameObject);

            _spawned.Clear();
        }
    }
}
