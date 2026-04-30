using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Erumperem.Combat
{
    /// <summary>
    /// Protótipo 2v4: unidades já colocadas na cena; liga <see cref="CombatCapsuleTag"/> ao estado do <see cref="BattleSimulator"/>.
    /// Clique no alvo para lançar a skill; teclas 1–7 só escolhem o slot (mesmo que o botão).
    /// Apresentação (UI, log, câmera) subscreve <see cref="CombatSessionHub"/>; este script não referencia esses serviços.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class CombatPrototypeController : MonoBehaviour
    {
        private const string ActionRockTweenId = "CombatActionRock";

        [Header("Sessão (eventos)")]
        [Tooltip("Opcional: emite apresentação e hooks de turno. Use CombatSceneViewBinder na cena para ligar UI.")]
        [SerializeField] private CombatSessionHub _sessionHub;

        [Header("Unidades na cena")]
        [Tooltip("Ordem: índice 0 = ally_1, 1 = ally_2 (deve coincidir com BattleFactory).")]
        [SerializeField] private Transform[] allyVisualRoots = new Transform[2];
        [Tooltip("Ordem: índice 0..3 = enemy_1 .. enemy_4.")]
        [SerializeField] private Transform[] enemyVisualRoots = new Transform[4];
        [Tooltip("Se ativo, escala Y do root pela % de HP (como as cápsulas antigas). Desliga para prefabs com escala fixa.")]
        [SerializeField] private bool syncHpAsVerticalScale = true;

        [Header("Debug")]
        [SerializeField] private bool logEventsToConsole = true;

        [Header("Painéis de resultado")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;

        [Header("Apresentação por ação (timings)")]
        [SerializeField] private float defaultPlaySeconds = 2.5f;
        [SerializeField] private float defaultPostPauseSeconds = 1.5f;
        [SerializeField] private CombatSkillPresentationTiming[] skillTimings = Array.Empty<CombatSkillPresentationTiming>();

        [Header("Feedback de dano (DOTween)")]
        [SerializeField] private Vector3 damagePunchScale = new(0.18f, 0.28f, 0.18f);
        [SerializeField] private float damagePunchDuration = 0.32f;
        [SerializeField] private int damagePunchVibrato = 8;
        [SerializeField] private float damagePunchElasticity = 0.55f;
        [SerializeField] private float damageShrinkDuration = 0.42f;

        [Header("Actor a agir (balanço frente–trás)")]
        [Tooltip("Força do DOPunchPosition em espaço local (ex.: Z = profundidade / frente do boneco).")]
        [SerializeField] private Vector3 actorActionRockPunch = new(0f, 0f, 0.14f);
        [SerializeField] private int actorActionRockVibrato = 12;
        [SerializeField] private float actorActionRockElasticity = 0.32f;

        private BattleState _state;
        private BattleSimulator _sim;
        private CombatEventCollector _collector;
        private SeededRandomSource _random;

        private readonly List<Combatant> _roundOrder = new();
        private int _actorIndex;
        private bool _preparedThisStep;
        private bool _battleEnded;
        private bool _needsPlayerInput;
        private Combatant _pendingPlayerActor;

        private readonly Dictionary<string, Transform> _views = new(StringComparer.Ordinal);
        private readonly HashSet<string> _damageFeedbackBusy = new(StringComparer.Ordinal);
        private bool _presentationBusy;
        private Transform _actionRockTransform;
        private Vector3 _actionRockBaseLocalPosition;
        private Combatant _selectedEnemyTarget;
        private Camera _camera;
        private int? _skillBarSelectedSlot;
        private string _skillBarSelectedOwnerId;
        private bool _leftClickPressedThisFrame;
        private bool _rightClickPressedThisFrame;
        private Vector2 _pointerScreenPosition;
        private bool _hasPointerScreenPosition;

        public BattleState BattleState => _state;
        public BattleSimulator BattleSimulator => _sim;
        public Combatant CurrentSelectedEnemy => _selectedEnemyTarget;

        public bool IsBattleOngoing => !_battleEnded && _state != null;

        public Transform TryGetUnitVisualRoot(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId) || !_views.TryGetValue(combatantId, out var root))
            {
                return null;
            }

            return root;
        }

        /// <summary>Para o marcador de turno: indica se, neste frame, o jogador comanda um herói e o marcador deve mostrar.</summary>
        public bool TryGetPlayerTurnMarkerState(out string combatantId, out bool shouldShowMarker)
        {
            combatantId = null;
            shouldShowMarker = false;
            if (_state == null || _battleEnded)
            {
                return false;
            }

            shouldShowMarker = _needsPlayerInput &&
                !_presentationBusy &&
                _pendingPlayerActor != null &&
                !_pendingPlayerActor.Health.IsDead &&
                IsPlayerControlled(_pendingPlayerActor);

            if (shouldShowMarker)
            {
                combatantId = _pendingPlayerActor.Identity.Id;
            }

            return true;
        }

        public Combatant FindCombatantById(string combatantId) => FindCombatant(combatantId);

        /// <summary>Id do herói cujo input de turno está ativo (teclas 1–7 usam isto na barra).</summary>
        public string PendingPlayerCombatantId => _pendingPlayerActor?.Identity?.Id;

        public bool IsPlayerCommandingCombatant(Combatant combatant)
        {
            if (combatant == null || _presentationBusy)
            {
                return false;
            }

            if (!_needsPlayerInput || _pendingPlayerActor == null)
            {
                return false;
            }

            return ReferenceEquals(combatant, _pendingPlayerActor) && IsPlayerControlled(combatant);
        }

        public void GetSkillBarSelection(out int? zeroBasedSlot, out string ownerCombatantId)
        {
            zeroBasedSlot = _skillBarSelectedSlot;
            ownerCombatantId = _skillBarSelectedOwnerId;
        }

        /// <summary>
        /// Único ponto para escolher um slot da hotbar (clique no botão ou tecla 1–7). Só um slot ativo:
        /// escolher outro (ex.: estava na 1 e clica ou pressiona 3) substitui a seleção anterior.
        /// </summary>
        public bool TrySelectSkillBarSlot(string ownerCombatantId, int zeroBasedSlot)
        {
            if (_battleEnded || _state == null || _presentationBusy)
            {
                return false;
            }

            if (!_needsPlayerInput || _pendingPlayerActor == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(ownerCombatantId) || zeroBasedSlot < 0 || zeroBasedSlot > 6)
            {
                return false;
            }

            if (!string.Equals(ownerCombatantId, _pendingPlayerActor.Identity.Id, StringComparison.Ordinal))
            {
                return false;
            }

            if (!CombatSkillSlotUiEligibility.IsSlotUiInteractable(
                    _state,
                    _sim,
                    _pendingPlayerActor,
                    zeroBasedSlot,
                    _selectedEnemyTarget))
            {
                return false;
            }

            _skillBarSelectedOwnerId = ownerCombatantId;
            _skillBarSelectedSlot = zeroBasedSlot;
            _sessionHub?.RaiseSkillBarBindingShouldSync();
            return true;
        }

        public void ClearSkillBarSelection()
        {
            if (!_skillBarSelectedSlot.HasValue && string.IsNullOrEmpty(_skillBarSelectedOwnerId))
            {
                return;
            }

            _skillBarSelectedSlot = null;
            _skillBarSelectedOwnerId = null;
            _sessionHub?.RaiseSkillBarSelectionClearedBySession();
        }

        public void NotifySkillBarSlotRequestFailed(int zeroBasedSlot)
        {
            if (_pendingPlayerActor == null)
            {
                return;
            }

            Debug.LogWarning($"Skill slot {zeroBasedSlot + 1} indisponível (CD, alvo, rank ou fora do loadout).");
            PublishPlayerSkillHelpForAlly(_pendingPlayerActor, FindAllyIndex(_pendingPlayerActor));
        }

        private void Awake()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError("CombatPrototypeController: defina a Main Camera na cena.");
            }
        }

        private void OnEnable()
        {
            SubscribeToInputEvents();
        }

        private void Start()
        {
            var dataDir = Path.Combine(Application.streamingAssetsPath, "Data");
            var skillsPath = Path.Combine(dataDir, "skills.json");
            var passivesPath = Path.Combine(dataDir, "passives.json");
            if (!File.Exists(skillsPath) || !File.Exists(passivesPath))
            {
                Debug.LogError(
                    $"Faltam JSON em StreamingAssets. Esperado: {skillsPath} e {passivesPath}. " +
                    "Copie a partir de Game.Simulations/Data/ ou rode tools/PublishGameCoreForUnity.ps1.");
                enabled = false;
                return;
            }

            var skills = CombatDataLoader.LoadSkills(skillsPath);
            var passives = CombatDataLoader.LoadPassives(passivesPath)
                .ToDictionary(passiveDefinition => passiveDefinition.Id, passiveDefinition => passiveDefinition);

            _random = new SeededRandomSource(UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2));
            _collector = new CombatEventCollector();
            _sim = new BattleSimulator(_random, _collector);

            _state = BattleFactory.CreateSampleBattle(
                skills,
                allyCount: 2,
                enemyCount: 4,
                corruptionValue: 0,
                allySkillIds: BattleFactory.WulfricFullSkillLoadout,
                passivesById: passives,
                unlockAllPassiveNodesForAllies: true);

            if (!TryBindSceneViewsToBattle())
            {
                enabled = false;
                return;
            }

            _sim.EmitBattleStarted(_state);
            BeginRound();
            _sessionHub?.RaiseCombatSessionReadyForUi(this);

            Debug.Log(
                "Combate: clique num herói para listar skills [1]–[7] no console; clique num inimigo para alvo; " +
                "teclas 1–7 = escolher skill; clique no alvo para lançar. Inimigos jogam até ser a tua vez.");
        }

        private void OnDisable()
        {
            UnsubscribeFromInputEvents();
            StopActorActionRock();
            foreach (var combatantIdAndTransform in _views)
            {
                combatantIdAndTransform.Value?.DOKill(false);
            }
        }

        private void SubscribeToInputEvents()
        {
            if (InputManager.Instance == null)
            {
                return;
            }

            InputManager.Instance.OnPointerPositionChanged += OnPointerPositionChanged;
            InputManager.Instance.OnLeftClickPressed += OnLeftClickPressed;
            InputManager.Instance.OnRightClickPressed += OnRightClickPressed;
        }

        private void UnsubscribeFromInputEvents()
        {
            if (InputManager.Instance == null)
            {
                return;
            }

            InputManager.Instance.OnPointerPositionChanged -= OnPointerPositionChanged;
            InputManager.Instance.OnLeftClickPressed -= OnLeftClickPressed;
            InputManager.Instance.OnRightClickPressed -= OnRightClickPressed;
        }

        private void OnPointerPositionChanged(Vector2 pointerScreenPosition)
        {
            _pointerScreenPosition = pointerScreenPosition;
            _hasPointerScreenPosition = true;
        }

        private void OnLeftClickPressed() => _leftClickPressedThisFrame = true;
        private void OnRightClickPressed() => _rightClickPressedThisFrame = true;

        private void ConsumeFrameInputFlags()
        {
            _leftClickPressedThisFrame = false;
            _rightClickPressedThisFrame = false;
        }

        private void BeginRound()
        {
            _state.TurnNumber++;
            _roundOrder.Clear();
            _roundOrder.AddRange(_sim.BuildInitiativeOrder(_state));
            _actorIndex = 0;
            _preparedThisStep = false;
        }

        private void Update()
        {
            if (_battleEnded || _state == null)
            {
                ConsumeFrameInputFlags();
                return;
            }

            while (!_battleEnded && !_needsPlayerInput && !_presentationBusy)
            {
                if (!AdvanceCombatStep())
                {
                    break;
                }
            }

            TryDeselectSkillBarWithRightButton();
            PickTargetFromMouse();
            SyncUnitVisuals();
            ConsumeFrameInputFlags();
        }

        private void PublishPlayerSkillHelpForAlly(Combatant ally, int allyIndex)
        {
            if (ally == null)
            {
                return;
            }

            var text = CombatSkillBarDebug.BuildHotbarPanelText(ally, allyIndex, _state, _sim, _selectedEnemyTarget);
            _sessionHub?.RaisePlayerSkillHelpText(text);
        }


        private int FindAllyIndex(Combatant ally)
        {
            for (var allySearchIndex = 0; allySearchIndex < _state.Allies.Count; allySearchIndex++)
            {
                if (ReferenceEquals(_state.Allies[allySearchIndex], ally))
                {
                    return allySearchIndex;
                }
            }

            return 0;
        }

        /// <summary>Botão direito: limpa o slot da skill escolhido (só um slot pode estar ativo).</summary>
        private void TryDeselectSkillBarWithRightButton()
        {
            if (!_rightClickPressedThisFrame)
            {
                return;
            }

            if (!HasSkillBarSelectionPendingUse())
            {
                return;
            }

            ClearSkillBarSelection();
        }

        private void PickTargetFromMouse()
        {
            if (!_leftClickPressedThisFrame || _camera == null || !_hasPointerScreenPosition)
            {
                return;
            }

            //if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            //{
            //    return;
            //}

            var ray = _camera.ScreenPointToRay(_pointerScreenPosition);
            if (!Physics.Raycast(ray, out var hit, 200f))
            {
                return;
            }

            var tag = hit.collider.GetComponentInParent<CombatCapsuleTag>();
            if (tag == null || string.IsNullOrEmpty(tag.combatantId))
            {
                return;
            }

            var hitAlly = _state.Allies.FirstOrDefault(ally =>
                ally.Identity.Id == tag.combatantId && !ally.Health.IsDead);
            if (hitAlly != null)
            {
                if (HasSkillBarSelectionPendingUse() && TryCastUiSelectedSkillOnTarget(hitAlly))
                {
                    return;
                }

                if (HasSkillBarSelectionPendingUse())
                {
                    Debug.LogWarning("Skill (UI) inválida para este aliado / rank / cooldown.");
                    PublishPlayerSkillHelpForAlly(_pendingPlayerActor, FindAllyIndex(_pendingPlayerActor));
                    return;
                }

                var idx = 0;
                for (var i = 0; i < _state.Allies.Count; i++)
                {
                    if (ReferenceEquals(_state.Allies[i], hitAlly))
                    {
                        idx = i;
                        break;
                    }
                }

                CombatSkillBarDebug.LogHotbar(hitAlly, idx, _state);
                PublishPlayerSkillHelpForAlly(hitAlly, idx);
                return;
            }

            var hitEnemy = _state.Enemies.FirstOrDefault(enemy =>
                enemy.Identity.Id == tag.combatantId && !enemy.Health.IsDead);
            if (hitEnemy == null)
            {
                return;
            }

            if (HasSkillBarSelectionPendingUse() && TryCastUiSelectedSkillOnTarget(hitEnemy))
            {
                return;
            }

            if (HasSkillBarSelectionPendingUse())
            {
                Debug.LogWarning("Skill (UI) inválida para este inimigo / rank / cooldown.");
                PublishPlayerSkillHelpForAlly(_pendingPlayerActor, FindAllyIndex(_pendingPlayerActor));
                return;
            }

            _selectedEnemyTarget = hitEnemy;
            Debug.Log($"Alvo: {_selectedEnemyTarget.Identity.Id} (HP {_selectedEnemyTarget.Health.CurrentHp}/{_selectedEnemyTarget.Health.MaxHp})");
            if (_needsPlayerInput && _pendingPlayerActor != null)
            {
                PublishPlayerSkillHelpForAlly(_pendingPlayerActor, FindAllyIndex(_pendingPlayerActor));
            }
        }

        private bool HasSkillBarSelectionPendingUse() =>
            _skillBarSelectedSlot.HasValue && !string.IsNullOrEmpty(_skillBarSelectedOwnerId);

        private bool TryCastUiSelectedSkillOnTarget(Combatant target)
        {
            if (!_needsPlayerInput || _pendingPlayerActor == null || _presentationBusy)
            {
                return false;
            }

            if (!HasSkillBarSelectionPendingUse())
            {
                return false;
            }

            if (!string.Equals(_skillBarSelectedOwnerId, _pendingPlayerActor.Identity.Id, StringComparison.Ordinal))
            {
                return false;
            }

            var action = PlayerActionBuilder.TryCreate(
                _state,
                _sim,
                _pendingPlayerActor,
                _skillBarSelectedSlot.Value,
                target);
            if (action == null)
            {
                return false;
            }

            if (_state.Enemies.Any(e => e.Identity.Id == target.Identity.Id && !e.Health.IsDead))
            {
                _selectedEnemyTarget = target;
            }
            else
            {
                _selectedEnemyTarget = null;
            }

            _needsPlayerInput = false;
            _pendingPlayerActor = null;
            _presentationBusy = true;
            ClearSkillBarSelection();
            StartCoroutine(
                PresentActionRoutine(
                    action,
                    () =>
                    {
                        _actorIndex++;
                        _preparedThisStep = false;
                    }));
            return true;
        }

        private bool AdvanceCombatStep()
        {
            if (_presentationBusy)
            {
                return false;
            }

            if (_state.IsFinished)
            {
                EndBattle();
                return false;
            }

            while (_actorIndex >= _roundOrder.Count)
            {
                BeginRound();
                if (_state.IsFinished)
                {
                    EndBattle();
                    return false;
                }
            }

            var actor = _roundOrder[_actorIndex];
            if (actor.Health.IsDead)
            {
                _actorIndex++;
                _preparedThisStep = false;
                return true;
            }

            if (!_preparedThisStep)
            {
                if (!_sim.TryPrepareActorTurn(_state, actor))
                {
                    _actorIndex++;
                    _preparedThisStep = false;
                    return true;
                }

                _preparedThisStep = true;
                _sessionHub?.RaiseTurnStarted();
            }

            if (IsPlayerControlled(actor))
            {
                _needsPlayerInput = true;
                _pendingPlayerActor = actor;
                _sessionHub?.RaisePlayerCommandRequired(actor);
                PublishPlayerSkillHelpForAlly(actor, FindAllyIndex(actor));
                return false;
            }

            var chosenAiAction = _sim.ChooseAiAction(_state, actor);
            if (chosenAiAction != null)
            {
                _presentationBusy = true;
                StartCoroutine(
                    PresentActionRoutine(
                        chosenAiAction,
                        () =>
                        {
                            _actorIndex++;
                            _preparedThisStep = false;
                        }));
                return false;
            }

            _actorIndex++;
            _preparedThisStep = false;
            _sessionHub?.RaiseTurnEnded();
            return true;
        }

        private static bool IsPlayerControlled(Combatant actor) =>
            actor.AI == null && actor.Identity.Faction == Faction.Player;

        private void EndBattle()
        {
            if (_battleEnded)
            {
                return;
            }

            _sessionHub?.RaiseCombatSessionClosed();
            _battleEnded = true;
            _needsPlayerInput = false;
            ClearSkillBarSelection();
            _sim.EmitBattleEnded(_state);
            LogLastEvents();
            //Debug.Log($"Batalha terminou. Vencedor: {_state.Winner}");

            if (_state.Winner == Side.Allies)
            {
                victoryPanel.SetActive(true);
            }
            else if (_state.Winner == Side.Enemies)
            {
                defeatPanel.SetActive(true);
            }
            else
            {
                Debug.Log("Empate?");
            }
        }

        private void LogLastEvents()
        {
            if (!logEventsToConsole || _collector.Events.Count == 0)
            {
                return;
            }

            var last = _collector.Events[^1];
            Debug.Log($"[Combat] {last.EventType} turn={last.Turn} actor={last.ActorId} target={last.TargetId} skill={last.SkillId} dmg={last.DamageAmount}");
        }

        private void GetTimingForSkill(string skillId, out float playSeconds, out float postPauseSeconds)
        {
            playSeconds = defaultPlaySeconds;
            postPauseSeconds = defaultPostPauseSeconds;
            if (skillTimings == null)
            {
                return;
            }

            foreach (var entry in skillTimings)
            {
                if (entry == null || string.IsNullOrEmpty(entry.skillId))
                {
                    continue;
                }

                if (!string.Equals(entry.skillId, skillId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                playSeconds = Mathf.Max(0f, entry.playSeconds);
                postPauseSeconds = Mathf.Max(0f, entry.postPauseSeconds);
                return;
            }
        }

        private IEnumerator PresentActionRoutine(ChosenAction action, Action onStepComplete)
        {
            try
            {
                StopActorActionRock();
                _sessionHub?.RaiseCinemachineFocusEnded();
                _sessionHub?.RaiseActionPresentationStarted();
                GetTimingForSkill(action.Skill.Id, out var play, out var postPause);
                var rockDuration = Mathf.Max(0f, play + postPause);

                var startIdx = _collector.Events.Count;
                _sim.ResolveChosenAction(_state, action);
                var endIdx = _collector.Events.Count;
                var count = endIdx - startIdx;
                if (count > 0)
                {
                    var slice = _collector.Events.GetRange(startIdx, count);
                    var narrativeLines = CombatNarrativeFormatter.BuildLines(_state, action, slice).ToList();
                    if (narrativeLines.Count > 0)
                    {
                        _sessionHub?.RaiseNarrativeLines(narrativeLines);
                    }

                    foreach (var combatEvent in slice)
                    {
                        if (combatEvent.EventType == BattleEventType.DamageApplied && combatEvent.DamageAmount > 0)
                        {
                            PlayDamageVisualFeedback(combatEvent.TargetId);
                        }
                    }

                    LogLastEvents();
                }

                var actorAfter = FindCombatantById(action.Actor.Identity.Id);
                if (actorAfter != null &&
                    !actorAfter.Health.IsDead &&
                    _views.TryGetValue(action.Actor.Identity.Id, out var actorVisualRoot))
                {
                    _views.TryGetValue(action.Target.Identity.Id, out var targetVisualRoot);
                    _sessionHub?.RaiseCinemachineFocusBegan(actorVisualRoot, targetVisualRoot);
                }

                if (actorAfter != null && !actorAfter.Health.IsDead && rockDuration > 0.02f)
                {
                    BeginActorActionRock(action, rockDuration);
                }

                if (play > 0f)
                {
                    yield return new WaitForSeconds(play);
                }

                if (_battleEnded)
                {
                    yield break;
                }

                if (postPause > 0f)
                {
                    yield return new WaitForSeconds(postPause);
                }
            }
            finally
            {
                _sessionHub?.RaiseCinemachineFocusEnded();
                StopActorActionRock();
                _presentationBusy = false;
                onStepComplete?.Invoke();
                _sessionHub?.RaiseTurnEnded();
                StartCoroutine(NotifyPresentationEndedDeferred());
                if (_state.IsFinished && !_battleEnded)
                {
                    EndBattle();
                }
            }
        }

        private IEnumerator NotifyPresentationEndedDeferred()
        {
            yield return null;
            _sessionHub?.RaiseActionPresentationEnded();
        }

        private void BeginActorActionRock(ChosenAction action, float totalDurationSeconds)
        {
            StopActorActionRock();
            if (totalDurationSeconds <= 0.02f)
            {
                return;
            }

            if (!_views.TryGetValue(action.Actor.Identity.Id, out var root) || root == null)
            {
                return;
            }

            _actionRockTransform = root;
            _actionRockBaseLocalPosition = root.localPosition;
            root.DOPunchPosition(
                    actorActionRockPunch,
                    totalDurationSeconds,
                    actorActionRockVibrato,
                    actorActionRockElasticity)
                .SetRelative(true)
                .SetId(ActionRockTweenId)
                .SetTarget(root)
                .OnKill(RestoreActorActionRockLocal)
                .OnComplete(RestoreActorActionRockLocal);
        }

        private void RestoreActorActionRockLocal()
        {
            if (_actionRockTransform == null)
            {
                return;
            }

            _actionRockTransform.localPosition = _actionRockBaseLocalPosition;
            _actionRockTransform = null;
        }

        private void StopActorActionRock()
        {
            DOTween.Kill(ActionRockTweenId, false);
            RestoreActorActionRockLocal();
        }

        private void PlayDamageVisualFeedback(string targetId)
        {
                    // --- ÁUDIO!!! ---
            if (AudioManager.instance != null)
            {
                AudioManager.instance.PlaySFX("Damage");
            }
  
            if (!_views.TryGetValue(targetId, out var root) || root == null)
            {
                return;
            }

            var combatant = FindCombatantById(targetId);
            if (combatant == null || combatant.Health.IsDead)
            {
                return;
            }

            _damageFeedbackBusy.Add(targetId);
            root.DOKill(false);
            var sequence = DOTween.Sequence();
            sequence.SetTarget(root);
            sequence.Append(
                root.DOPunchScale(
                    damagePunchScale,
                    damagePunchDuration,
                    damagePunchVibrato,
                    damagePunchElasticity));
            if (syncHpAsVerticalScale)
            {
                var targetY = Mathf.Max(0.3f, combatant.Health.CurrentHp / (float)combatant.Health.MaxHp);
                sequence.Append(root.DOScaleY(targetY, damageShrinkDuration).SetEase(Ease.OutCubic));
            }

            sequence.OnComplete(() => _damageFeedbackBusy.Remove(targetId));
        }

        private bool TryBindSceneViewsToBattle()
        {
            var allyCount = _state.Allies.Count;
            var enemyCount = _state.Enemies.Count;

            if (allyVisualRoots == null || allyVisualRoots.Length != allyCount)
            {
                Debug.LogError(
                    $"CombatPrototypeController: esperados {allyCount} Ally Visual Roots (ally_1..ally_{allyCount}). " +
                    $"Atual: {(allyVisualRoots == null ? 0 : allyVisualRoots.Length)}.");
                return false;
            }

            if (enemyVisualRoots == null || enemyVisualRoots.Length != enemyCount)
            {
                Debug.LogError(
                    $"CombatPrototypeController: esperados {enemyCount} Enemy Visual Roots (enemy_1..enemy_{enemyCount}). " +
                    $"Atual: {(enemyVisualRoots == null ? 0 : enemyVisualRoots.Length)}.");
                return false;
            }

            for (var allyIndex = 0; allyIndex < allyCount; allyIndex++)
            {
                var root = allyVisualRoots[allyIndex];
                if (root == null)
                {
                    Debug.LogError($"CombatPrototypeController: Ally Visual Roots[{allyIndex}] está vazio.");
                    return false;
                }

                var ally = _state.Allies[allyIndex];
                EnsureCombatCapsuleTagOnUnit(root, ally.Identity.Id);
                _views[ally.Identity.Id] = root;
            }

            for (var enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                var root = enemyVisualRoots[enemyIndex];
                if (root == null)
                {
                    Debug.LogError($"CombatPrototypeController: Enemy Visual Roots[{enemyIndex}] está vazio.");
                    return false;
                }

                var enemy = _state.Enemies[enemyIndex];
                EnsureCombatCapsuleTagOnUnit(root, enemy.Identity.Id);
                _views[enemy.Identity.Id] = root;
            }

            return true;
        }

        private static void EnsureCombatCapsuleTagOnUnit(Transform unitRoot, string combatantId)
        {
            var tag = unitRoot.GetComponentInChildren<CombatCapsuleTag>(true);
            if (tag == null)
            {
                tag = unitRoot.gameObject.AddComponent<CombatCapsuleTag>();
            }

            tag.combatantId = combatantId;
        }

        private void SyncUnitVisuals()
        {
            foreach (var combatantIdAndCapsule in _views)
            {
                var combatantId = combatantIdAndCapsule.Key;
                var unitRoot = combatantIdAndCapsule.Value;
                if (unitRoot == null)
                {
                    continue;
                }

                var combatant = FindCombatantById(combatantId);
                if (combatant == null)
                {
                    continue;
                }

                if (combatant.Health.IsDead)
                {
                    unitRoot.gameObject.SetActive(false);
                }
                else
                {
                    unitRoot.gameObject.SetActive(true);
                    if (syncHpAsVerticalScale && !_damageFeedbackBusy.Contains(combatantId))
                    {
                        unitRoot.localScale = new Vector3(
                            1f,
                            Mathf.Max(0.3f, combatant.Health.CurrentHp / (float)combatant.Health.MaxHp),
                            1f);
                    }
                }
            }
        }

        private Combatant FindCombatant(string id)
        {
            foreach (var ally in _state.Allies)
            {
                if (ally.Identity.Id == id)
                {
                    return ally;
                }
            }

            foreach (var enemy in _state.Enemies)
            {
                if (enemy.Identity.Id == id)
                {
                    return enemy;
                }
            }

            return null;
        }
    }
}
