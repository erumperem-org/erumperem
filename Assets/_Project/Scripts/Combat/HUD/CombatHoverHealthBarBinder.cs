using UnityEngine;
using TMPro;
using UnityEngine.UI; // ADICIONADO: Necessário para componentes de Imagem (UI)
using System.Collections;
using System.Collections.Generic; // ADICIONADO: Necessário para usar Listas e Dicionários
using Erumperem.Combat.HealthBars;

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
            [Tooltip("Arraste a foto/ícone correspondente a esta unidade.")]
            public Sprite unitIcon;
        }

        [Header("Referência Central (Arraste o 'combatlogc' aqui)")]
        [SerializeField] private GameObject combatLogicCenter;

        [Header("Filtro de Alvo")]
        [Tooltip("Marque TRUE se esta barra for para exibir apenas os Aliados/Players. Marque FALSE se for apenas para Inimigos.")]
        [SerializeField] private bool isPlayerBar = false;

        [Header("UI de Texto e Imagem")]
        [Tooltip("Arraste o componente de texto que exibirá o nome do Prefab aqui.")]
        [SerializeField] private TextMeshProUGUI unitNameText;

        [Tooltip("Arraste o componente de Image da UI que exibirá o retrato da unidade.")]
        [SerializeField] private Image unitPortraitImage; // ADICIONADO: Referência para a imagem na UI

        [Header("Configuração de Ícones (Novo)")]
        [Tooltip("Adicione os prefabs e suas respectivas imagens aqui.")]
        [SerializeField] private List<UnitIconMapping> iconMappings; // ADICIONADO: Lista que aparece no Inspector

        private CombatSessionHub _sessionHub;
        private CombatHoverFocusMarker _hoverMarker;
        private HealthBarHudView _hudView;

        private CombatPrototypeController _activeCombatSession;
        private string _currentTrackedCombatantId = "";
        private Coroutine _initializationRoutine;

        // Otimização: Dicionário em cache para buscas ultra rápidas por nome
        private readonly Dictionary<string, Sprite> _iconCache = new();

        private void Awake()
        {
            _hudView = GetComponent<HealthBarHudView>();

            if (combatLogicCenter != null)
            {
                _sessionHub = combatLogicCenter.GetComponent<CombatSessionHub>();
                _hoverMarker = combatLogicCenter.GetComponent<CombatHoverFocusMarker>();
            }
            else
            {
                Debug.LogError($"{nameof(CombatHoverHealthBarBinder)}: O objeto 'combatLogicCenter' não foi arrastado no Inspetor!", this);
            }

            // ADICIONADO: Transforma a lista do Inspector em um dicionário rápido em memória
            InitializeIconCache();
        }

        // ADICIONADO: Preenche o dicionário usando o nome do prefab como chave
        private void InitializeIconCache()
        {
            if (iconMappings == null) return;

            foreach (var mapping in iconMappings)
            {
                if (mapping.unitPrefab != null && mapping.unitIcon != null)
                {
                    // Guardamos o nome do prefab original (sem "(Clone)")
                    string prefabName = mapping.unitPrefab.name;
                    if (!_iconCache.ContainsKey(prefabName))
                    {
                        _iconCache.Add(prefabName, mapping.unitIcon);
                    }
                }
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

            bool isAlly = targetCombatantId.StartsWith("ally", System.StringComparison.OrdinalIgnoreCase);

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

        // ALTERADO: Método renomeado de UpdateNameText para UpdateVisuals pois agora cuida da Imagem também
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
                        string rawName = capsule.gameObject.name;

                        // Limpa o "(Clone)" para podermos comparar com o Prefab original
                        if (rawName.EndsWith("(Clone)", System.StringComparison.OrdinalIgnoreCase))
                        {
                            rawName = rawName.Replace("(Clone)", "").Trim();
                        }

                        // ==========================================
                        // LÓGICA DA IMAGEM (NOVO)
                        // ==========================================
                        if (unitPortraitImage != null)
                        {
                            // Buscamos no nosso dicionário se existe uma imagem para o nome desse prefab limpo
                            if (_iconCache.TryGetValue(rawName, out Sprite unitSprite))
                            {
                                unitPortraitImage.gameObject.SetActive(true);
                                unitPortraitImage.sprite = unitSprite;
                            }
                            else
                            {
                                // Se não achar o ícone, desativa a imagem ou coloca um ícone padrão
                                unitPortraitImage.gameObject.SetActive(false);
                            }
                        }

                        // ==========================================
                        // LÓGICA DO TEXTO (MANTIDA)
                        // ==========================================
                        if (unitNameText != null)
                        {
                            rawName = rawName.Replace("_", " ");

                            string formattedName = System.Text.RegularExpressions.Regex.Replace(
                                rawName,
                                "([a-z])([A-Z])",
                                "$1 $2"
                            );

                            formattedName = System.Text.RegularExpressions.Regex.Replace(formattedName, @"\s+", " ").Trim();
                            unitNameText.text = formattedName;
                        }

                        return;
                    }
                }

                if (unitNameText != null) unitNameText.text = combatantId;
            }
        }
    }
}
