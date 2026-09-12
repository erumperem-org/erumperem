using System.Collections.Generic;
using UnityEngine;

namespace Erumperem.Combat.Authoring
{
    /// <summary>
    /// Optional grouping of combat abilities for one hero. Export scans every CombatAbilityAsset;
    /// listing them here is for designers, not required for the catalog to write.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterCombatKit", menuName = "Erumperem/Combat/Character Kit")]
    public sealed class CharacterCombatKitAsset : ScriptableObject
    {
        [Tooltip("Character id this kit belongs to (wulfric, buck, maria). Not Matsuda.")]
        [SerializeField] private string _characterId = "";

        [Tooltip("Abilities in this kit. Duplicate a CombatAbilityAsset, fill it, then drag it here.")]
        [SerializeField] private List<CombatAbilityAsset> _abilities = new();

        public string CharacterId => _characterId?.Trim() ?? "";
        public IReadOnlyList<CombatAbilityAsset> Abilities => _abilities ?? new List<CombatAbilityAsset>();
    }
}
