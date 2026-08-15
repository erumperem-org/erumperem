using System;
using UnityEngine;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Idempotent subscribe/unsubscribe to <see cref="CombatSessionHub"/> session lifecycle events
    /// plus catch-up when UI enables mid-battle (AUDITORIA DRY #54).
    /// </summary>
    public sealed class CombatSessionHubSubscription
    {
        private CombatSessionHub _sessionHub;
        private Action<CombatPrototypeController> _onCombatSessionReadyForUi;
        private Action _onCombatSessionClosed;

        public void Subscribe(
            CombatSessionHub sessionHub,
            Action<CombatPrototypeController> onCombatSessionReadyForUi,
            Action onCombatSessionClosed)
        {
            Unsubscribe();

            _sessionHub = sessionHub;
            _onCombatSessionReadyForUi = onCombatSessionReadyForUi;
            _onCombatSessionClosed = onCombatSessionClosed;

            if (_sessionHub == null)
            {
                return;
            }

            _sessionHub.OnCombatSessionReadyForUi -= DispatchCombatSessionReadyForUi;
            _sessionHub.OnCombatSessionClosed -= DispatchCombatSessionClosed;
            _sessionHub.OnCombatSessionReadyForUi += DispatchCombatSessionReadyForUi;
            _sessionHub.OnCombatSessionClosed += DispatchCombatSessionClosed;
        }

        public void Unsubscribe()
        {
            if (_sessionHub != null)
            {
                _sessionHub.OnCombatSessionReadyForUi -= DispatchCombatSessionReadyForUi;
                _sessionHub.OnCombatSessionClosed -= DispatchCombatSessionClosed;
            }

            _sessionHub = null;
            _onCombatSessionReadyForUi = null;
            _onCombatSessionClosed = null;
        }

        public void TryCatchUpWithActiveCombatSession(CombatPrototypeController currentlyBoundCombatSession)
        {
            if (currentlyBoundCombatSession != null)
            {
                return;
            }

            var activeCombatSession = UnityEngine.Object.FindFirstObjectByType<CombatPrototypeController>();
            if (activeCombatSession != null && activeCombatSession.IsBattleOngoing)
            {
                DispatchCombatSessionReadyForUi(activeCombatSession);
            }
        }

        private void DispatchCombatSessionReadyForUi(CombatPrototypeController combatSession) =>
            _onCombatSessionReadyForUi?.Invoke(combatSession);

        private void DispatchCombatSessionClosed() =>
            _onCombatSessionClosed?.Invoke();
    }
}
