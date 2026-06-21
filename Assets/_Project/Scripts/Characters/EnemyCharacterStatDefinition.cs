using Game.Core.Domain;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Characters
{
    /// <summary>
    /// Stats de combate de um arquétipo inimigo.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Erumperem/Characters/Enemy Character Stat Definition",
        fileName = "EnemyCharacterStatDefinition")]
    public sealed class EnemyCharacterStatDefinition : ScriptableObject
    {
        [Header("Identificação")]
        [Tooltip("Deve coincidir com EnemyVisualDefinition.characterStatId ou nome derivado do asset.")]
        [SerializeField] private string characterId;

        [SerializeField] private string displayName;

        [Header("Combate")]
        [Min(1)]
        [SerializeField] private int combatMaxHitPoints = 20;

        [Min(0)]
        [SerializeField] private int speed = 4;

        [Min(0)]
        [SerializeField] private double accuracy = 1.0;

        [Min(0)]
        [SerializeField] private double critChance = 0.03;

        [SerializeField] private double burnResistance = 0.05;
        [SerializeField] private double blightResistance = 0.05;
        [SerializeField] private double stunResistance = 0.05;

        [SerializeField] private ElementType elementType = ElementType.Anomaly;

        public string CharacterId => characterId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? characterId : displayName;

        public void ApplyToCombatant(Combatant combatant, bool preserveCurrentHitPoints = false)
        {
            CharacterCombatStatApplicator.Apply(
                combatant,
                DisplayName,
                combatMaxHitPoints,
                speed,
                accuracy,
                critChance,
                burnResistance,
                blightResistance,
                stunResistance,
                elementType,
                preserveCurrentHitPoints);
        }
    }
}
