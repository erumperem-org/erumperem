using Game.Core.Domain;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Characters
{
    /// <summary>
    /// Stats de combate e estado inicial de exploração de um aliado jogável.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Erumperem/Characters/Ally Character Stat Definition",
        fileName = "AllyCharacterStatDefinition")]
    public sealed class AllyCharacterStatDefinition : ScriptableObject
    {
        [Header("Identificação")]
        [Tooltip("Deve coincidir com PlayableCharacter.CharacterName.")]
        [SerializeField] private string characterId;

        [SerializeField] private string displayName;

        [Header("Exploração")]
        [SerializeField] private PlayableCharacterState defaultExplorationState = PlayableCharacterState.Resting;

        [Header("Combate")]
        [Min(1)]
        [SerializeField] private int combatMaxHitPoints = 40;

        [Min(0)]
        [SerializeField] private int speed = 6;

        [Min(0)]
        [SerializeField] private double accuracy = 1.0;

        [Min(0)]
        [SerializeField] private double critChance = 0.05;

        [SerializeField] private double burnResistance = 0.15;
        [SerializeField] private double blightResistance = 0.15;
        [SerializeField] private double stunResistance = 0.15;

        [SerializeField] private ElementType elementType = ElementType.Fire;

        [Tooltip("ID em skill_trees.json (ex.: wulfric).")]
        [SerializeField] private string progressionCharacterId;

        [Header("Visual de combate")]
        [Tooltip("Prefab instanciado no slot ally_1/ally_2 (Animator + collider no root).")]
        [SerializeField] private GameObject battlePrefab;

        [Tooltip("1 = frente (Main), 2 = atrás (Companion).")]
        [Min(1)]
        [SerializeField] private int battleFormationRank = 1;

        public string CharacterId => characterId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? characterId : displayName;
        public PlayableCharacterState DefaultExplorationState => defaultExplorationState;
        public int CombatMaxHitPoints => combatMaxHitPoints;
        public string ProgressionCharacterId => progressionCharacterId;
        public GameObject BattlePrefab => battlePrefab;
        public int BattleFormationRank => battleFormationRank;

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
