using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Dados de um arquétipo inimigo: tier, elemento, peso de spawn e prefab de batalha (Animator Idle/Attack/Death).
    /// </summary>
    [CreateAssetMenu(menuName = "Erumperem/Combat/Enemy Visual Definition", fileName = "EnemyVisualDefinition")]
    public sealed class EnemyVisualDefinition : ScriptableObject
    {
        [Min(0)] public int tier = 0;

        public EnemyElementType elementType = EnemyElementType.Fire;

        [Tooltip("Peso relativo na tabela de spawn (não precisa somar 100).")]
        [Min(0f)] public float spawnWeight = 1f;

        [Tooltip("Root de batalha: collider + CombatCapsuleTag (runtime) + EnemyAnimationController + modelo com Animator.")]
        public GameObject battlePrefab;
    }
}
