using Services.Navigation;
using UnityEngine;

namespace Services.Spawning
{
    /// <summary>
    /// Wrapper MonoBehaviour de <see cref="NavMeshSpawnPositionService"/>.
    /// Permite configurar centro e raio no Inspector e ser referenciado
    /// como campo serializado na pool sem quebrar a injeção de dependência.
    ///
    /// A lógica real vive em <see cref="NavMeshSpawnPositionService"/> —
    /// este componente só faz a ponte entre Unity e o serviço puro.
    /// </summary>
    public sealed class NavMeshSpawnPositionServiceMono
        : MonoBehaviour, ISpawnPositionService
    {
        [Header("Configuração de spawn")]
        [Tooltip("Centro da área de spawn. Se vazio, usa a posição do GameObject.")]
        [SerializeField] private Vector3 _center = Vector3.zero;

        [Tooltip("Raio de busca aleatória no NavMesh.")]
        [SerializeField] private float _radius = 50f;

        [Tooltip("NavMeshService compartilhado. Deve ser o mesmo usado pelos agentes da pool.")]
        [SerializeField] private NavMeshService _navMeshService;

        private ISpawnPositionService _service;

        private void Awake()
        {
            // Se centro não foi configurado, usa a posição do próprio GameObject
            Vector3 center = _center == Vector3.zero ? transform.position : _center;

            _service = new NavMeshSpawnPositionService(_navMeshService, center, _radius);
        }

        // ── ISpawnPositionService (delega para o serviço puro) ─────────────────

        public Vector3 GetPosition(Vector3 center, float radius)
            => _service.GetPosition(center, radius);

        public Vector3 GetPosition()
            => _service.GetPosition();

        public bool TryGetPosition(Vector3 center, float radius, out Vector3 result)
            => _service.TryGetPosition(center, radius, out result);

        public bool TryGetPosition(out Vector3 result)
=> _service.TryGetPosition(out result);

        // ── Gizmo de debug ────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.2f);
            Gizmos.DrawSphere(_center == Vector3.zero ? transform.position : _center, _radius);
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(_center == Vector3.zero ? transform.position : _center, _radius);
        }
    }
}