using System;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Dados de um arquétipo inimigo: tier, elemento, peso de spawn, prefab de batalha
    /// (Animator Idle/Attack/Death) e (opcional) loadout de skills do Combatant.
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

        [Tooltip("Loadout de skills usado quando este arquétipo é instanciado num slot inimigo. " +
                 "Se vazio, mantém o loadout default que o BattleFactory atribuiu. " +
                 "Para variantes (ex.: CorruptedMiner Ametista), inclua a skill especial dessa variante.")]
        public string[] enemySkillIds = Array.Empty<string>();
    }
}
