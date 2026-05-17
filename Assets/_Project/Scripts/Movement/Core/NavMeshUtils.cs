using UnityEngine;
using UnityEngine.AI;

namespace Core.Exploration.Character.Movement
{
    /// <summary>
    /// Utilitários de NavMesh que não dependem de um agente específico.
    /// Centraliza chamadas à API estática do Unity para evitar duplicação nos behaviors.
    /// </summary>
    internal static class NavMeshUtils
    {
        /// <summary>
        /// Amostra o ponto válido mais próximo na NavMesh.
        /// Retorna true e preenche <paramref name="result"/> se encontrado.
        /// </summary>
        public static bool SamplePosition(Vector3 source, out Vector3 result, float maxDistance,
            int areaMask = NavMesh.AllAreas)
        {
            result = source;
            if (!NavMesh.SamplePosition(source, out var hit, maxDistance, areaMask)) return false;
            result = hit.position;
            return true;
        }
    }
}
