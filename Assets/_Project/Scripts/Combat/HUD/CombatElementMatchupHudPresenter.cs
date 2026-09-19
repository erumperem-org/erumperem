using System.Linq;
using DG.Tweening;
using Game.Core.Config;
using Game.Core.Engine;
using Game.Core.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Erumperem.Combat
{
    /// <summary>
    /// Shows Fire &gt; Metal &gt; Anomaly advantage / disadvantage when a skill is selected
    /// against the hovered or currently selected target.
    /// </summary>
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    public sealed class CombatElementMatchupHudPresenter : MonoBehaviour
    {
        private const string RuntimeRootObjectName = "CombatElementMatchupHudRoot";
        private const float PunchDurationSeconds = 0.22f;

        [SerializeField] private TextMeshProUGUI matchupLabel;
        [SerializeField] private CombatSkillButtonBarUIManager skillButtonBarUIManager;

        private CombatPrototypeController _combatSession;
        private RectTransform _matchupRoot;
        private string _lastRenderedSignature = string.Empty;

        private void Awake()
        {
            if (skillButtonBarUIManager == null)
            {
                skillButtonBarUIManager = FindFirstObjectByType<CombatSkillButtonBarUIManager>();
            }
        }

        private void LateUpdate()
        {
            if (_combatSession == null)
            {
                _combatSession = FindFirstObjectByType<CombatPrototypeController>();
            }

            if (_combatSession == null || !_combatSession.IsBattleOngoing)
            {
                HideMatchup();
                return;
            }

            if (!TryResolveSelectedSkillAndTarget(out var actingCombatant, out var skill, out var previewTarget))
            {
                HideMatchup();
                return;
            }

            var matchup = CombatDamageCalculator.GetElementMatchup(actingCombatant, previewTarget, skill);
            if (matchup == ElementMatchupKind.Neutral)
            {
                HideMatchup();
                return;
            }

            var attackElement = CombatDamageCalculator.ResolveAttackElement(actingCombatant, skill);
            var defenseElement = previewTarget.ElementAffinity.Element;
            var matchupText = matchup == ElementMatchupKind.Advantage
                ? $"Advantage  {attackElement} > {defenseElement}"
                : $"Disadvantage  {attackElement} < {defenseElement}";
            var signature = $"{skill.Id}|{previewTarget.Identity.Id}|{matchup}";
            ShowMatchup(matchupText, signature);
        }

        private bool TryResolveSelectedSkillAndTarget(
            out Combatant actingCombatant,
            out SkillDefinition skill,
            out Combatant previewTarget)
        {
            actingCombatant = null;
            skill = null;
            previewTarget = null;

            _combatSession.GetSkillBarSelection(out var selectedSlot, out var ownerCombatantId);
            if (!selectedSlot.HasValue || string.IsNullOrEmpty(ownerCombatantId))
            {
                return false;
            }

            actingCombatant = _combatSession.FindCombatantById(ownerCombatantId);
            if (actingCombatant == null || !_combatSession.IsPlayerCommandingCombatant(actingCombatant))
            {
                return false;
            }

            var battleState = _combatSession.BattleState;
            var skillIds = actingCombatant.SkillLoadout.Skills
                .Where(skillId => battleState.SkillsById.ContainsKey(skillId))
                .Take(7)
                .ToList();
            if (selectedSlot.Value < 0 || selectedSlot.Value >= skillIds.Count)
            {
                return false;
            }

            var selectedSkillId = skillIds[selectedSlot.Value];
            if (!battleState.SkillsById.TryGetValue(selectedSkillId, out skill))
            {
                return false;
            }

            if (skillButtonBarUIManager != null &&
                skillButtonBarUIManager.TryGetHoveredLivingCombatant(out var hoveredCombatant))
            {
                previewTarget = hoveredCombatant;
            }
            else
            {
                previewTarget = _combatSession.CurrentSelectedEnemy;
            }

            if (previewTarget == null || previewTarget.Health.IsDead)
            {
                previewTarget = SkillTargetResolver.ResolvePreferredSelection(
                    battleState,
                    actingCombatant,
                    skill,
                    _combatSession.CurrentSelectedEnemy);
            }

            return previewTarget != null;
        }

        private void ShowMatchup(string matchupText, string signature)
        {
            EnsureMatchupCreated();
            if (matchupLabel == null || _matchupRoot == null)
            {
                return;
            }

            _matchupRoot.gameObject.SetActive(true);
            if (string.Equals(_lastRenderedSignature, signature, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastRenderedSignature = signature;
            matchupLabel.text = matchupText;
            _matchupRoot.DOKill();
            _matchupRoot.localScale = Vector3.one;
            _matchupRoot.DOPunchScale(new Vector3(0.1f, 0.14f, 0f), PunchDurationSeconds, 6, 0.5f)
                .SetLink(_matchupRoot.gameObject);
        }

        private void HideMatchup()
        {
            _lastRenderedSignature = string.Empty;
            if (_matchupRoot != null)
            {
                _matchupRoot.gameObject.SetActive(false);
            }
        }

        private void EnsureMatchupCreated()
        {
            if (matchupLabel != null && _matchupRoot != null)
            {
                return;
            }

            var overlayCanvas = ResolveOverlayCanvas();
            if (overlayCanvas == null)
            {
                return;
            }

            var existingRoot = overlayCanvas.transform.Find(RuntimeRootObjectName);
            if (existingRoot != null)
            {
                _matchupRoot = existingRoot as RectTransform;
                matchupLabel = existingRoot.GetComponentInChildren<TextMeshProUGUI>(true);
                return;
            }

            var rootObject = new GameObject(RuntimeRootObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rootObject.transform.SetParent(overlayCanvas.transform, false);
            _matchupRoot = rootObject.GetComponent<RectTransform>();
            _matchupRoot.anchorMin = new Vector2(0.5f, 0f);
            _matchupRoot.anchorMax = new Vector2(0.5f, 0f);
            _matchupRoot.pivot = new Vector2(0.5f, 0f);
            _matchupRoot.anchoredPosition = new Vector2(0f, 96f);
            _matchupRoot.sizeDelta = new Vector2(360f, 36f);

            var background = rootObject.GetComponent<Image>();
            background.color = new Color(0.08f, 0.07f, 0.05f, 0.78f);
            background.raycastTarget = false;

            var labelObject = new GameObject("ElementMatchupLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(rootObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 2f);
            labelRect.offsetMax = new Vector2(-8f, -2f);

            matchupLabel = labelObject.GetComponent<TextMeshProUGUI>();
            matchupLabel.alignment = TextAlignmentOptions.Center;
            matchupLabel.fontSize = 20f;
            matchupLabel.fontStyle = FontStyles.Bold;
            matchupLabel.raycastTarget = false;
        }

        private static Canvas ResolveOverlayCanvas()
        {
            var skillInfoPanel = FindFirstObjectByType<CombatSkillInfoPanelPresenter>();
            if (skillInfoPanel != null)
            {
                var panelCanvas = skillInfoPanel.GetComponentInParent<Canvas>();
                if (panelCanvas != null)
                {
                    return panelCanvas;
                }
            }

            var flowHud = FindFirstObjectByType<CombatFlowHudPresenter>();
            if (flowHud != null)
            {
                var flowCanvas = flowHud.GetComponentInParent<Canvas>();
                if (flowCanvas != null)
                {
                    return flowCanvas.rootCanvas;
                }
            }

            return FindFirstObjectByType<Canvas>();
        }
    }
}
