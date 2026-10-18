using Erumperem.Combat;
using UnityEngine;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class CombatInputHoverMarkerBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CombatInputController inputController;
    [SerializeField] private CombatPrototypeController combatSession;
    [SerializeField] private CombatSkillButtonBarUIManager skillBarUiManager;
    [SerializeField] private CombatHoverFocusMarker hoverFocusMarker;

    private void Awake()
    {
        ResolveReferences();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (inputController == null || combatSession == null || hoverFocusMarker == null)
        {
            return;
        }

        if (InputDeviceModeManager.IsMouseMode)
        {
            hoverFocusMarker.ClearExternalTarget();
            return;
        }

        if (inputController.CurrentPhase != CombatInputPhase.TargetSelection ||
            string.IsNullOrEmpty(inputController.FocusedTargetCombatantId))
        {
            hoverFocusMarker.ClearExternalTarget();
            return;
        }

        if (skillBarUiManager != null && skillBarUiManager.TryGetHoveredLivingCombatant(out _))
        {
            hoverFocusMarker.ClearExternalTarget();
            return;
        }

        var target = combatSession.FindCombatantById(inputController.FocusedTargetCombatantId);

        if (target == null || target.Health.IsDead)
        {
            hoverFocusMarker.ClearExternalTarget();
            return;
        }

        var unitRoot = combatSession.TryGetUnitVisualRoot(target.Identity.Id);

        if (unitRoot == null || !unitRoot.gameObject.activeInHierarchy)
        {
            hoverFocusMarker.ClearExternalTarget();
            return;
        }

        var topWorldY = CombatUnitColliderVerticalExtents.TryGetTopWorldY(unitRoot, out var colliderTopWorldY)
            ? colliderTopWorldY
            : unitRoot.position.y;

        var markerPosition = unitRoot.position;
        markerPosition.y = topWorldY;
        markerPosition += hoverFocusMarker.MarkerOffset;

        hoverFocusMarker.PresentExternal(markerPosition, target.Identity.Id);
    }

    private void OnDisable()
    {
        if (hoverFocusMarker != null)
        {
            hoverFocusMarker.ClearExternalTarget();
        }
    }

    private void ResolveReferences()
    {
        if (inputController == null)
        {
            inputController = FindFirstObjectByType<CombatInputController>();
        }

        if (combatSession == null)
        {
            combatSession = FindFirstObjectByType<CombatPrototypeController>();
        }

        if (skillBarUiManager == null)
        {
            skillBarUiManager = FindFirstObjectByType<CombatSkillButtonBarUIManager>();
        }

        if (hoverFocusMarker == null)
        {
            hoverFocusMarker = FindFirstObjectByType<CombatHoverFocusMarker>();
        }
    }
}