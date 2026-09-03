using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Gerencia e persiste globalmente a preferência de velocidade de combate do jogador.
    /// </summary>
    public static class CombatSpeedSettings
    {
        private const string SpeedPrefKey = "CombatSpeedMultiplier";

        /// <summary>
        /// Multiplicador de velocidade das ações e animações de combate.
        /// Valor padrão: 1.0f (velocidade normal).
        /// </summary>
        public static float SpeedMultiplier
        {
            get => PlayerPrefs.GetFloat(SpeedPrefKey, 1.0f);
            set => PlayerPrefs.SetFloat(SpeedPrefKey, Mathf.Max(0.1f, value));
        }
    }
}