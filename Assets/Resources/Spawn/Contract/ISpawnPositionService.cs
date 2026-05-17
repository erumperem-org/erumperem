using UnityEngine;

namespace Services.Spawning
{
    /// <summary>
    /// Contrato para resolução de posições de spawn válidas no NavMesh.
    ///
    /// Responsabilidade única: dado um contexto (centro + raio), retornar
    /// um <see cref="Vector3"/> garantidamente navegável.
    ///
    /// O que NÃO é responsabilidade desta interface:
    ///   • Não instancia objetos.
    ///   • Não move agentes.
    ///   • Não conhece pools nem builders.
    /// </summary>
    public interface ISpawnPositionService
    {
        /// <summary>
        /// Retorna um ponto aleatório navegável dentro de <paramref name="radius"/>
        /// a partir de <paramref name="center"/>.
        /// Retorna <see cref="Vector3.zero"/> se nenhum ponto for encontrado.
        /// </summary>
        Vector3 GetPosition(Vector3 center, float radius);

        /// <summary>
        /// Variante com centro e raio pré-configurados no serviço (contexto fixo de spawn).
        /// Útil quando a pool tem uma única área de spawn definida em design time.
        /// </summary>
        Vector3 GetPosition();

        /// <summary>
        /// Retorna true e preenche <paramref name="result"/> se um ponto navegável
        /// foi encontrado. Permite ao caller tratar o caso de falha explicitamente
        /// em vez de receber Vector3.zero silenciosamente.
        /// </summary>
        bool TryGetPosition(Vector3 center, float radius, out Vector3 result);

        /// <summary>
        /// Variante usando centro e raio padrão configurados no serviço.
        /// </summary>
        bool TryGetPosition(out Vector3 result);
    }
}