using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Identifica a cápsula visual com o <see cref="Game.Core.Models.Combatant.Identity.Id"/> do motor.
    /// </summary>
    public sealed class CombatCapsuleTag : MonoBehaviour
    {
        [Tooltip("Deve coincidir com Identity.Id (ex.: ally_1, enemy_2).")]
        public string combatantId = "";
    }
}
