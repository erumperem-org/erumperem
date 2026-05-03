using System;
using System.Collections.Generic;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Ponto único de subscrição para o fluxo de combate (UI, câmera, log). O
    /// <see cref="CombatPrototypeController"/> apenas chama os métodos Raise*; não conhece listeners.
    /// </summary>
    public sealed class CombatSessionHub : MonoBehaviour
    {
        public event Action OnTurnStarted;
        public event Action OnTurnEnded;

        public event Action<Combatant> OnPlayerCommandRequired;

        public event Action OnSkillBarBindingShouldSync;
        public event Action OnSkillBarSelectionClearedBySession;

        public event Action<string> OnPlayerSkillHelpText;

        public event Action<IReadOnlyList<string>> OnNarrativeLines;

        public event Action OnActionPresentationStarted;
        public event Action OnActionPresentationEnded;

        public event Action OnCombatSessionClosed;

        public event Action<CombatPrototypeController> OnCombatSessionReadyForUi;

        public event Action<Transform, Transform> OnCinemachineFocusBegan;
        public event Action OnCinemachineFocusEnded;

        /// <summary>delta, newValue, previousTier (nullable before first combat sync), newTier.</summary>
        public event Action<double, double, int?, int> OnBattleCorruptionAdjusted;

        /// <summary>Fires only when corruption tier crosses (exclusive).</summary>
        public event Action<int, int> OnBattleCorruptionTierReached;

        /// <summary>Positive corruption delta for lightweight presentation hooks.</summary>
        public event Action<double> OnBattleCorruptionIncreasePulse;

        internal void RaiseTurnStarted() => OnTurnStarted?.Invoke();

        internal void RaiseTurnEnded() => OnTurnEnded?.Invoke();

        internal void RaisePlayerCommandRequired(Combatant pendingPlayer) =>
            OnPlayerCommandRequired?.Invoke(pendingPlayer);

        internal void RaiseSkillBarBindingShouldSync() => OnSkillBarBindingShouldSync?.Invoke();

        internal void RaiseSkillBarSelectionClearedBySession() =>
            OnSkillBarSelectionClearedBySession?.Invoke();

        internal void RaisePlayerSkillHelpText(string text) => OnPlayerSkillHelpText?.Invoke(text);

        internal void RaiseNarrativeLines(IReadOnlyList<string> lines) =>
            OnNarrativeLines?.Invoke(lines);

        internal void RaiseActionPresentationStarted() => OnActionPresentationStarted?.Invoke();

        internal void RaiseActionPresentationEnded() => OnActionPresentationEnded?.Invoke();

        internal void RaiseCombatSessionClosed() => OnCombatSessionClosed?.Invoke();

        internal void RaiseCombatSessionReadyForUi(CombatPrototypeController controller) =>
            OnCombatSessionReadyForUi?.Invoke(controller);

        internal void RaiseCinemachineFocusBegan(Transform actorRoot, Transform targetRoot) =>
            OnCinemachineFocusBegan?.Invoke(actorRoot, targetRoot);

        internal void RaiseCinemachineFocusEnded() => OnCinemachineFocusEnded?.Invoke();

        internal void RaiseBattleCorruptionAdjusted(double delta, double newCorruptionValue, int? previousTier, int newTier) =>
            OnBattleCorruptionAdjusted?.Invoke(delta, newCorruptionValue, previousTier, newTier);

        internal void RaiseBattleCorruptionTierReached(int previousTier, int newTier) =>
            OnBattleCorruptionTierReached?.Invoke(previousTier, newTier);

        internal void RaiseBattleCorruptionIncreasePulse(double positiveDelta) =>
            OnBattleCorruptionIncreasePulse?.Invoke(positiveDelta);
    }
}
