using System;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Recebe o Animation Event <c>Impact</c> dos clips de ataque. Deve ficar no mesmo GameObject do Animator;
    /// o orquestrador adiciona automaticamente se faltar. Marca o instante do Target Focus no Action Shot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatAttackImpactRelay : MonoBehaviour
    {
        public event Action Impacted;

        /// <summary>Chamado pelo Animation Event <c>Impact</c> no frame em que o golpe acerta.</summary>
        public void Impact() => Impacted?.Invoke();
    }
}
