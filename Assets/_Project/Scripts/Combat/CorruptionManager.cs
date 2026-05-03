using System;
using Game.Core.Analytics;
using Game.Core.Config;
using Game.Core.Domain;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Espelho global da corrupção para menus / HUD e evento exclusivo de mudança de tier.
    /// Sincronizado a partir dos eventos de combate via <see cref="NotifyCombatCorruptionAdjusted"/>.
    /// </summary>
    public sealed class CorruptionManager : MonoBehaviour
    {
        public static CorruptionManager Instance { get; private set; }

        /// <summary>Invocado apenas quando o tier efetivo muda (subida ou descida).</summary>
        public event Action<int, int> OnCorruptionTierChanged;

        /// <summary>Invocado sempre que o valor espelhado de corrupção muda (combate ou <see cref="SetCorruptionValue"/>).</summary>
        public event Action<double> OnMirrorCorruptionValueChanged;

        [SerializeField] private double mirrorCorruptionValue;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public double GetCorruptionValue() => mirrorCorruptionValue;

        public void SetCorruptionValue(double value)
        {
            var tierBefore = CorruptionTierCalculator.GetTier(mirrorCorruptionValue);
            mirrorCorruptionValue = Math.Max(CorruptionRules.MinCorruptionValue, value);
            OnMirrorCorruptionValueChanged?.Invoke(mirrorCorruptionValue);
            var tierAfter = CorruptionTierCalculator.GetTier(mirrorCorruptionValue);
            if (tierBefore != tierAfter)
            {
                OnCorruptionTierChanged?.Invoke(tierBefore, tierAfter);
            }
        }

        /// <summary>
        /// Atualiza o espelho e emite <see cref="OnCorruptionTierChanged"/> quando o tier mudou neste passo.
        /// </summary>
        public void NotifyCombatCorruptionAdjusted(CombatEvent combatEvent)
        {
            if (combatEvent.EventType != BattleEventType.CorruptionAdjusted)
            {
                return;
            }

            mirrorCorruptionValue = combatEvent.CorruptionValue;
            OnMirrorCorruptionValueChanged?.Invoke(mirrorCorruptionValue);

            if (combatEvent.PreviousCorruptionTier.HasValue &&
                combatEvent.PreviousCorruptionTier.Value != combatEvent.CorruptionTier)
            {
                OnCorruptionTierChanged?.Invoke(combatEvent.PreviousCorruptionTier.Value, combatEvent.CorruptionTier);
            }
        }
    }
}
