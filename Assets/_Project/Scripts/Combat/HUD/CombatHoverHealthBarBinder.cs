using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Erumperem.Combat;
using Erumperem.Combat.HealthBars;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Erumperem.Combat.HealthBars
{
    [RequireComponent(typeof(HealthBarHudView))]
    public sealed class CombatHoverHealthBarBinder : MonoBehaviour
    {
        // ADICIONADO: Uma estrutura simples para criar o mapeamento no Inspector
        [System.Serializable]
        public struct UnitIconMapping
        {
            [Tooltip("Arraste o PREFAB original da unidade aqui.")]
            public GameObject unitPrefab;
            [Tooltip("Arraste a foto/˜cone correspondente a esta unidade.")]
            public Sprite unitIcon;
        }

        [Header("Refer˜ncia Central (Arraste o 'combatlogc' aqui)")]
        [SerializeField] private GameObject combatLogicCenter;

        [Header("Filtro de Alvo")]
        [Tooltip("Marque TRUE se esta barra for para exibir apenas os Aliados/Players. Marque FALSE se for apenas para Inimigos.")]
        [SerializeField] private bool isPlayerBar = false;

        [Header("UI de Texto e Imagem")]
        [Tooltip("Arraste o componente de texto que exibir˜ o nome do Prefab aqui.")]
        [SerializeField] private TextMeshProUGUI unitNameText;

        [Tooltip("Arraste o componente de Image da UI que exibir˜ o retrato da unidade.")]
        [SerializeField] private Image unitPortraitImage; // ADICIONADO: Refer˜ncia para a imagem na UI

        [Header("Configura˜˜o de ˜cones (Novo)")]
        [Tooltip("Adicione os prefabs e suas respectivas imagens aqui.")]
        [SerializeField] private List<UnitIconMapping> iconMappings; // ADICIONADO: Lista que aparece no Inspector

        private CombatSessionHub _sessionHub;
        private CombatHoverFocusMarker _hoverMarker;
        private HealthBarHudView _hudView;

        private CombatPrototypeController _activeCombatSession;
        private string _currentTrackedCombatantId = "";
        private Coroutine _initializationRoutine;

        // Otimiza˜˜o: Dicion˜rio em cache para buscas ultra r˜pidas por nome
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
                _hoverMarker = combatLogicCenter.GetComponent<CombatHoverFocusMarker>();
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
            if (_sessionHub == null) return;

            _sessionHub.OnCombatSessionReadyForUi += HandleCombatSessionReady;
            _sessionHub.OnCombatSessionClosed += HandleCombatSessionClosed;
        }

        private void OnDisable()
        {
            if (_sessionHub == null) return;

            _sessionHub.OnCombatSessionReadyForUi -= HandleCombatSessionReady;
            _sessionHub.OnCombatSessionClosed -= HandleCombatSessionClosed;

            if (_initializationRoutine != null)
            {
                StopCoroutine(_initializationRoutine);
            }
        }

        private void HandleCombatSessionReady(CombatPrototypeController controller)
        {
            _activeCombatSession = controller;
            SupplementIconCacheFromVisualRoots(controller);

            string defaultId = isPlayerBar ? "ally_1" : "enemy_1";

            if (_activeCombatSession != null && _activeCombatSession.FindCombatantById(defaultId) != null)
            {
                _currentTrackedCombatantId = defaultId;
                _hudView.Configure(_activeCombatSession, _currentTrackedCombatantId);

                if (_initializationRoutine != null) StopCoroutine(_initializationRoutine);
                _initializationRoutine = StartCoroutine(DeferredInitialTextUpdate(_currentTrackedCombatantId));
            }
        }

        private IEnumerator DeferredInitialTextUpdate(string combatantId)
        {
            yield return new WaitForEndOfFrame();
            UpdateVisuals(combatantId); // ALTERADO: Agora atualiza Texto E Imagem
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
            _currentTrackedCombatantId = "";
            _hudView.ClearSkillDamagePreview();
        }

        private void LateUpdate()
        {
            if (_activeCombatSession == null || !_activeCombatSession.IsBattleOngoing || _hoverMarker == null)
            {
                return;
            }

            string targetCombatantId = FindCurrentHoveredCombatantId();

            if (string.IsNullOrEmpty(targetCombatantId))
            {
                return;
            }

            bool isAlly = targetCombatantId.StartsWith("ally", StringComparison.OrdinalIgnoreCase);

            if (isPlayerBar != isAlly)
            {
                return;
            }

            if (targetCombatantId != _currentTrackedCombatantId)
            {
                _currentTrackedCombatantId = targetCombatantId;
                _hudView.Configure(_activeCombatSession, _currentTrackedCombatantId);
                UpdateVisuals(_currentTrackedCombatantId); // ALTERADO: Agora atualiza Texto E Imagem
            }
        }

        private string FindCurrentHoveredCombatantId()
        {
            if (!_hoverMarker.isActiveAndEnabled) return null;

            var fieldInfo = typeof(CombatHoverFocusMarker).GetField("_lastJuiceCombatantId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (fieldInfo != null)
            {
                return fieldInfo.GetValue(_hoverMarker) as string;
            }

            return null;
        }

        // ALTERADO: M˜todo renomeado de UpdateNameText para UpdateVisuals pois agora cuida da Imagem tamb˜m
        private void UpdateVisuals(string combatantId)
        {
            if (_activeCombatSession == null) return;

            var combatant = _activeCombatSession.FindCombatantById(combatantId);
            if (combatant != null)
            {
                var allCapsules = FindObjectsByType<CombatCapsuleTag>(FindObjectsSortMode.None);
                foreach (var capsule in allCapsules)
                {
                    if (capsule.combatantId == combatantId)
                    {
                        var visualLookupName = GetVisualLookupName(capsule.gameObject.name);

                        if (unitPortraitImage != null)
                        {
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

                        if (unitNameText != null)
                        {
                            var displayName = visualLookupName.Replace("_", " ");
                            displayName = Regex.Replace(displayName, "([a-z])([A-Z])", "$1 $2");
                            displayName = Regex.Replace(displayName, @"\s+", " ").Trim();
                            unitNameText.text = displayName;
                        }

                        return;
                    }
                }

                if (unitNameText != null) unitNameText.text = combatantId;
            }
        }

        private bool TryResolvePortraitSprite(string combatantId, string visualLookupName, out Sprite portraitSprite)
        {
            if (_iconCache.TryGetValue(visualLookupName, out portraitSprite))
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
