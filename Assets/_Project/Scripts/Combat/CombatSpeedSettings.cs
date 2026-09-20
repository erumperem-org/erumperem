using System;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Gerencia e persiste globalmente a preferência de velocidade de combate do jogador.
    /// Não altera Time.timeScale — a velocidade é consumida diretamente pelas animações e esperas de combate.
    /// </summary>
    public static class CombatSpeedSettings
    {
        private const string SpeedPrefKey = "CombatSpeedMultiplier";

        public static event Action<float> OnSpeedMultiplierChanged;

        /// <summary>
        /// Multiplicador de velocidade das ações e animações de combate.
        /// Valor padrão: 1.0f (velocidade normal).
        /// </summary>
        public static float SpeedMultiplier
        {
            get => PlayerPrefs.GetFloat(SpeedPrefKey, 1.0f);
            set
            {
                float clamped = Mathf.Max(0.1f, value);
                PlayerPrefs.SetFloat(SpeedPrefKey, clamped);
                OnSpeedMultiplierChanged?.Invoke(clamped);
            }
        }
    }
}