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
    /// Protótipo 2v4: cápsulas, clique para alvo inimigo, teclas 1–7 para skills (primeiras 7 do loadout).
    /// Orquestra <see cref="BattleSimulator"/> sem duplicar regras de combate.
    /// </summary>
    public sealed class CombatPrototypeController : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private bool createGroundIfMissing = true;
        [SerializeField] private Material allyMaterial;
        [SerializeField] private Material enemyMaterial;

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
            if (createGroundIfMissing && GameObject.Find("CombatGround") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "CombatGround";
                ground.transform.localScale = new Vector3(3f, 1f, 3f);
            }

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
            var passives = CombatDataLoader.LoadPassives(passivesPath).ToDictionary(p => p.Id, p => p);

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

            SpawnViews();
            _sim.EmitBattleStarted(_state);
            BeginRound();

            Debug.Log(
                "Combate: clique num inimigo para alvo; teclas 1–7 = skills (aliado atual). " +
                "Inimigos jogam automaticamente até ser a vez de um herói.");
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

            SyncCapsuleVisuals();
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

            var hitEnemy = _state.Enemies.FirstOrDefault(e =>
                e.Identity.Id == tag.combatantId && !e.Health.IsDead);
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

            var ai = _sim.ChooseAiAction(_state, actor);
            if (ai != null)
            {
                _sim.ResolveChosenAction(_state, ai);
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
            var kb = Keyboard.current;
            if (kb == null || _pendingPlayerActor == null)
            {
                return;
            }

            for (var i = 0; i < 7; i++)
            {
                var key = Key.Digit1 + i;
                if (!kb[key].wasPressedThisFrame)
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

        private void SpawnViews()
        {
            const float spacing = 2f;
            var allyZ = -3f;
            var enemyZ = 3f;

            for (var i = 0; i < _state.Allies.Count; i++)
            {
                var c = _state.Allies[i];
                var x = (i - (_state.Allies.Count - 1) / 2f) * spacing;
                var t = CreateCapsule(c.Identity.Id, new Vector3(x, 1f, allyZ), allyMaterial, new Color(0.3f, 0.5f, 1f));
                _views[c.Identity.Id] = t;
            }

            for (var i = 0; i < _state.Enemies.Count; i++)
            {
                var c = _state.Enemies[i];
                var x = (i - (_state.Enemies.Count - 1) / 2f) * spacing;
                var t = CreateCapsule(c.Identity.Id, new Vector3(x, 1f, enemyZ), enemyMaterial, new Color(1f, 0.35f, 0.35f));
                _views[c.Identity.Id] = t;
            }
        }

        private Transform CreateCapsule(string id, Vector3 position, Material mat, Color fallback)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Unit_{id}";
            go.transform.position = position;
            var tag = go.AddComponent<CombatCapsuleTag>();
            tag.combatantId = id;
            var r = go.GetComponent<Renderer>();
            if (mat != null)
            {
                r.sharedMaterial = mat;
            }
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
                var m = new Material(sh);
                if (m.HasProperty("_BaseColor"))
                {
                    m.SetColor("_BaseColor", fallback);
                }
                else if (m.HasProperty("_Color"))
                {
                    m.SetColor("_Color", fallback);
                }

                r.sharedMaterial = m;
            }

            return go.transform;
        }

        private void SyncCapsuleVisuals()
        {
            foreach (var kv in _views)
            {
                var id = kv.Key;
                var t = kv.Value;
                if (t == null)
                {
                    continue;
                }

                var c = FindCombatant(id);
                if (c == null)
                {
                    continue;
                }

                if (c.Health.IsDead)
                {
                    t.gameObject.SetActive(false);
                }
                else
                {
                    t.localScale = new Vector3(1f, Mathf.Max(0.3f, c.Health.CurrentHp / (float)c.Health.MaxHp), 1f);
                }
            }
        }

        private Combatant FindCombatant(string id)
        {
            foreach (var a in _state.Allies)
            {
                if (a.Identity.Id == id)
                {
                    return a;
                }
            }

            foreach (var e in _state.Enemies)
            {
                if (e.Identity.Id == id)
                {
                    return e;
                }
            }

            return null;
        }
    }
}
