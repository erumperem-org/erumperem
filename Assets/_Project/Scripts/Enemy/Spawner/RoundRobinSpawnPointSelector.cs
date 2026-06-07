// ============================================================
// RoundRobinSpawnPointSelector.cs
// Namespace : Systems.NPC.Spawner
// ============================================================
// Responsabilidade única: selecionar spawn points em round-robin
// simples, sem filtro de visão do Player.
// ============================================================

using UnityEngine;

namespace Systems.NPC.Spawner
{
    public sealed class RoundRobinSpawnPointSelector : ISpawnPointSelector
    {
        private readonly Transform[] _points;
        private int _index;

        public RoundRobinSpawnPointSelector(Transform[] points)
        {
            _points = points;
        }

        public bool HasAny => _points != null && _points.Length > 0;

        public Transform Next()
        {
            if (!HasAny) return null;
            var point = _points[_index % _points.Length];
            _index++;
            return point;
        }
    }
}
