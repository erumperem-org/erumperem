using UnityEngine;
using Erumperem.Combat.HealthBars;

namespace Erumperem.Combat.HealthBars
{
    [RequireComponent(typeof(HealthBarHudView))]
    public sealed class CombatHoverHealthBarBinder : MonoBehaviour
    {
        [Header("Referência Central (Arraste o 'combatlogc' aqui)")]
        [SerializeField] private GameObject combatLogicCenter;

        [Header("Filtro de Alvo")]
        [Tooltip("Marque TRUE se esta barra for para exibir apenas os Aliados/Players. Marque FALSE se for apenas para Inimigos.")]
        [SerializeField] private bool isPlayerBar = false;

        private CombatSessionHub _sessionHub;
        private CombatHoverFocusMarker _hoverMarker;
        private HealthBarHudView _hudView;

        private CombatPrototypeController _activeCombatSession;
        private string _currentTrackedCombatantId = "";

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
        }

        private void HandleCombatSessionReady(CombatPrototypeController controller)
        {
            _activeCombatSession = controller;
        }

        private void HandleCombatSessionClosed()
        {
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

            // Busca o ID do inimigo/unidade sob o mouse
            string targetCombatantId = FindCurrentHoveredCombatantId();

            if (string.IsNullOrEmpty(targetCombatantId))
            {
                return;
            }

            // NOVO: Validação do tipo de unidade (Filtro por prefixo do ID)
            bool isAlly = targetCombatantId.StartsWith("ally", System.StringComparison.OrdinalIgnoreCase);

            // Se eu sou uma barra de Player mas o hover está num inimigo, OU
            // se eu sou uma barra de Inimigo mas o hover está num aliado -> Ignora o comando!
            if (isPlayerBar != isAlly)
            {
                return;
            }

            // Se passou no filtro e é um ID diferente do atual, atualiza a barra
            if (targetCombatantId != _currentTrackedCombatantId)
            {
                _currentTrackedCombatantId = targetCombatantId;
                _hudView.Configure(_activeCombatSession, _currentTrackedCombatantId);
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
    }
}
