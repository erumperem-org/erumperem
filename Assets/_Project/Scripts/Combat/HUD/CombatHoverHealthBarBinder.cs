using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Erumperem.Combat;
using Erumperem.Combat.HealthBars;
using Erumperem.Combat.Runtime;
using Game.Core.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Erumperem.Combat.HealthBars
{
    [DefaultExecutionOrder(25)]
    [RequireComponent(typeof(HealthBarHudView))]
    public sealed class CombatHoverHealthBarBinder : MonoBehaviour
    {
        [Serializable]
        public struct UnitIconMapping
        {
            [Tooltip("Arraste o PREFAB original da unidade aqui.")]
            public GameObject unitPrefab;
            [Tooltip("Arraste o icone correspondente a esta unidade.")]
            public Sprite unitIcon;
        }

        [Header("Referencia central")]
        [SerializeField] private GameObject combatLogicCenter;

        [Header("Painel")]
        [Tooltip("TRUE = painel esquerdo (aliado). FALSE = painel direito (foco contextual).")]
        [SerializeField] private bool isPlayerBar = false;

        [Header("UI de texto e imagem")]
        [SerializeField] private TextMeshProUGUI unitNameText;
        [SerializeField] private TextMeshProUGUI unitDescriptionText;
        [SerializeField] private Image unitPortraitImage;

        [Header("Icones")]
        [SerializeField] private List<UnitIconMapping> iconMappings;

        private CombatSessionHub _sessionHub;
        private readonly CombatSessionHubSubscription _sessionHubSubscription = new();
        private CombatHudPanelFocusCoordinator _panelFocusCoordinator;
        private HealthBarHudView _hudView;

        private CombatPrototypeController _activeCombatSession;
        private string _currentTrackedCombatantId = string.Empty;
        private Coroutine _initializationRoutine;

        private readonly Dictionary<string, Sprite> _iconCache = new();

        private void Awake()
        {
            _hudView = GetComponent<HealthBarHudView>();
            ResolveCombatServices();
            RebuildIconCache();
        }

        private void ResolveCombatServices()
        {
            if (combatLogicCenter == null)
            {
                var sessionHubInScene = FindFirstObjectByType<CombatSessionHub>();
                if (sessionHubInScene != null)
                {
                    combatLogicCenter = sessionHubInScene.gameObject;
                }
            }

            if (combatLogicCenter != null)
            {
                _sessionHub = combatLogicCenter.GetComponent<CombatSessionHub>();
            }

            _panelFocusCoordinator = GetComponentInParent<CombatHudPanelFocusCoordinator>();
            if (_panelFocusCoordinator == null)
            {
                _panelFocusCoordinator = FindFirstObjectByType<CombatHudPanelFocusCoordinator>();
            }

            if (_sessionHub == null)
            {
                Debug.LogError($"{nameof(CombatHoverHealthBarBinder)}: CombatSessionHub nao encontrado na cena.", this);
            }
        }

        private void RebuildIconCache()
        {
            _iconCache.Clear();
            if (iconMappings == null)
            {
                return;
            }

            foreach (var iconMapping in iconMappings)
            {
                if (iconMapping.unitPrefab == null || iconMapping.unitIcon == null)
                {
                    continue;
                }

                var lookupName = GetVisualLookupName(iconMapping.unitPrefab.name);
                if (!_iconCache.ContainsKey(lookupName))
                {
                    _iconCache.Add(lookupName, iconMapping.unitIcon);
                }
            }
        }

        private void SupplementIconCacheFromVisualRoots(CombatPrototypeController controller)
        {
            if (iconMappings == null || controller == null)
            {
                return;
            }

            for (var mappingIndex = 0; mappingIndex < iconMappings.Count; mappingIndex++)
            {
                var iconMapping = iconMappings[mappingIndex];
                if (iconMapping.unitIcon == null)
                {
                    continue;
                }

                if (iconMapping.unitPrefab != null)
                {
                    _iconCache[GetVisualLookupName(iconMapping.unitPrefab.name)] = iconMapping.unitIcon;
                    continue;
                }

                var combatantId = isPlayerBar
                    ? $"ally_{mappingIndex + 1}"
                    : $"enemy_{mappingIndex + 1}";

                var visualRoot = controller.TryGetUnitVisualRoot(combatantId);
                if (visualRoot == null)
                {
                    continue;
                }

                _iconCache[GetVisualLookupName(visualRoot.gameObject.name)] = iconMapping.unitIcon;
            }
        }

        private void OnEnable()
        {
            ResolveCombatServices();
            _sessionHubSubscription.Subscribe(_sessionHub, HandleCombatSessionReady, HandleCombatSessionClosed);
            _sessionHubSubscription.TryCatchUpWithActiveCombatSession(_activeCombatSession);
        }

        private void OnDisable()
        {
            _sessionHubSubscription.Unsubscribe();

            if (_initializationRoutine != null)
            {
                StopCoroutine(_initializationRoutine);
            }
        }

        private void HandleCombatSessionReady(CombatPrototypeController controller)
        {
            _activeCombatSession = controller;
            SupplementIconCacheFromVisualRoots(controller);

            var defaultCombatantId = isPlayerBar ? "ally_1" : "enemy_1";
            if (_activeCombatSession != null && _activeCombatSession.FindCombatantById(defaultCombatantId) != null)
            {
                ApplyTrackedCombatant(defaultCombatantId);

                if (_initializationRoutine != null)
                {
                    StopCoroutine(_initializationRoutine);
                }

                _initializationRoutine = StartCoroutine(DeferredInitialTextUpdate(defaultCombatantId));
            }
        }

        private IEnumerator DeferredInitialTextUpdate(string combatantId)
        {
            yield return new WaitForEndOfFrame();
            UpdateVisuals(combatantId);
            _initializationRoutine = null;
        }

        private void HandleCombatSessionClosed()
        {
            if (_initializationRoutine != null)
            {
                StopCoroutine(_initializationRoutine);
                _initializationRoutine = null;
            }

            _activeCombatSession = null;
            _currentTrackedCombatantId = string.Empty;
            _hudView.ClearSkillDamagePreview();
        }

        private void LateUpdate()
        {
            if (_activeCombatSession == null ||
                !_activeCombatSession.IsBattleOngoing ||
                _panelFocusCoordinator == null)
            {
                return;
            }

            var focusCombatantId = isPlayerBar
                ? _panelFocusCoordinator.LeftAllyCombatantId
                : _panelFocusCoordinator.RightFocusCombatantId;

            if (string.IsNullOrEmpty(focusCombatantId))
            {
                return;
            }

            if (!string.Equals(focusCombatantId, _currentTrackedCombatantId, StringComparison.Ordinal))
            {
                ApplyTrackedCombatant(focusCombatantId);
                UpdateVisuals(focusCombatantId);
            }
        }

        private void ApplyTrackedCombatant(string combatantId)
        {
            _currentTrackedCombatantId = combatantId;
            _hudView.Configure(_activeCombatSession, combatantId);
        }

        private void UpdateVisuals(string combatantId)
        {
            if (_activeCombatSession == null)
            {
                return;
            }

            var combatant = _activeCombatSession.FindCombatantById(combatantId);
            if (combatant == null)
            {
                return;
            }

            var displayName = BuildDisplayName(combatant, combatantId);
            if (unitNameText != null)
            {
                unitNameText.text = displayName;
            }

            if (unitDescriptionText != null)
            {
                unitDescriptionText.text = BuildDescriptionLine(combatant);
            }

            if (unitPortraitImage == null)
            {
                return;
            }

            var visualLookupName = TryGetVisualLookupNameForCombatant(combatantId);
            if (TryResolvePortraitSprite(combatantId, visualLookupName, out var portraitSprite))
            {
                unitPortraitImage.gameObject.SetActive(true);
                unitPortraitImage.sprite = portraitSprite;
            }
            else
            {
                unitPortraitImage.gameObject.SetActive(false);
            }
        }

        private string BuildDisplayName(Combatant combatant, string combatantId)
        {
            var visualLookupName = TryGetVisualLookupNameForCombatant(combatantId);
            if (!string.IsNullOrEmpty(visualLookupName))
            {
                return FormatVisualDisplayName(visualLookupName);
            }

            if (!string.IsNullOrWhiteSpace(combatant.Identity.DisplayName))
            {
                return combatant.Identity.DisplayName;
            }

            return combatantId;
        }

        private static string BuildDescriptionLine(Combatant combatant)
        {
            var maxHp = Math.Max(1, combatant.Health.MaxHp);
            var currentHp = Math.Clamp(combatant.Health.CurrentHp, 0, maxHp);
            var healthPercent = Mathf.RoundToInt((float)currentHp / maxHp * 100f);
            return $"{currentHp}/{maxHp} HP ({healthPercent}%)";
        }

        private string TryGetVisualLookupNameForCombatant(string combatantId)
        {
            var visualRoot = _activeCombatSession.TryGetUnitVisualRoot(combatantId);
            if (visualRoot != null)
            {
                return GetVisualLookupName(visualRoot.gameObject.name);
            }

            var allCapsules = FindObjectsByType<CombatCapsuleTag>(FindObjectsSortMode.None);
            foreach (var capsule in allCapsules)
            {
                if (capsule.combatantId == combatantId)
                {
                    return GetVisualLookupName(capsule.gameObject.name);
                }
            }

            return string.Empty;
        }

        private static string FormatVisualDisplayName(string visualLookupName)
        {
            var displayName = visualLookupName.Replace("_", " ");
            displayName = Regex.Replace(displayName, "([a-z])([A-Z])", "$1 $2");
            return Regex.Replace(displayName, @"\s+", " ").Trim();
        }

        private bool TryResolvePortraitSprite(string combatantId, string visualLookupName, out Sprite portraitSprite)
        {
            if (!string.IsNullOrEmpty(visualLookupName) &&
                _iconCache.TryGetValue(visualLookupName, out portraitSprite))
            {
                return true;
            }

            if (iconMappings == null || string.IsNullOrEmpty(combatantId))
            {
                portraitSprite = null;
                return false;
            }

            var combatantIndex = ParseCombatantIndex(combatantId);
            if (combatantIndex < 0 || combatantIndex >= iconMappings.Count)
            {
                portraitSprite = null;
                return false;
            }

            portraitSprite = iconMappings[combatantIndex].unitIcon;
            return portraitSprite != null;
        }

        private static int ParseCombatantIndex(string combatantId)
        {
            var separatorIndex = combatantId.LastIndexOf('_');
            if (separatorIndex < 0 || separatorIndex >= combatantId.Length - 1)
            {
                return -1;
            }

            return int.TryParse(combatantId[(separatorIndex + 1)..], out var parsedIndex)
                ? parsedIndex - 1
                : -1;
        }

        private static string GetVisualLookupName(string rawObjectName)
        {
            if (rawObjectName.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase))
            {
                return rawObjectName.Replace("(Clone)", "", StringComparison.OrdinalIgnoreCase).Trim();
            }

            return rawObjectName;
        }
    }
}
