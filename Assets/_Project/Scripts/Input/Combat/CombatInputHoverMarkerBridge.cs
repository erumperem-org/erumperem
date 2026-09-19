using System.Reflection;
using Erumperem.Combat;
using UnityEngine;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class CombatInputHoverMarkerBridge : MonoBehaviour
{
    private const string PresentAtMethodName = "PresentAt";
    private const string EnsureCreatedMethodName = "EnsureCreated";
    private const string MarkerOffsetFieldName = "markerOffset";

    [Header("References")]
    [SerializeField] private CombatInputController inputController;
    [SerializeField] private CombatPrototypeController combatSession;
    [SerializeField] private CombatSkillButtonBarUIManager skillBarUiManager;
    [SerializeField] private CombatHoverFocusMarker hoverFocusMarker;

    private MethodInfo _presentAtMethod;
    private MethodInfo _ensureCreatedMethod;
    private FieldInfo _markerOffsetField;

    private void Awake()
    {
        ResolveReferences();
        ResolveHoverMarkerMembers();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (inputController == null || combatSession == null || hoverFocusMarker == null)
        {
            return;
        }

        if (inputController.CurrentPhase != CombatInputPhase.TargetSelection)
        {
            return;
        }

        if (string.IsNullOrEmpty(inputController.FocusedTargetCombatantId))
        {
            return;
        }

        if (skillBarUiManager != null && skillBarUiManager.TryGetHoveredLivingCombatant(out _))
        {
            return;
        }

        var target = combatSession.FindCombatantById(inputController.FocusedTargetCombatantId);

        if (target == null || target.Health.IsDead)
        {
            return;
        }

        var unitRoot = combatSession.TryGetUnitVisualRoot(target.Identity.Id);

        if (unitRoot == null || !unitRoot.gameObject.activeInHierarchy)
        {
            return;
        }

        if (_presentAtMethod == null || _markerOffsetField == null)
        {
            ResolveHoverMarkerMembers();
        }

        if (_presentAtMethod == null || _markerOffsetField == null)
        {
            return;
        }

        _ensureCreatedMethod?.Invoke(hoverFocusMarker, null);

        var topWorldY = CombatUnitColliderVerticalExtents.TryGetTopWorldY(unitRoot, out var colliderTopWorldY) ? colliderTopWorldY : unitRoot.position.y;
        var markerPosition = unitRoot.position;
        markerPosition.y = topWorldY;
        markerPosition += (Vector3)_markerOffsetField.GetValue(hoverFocusMarker);

        _presentAtMethod.Invoke(hoverFocusMarker, new object[] { markerPosition, target.Identity.Id });
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

    private void ResolveHoverMarkerMembers()
    {
        if (hoverFocusMarker == null)
        {
            return;
        }

        var markerType = hoverFocusMarker.GetType();
        var privateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        _presentAtMethod = markerType.GetMethod(PresentAtMethodName, privateInstanceFlags);
        _ensureCreatedMethod = markerType.GetMethod(EnsureCreatedMethodName, privateInstanceFlags);
        _markerOffsetField = markerType.GetField(MarkerOffsetFieldName, privateInstanceFlags);

        if (_presentAtMethod == null)
        {
            Debug.LogError($"CombatInputHoverMarkerBridge: método privado '{PresentAtMethodName}' não encontrado em CombatHoverFocusMarker.", this);
        }

        if (_markerOffsetField == null)
        {
            Debug.LogError($"CombatInputHoverMarkerBridge: campo privado '{MarkerOffsetFieldName}' não encontrado em CombatHoverFocusMarker.", this);
        }
    }
}
