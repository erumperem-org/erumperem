using Services.Navigation;
using UnityEngine;

namespace Services.Spawning
{
    /// <summary>
    /// Implementação de <see cref="ISpawnPositionService"/> usando <see cref="NavMeshService"/>.
    ///
    /// Delega toda query de NavMesh ao serviço já existente — não acessa
    /// <see cref="UnityEngine.AI.NavMesh"/> diretamente, sem duplicar a lógica
    /// de <see cref="NavMeshService.SamplePosition"/>.
    ///
    /// Centro e raio padrão são configurados via construtor ou no Inspector
    /// quando usado como <see cref="UnityEngine.MonoBehaviour"/> wrapper.
    /// </summary>
    public sealed class NavMeshSpawnPositionService : ISpawnPositionService
    {
        private readonly INavMeshService _navMesh;
        private readonly Vector3 _defaultCenter;
        private readonly float _defaultRadius;

        /// <param name="navMesh">Serviço de NavMesh já inicializado.</param>
        /// <param name="defaultCenter">Centro padrão usado em <see cref="GetPosition()"/>.</param>
        /// <param name="defaultRadius">Raio padrão usado em <see cref="GetPosition()"/>.</param>
        public NavMeshSpawnPositionService(
            INavMeshService navMesh,
            Vector3 defaultCenter,
            float defaultRadius)
        {
            _navMesh = navMesh;
            _defaultCenter = defaultCenter;
            _defaultRadius = defaultRadius;
        }

        // ── ISpawnPositionService ──────────────────────────────────────────────

        /// <inheritdoc/>
        public Vector3 GetPosition(Vector3 center, float radius)
        {
            if (_navMesh == null)
                return Vector3.zero;

            Vector3 candidate = center + Random.insideUnitSphere * radius;
            candidate.y = center.y; // mantém altura de referência antes do sample

            return _navMesh.SamplePosition(candidate, out Vector3 result, radius)
                ? result
                : Vector3.zero;
        }

        /// <inheritdoc/>
        public Vector3 GetPosition()
            => GetPosition(_defaultCenter, _defaultRadius);

        /// <inheritdoc/>
        public bool TryGetPosition(Vector3 center, float radius, out Vector3 result)
        {
            if (_navMesh == null)
            {
                result = Vector3.zero;
                return false;
            }

            Vector3 candidate = center + Random.insideUnitSphere * radius;
            candidate.y = center.y;

            if (_navMesh.SamplePosition(candidate, out result, radius))
                return true;

            result = Vector3.zero;
            return false;
        }

        public bool TryGetPosition(out Vector3 result) => TryGetPosition(_defaultCenter, _defaultRadius, out result);
    }
}