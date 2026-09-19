using System;
using System.Collections.Generic;
using System.Linq;
using Erumperem.Combat.Runtime;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Presentation;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Mostra <see cref="SelectedEnemyIcon"/> em inimigos em hover, durante ação inimiga e em preview multi-alvo.
    /// </summary>
    [DefaultExecutionOrder(22)]
    public sealed class CombatEnemySelectionMarkerBinder : MonoBehaviour
    {
        [SerializeField] private CombatPrototypeController combatSession;
        [SerializeField] private CombatSessionHub sessionHub;
        [SerializeField] private CombatSkillButtonBarUIManager skillButtonBarUIManager;

        [Header("Marcador")]
        [SerializeField] private GameObject enemySelectionMarkerPrefab;
        [SerializeField] private Vector3 markerLocalOffset = new(0f, -0.9f, 0f);
        [SerializeField] private Vector3 markerBaseEuler = new(90f, 0f, 0f);
        [SerializeField] private float markerSpinZDegreesPerSecond;

        [Header("Raycast")]
        [SerializeField] private float worldRaycastDistance = 200f;

        private readonly Dictionary<string, Transform> _markerTransformsByCombatantId = new(StringComparer.Ordinal);
        private readonly HashSet<string> _highlightedEnemyCombatantIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _markerSpinZDegreesByCombatantId = new(StringComparer.Ordinal);
        private readonly CombatPointerRaycastService _pointerRaycast = new();
        private readonly CombatSessionHubSubscription _sessionHubSubscription = new();

        private void Awake()
        {
            _pointerRaycast.Configure(Camera.main, worldRaycastDistance);
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            _sessionHubSubscription.Subscribe(sessionHub, HandleCombatSessionReadyForUi, HandleCombatSessionClosed);
            _sessionHubSubscription.TryCatchUpWithActiveCombatSession(combatSession);
        }

        private void OnDisable()
        {
            _sessionHubSubscription.Unsubscribe();
            ClearAllMarkers();
        }

        private void LateUpdate()
        {
            if (combatSession == null || !combatSession.IsBattleOngoing)
            {
                ClearAllMarkers();
                return;
            }

            CollectHighlightedEnemyCombatantIds(_highlightedEnemyCombatantIds);
            SyncMarkerInstances(_highlightedEnemyCombatantIds);
        }

        private void ResolveReferences()
        {
            if (combatSession == null)
            {
                combatSession = FindFirstObjectByType<CombatPrototypeController>();
            }

            if (sessionHub == null)
            {
                sessionHub = FindFirstObjectByType<CombatSessionHub>();
            }

            if (skillButtonBarUIManager == null)
            {
                skillButtonBarUIManager = FindFirstObjectByType<CombatSkillButtonBarUIManager>();
            }
        }

        private void HandleCombatSessionReadyForUi(CombatPrototypeController controller)
        {
            combatSession = controller;
        }

        private void HandleCombatSessionClosed()
        {
            combatSession = null;
            ClearAllMarkers();
        }

        private void CollectHighlightedEnemyCombatantIds(HashSet<string> highlightedEnemyCombatantIds)
        {
            highlightedEnemyCombatantIds.Clear();

            if (combatSession != null &&
                combatSession.TryGetOngoingActionPresentationCombatantIds(
                    out var presentationActorCombatantId,
                    out var presentationTargetCombatantId))
            {
                TryAddEnemyCombatantId(highlightedEnemyCombatantIds, presentationActorCombatantId);
                TryAddEnemyCombatantId(highlightedEnemyCombatantIds, presentationTargetCombatantId);
            }

            if (TryRaycastHoveredEnemyCombatantId(out var hoveredEnemyCombatantId))
            {
                highlightedEnemyCombatantIds.Add(hoveredEnemyCombatantId);
            }

            CollectSkillPreviewEnemyCombatantIds(highlightedEnemyCombatantIds);
        }

        private void CollectSkillPreviewEnemyCombatantIds(HashSet<string> highlightedEnemyCombatantIds)
        {
            if (combatSession == null ||
                combatSession.IsActionPresentationOngoing ||
                !TryResolvePreviewedSkillContext(
                    out var actingCombatant,
                    out var zeroBasedSlotIndex,
                    out var skillDefinition))
            {
                return;
            }

            var primaryTarget = ResolvePrimaryTargetForSkillPreview(
                actingCombatant,
                skillDefinition,
                zeroBasedSlotIndex);

            var affectedCombatantIds = SkillCombatTargetPreviewResolver.ResolveAffectedCombatantIds(
                combatSession.BattleState,
                actingCombatant,
                skillDefinition,
                primaryTarget);

            foreach (var affectedCombatantId in affectedCombatantIds)
            {
                TryAddEnemyCombatantId(highlightedEnemyCombatantIds, affectedCombatantId);
            }
        }

        private bool TryResolvePreviewedSkillContext(
            out Combatant actingCombatant,
            out int zeroBasedSlotIndex,
            out SkillDefinition skillDefinition)
        {
            actingCombatant = null;
            zeroBasedSlotIndex = default;
            skillDefinition = null;

            var battleState = combatSession.BattleState;
            if (battleState == null)
            {
                return false;
            }

            var pendingPlayerCombatantId = combatSession.PendingPlayerCombatantId;
            if (string.IsNullOrEmpty(pendingPlayerCombatantId))
            {
                return false;
            }

            actingCombatant = combatSession.FindCombatantById(pendingPlayerCombatantId);
            if (actingCombatant == null ||
                actingCombatant.Health.IsDead ||
                !combatSession.IsPlayerCommandingCombatant(actingCombatant))
            {
                return false;
            }

            combatSession.GetSkillBarSelection(out var selectedSlot, out var selectedOwnerCombatantId);
            var skillsRowView = skillButtonBarUIManager?.SkillsRowView;
            var hasLockedSelection = selectedSlot.HasValue &&
                string.Equals(selectedOwnerCombatantId, pendingPlayerCombatantId, StringComparison.Ordinal);

            if (hasLockedSelection)
            {
                zeroBasedSlotIndex = selectedSlot.Value;
            }
            else if (skillsRowView != null && skillsRowView.HoveredZeroBasedSlotIndex.HasValue)
            {
                zeroBasedSlotIndex = skillsRowView.HoveredZeroBasedSlotIndex.Value;
            }
            else
            {
                return false;
            }

            var skillIds = actingCombatant.SkillLoadout.Skills
                .Where(skillId => battleState.SkillsById.ContainsKey(skillId))
                .Take(CharacterSkillButtonsRowView.MaxVisibleSlots)
                .ToList();

            if (zeroBasedSlotIndex < 0 || zeroBasedSlotIndex >= skillIds.Count)
            {
                return false;
            }

            return battleState.SkillsById.TryGetValue(skillIds[zeroBasedSlotIndex], out skillDefinition);
        }

        private Combatant? ResolvePrimaryTargetForSkillPreview(
            Combatant actingCombatant,
            SkillDefinition skillDefinition,
            int zeroBasedSlotIndex)
        {
            var battleState = combatSession.BattleState;

            Combatant? preferredCombatant = null;
            if (skillButtonBarUIManager != null &&
                skillButtonBarUIManager.TryGetHoveredLivingCombatant(out var hoveredCombatant))
            {
                preferredCombatant = hoveredCombatant;
            }
            else
            {
                preferredCombatant = combatSession.CurrentSelectedEnemy;
            }

            return SkillTargetResolver.ResolvePreferredSelection(
                battleState,
                actingCombatant,
                skillDefinition,
                preferredCombatant);
        }

        private void TryAddEnemyCombatantId(HashSet<string> highlightedEnemyCombatantIds, string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId) || !IsEnemyCombatantId(combatantId))
            {
                return;
            }

            var combatant = combatSession.FindCombatantById(combatantId);
            if (combatant == null || combatant.Health.IsDead)
            {
                return;
            }

            highlightedEnemyCombatantIds.Add(combatantId);
        }

        private bool TryRaycastHoveredEnemyCombatantId(out string hoveredEnemyCombatantId)
        {
            hoveredEnemyCombatantId = string.Empty;

            if (skillButtonBarUIManager != null &&
                skillButtonBarUIManager.TryGetHoveredLivingCombatant(out var hoveredCombatant) &&
                IsEnemyCombatantId(hoveredCombatant.Identity.Id))
            {
                hoveredEnemyCombatantId = hoveredCombatant.Identity.Id;
                return true;
            }

            if (!_pointerRaycast.TryRaycastCombatCapsuleTagFromInputManager(out var capsuleTag) ||
                !IsEnemyCombatantId(capsuleTag.combatantId))
            {
                return false;
            }

            var hoveredEnemy = combatSession.FindCombatantById(capsuleTag.combatantId);
            if (hoveredEnemy == null || hoveredEnemy.Health.IsDead)
            {
                return false;
            }

            hoveredEnemyCombatantId = capsuleTag.combatantId;
            return true;
        }

        private void SyncMarkerInstances(HashSet<string> highlightedEnemyCombatantIds)
        {
            if (enemySelectionMarkerPrefab == null)
            {
                return;
            }

            var obsoleteCombatantIds = _markerTransformsByCombatantId.Keys
                .Where(existingCombatantId => !highlightedEnemyCombatantIds.Contains(existingCombatantId))
                .ToList();

            foreach (var obsoleteCombatantId in obsoleteCombatantIds)
            {
                if (_markerTransformsByCombatantId.Remove(obsoleteCombatantId, out var obsoleteMarkerTransform) &&
                    obsoleteMarkerTransform != null)
                {
                    Destroy(obsoleteMarkerTransform.gameObject);
                }

                _markerSpinZDegreesByCombatantId.Remove(obsoleteCombatantId);
            }

            foreach (var highlightedEnemyCombatantId in highlightedEnemyCombatantIds)
            {
                var unitVisualRoot = combatSession.TryGetUnitVisualRoot(highlightedEnemyCombatantId);
                if (unitVisualRoot == null)
                {
                    continue;
                }

                if (!_markerTransformsByCombatantId.TryGetValue(highlightedEnemyCombatantId, out var markerTransform) ||
                    markerTransform == null)
                {
                    var markerObject = Instantiate(enemySelectionMarkerPrefab, unitVisualRoot, false);
                    markerObject.name = $"SelectedEnemyIcon_{highlightedEnemyCombatantId}";
                    ApplyIgnoreRaycastLayerRecursively(markerObject);
                    markerTransform = markerObject.transform;
                    _markerTransformsByCombatantId[highlightedEnemyCombatantId] = markerTransform;
                    _markerSpinZDegreesByCombatantId[highlightedEnemyCombatantId] = 0f;
                }
                else if (markerTransform.parent != unitVisualRoot)
                {
                    markerTransform.SetParent(unitVisualRoot, false);
                }

                if (!markerTransform.gameObject.activeSelf)
                {
                    markerTransform.gameObject.SetActive(true);
                }

                markerTransform.localPosition = markerLocalOffset;

                if (markerSpinZDegreesPerSecond > 0f)
                {
                    var spinZDegrees = _markerSpinZDegreesByCombatantId[highlightedEnemyCombatantId];
                    spinZDegrees += markerSpinZDegreesPerSecond * Time.deltaTime;
                    if (spinZDegrees >= 360f)
                    {
                        spinZDegrees -= 360f;
                    }

                    _markerSpinZDegreesByCombatantId[highlightedEnemyCombatantId] = spinZDegrees;
                    markerTransform.localEulerAngles = new Vector3(
                        markerBaseEuler.x,
                        markerBaseEuler.y,
                        markerBaseEuler.z + spinZDegrees);
                }
                else
                {
                    markerTransform.localEulerAngles = markerBaseEuler;
                }
            }
        }

        private void ClearAllMarkers()
        {
            foreach (var markerEntry in _markerTransformsByCombatantId.Values)
            {
                if (markerEntry != null)
                {
                    Destroy(markerEntry.gameObject);
                }
            }

            _markerTransformsByCombatantId.Clear();
            _markerSpinZDegreesByCombatantId.Clear();
            _highlightedEnemyCombatantIds.Clear();
        }

        private static bool IsEnemyCombatantId(string combatantId) =>
            !string.IsNullOrEmpty(combatantId) &&
            combatantId.StartsWith("enemy", StringComparison.OrdinalIgnoreCase);

        private static void ApplyIgnoreRaycastLayerRecursively(GameObject markerObject)
        {
            var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer < 0)
            {
                return;
            }

            ApplyLayerRecursively(markerObject.transform, ignoreRaycastLayer);
        }

        private static void ApplyLayerRecursively(Transform markerTransform, int layer)
        {
            markerTransform.gameObject.layer = layer;
            for (var childIndex = 0; childIndex < markerTransform.childCount; childIndex++)
            {
                ApplyLayerRecursively(markerTransform.GetChild(childIndex), layer);
            }
        }
    }
}
