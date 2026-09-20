using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Erumperem.Combat;
using Erumperem.Combat.HealthBars;
using Erumperem.Combat.Runtime;
using Game.Core.Almanac;
using Game.Core.Domain;
using Game.Core.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Erumperem.Combat.HealthBars
{
    /// <summary>
    /// Vincula os dados de vida, nome, descrição e portrait no painel da HUD (esquerdo ou direito).
    /// Suporta dinamicamente aliados e inimigos em qualquer um dos dois lados.
    /// </summary>
    [DefaultExecutionOrder(25)]
    [RequireComponent(typeof(HealthBarHudView))]
    public sealed class CombatHoverHealthBarBinder : MonoBehaviour
    {
        [Serializable]
        public struct UnitIconMapping
        {
            [Tooltip("Arraste o PREFAB original da unidade aqui.")]
            public GameObject unitPrefab;
            [Tooltip("Arraste o ícone correspondente a esta unidade.")]
            public Sprite unitIcon;
        }

        [Header("Referência central")]
        [SerializeField] private GameObject combatLogicCenter;

        [Header("Painel")]
        [Tooltip("TRUE = painel esquerdo. FALSE = painel direito.")]
        [SerializeField] private bool isPlayerBar = false;

        [Header("UI de texto e imagem")]
        [SerializeField] private TextMeshProUGUI unitNameText;
        [SerializeField] private TextMeshProUGUI unitDescriptionText;
        [SerializeField] private Image unitPortraitImage;

        [Header("Ícones")]
        [SerializeField] private List<UnitIconMapping> iconMappings;

        private CombatSessionHub _sessionHub;
        private readonly CombatSessionHubSubscription _sessionHubSubscription = new();
        private CombatHudPanelFocusCoordinator _panelFocusCoordinator;
        private HealthBarHudView _hudView;

        private CombatPrototypeController _activeCombatSession;
        private string _currentTrackedCombatantId = string.Empty;
        private Coroutine _initializationRoutine;

        private readonly Dictionary<string, Sprite> _iconCache = new(StringComparer.OrdinalIgnoreCase);

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
                Debug.LogError($"{nameof(CombatHoverHealthBarBinder)}: CombatSessionHub não encontrado na cena.", this);
            }
        }

        private void RebuildIconCache()
        {
            _iconCache.Clear();
            if (iconMappings == null) return;

            foreach (var iconMapping in iconMappings)
            {
                if (iconMapping.unitPrefab == null || iconMapping.unitIcon == null) continue;

                var lookupName = GetVisualLookupName(iconMapping.unitPrefab.name);
                _iconCache[lookupName] = iconMapping.unitIcon;
            }
        }

        private void SupplementIconCacheFromVisualRoots(CombatPrototypeController controller)
        {
            if (controller?.BattleState == null) return;

            // Registra todos os combatentes (aliados e inimigos) no cache por ID e nome
            foreach (var combatant in controller.BattleState.GetAllCombatants())
            {
                var visualRoot = controller.TryGetUnitVisualRoot(combatant.Identity.Id);
                if (visualRoot == null) continue;

                var cleanName = GetVisualLookupName(visualRoot.gameObject.name);

                if (_iconCache.TryGetValue(cleanName, out var sprite))
                {
                    _iconCache[combatant.Identity.Id] = sprite;
                    _iconCache[combatant.Identity.DisplayName] = sprite;
                }
            }
        }

        private void OnEnable()
        {
            ResolveCombatServices();
            _sessionHubSubscription.Subscribe(_sessionHub, HandleCombatSessionReady, HandleCombatSessionClosed);

            if (_panelFocusCoordinator != null)
            {
                _panelFocusCoordinator.OnFocusChanged -= HandleCoordinatorFocusChanged;
                _panelFocusCoordinator.OnFocusChanged += HandleCoordinatorFocusChanged;
            }

            _sessionHubSubscription.TryCatchUpWithActiveCombatSession(_activeCombatSession);
        }

        private void OnDisable()
        {
            _sessionHubSubscription.Unsubscribe();

            if (_panelFocusCoordinator != null)
            {
                _panelFocusCoordinator.OnFocusChanged -= HandleCoordinatorFocusChanged;
            }

            if (_initializationRoutine != null)
            {
                StopCoroutine(_initializationRoutine);
                _initializationRoutine = null;
            }
        }

        private void HandleCoordinatorFocusChanged()
        {
            UpdateFromCurrentCoordinatorFocus();
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
            UpdateFromCurrentCoordinatorFocus();
        }

        private void UpdateFromCurrentCoordinatorFocus()
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
            }

            UpdateVisuals(focusCombatantId);
        }

        private void ApplyTrackedCombatant(string combatantId)
        {
            _currentTrackedCombatantId = combatantId;
            _hudView.Configure(_activeCombatSession, combatantId);
        }

        private void UpdateVisuals(string combatantId)
        {
            if (_activeCombatSession == null) return;

            var combatant = _activeCombatSession.FindCombatantById(combatantId);
            if (combatant == null) return;

            var displayName = BuildDisplayName(combatant, combatantId);
            if (unitNameText != null)
            {
                unitNameText.text = displayName;
            }

            if (unitDescriptionText != null)
            {
                unitDescriptionText.text = BuildDescriptionLine(combatant, _activeCombatSession);
            }

            if (unitPortraitImage != null)
            {
                var visualLookupName = TryGetVisualLookupNameForCombatant(combatantId);
                if (TryResolvePortraitSprite(combatant, combatantId, visualLookupName, out var portraitSprite))
                {
                    unitPortraitImage.gameObject.SetActive(true);
                    unitPortraitImage.sprite = portraitSprite;
                }
                else
                {
                    unitPortraitImage.gameObject.SetActive(false);
                }
            }
        }

        private string BuildDisplayName(Combatant combatant, string combatantId)
        {
            if (!string.IsNullOrWhiteSpace(combatant.Identity.DisplayName))
            {
                return combatant.Identity.DisplayName;
            }

            var visualLookupName = TryGetVisualLookupNameForCombatant(combatantId);
            if (!string.IsNullOrEmpty(visualLookupName))
            {
                return FormatVisualDisplayName(visualLookupName);
            }

            return combatantId;
        }

        private static string BuildDescriptionLine(Combatant combatant, CombatPrototypeController combatSession)
        {
            var maxHp = Math.Max(1, combatant.Health.MaxHp);
            var currentHp = Math.Clamp(combatant.Health.CurrentHp, 0, maxHp);
            var healthPercent = Mathf.RoundToInt((float)currentHp / maxHp * 100f);
            var healthLine = $"{currentHp}/{maxHp} HP ({healthPercent}%)";

            // Se for Herói/Aliado, mostra apenas papel e HP
            if (combatant.Identity.Faction == Faction.Player)
            {
                var roleLabel = combatant.PartyRole == CombatantPartyRole.Leader ? "Leader" : "Companion";
                return $"{healthLine} • {roleLabel}";
            }

            // Se for Inimigo, consulta o Almanaque
            if (combatSession?.BattleState == null)
            {
                return healthLine;
            }

            var battleState = combatSession.BattleState;
            if (!EnemyCatalogIdentity.TryResolveEnemyCatalogId(combatant, out var enemyCatalogId))
            {
                enemyCatalogId = combatant.Identity.DisplayName;
            }

            battleState.EnemyDefinitionsById.TryGetValue(enemyCatalogId, out var catalogDefinition);
            var almanacEntry = EnemyAlmanacEntryBuilder.Build(
                enemyCatalogId,
                battleState.EnemyAlmanac,
                catalogDefinition,
                combatant,
                EnemyAlmanacSkillMap.AsReadOnly(battleState.SkillsById),
                battleState.PassivesById);

            return healthLine + "\n" + EnemyAlmanacEntryBuilder.FormatPlayerFacingText(almanacEntry);
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

        private bool TryResolvePortraitSprite(Combatant combatant, string combatantId, string visualLookupName, out Sprite portraitSprite)
        {
            portraitSprite = null;

            if (!string.IsNullOrEmpty(combatantId) && _iconCache.TryGetValue(combatantId, out portraitSprite))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(combatant.Identity.DisplayName) && _iconCache.TryGetValue(combatant.Identity.DisplayName, out portraitSprite))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(visualLookupName) && _iconCache.TryGetValue(visualLookupName, out portraitSprite))
            {
                return true;
            }

            // Consulta o outro binder irmão da cena se ele tiver o mapeamento registrado
            foreach (var otherBinder in FindObjectsByType<CombatHoverHealthBarBinder>(FindObjectsSortMode.None))
            {
                if (otherBinder == this || otherBinder.iconMappings == null) continue;

                foreach (var mapping in otherBinder.iconMappings)
                {
                    if (mapping.unitPrefab != null && mapping.unitIcon != null)
                    {
                        var cleanPrefabName = GetVisualLookupName(mapping.unitPrefab.name);
                        if (string.Equals(cleanPrefabName, visualLookupName, StringComparison.OrdinalIgnoreCase))
                        {
                            portraitSprite = mapping.unitIcon;
                            _iconCache[visualLookupName] = portraitSprite;
                            return true;
                        }
                    }
                }
            }

            return false;
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