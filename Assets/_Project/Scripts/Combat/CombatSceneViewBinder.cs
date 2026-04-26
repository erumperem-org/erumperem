using System.Collections.Generic;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Liga serviços de apresentação (UI, log, Cinemachine, marcador de turno) ao
    /// <see cref="CombatSessionHub"/>. O controlador de combate não referencia estes tipos.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class CombatSceneViewBinder : MonoBehaviour
    {
        [SerializeField] private CombatPrototypeController combatSession;
        [SerializeField] private CombatSessionHub sessionHub;

        [Header("Apresentação")]
        [SerializeField] private CombatLogStackView combatLog;
        [SerializeField] private CombatPresentationHub presentationHub;

        [Header("Cinemachine (opcional)")]
        [SerializeField] private CombatCinemachineDirector combatCinemachineDirector;

        [Header("UI — barra de skills")]
        [SerializeField] private CombatSkillButtonBarUIManager skillButtonBarUIManager;

        [Header("Indicador de turno (opcional)")]
        [SerializeField] private GameObject currentTurnMarkerPrefab;
        [SerializeField] private Vector3 currentTurnMarkerLocalOffset;
        [SerializeField] private Vector3 currentTurnMarkerBaseEuler = new(90f, 0f, 0f);
        [SerializeField] private float currentTurnMarkerZSpinDegreesPerSecond = 90f;

        private Transform _currentTurnMarkerTransform;
        private string _turnMarkerLastCombatantId;
        private float _turnMarkerSpinZDegrees;
        private bool _skillBarInitialized;

        private void OnEnable()
        {
            if (sessionHub == null)
            {
                return;
            }

            sessionHub.OnPlayerSkillHelpText += HandlePlayerSkillHelpText;
            sessionHub.OnNarrativeLines += HandleNarrativeLines;
            sessionHub.OnActionPresentationStarted += HandleActionPresentationStarted;
            sessionHub.OnActionPresentationEnded += HandleActionPresentationEnded;
            sessionHub.OnCombatSessionClosed += HandleCombatSessionClosed;
            sessionHub.OnCinemachineFocusBegan += HandleCinemachineFocusBegan;
            sessionHub.OnCinemachineFocusEnded += HandleCinemachineFocusEnded;
            sessionHub.OnSkillBarBindingShouldSync += HandleSkillBarBindingShouldSync;
            sessionHub.OnSkillBarSelectionClearedBySession += HandleSkillBarSelectionClearedBySession;
            sessionHub.OnCombatSessionReadyForUi += HandleCombatSessionReadyForUi;
        }

        private void OnDisable()
        {
            if (sessionHub == null)
            {
                return;
            }

            sessionHub.OnPlayerSkillHelpText -= HandlePlayerSkillHelpText;
            sessionHub.OnNarrativeLines -= HandleNarrativeLines;
            sessionHub.OnActionPresentationStarted -= HandleActionPresentationStarted;
            sessionHub.OnActionPresentationEnded -= HandleActionPresentationEnded;
            sessionHub.OnCombatSessionClosed -= HandleCombatSessionClosed;
            sessionHub.OnCinemachineFocusBegan -= HandleCinemachineFocusBegan;
            sessionHub.OnCinemachineFocusEnded -= HandleCinemachineFocusEnded;
            sessionHub.OnSkillBarBindingShouldSync -= HandleSkillBarBindingShouldSync;
            sessionHub.OnSkillBarSelectionClearedBySession -= HandleSkillBarSelectionClearedBySession;
            sessionHub.OnCombatSessionReadyForUi -= HandleCombatSessionReadyForUi;

            if (_currentTurnMarkerTransform != null)
            {
                Destroy(_currentTurnMarkerTransform.gameObject);
                _currentTurnMarkerTransform = null;
            }

            _turnMarkerLastCombatantId = null;
        }

        private void Update()
        {
            if (combatSession == null || skillButtonBarUIManager == null)
            {
                return;
            }

            if (_skillBarInitialized && combatSession.IsBattleOngoing)
            {
                skillButtonBarUIManager.Tick();
            }
        }

        private void LateUpdate()
        {
            SyncCurrentTurnMarker();
        }

        private void HandleCombatSessionReadyForUi(CombatPrototypeController controller)
        {
            if (controller == null || skillButtonBarUIManager == null || _skillBarInitialized)
            {
                return;
            }

            if (combatSession == null)
            {
                combatSession = controller;
            }

            skillButtonBarUIManager.Initialize(controller);
            _skillBarInitialized = true;
        }

        private void HandlePlayerSkillHelpText(string text)
        {
            if (presentationHub == null)
            {
                return;
            }

            presentationHub.PublishPlayerSkillHelp(text);
        }

        private void HandleNarrativeLines(IReadOnlyList<string> lines)
        {
            if (combatLog == null || lines == null || lines.Count == 0)
            {
                return;
            }

            foreach (var line in lines)
            {
                combatLog.Push(line);
            }
        }

        private void HandleActionPresentationStarted()
        {
            presentationHub?.PublishActionPresentationStarted();
        }

        private void HandleActionPresentationEnded()
        {
            presentationHub?.PublishActionPresentationEnded();
        }

        private void HandleCombatSessionClosed()
        {
            presentationHub?.PublishCombatEnded();
            skillButtonBarUIManager?.OnBattleEnded();
        }

        private void HandleCinemachineFocusBegan(Transform actorRoot, Transform targetRoot)
        {
            combatCinemachineDirector?.BeginActionFocus(actorRoot, targetRoot);
        }

        private void HandleCinemachineFocusEnded()
        {
            combatCinemachineDirector?.EndActionFocus();
        }

        private void HandleSkillBarBindingShouldSync()
        {
            skillButtonBarUIManager?.SyncVisibleRowWithBattle();
        }

        private void HandleSkillBarSelectionClearedBySession()
        {
            skillButtonBarUIManager?.OnSkillBarSelectionCleared();
        }

        private void DeactivateCurrentTurnMarker()
        {
            if (_currentTurnMarkerTransform == null)
            {
                return;
            }

            _currentTurnMarkerTransform.gameObject.SetActive(false);
            _turnMarkerLastCombatantId = null;
        }

        private void SyncCurrentTurnMarker()
        {
            if (currentTurnMarkerPrefab == null || combatSession == null)
            {
                return;
            }

            if (!combatSession.TryGetPlayerTurnMarkerState(out var combatantId, out var shouldShowMarker))
            {
                DeactivateCurrentTurnMarker();
                return;
            }

            if (!shouldShowMarker || string.IsNullOrEmpty(combatantId))
            {
                DeactivateCurrentTurnMarker();
                return;
            }

            var parent = combatSession.TryGetUnitVisualRoot(combatantId);
            if (parent == null)
            {
                return;
            }

            if (_currentTurnMarkerTransform == null)
            {
                var turnMarkerObject = Instantiate(currentTurnMarkerPrefab, parent, false);
                _currentTurnMarkerTransform = turnMarkerObject.transform;
            }
            else
            {
                if (_currentTurnMarkerTransform.parent != parent)
                {
                    _currentTurnMarkerTransform.SetParent(parent, false);
                }

                if (!_currentTurnMarkerTransform.gameObject.activeSelf)
                {
                    _currentTurnMarkerTransform.gameObject.SetActive(true);
                }
            }

            if (!string.Equals(_turnMarkerLastCombatantId, combatantId, System.StringComparison.Ordinal))
            {
                _turnMarkerLastCombatantId = combatantId;
                _turnMarkerSpinZDegrees = 0f;
            }

            _currentTurnMarkerTransform.localPosition = currentTurnMarkerLocalOffset;
            _turnMarkerSpinZDegrees += currentTurnMarkerZSpinDegreesPerSecond * Time.deltaTime;
            if (_turnMarkerSpinZDegrees >= 360f)
            {
                _turnMarkerSpinZDegrees -= 360f;
            }

            var baseEuler = currentTurnMarkerBaseEuler;
            _currentTurnMarkerTransform.localEulerAngles = new Vector3(
                baseEuler.x,
                baseEuler.y,
                baseEuler.z + _turnMarkerSpinZDegrees);
        }
    }
}
