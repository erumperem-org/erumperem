using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using Erumperem.Characters;
using Erumperem.Combat.Runtime;
using Erumperem.Progression;
using Erumperem.UI;
using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Data;
using Game.Core.Diagnostics;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Progression;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Protótipo 2v4: unidades já colocadas na cena; liga <see cref="CombatCapsuleTag"/> ao estado do <see cref="BattleSimulator"/>.
    /// Clique no alvo para lançar a skill; teclas 1–7 só escolhem o slot (mesmo que o botão).
    /// Apresentação (UI, log, câmara) subscreve <see cref="CombatSessionHub"/>; este script não referencia esses serviços.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class CombatPrototypeController : MonoBehaviour
    {
        private const string CorruptionPulseTweenId = "CombatCorruptionPulse";

        [Header("Sessão (eventos)")]
        [Tooltip("Opcional: emite apresentação e hooks de turno. Use CombatSceneViewBinder na cena para ligar UI.")]
        [SerializeField] private CombatSessionHub _sessionHub;

        [Header("Unidades na cena")]
        [Tooltip("Ordem: índice 0 = ally_1, 1 = ally_2 (deve coincidir com BattleFactory).")]
        [SerializeField] private Transform[] allyVisualRoots = new Transform[2];
        [Tooltip("Ordem: índice 0..3 = enemy_1 .. enemy_4.")]
        [SerializeField] private Transform[] enemyVisualRoots = new Transform[4];

        [Header("Inimigos visuais (prefabs)")]
        [Tooltip("Se ativo e o catálogo tiver entradas válidas, instancia prefab por slot e injeta CombatCapsuleTag no root do instance.")]
        [SerializeField] private bool spawnEnemyModelsFromCatalog = false;

        [SerializeField] private EnemyVisualSpawnCatalog enemyVisualSpawnCatalog;

        [Tooltip("Visual/stats do Horse Boss para encounters especiais (overworld). Se vazio, procura HorseBoss no spawn catalog.")]
        [SerializeField] private EnemyVisualDefinition horseBossVisualDefinition;

        [Tooltip("Stats de combate dos aliados (Wulfric, Matsuda, etc.).")]
        [SerializeField] private AllyCharacterStatCatalog allyCharacterStatCatalog;

        [Tooltip("Stats de combate dos inimigos (BeaconOfHope, CorruptedMiner, etc.).")]
        [SerializeField] private EnemyCharacterStatCatalog enemyCharacterStatCatalog;

        [Tooltip("Legado: escala Y do root pela % de HP (cápsulas antigas). " +
                 "Se estás a usar CombatHealthBarsBinder + HealthBarHudView (UI diegética), deixa DESLIGADO. " +
                 "Por default vem desligado para não conflitar.")]
        [SerializeField] private bool syncHpAsVerticalScale = false;

        [Header("Debug")]
        [SerializeField] private bool logEventsToConsole = true;

        [Tooltip("Ignora o resultado da rolagem de iniciativa e faz a equipa dos aliados agir primeiro em cada ronda.")]
        [SerializeField] private bool forceAlliesInitiativeFirst;

        [Header("Progression")]
        [Tooltip("Opcional na cena; se vazio usa PlayerProgressionService.Instance (DontDestroyOnLoad).")]
        [SerializeField] private PlayerProgressionService _progressionService;

        [Tooltip("Desbloqueia todas as passivas do JSON para aliados (teste). Skills activas vêm do save via árvore.")]
        [SerializeField] private bool _devUnlockAllPassives;

        [Header("Combat authoring (overrides JSON by node id)")]
        [Tooltip("Para cada entrada: se passiva → entra em PassivesById; se activa → substitui/define SkillDefinition em SkillsById. JSON continua base para o que não listares aqui.")]
        [SerializeField] private SkillTreeNodeAsset[] _skillTreeAuthoringAssets =
            System.Array.Empty<SkillTreeNodeAsset>();

        [Header("Painéis de resultado")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;

        [Header("Apresentação por ação (timings)")]
        [SerializeField] private float defaultPlaySeconds = 2.5f;
        [SerializeField] private float defaultPostPauseSeconds = 1.5f;
        [SerializeField] private CombatSkillPresentationTiming[] skillTimings = Array.Empty<CombatSkillPresentationTiming>();

        [Header("Inimigos — apresentação (EnemyAnimationController)")]
        [SerializeField] private float enemyAttackClipMarginSeconds = 0.5f;
        [SerializeField] private float enemyDeathClipMarginSeconds = 1f;

        [Header("Feedback de dano (DOTween)")]
        [SerializeField] private Vector3 damagePunchScale = new(0.18f, 0.28f, 0.18f);
        [SerializeField] private float damagePunchDuration = 0.32f;
        [SerializeField] private int damagePunchVibrato = 8;
        [SerializeField] private float damagePunchElasticity = 0.55f;
        [SerializeField] private float damageShrinkDuration = 0.42f;

        [Header("Feedback de corrupção (DOTween)")]
        [Tooltip("Opcional: objeto UI ou marcador que recebe punch quando a corrupção aumenta.")]
        [SerializeField] private Transform corruptionIncreaseFeedbackRoot;
        [SerializeField] private Vector3 corruptionPulseScale = new(0.14f, 0.14f, 0.14f);
        [SerializeField] private float corruptionPulseDuration = 0.32f;
        [SerializeField] private int corruptionPulseVibrato = 10;
        [SerializeField] private float corruptionPulseElasticity = 0.45f;

        [Header("Actor a agir (balanço frente–trás)")]
        [Tooltip("Força do DOPunchPosition em espaço local (ex.: Z = profundidade / frente do boneco).")]
        [SerializeField] private Vector3 actorActionRockPunch = new(0f, 0f, 0.14f);
        [SerializeField] private int actorActionRockVibrato = 12;
        [SerializeField] private float actorActionRockElasticity = 0.32f;

        private readonly CombatSessionRuntime _runtime = new();
        private readonly CombatBattleOutcomeMonitor _battleOutcomeMonitor = new();

        private CombatPointerRaycastService _pointerRaycast;
        private CombatUnitVisualSynchronizer _unitVisualSynchronizer;
        private CombatTurnAdvanceDriver _turnAdvanceDriver;
        private CombatTurnAdvanceCallbacks _turnAdvanceCallbacks;
        private CombatPlayerTargetSelectionBridge _playerTargetSelection;
        private CombatBattleOutcomePresenter _battleOutcomePresenter;
        private CombatDebugCheatController _debugCheats;
        private CombatActionPresentationOrchestrator _actionPresentation;
        private CombatSceneUnitVisualBinder _sceneUnitVisualBinder;

        private bool _leftClickPressedThisFrame;
        private bool _rightClickPressedThisFrame;
        private Vector2 _pointerScreenPosition;
        private bool _hasPointerScreenPosition;

        public BattleState BattleState => _runtime.State;
        public BattleSimulator BattleSimulator => _runtime.Simulator;
        public Combatant CurrentSelectedEnemy => _runtime.SelectedEnemyTarget;

        public bool IsBattleOngoing => _runtime.IsBattleOngoing;

        public bool IsActionPresentationOngoing => _runtime.IsActionPresentationOngoing;

        /// <summary>
        /// Prefer over direct <see cref="BattleState"/> / <see cref="BattleSimulator"/> access when HUD only needs read-only engine state (AUDITORIA #23).
        /// </summary>
        public bool TryGetBattleEngineForHud(out BattleState battleState, out BattleSimulator battleSimulator)
        {
            battleState = _runtime.State;
            battleSimulator = _runtime.Simulator;
            return battleState != null;
        }

        public bool TryGetOngoingActionPresentationCombatantIds(
            out string actorCombatantId,
            out string targetCombatantId)
        {
            if (!_runtime.PresentationBusy)
            {
                actorCombatantId = string.Empty;
                targetCombatantId = string.Empty;
                return false;
            }

            actorCombatantId = _runtime.OngoingPresentationActorCombatantId;
            targetCombatantId = _runtime.OngoingPresentationTargetCombatantId;
            return true;
        }

        public Transform TryGetUnitVisualRoot(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId) ||
                !_runtime.UnitVisualRootsByCombatantId.TryGetValue(combatantId, out var root))
            {
                return null;
            }

            return root;
        }

        public bool TryGetPlayerTurnMarkerState(out string combatantId, out bool shouldShowMarker)
        {
            combatantId = null;
            shouldShowMarker = false;
            if (_runtime.State == null || _runtime.BattleEnded)
            {
                return false;
            }

            shouldShowMarker = _runtime.NeedsPlayerInput &&
                !_runtime.PresentationBusy &&
                _runtime.PendingPlayerActor != null &&
                !_runtime.PendingPlayerActor.Health.IsDead &&
                CombatTurnAdvanceDriver.IsPlayerControlled(_runtime.PendingPlayerActor);

            if (shouldShowMarker)
            {
                combatantId = _runtime.PendingPlayerActor.Identity.Id;
            }

            return true;
        }

        public Combatant FindCombatantById(string combatantId) => _runtime.FindCombatantById(combatantId);

        public string PendingPlayerCombatantId => _runtime.PendingPlayerActor?.Identity?.Id;

        public bool IsPlayerCommandingCombatant(Combatant combatant)
        {
            if (combatant == null || _runtime.PresentationBusy)
            {
                return false;
            }

            if (!_runtime.NeedsPlayerInput || _runtime.PendingPlayerActor == null)
            {
                return false;
            }

            return ReferenceEquals(combatant, _runtime.PendingPlayerActor) &&
                   CombatTurnAdvanceDriver.IsPlayerControlled(combatant);
        }

        public void GetSkillBarSelection(out int? zeroBasedSlot, out string ownerCombatantId)
        {
            zeroBasedSlot = _runtime.SkillBarSelectedSlot;
            ownerCombatantId = _runtime.SkillBarSelectedOwnerId;
        }

        public bool TrySelectSkillBarSlot(string ownerCombatantId, int zeroBasedSlot)
        {
            if (_runtime.BattleEnded || _runtime.State == null || _runtime.PresentationBusy)
            {
                return false;
            }

            if (!_runtime.NeedsPlayerInput || _runtime.PendingPlayerActor == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(ownerCombatantId) || zeroBasedSlot < 0 || zeroBasedSlot > 6)
            {
                return false;
            }

            if (!string.Equals(ownerCombatantId, _runtime.PendingPlayerActor.Identity.Id, StringComparison.Ordinal))
            {
                return false;
            }

            if (!CombatSkillSlotUiEligibility.IsSlotUiInteractable(
                    _runtime.State,
                    _runtime.Simulator,
                    _runtime.PendingPlayerActor,
                    zeroBasedSlot,
                    _runtime.SelectedEnemyTarget))
            {
                return false;
            }

            _runtime.SkillBarSelectedOwnerId = ownerCombatantId;
            _runtime.SkillBarSelectedSlot = zeroBasedSlot;
            _sessionHub?.RaiseSkillBarBindingShouldSync();
            return true;
        }

        public void ClearSkillBarSelection() => _playerTargetSelection?.ClearSkillBarSelection();

        public void NotifySkillBarSlotRequestFailed(int zeroBasedSlot)
        {
            if (_runtime.PendingPlayerActor == null)
            {
                return;
            }

            Debug.LogWarning($"Skill slot {zeroBasedSlot + 1} indisponível (alvo ou fora do loadout).");
            PublishPlayerSkillHelpForAlly(_runtime.PendingPlayerActor, FindAllyIndex(_runtime.PendingPlayerActor));
        }

        public void DebugKillAllEnemiesInstantly() =>
            _debugCheats?.DebugKillAllEnemiesInstantly(() => _playerTargetSelection?.ClearSkillBarSelection());

        public void DebugKillAllAlliesInstantly() =>
            _debugCheats?.DebugKillAllAlliesInstantly(() => _playerTargetSelection?.ClearSkillBarSelection());

        private void Awake()
        {
            HealDebugTrace.OnLog = static message => Debug.Log(message);
            EnsureCollaboratorsCreated();
            _pointerRaycast.Configure(Camera.main);
            if (_pointerRaycast.MainCamera == null)
            {
                Debug.LogError("CombatPrototypeController: defina a Main Camera na cena.");
            }
        }

        private void OnEnable() => SubscribeToInputEvents();

        private void Start()
        {
            var dataDir = Path.Combine(Application.streamingAssetsPath, "Data");
            var skillsPath = Path.Combine(dataDir, "skills.json");
            var skillTreesPath = Path.Combine(dataDir, "skill_trees.json");
            var passivesPath = Path.Combine(dataDir, "passives.json");
            var enemiesPath = Path.Combine(dataDir, "enemies.json");

            var hasAnyPassiveAuthoring = _skillTreeAuthoringAssets != null &&
                                         _skillTreeAuthoringAssets.Any(asset =>
                                             asset != null && asset.IsPassiveNode &&
                                             !string.IsNullOrWhiteSpace(asset.NodeId));
            var passiveJsonExists = File.Exists(passivesPath);
            if (!File.Exists(skillsPath) || !File.Exists(skillTreesPath))
            {
                Debug.LogError(
                    $"Faltam JSON em StreamingAssets. Esperado: {skillsPath} e {skillTreesPath}. " +
                    "Copie a partir de Game.Simulations/Data/ ou rode tools/PublishGameCoreForUnity.ps1.");
                enabled = false;
                return;
            }

            if (!passiveJsonExists && !hasAnyPassiveAuthoring)
            {
                Debug.LogError(
                    $"Passivas: falta {passivesPath} ou pelo menos um {nameof(SkillTreeNodeAsset)} passivo em _skillTreeAuthoringAssets.");
                enabled = false;
                return;
            }

            var skillsById = CombatDataLoader.LoadSkills(skillsPath)
                .ToDictionary(skillDefinition => skillDefinition.Id, skillDefinition => skillDefinition);
            MergeActiveSkillsFromAuthoringAssets(skillsById);
            var skills = skillsById.Values.ToList();

            Dictionary<string, PassiveDefinition> passives;
            if (passiveJsonExists)
            {
                passives = CombatDataLoader.LoadPassives(passivesPath)
                    .ToDictionary(passiveDefinition => passiveDefinition.Id, passiveDefinition => passiveDefinition);
            }
            else
            {
                passives = new Dictionary<string, PassiveDefinition>(StringComparer.Ordinal);
            }

            MergePassiveDefinitionsFromAuthoringAssets(passives);

            IReadOnlyDictionary<string, EnemyDefinition> enemyDefinitionsById =
                new Dictionary<string, EnemyDefinition>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(enemiesPath))
            {
                enemyDefinitionsById = CombatDataLoader.BuildEnemyDefinitionIndex(
                    CombatDataLoader.LoadEnemies(enemiesPath));
            }

            var skillTreesList = CombatDataLoader.LoadSkillTrees(skillTreesPath);
            var partyCharacterNames = CombatPartyResolver.GetCombatAllyCharacterNames();
            Debug.Log(
                $"CombatPrototypeController: party de combate = [{string.Join(", ", partyCharacterNames)}].",
                this);

            var progression = _progressionService != null
                ? _progressionService
                : FindFirstObjectByType<PlayerProgressionService>();
            if (progression == null && !_devUnlockAllPassives)
            {
                var autoRoot = new GameObject(nameof(PlayerProgressionService));
                progression = autoRoot.AddComponent<PlayerProgressionService>();
            }

            _runtime.Random = new SeededRandomSource(UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2));
            _runtime.EventCollector = new CombatEventCollector();
            _runtime.Simulator = new BattleSimulator(_runtime.Random, _runtime.EventCollector);

            _runtime.State = BattleFactory.CreateSampleBattle(
                skills,
                allyCount: 2,
                enemyCount: 4,
                corruptionValue: 0,
                allySkillIds: BattleFactory.DefaultAllySkillIds,
                passivesById: passives,
                unlockAllPassiveNodesForAllies: false,
                enemyDefinitionsById: enemyDefinitionsById);

            ApplyCharacterStatsFromCatalog(partyCharacterNames, applyHealth: true);

            StartCoroutine(ApplySaveToBattleStateAndStartCombatRoutine(
                partyCharacterNames,
                skillTreesList,
                progression,
                passives));
        }

        private void OnDisable()
        {
            _battleOutcomeMonitor.End();
            _debugCheats?.ClearAllCombatCheats();
            HealDebugTrace.OnLog = null;
            UnsubscribeFromInputEvents();
            _actionPresentation?.StopActorActionRock();
            DOTween.Kill(CorruptionPulseTweenId, false);
            foreach (var combatantIdAndTransform in _runtime.UnitVisualRootsByCombatantId)
            {
                combatantIdAndTransform.Value?.DOKill(false);
            }

            _unitVisualSynchronizer?.Clear();
        }

        private void EnsureCollaboratorsCreated()
        {
            _pointerRaycast ??= new CombatPointerRaycastService();
            _unitVisualSynchronizer ??= new CombatUnitVisualSynchronizer();
            _turnAdvanceDriver ??= new CombatTurnAdvanceDriver();

            _debugCheats ??= new CombatDebugCheatController(
                _runtime,
                _unitVisualSynchronizer,
                enemyDeathClipMarginSeconds);

            _battleOutcomePresenter ??= new CombatBattleOutcomePresenter(
                _runtime,
                _sessionHub,
                victoryPanel,
                defeatPanel,
                logEventsToConsole);

            _actionPresentation ??= new CombatActionPresentationOrchestrator(
                this,
                _runtime,
                _sessionHub,
                _unitVisualSynchronizer,
                BuildActionPresentationSettings(),
                logEventsToConsole);

            _sceneUnitVisualBinder ??= new CombatSceneUnitVisualBinder(
                _runtime,
                _unitVisualSynchronizer,
                BuildSceneUnitVisualBinderSettings());

            _playerTargetSelection ??= new CombatPlayerTargetSelectionBridge(
                _runtime,
                _pointerRaycast,
                _sessionHub,
                FindAllyIndex,
                PublishPlayerSkillHelpForAlly,
                _actionPresentation.PresentChosenAction);

            _turnAdvanceCallbacks ??= new CombatTurnAdvanceCallbacks
            {
                SessionHub = _sessionHub,
                ProcessTurnStartCombatEvents = ProcessTurnStartCombatEvents,
                PresentChosenAction = _actionPresentation.PresentChosenAction,
                FindAllyIndex = FindAllyIndex,
                PublishPlayerSkillHelp = PublishPlayerSkillHelpForAlly,
            };
        }

        private CombatActionPresentationSettings BuildActionPresentationSettings() =>
            new()
            {
                DefaultPlaySeconds = defaultPlaySeconds,
                DefaultPostPauseSeconds = defaultPostPauseSeconds,
                SkillTimings = skillTimings,
                EnemyAttackClipMarginSeconds = enemyAttackClipMarginSeconds,
                EnemyDeathClipMarginSeconds = enemyDeathClipMarginSeconds,
                DamagePunchScale = damagePunchScale,
                DamagePunchDuration = damagePunchDuration,
                DamagePunchVibrato = damagePunchVibrato,
                DamagePunchElasticity = damagePunchElasticity,
                DamageShrinkDuration = damageShrinkDuration,
                SyncHpAsVerticalScale = syncHpAsVerticalScale,
                CorruptionIncreaseFeedbackRoot = corruptionIncreaseFeedbackRoot,
                CorruptionPulseScale = corruptionPulseScale,
                CorruptionPulseDuration = corruptionPulseDuration,
                CorruptionPulseVibrato = corruptionPulseVibrato,
                CorruptionPulseElasticity = corruptionPulseElasticity,
                ActorActionRockPunch = actorActionRockPunch,
                ActorActionRockVibrato = actorActionRockVibrato,
                ActorActionRockElasticity = actorActionRockElasticity,
            };

        private CombatSceneUnitVisualBinderSettings BuildSceneUnitVisualBinderSettings() =>
            new()
            {
                AllyVisualRoots = allyVisualRoots,
                EnemyVisualRoots = enemyVisualRoots,
                SpawnEnemyModelsFromCatalog = spawnEnemyModelsFromCatalog,
                EnemyVisualSpawnCatalog = enemyVisualSpawnCatalog,
                HorseBossVisualDefinition = horseBossVisualDefinition,
                AllyCharacterStatCatalog = allyCharacterStatCatalog,
                EnemyCharacterStatCatalog = enemyCharacterStatCatalog,
                LogContext = this,
            };

        private IEnumerator ApplySaveToBattleStateAndStartCombatRoutine(
            IReadOnlyList<string> partyCharacterNames,
            IReadOnlyList<CharacterSkillTreesDefinition> skillTreesList,
            PlayerProgressionService progression,
            IReadOnlyDictionary<string, PassiveDefinition> passives)
        {
            var loadContext = ExplorationLoadContext.EnsureRuntimeInstance(allyCharacterStatCatalog);
            var loadSaveTask = loadContext.EnsureSaveLoadedFromDiskAsync();

            while (!loadSaveTask.IsCompleted)
            {
                yield return null;
            }

            if (loadSaveTask.IsFaulted)
            {
                Debug.LogError(
                    $"[Save] Falha ao carregar exploration_save.json: " +
                    $"{loadSaveTask.Exception?.GetBaseException().Message}");
            }

            CombatExplorationBridge.Instance?.SeedBattleFromExploration(_runtime.State);

            ApplyPerAllyLoadoutsAndProgression(
                partyCharacterNames,
                skillTreesList,
                progression,
                passives);

            if (!_sceneUnitVisualBinder.TryBindSceneViewsToBattle())
            {
                enabled = false;
                yield break;
            }

            _runtime.Simulator.EmitBattleStarted(_runtime.State);
            _battleOutcomeMonitor.Begin(_runtime.State, _runtime.EventCollector, EndBattle);
            ApplyDebugInitiativeOverrides();
            _turnAdvanceDriver.BeginRound(_runtime);
            _sessionHub?.RaiseCombatSessionReadyForUi(this);

            Debug.Log(
                "Combate: clique num herói para listar skills [1]–[7] no console; clique num inimigo para alvo; " +
                "teclas 1–7 = escolher skill; clique no alvo para lançar. Inimigos jogam até ser a tua vez.");
        }

        private void Update()
        {
            if (_runtime.BattleEnded || _runtime.State == null)
            {
                ConsumeFrameInputFlags();
                return;
            }

            while (!_runtime.BattleEnded && !_runtime.NeedsPlayerInput && !_runtime.PresentationBusy)
            {
                if (!_turnAdvanceDriver.TryAdvanceCombatStep(_runtime, _turnAdvanceCallbacks))
                {
                    break;
                }
            }

            _playerTargetSelection.TryDeselectSkillBarWithRightButton(_rightClickPressedThisFrame);
            _playerTargetSelection.PickTargetFromMouse(
                _leftClickPressedThisFrame,
                _pointerScreenPosition,
                _hasPointerScreenPosition);
            _unitVisualSynchronizer.SyncUnitVisuals(
                _runtime.UnitVisualRootsByCombatantId,
                _runtime.FindCombatantById,
                enemyDeathClipMarginSeconds,
                syncHpAsVerticalScale,
                _runtime.DamageFeedbackBusy);
            ConsumeFrameInputFlags();
        }

        private void EndBattle()
        {
            if (_runtime.BattleEnded)
            {
                return;
            }

            _battleOutcomeMonitor.End();
            _battleOutcomePresenter.EndBattle(
                _debugCheats.ClearAllCombatCheats,
                () => _playerTargetSelection?.ClearSkillBarSelection());
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
            InputManager.Instance.OnCombatCheatKillAllEnemiesPressed += OnCombatCheatKillAllEnemiesPressed;
            InputManager.Instance.OnCombatCheatKillAllAlliesPressed += OnCombatCheatKillAllAlliesPressed;
            InputManager.Instance.OnCombatCheatInfiniteAllyHealthPressed += OnCombatCheatInfiniteAllyHealthPressed;
            InputManager.Instance.OnCombatCheatDoubleAllyDamagePressed += OnCombatCheatDoubleAllyDamagePressed;
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
            InputManager.Instance.OnCombatCheatKillAllEnemiesPressed -= OnCombatCheatKillAllEnemiesPressed;
            InputManager.Instance.OnCombatCheatKillAllAlliesPressed -= OnCombatCheatKillAllAlliesPressed;
            InputManager.Instance.OnCombatCheatInfiniteAllyHealthPressed -= OnCombatCheatInfiniteAllyHealthPressed;
            InputManager.Instance.OnCombatCheatDoubleAllyDamagePressed -= OnCombatCheatDoubleAllyDamagePressed;
        }

        private void OnPointerPositionChanged(Vector2 pointerScreenPosition)
        {
            _pointerScreenPosition = pointerScreenPosition;
            _hasPointerScreenPosition = true;
        }

        private void OnLeftClickPressed() => _leftClickPressedThisFrame = true;
        private void OnRightClickPressed() => _rightClickPressedThisFrame = true;
        private void OnCombatCheatKillAllEnemiesPressed() => DebugKillAllEnemiesInstantly();
        private void OnCombatCheatKillAllAlliesPressed() => DebugKillAllAlliesInstantly();
        private void OnCombatCheatInfiniteAllyHealthPressed() => _debugCheats?.ToggleInfiniteAllyHealthCheat();
        private void OnCombatCheatDoubleAllyDamagePressed() => _debugCheats?.ToggleDoubleAllyDamageCheat();

        private void ConsumeFrameInputFlags()
        {
            _leftClickPressedThisFrame = false;
            _rightClickPressedThisFrame = false;
        }

        private void ApplyDebugInitiativeOverrides()
        {
            if (!forceAlliesInitiativeFirst || _runtime.State?.Initiative is null)
            {
                return;
            }

            var rolledInitiative = _runtime.State.Initiative;
            _runtime.State.Initiative = new BattleInitiativeSnapshot
            {
                FirstActingSide = Side.Allies,
                AllyTeamTotal = rolledInitiative.AllyTeamTotal,
                EnemyTeamTotal = rolledInitiative.EnemyTeamTotal,
                RollsByCombatantId = rolledInitiative.RollsByCombatantId,
            };

            if (logEventsToConsole)
            {
                Debug.Log(
                    $"Debug: iniciativa forçada para aliados " +
                    $"(rolagem original: aliados {rolledInitiative.AllyTeamTotal} vs inimigos {rolledInitiative.EnemyTeamTotal}, " +
                    $"vencedor seria {rolledInitiative.FirstActingSide}).");
            }
        }

        private void PublishPlayerSkillHelpForAlly(Combatant ally, int allyIndex)
        {
            if (ally == null)
            {
                return;
            }

            var text = CombatSkillBarDebug.BuildHotbarPanelText(
                ally,
                allyIndex,
                _runtime.State,
                _runtime.Simulator,
                _runtime.SelectedEnemyTarget);
            _sessionHub?.RaisePlayerSkillHelpText(text);
        }

        private int FindAllyIndex(Combatant ally)
        {
            for (var allySearchIndex = 0; allySearchIndex < _runtime.State.Allies.Count; allySearchIndex++)
            {
                if (ReferenceEquals(_runtime.State.Allies[allySearchIndex], ally))
                {
                    return allySearchIndex;
                }
            }

            return 0;
        }

        private void ProcessTurnStartCombatEvents(int turnEventStartIndex)
        {
            if (_runtime.EventCollector == null || _runtime.State == null ||
                turnEventStartIndex >= _runtime.EventCollector.Events.Count)
            {
                return;
            }

            var turnEvents = _runtime.EventCollector.Events.GetRange(
                turnEventStartIndex,
                _runtime.EventCollector.Events.Count - turnEventStartIndex);
            var narrativeLines = new List<string>();
            foreach (var combatEvent in turnEvents)
            {
                if (combatEvent.EventType == BattleEventType.CombatantSpawned)
                {
                    _sceneUnitVisualBinder.TrySpawnSummonedEnemyVisual(combatEvent);
                    var summonLine = PlayerFacingText.FormatCombatantSpawnedLine(_runtime.State, combatEvent);
                    if (!string.IsNullOrEmpty(summonLine))
                    {
                        narrativeLines.Add(summonLine);
                    }
                }
            }

            if (narrativeLines.Count > 0)
            {
                _sessionHub?.RaiseNarrativeLines(narrativeLines);
            }

            if (logEventsToConsole && _runtime.EventCollector.Events.Count > 0)
            {
                var lastEvent = _runtime.EventCollector.Events[^1];
                Debug.Log(
                    $"[Combat] {lastEvent.EventType} turn={lastEvent.Turn} actor={lastEvent.ActorId} " +
                    $"target={lastEvent.TargetId} skill={lastEvent.SkillId} dmg={lastEvent.DamageAmount}");
            }
        }

        private void ApplyCharacterStatsFromCatalog(
            IReadOnlyList<string> partyCharacterNames,
            bool applyHealth = true)
        {
            if (allyCharacterStatCatalog == null || _runtime.State == null)
            {
                return;
            }

            partyCharacterNames ??= CombatPartyResolver.GetCombatAllyCharacterNames();

            for (var allyIndex = 0; allyIndex < _runtime.State.Allies.Count && allyIndex < partyCharacterNames.Count; allyIndex++)
            {
                var characterName = partyCharacterNames[allyIndex];
                if (!allyCharacterStatCatalog.TryGetDefinition(characterName, out var allyCharacterStatDefinition))
                {
                    continue;
                }

                var ally = _runtime.State.Allies[allyIndex];
                allyCharacterStatDefinition.ApplyToCombatant(
                    ally,
                    preserveCurrentHitPoints: false,
                    applyHealth: applyHealth);

                if (ally.Position != null)
                {
                    ally.Position.FrontRank = Mathf.Max(1, allyCharacterStatDefinition.BattleFormationRank);
                }
            }
        }

        private void ApplyPerAllyLoadoutsAndProgression(
            IReadOnlyList<string> partyCharacterNames,
            IReadOnlyList<CharacterSkillTreesDefinition> skillTreesList,
            PlayerProgressionService progression,
            IReadOnlyDictionary<string, PassiveDefinition> passivesById)
        {
            if (_runtime.State == null || partyCharacterNames == null)
            {
                return;
            }

            for (var allyIndex = 0; allyIndex < _runtime.State.Allies.Count && allyIndex < partyCharacterNames.Count; allyIndex++)
            {
                var characterName = partyCharacterNames[allyIndex];
                var ally = _runtime.State.Allies[allyIndex];
                var progressionCharacterId = ResolveProgressionCharacterId(characterName);
                var innateSkillIds = ResolveInnateSkillIds(progressionCharacterId);
                List<string> allySkillIds;

                IReadOnlyDictionary<string, bool> unlockedForBattle =
                    new Dictionary<string, bool>(StringComparer.Ordinal);

                if (!string.IsNullOrWhiteSpace(progressionCharacterId))
                {
                    var characterTrees = SkillTreeLookup.FindCharacterTrees(skillTreesList, progressionCharacterId);
                    if (characterTrees != null && progression != null)
                    {
                        unlockedForBattle = progression.GetUnlockedNodesForCharacter(progressionCharacterId);
                        allySkillIds = SkillTreeLookup.BuildPlayerSkillLoadout(
                            characterTrees,
                            unlockedForBattle,
                            innateSkillIds);
                        var pointsSpent = SkillTreeLookup.SumUnlockedNodeCosts(characterTrees, unlockedForBattle);
                        ApplyLoadoutAndProgressionToAlly(ally, allySkillIds, unlockedForBattle, pointsSpent);
                        continue;
                    }

                    if (characterTrees != null)
                    {
                        allySkillIds = SkillTreeLookup.BuildPlayerSkillLoadout(
                            characterTrees,
                            unlockedForBattle,
                            innateSkillIds);
                        ApplyLoadoutAndProgressionToAlly(ally, allySkillIds, unlockedForBattle, pointsSpent: 0);
                        continue;
                    }
                }

                allySkillIds = innateSkillIds.Count > 0
                    ? innateSkillIds.ToList()
                    : BattleFactory.DefaultAllySkillIds.ToList();
                ApplyLoadoutAndProgressionToAlly(ally, allySkillIds, unlockedForBattle, pointsSpent: 0);
            }

            if (_devUnlockAllPassives && passivesById != null)
            {
                BattleFactory.UnlockAllPassivesFromCatalog(_runtime.State, passivesById);
            }
        }

        private static void ApplyLoadoutAndProgressionToAlly(
            Combatant ally,
            IReadOnlyList<string> allySkillIds,
            IReadOnlyDictionary<string, bool> unlockedForBattle,
            int pointsSpent)
        {
            ally.SkillLoadout.Skills.Clear();
            if (allySkillIds != null)
            {
                foreach (var skillId in allySkillIds)
                {
                    if (!string.IsNullOrWhiteSpace(skillId))
                    {
                        ally.SkillLoadout.Skills.Add(skillId);
                    }
                }
            }

            ally.Progression.UnlockedNodes.Clear();
            foreach (var nodeIdAndUnlocked in unlockedForBattle)
            {
                ally.Progression.UnlockedNodes[nodeIdAndUnlocked.Key] = nodeIdAndUnlocked.Value;
            }

            ally.Progression.SpentPoints = pointsSpent;
        }

        private string ResolveProgressionCharacterId(string characterName)
        {
            if (allyCharacterStatCatalog != null &&
                allyCharacterStatCatalog.TryGetDefinition(characterName, out var allyCharacterStatDefinition) &&
                !string.IsNullOrWhiteSpace(allyCharacterStatDefinition.ProgressionCharacterId))
            {
                return allyCharacterStatDefinition.ProgressionCharacterId;
            }

            return null;
        }

        private static IReadOnlyList<string> ResolveInnateSkillIds(string progressionCharacterId) =>
            BattleFactory.ResolveInnateSkillIds(progressionCharacterId);

        private void MergeActiveSkillsFromAuthoringAssets(Dictionary<string, SkillDefinition> skillsById)
        {
            if (_skillTreeAuthoringAssets == null)
            {
                return;
            }

            foreach (var asset in _skillTreeAuthoringAssets)
            {
                if (asset == null || asset.IsPassiveNode || string.IsNullOrWhiteSpace(asset.NodeId))
                {
                    continue;
                }

                try
                {
                    skillsById[asset.NodeId] = asset.ToRuntimeSkillDefinition();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"CombatPrototypeController: activo SO '{asset.name}' ignorado — {ex.Message}",
                        asset);
                }
            }
        }

        private void MergePassiveDefinitionsFromAuthoringAssets(Dictionary<string, PassiveDefinition> passivesById)
        {
            if (_skillTreeAuthoringAssets == null)
            {
                return;
            }

            foreach (var asset in _skillTreeAuthoringAssets)
            {
                if (asset == null || !asset.IsPassiveNode || string.IsNullOrWhiteSpace(asset.NodeId))
                {
                    continue;
                }

                try
                {
                    passivesById[asset.NodeId] = asset.ToRuntimePassiveDefinition();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"CombatPrototypeController: passiva SO '{asset.name}' ignorada — {ex.Message}",
                        asset);
                }
            }
        }
    }
}
