// ============================================================
// PlayerAwareSpawnPointSelector.cs
// Namespace : Systems.NPC.Spawner
// ============================================================
// Responsabilidade única: selecionar spawn points fora do
// raio de visão do Player, ordenados por proximidade.
//
// Extraído de NpcEnemySpawner onde era uma responsabilidade
// secundária embutida nos métodos SpawnOne/ExecuteSpawnBatch.
// ============================================================

using UnityEngine;

namespace Systems.NPC.Spawner
{
    public sealed class PlayerAwareSpawnPointSelector : ISpawnPointSelector
    {
        private readonly Transform[] _points;
        private readonly Transform   _player;
        private readonly float       _visionRadiusSq;

        private Transform[] _sorted   = new Transform[0];
        private int         _index;
        private bool        _dirty    = true;

        public PlayerAwareSpawnPointSelector(
            Transform[] points,
            Transform   player,
            float       visionRadius)
        {
            _points         = points;
            _player         = player;
            _visionRadiusSq = visionRadius * visionRadius;
        }

        public bool HasAny
        {
            get
            {
                Rebuild();
                return _sorted.Length > 0;
            }
        }

        public Transform Next()
        {
            Rebuild();
            if (_sorted.Length == 0) return null;

            var point = _sorted[_index % _sorted.Length];
            _index++;
            _dirty = true; // recalcula no próximo chamado
            return point;
        }

        // ── Rebuild ───────────────────────────────────────────────────────

        private void Rebuild()
        {
            if (!_dirty) return;
            _dirty = false;

            Vector3 playerPos = _player != null ? _player.position : Vector3.zero;

            int validCount = 0;
            var temp = new Transform[_points.Length];

            foreach (var p in _points)
            {
                if (p == null) continue;
                if ((p.position - playerPos).sqrMagnitude < _visionRadiusSq) continue;
                temp[validCount++] = p;
            }

            // Insertion sort por distância ao player (crescente)
            for (int i = 1; i < validCount; i++)
            {
                var   key     = temp[i];
                float keyDist = (key.position - playerPos).sqrMagnitude;
                int   j       = i - 1;

                while (j >= 0 && (temp[j].position - playerPos).sqrMagnitude > keyDist)
                {
                    temp[j + 1] = temp[j];
                    j--;
                }
                temp[j + 1] = key;
            }

            if (_sorted.Length != validCount)
                _sorted = new Transform[validCount];

            System.Array.Copy(temp, _sorted, validCount);

            if (_sorted.Length > 0)
                _index = _index % _sorted.Length;
        }
    }
}
