using Game.Core.Domain;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Characters
{
    /// <summary>
    /// Stats base de um personagem (aliado jogável ou inimigo) para exploração e combate.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Erumperem/Characters/Character Stat Definition",
        fileName = "CharacterStatDefinition")]
    public sealed class CharacterStatDefinition : ScriptableObject
    {
        [Header("Identificação")]
        [Tooltip("Chave única — deve coincidir com PlayableCharacter.CharacterName ou characterStatId do inimigo.")]
        [SerializeField] private string characterId;

        [SerializeField] private string displayName;

        [Tooltip("Aliados jogáveis usam os campos de exploração; inimigos ignoram-nos.")]
        [SerializeField] private bool isPlayableAlly;

        [Header("Exploração (aliados)")]
        [SerializeField] private PlayableCharacterState defaultExplorationState = PlayableCharacterState.Resting;

        [Min(1f)]
        [SerializeField] private float explorationMaxHealth = 100f;

        [Tooltip("HP inicial. Zero = HP cheio (explorationMaxHealth).")]
        [Min(0f)]
        [SerializeField] private float defaultStartingHealth;

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
        [SerializeField] private double moveResistance = 0.15;
        [SerializeField] private double stunResistance = 0.15;
        [SerializeField] private double deathblowResistance = 0.15;

        [SerializeField] private ElementType elementType = ElementType.Fire;

        [Tooltip("ID em skill_trees.json (ex.: wulfric). Só relevante para aliados.")]
        [SerializeField] private string progressionCharacterId;

        public string CharacterId => characterId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? characterId : displayName;
        public bool IsPlayableAlly => isPlayableAlly;
        public PlayableCharacterState DefaultExplorationState => defaultExplorationState;
        public float ExplorationMaxHealth => explorationMaxHealth;
        public float DefaultStartingHealth => defaultStartingHealth;
        public int CombatMaxHitPoints => combatMaxHitPoints;
        public string ProgressionCharacterId => progressionCharacterId;

        public float ResolveStartingHealth()
        {
            if (defaultStartingHealth <= 0f)
            {
                return explorationMaxHealth;
            }

            return Mathf.Clamp(defaultStartingHealth, 0f, explorationMaxHealth);
        }

        public void ApplyToCombatant(Combatant combatant, bool preserveCurrentHitPoints = false)
        {
            if (combatant == null)
            {
                return;
            }

            var maxHitPoints = Mathf.Max(1, combatMaxHitPoints);
            var currentHitPoints = preserveCurrentHitPoints
                ? Mathf.Clamp(combatant.Health.CurrentHp, 0, maxHitPoints)
                : maxHitPoints;

            combatant.Health = new HealthComponent
            {
                MaxHp = maxHitPoints,
                CurrentHp = currentHitPoints,
                IsDead = currentHitPoints <= 0,
                IsDeathblowPending = false,
            };

            combatant.Stats = new StatsComponent
            {
                Speed = speed,
                Accuracy = accuracy,
                CritChance = critChance,
            };

            combatant.Resistances = new ResistanceComponent
            {
                BurnRes = burnResistance,
                BlightRes = blightResistance,
                MoveRes = moveResistance,
                StunRes = stunResistance,
                DeathblowRes = deathblowResistance,
            };

            combatant.ElementAffinity = new ElementAffinityComponent
            {
                Element = elementType,
            };

            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                var existingIdentity = combatant.Identity;
                combatant.Identity = new IdentityComponent
                {
                    Id = existingIdentity.Id,
                    DisplayName = DisplayName,
                    Faction = existingIdentity.Faction,
                    Tags = existingIdentity.Tags,
                };
            }
        }
    }
}
