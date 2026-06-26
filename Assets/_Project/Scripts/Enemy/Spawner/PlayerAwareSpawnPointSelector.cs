// ============================================================
// PlayerAwareSpawnPointSelector.cs
// Namespace : Systems.NPC.Spawner
// ============================================================
// Responsabilidade única: selecionar spawn points fora do
// raio mínimo de visão do Player e dentro do raio máximo,
// ordenados por proximidade ao player.
//
// CORREÇÕES:
//   [5] Adicionado _maxRadiusSq: pontos muito distantes do player
//       são descartados, garantindo que o player possa encontrar
//       os inimigos. maxSpawnRadius = 0 desativa o limite máximo.
//   [1] O construtor agora recebe o Transform por referência —
//       como Transform é um objeto Unity, a posição sempre
//       reflete o Main atual após RebuildSelector no Spawner.
// ============================================================

using UnityEngine;

namespace Systems.NPC.Spawner
{
    public sealed class PlayerAwareSpawnPointSelector : ISpawnPointSelector
    {
        private readonly Transform[] _points;
        private readonly Transform   _player;
        private readonly float       _minRadiusSq;

        // [5] 0 = sem limite máximo.
        private readonly float _maxRadiusSq;

        private Transform[] _sorted = new Transform[0];
        private int         _index;
        private bool        _dirty  = true;

        /// <param name="minSpawnRadius">
        ///   Distância mínima do player para spawnar (raio de visão).
        /// </param>
        /// <param name="maxSpawnRadius">
        ///   Distância máxima do player para spawnar.
        ///   Use 0 para sem limite.
        /// </param>
        public PlayerAwareSpawnPointSelector(
            Transform[] points,
            Transform   player,
            float       minSpawnRadius,
            float       maxSpawnRadius = 0f)
        {
            _points      = points;
            _player      = player;
            _minRadiusSq = minSpawnRadius * minSpawnRadius;
            _maxRadiusSq = maxSpawnRadius > 0f ? maxSpawnRadius * maxSpawnRadius : float.MaxValue;
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
            _dirty = true; // recalcula na próxima chamada (posição do player pode ter mudado)
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

                float distSq = (p.position - playerPos).sqrMagnitude;

                // [5] Descarta pontos dentro do raio mínimo (visão) OU além do raio máximo.
                if (distSq < _minRadiusSq) continue;
                if (distSq > _maxRadiusSq) continue;

                temp[validCount++] = p;
            }

            // Insertion sort por distância ao player (crescente — spawn no mais próximo válido).
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
