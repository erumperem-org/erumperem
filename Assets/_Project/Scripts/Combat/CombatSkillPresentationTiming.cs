using System;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Duração de “leitura” da ação no ecrã + pausa extra antes do próximo passo de combate (por skill).
    /// </summary>
    [Serializable]
    public sealed class CombatSkillPresentationTiming
    {
        [Tooltip("Id da skill em skills.json (ex. slash_wulfric).")]
        public string skillId = string.Empty;

        [Tooltip("Tempo em que a animação / mensagem fica em destaque antes da pausa extra.")]
        [Min(0f)]
        public float playSeconds = 2.5f;

        [Tooltip("Pausa adicional após playSeconds antes do próximo actor (ex. 2s após um ataque de 4s).")]
        [Min(0f)]
        public float postPauseSeconds = 1.5f;
    }
}
