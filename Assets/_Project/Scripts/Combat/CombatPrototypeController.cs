using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Erumperem.Combat
{
    /// <summary>
    /// Protótipo 2v4: unidades já colocadas na cena; liga <see cref="CombatCapsuleTag"/> ao estado do <see cref="BattleSimulator"/>.
    /// Clique para alvo inimigo / hotbar; teclas 1–7 para skills.
    /// </summary>
    public sealed class CombatPrototypeController : MonoBehaviour
    {
        [Header("Unidades na cena")]
        [Tooltip("Ordem: índice 0 = ally_1, 1 = ally_2 (deve coincidir com BattleFactory).")]
        [SerializeField] private Transform[] allyVisualRoots = new Transform[2];
        [Tooltip("Ordem: índice 0..3 = enemy_1 .. enemy_4.")]
        [SerializeField] private Transform[] enemyVisualRoots = new Transform[4];
        [Tooltip("Se ativo, escala Y do root pela % de HP (como as cápsulas antigas). Desliga para prefabs com escala fixa.")]
        [SerializeField] private bool syncHpAsVerticalScale = true;

        [Header("Debug")]
        [SerializeField] private bool logEventsToConsole = true;

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
        private Combatant _selectedEnemyTarget;
        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError("CombatPrototypeController: defina a Main Camera na cena.");
            }

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

            Debug.Log(
                "Combate: clique num herói para listar skills [1]–[7] no console; clique num inimigo para alvo; " +
                "teclas 1–7 = skill (no turno do herói). Inimigos jogam até ser a tua vez.");
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
                return;
            }

            PickTargetFromMouse();

            while (!_battleEnded && !_needsPlayerInput)
            {
                if (!AdvanceCombatStep())
                {
                    break;
                }
            }

            if (_needsPlayerInput)
            {
                TryPlayerHotkeys();
            }

            SyncUnitVisuals();
        }

        private void PickTargetFromMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || _camera == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            var ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
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
                return;
            }

            var hitEnemy = _state.Enemies.FirstOrDefault(enemy =>
                enemy.Identity.Id == tag.combatantId && !enemy.Health.IsDead);
            if (hitEnemy == null)
            {
                return;
            }

            _selectedEnemyTarget = hitEnemy;
            Debug.Log($"Alvo: {_selectedEnemyTarget.Identity.Id} (HP {_selectedEnemyTarget.Health.CurrentHp}/{_selectedEnemyTarget.Health.MaxHp})");
        }

        private bool AdvanceCombatStep()
        {
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
            }

            if (IsPlayerControlled(actor))
            {
                _needsPlayerInput = true;
                _pendingPlayerActor = actor;
                return false;
            }

            var chosenAiAction = _sim.ChooseAiAction(_state, actor);
            if (chosenAiAction != null)
            {
                _sim.ResolveChosenAction(_state, chosenAiAction);
                LogLastEvents();
            }

            _actorIndex++;
            _preparedThisStep = false;
            return true;
        }

        private static bool IsPlayerControlled(Combatant actor) =>
            actor.AI == null && actor.Identity.Faction == Faction.Player;

        private void TryPlayerHotkeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _pendingPlayerActor == null)
            {
                return;
            }

            for (var i = 0; i < 7; i++)
            {
                var key = Key.Digit1 + i;
                if (!keyboard[key].wasPressedThisFrame)
                {
                    continue;
                }

                var action = PlayerActionBuilder.TryCreate(
                    _state,
                    _sim,
                    _pendingPlayerActor,
                    i,
                    _selectedEnemyTarget);

                if (action == null)
                {
                    Debug.LogWarning($"Skill slot {i + 1} inválida (CD, alvo, rank ou fora do loadout).");
                    return;
                }

                _sim.ResolveChosenAction(_state, action);
                LogLastEvents();
                _actorIndex++;
                _preparedThisStep = false;
                _needsPlayerInput = false;
                _pendingPlayerActor = null;
                return;
            }
        }

        private void EndBattle()
        {
            if (_battleEnded)
            {
                return;
            }

            _battleEnded = true;
            _needsPlayerInput = false;
            _sim.EmitBattleEnded(_state);
            LogLastEvents();
            Debug.Log($"Batalha terminou. Vencedor: {_state.Winner}");
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

                var combatant = FindCombatant(combatantId);
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
                    if (syncHpAsVerticalScale)
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
