using System.Collections.Generic;
using Erumperem.Combat;
using Game.Core.Domain;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.Tokens
{
    /// <summary>
    /// Per-combatant body VFX for tokens: instantiate under socket on first stack,
    /// <see cref="GameObject.SetActive"/> false when stacks return to zero (keeps instance for reuse).
    /// Local scale stays identity so the effect inherits the unit hierarchy scale.
    /// </summary>
    public sealed class CombatantTokenStatusVfxPresenter
    {
        private readonly CombatPrototypeController _combatSession;
        private readonly string _combatantId;
        private readonly TokenVisualCatalog _catalog;
        private readonly Transform _unitVisualRoot;
        private readonly Dictionary<TokenStatusVfxSocketSlot, Transform> _socketsBySlot = new();
        private readonly Dictionary<TokenType, GameObject> _activeVfxByTokenType = new();
        private readonly HashSet<TokenStatusVfxSocketSlot> _missingSocketWarned = new();
        private bool _socketsCached;

        public CombatantTokenStatusVfxPresenter(
            CombatPrototypeController combatSession,
            string combatantId,
            TokenVisualCatalog catalog,
            Transform unitVisualRoot)
        {
            _combatSession = combatSession;
            _combatantId = combatantId ?? string.Empty;
            _catalog = catalog;
            _unitVisualRoot = unitVisualRoot;
        }

        public void RefreshFromBattleState()
        {
            if (_combatSession == null
                || _catalog == null
                || _unitVisualRoot == null
                || string.IsNullOrEmpty(_combatantId))
            {
                DisableAllActiveVfx();
                return;
            }

            if (!_combatSession.IsBattleOngoing)
            {
                DisableAllActiveVfx();
                return;
            }

            var combatant = _combatSession.FindCombatantById(_combatantId);
            if (combatant == null || combatant.Health.IsDead)
            {
                DisableAllActiveVfx();
                return;
            }

            EnsureSocketsCached();

            foreach (var definition in _catalog.Entries)
            {
                if (definition == null || !definition.HasStatusVfx)
                {
                    continue;
                }

                SyncTokenVfx(combatant, definition);
            }
        }

        public void TearDown()
        {
            foreach (var tokenTypeAndVfx in _activeVfxByTokenType)
            {
                var vfxInstance = tokenTypeAndVfx.Value;
                if (vfxInstance != null)
                {
                    Object.Destroy(vfxInstance);
                }
            }

            _activeVfxByTokenType.Clear();
            _socketsBySlot.Clear();
            _missingSocketWarned.Clear();
            _socketsCached = false;
        }

        private void SyncTokenVfx(Combatant combatant, TokenVisualDefinition definition)
        {
            var tokenType = definition.TokenType;
            var stackCount = combatant.Tokens.GetStacks(tokenType);
            var hasStacks = stackCount > 0;

            if (!hasStacks)
            {
                if (_activeVfxByTokenType.TryGetValue(tokenType, out var existingVfx) && existingVfx != null)
                {
                    if (existingVfx.activeSelf)
                    {
                        existingVfx.SetActive(false);
                    }
                }

                return;
            }

            if (!_socketsBySlot.TryGetValue(definition.statusVfxSocket, out var socketTransform)
                || socketTransform == null)
            {
                WarnMissingSocketOnce(definition.statusVfxSocket);
                return;
            }

            if (_activeVfxByTokenType.TryGetValue(tokenType, out var spawnedVfx) && spawnedVfx != null)
            {
                if (!spawnedVfx.activeSelf)
                {
                    spawnedVfx.SetActive(true);
                    RestartParticleSystems(spawnedVfx);
                }

                return;
            }

            var vfxInstance = Object.Instantiate(definition.statusVfxPrefab, socketTransform);
            vfxInstance.name = $"TokenVfx_{tokenType}";
            vfxInstance.transform.localPosition = Vector3.zero;
            vfxInstance.transform.localRotation = Quaternion.identity;
            vfxInstance.transform.localScale = Vector3.one;
            vfxInstance.SetActive(true);
            RestartParticleSystems(vfxInstance);
            _activeVfxByTokenType[tokenType] = vfxInstance;
        }

        private void DisableAllActiveVfx()
        {
            foreach (var tokenTypeAndVfx in _activeVfxByTokenType)
            {
                var vfxInstance = tokenTypeAndVfx.Value;
                if (vfxInstance != null && vfxInstance.activeSelf)
                {
                    vfxInstance.SetActive(false);
                }
            }
        }

        private void EnsureSocketsCached()
        {
            if (_socketsCached || _unitVisualRoot == null)
            {
                return;
            }

            _socketsCached = true;
            foreach (TokenStatusVfxSocketSlot socketSlot in System.Enum.GetValues(typeof(TokenStatusVfxSocketSlot)))
            {
                var socketName = TokenStatusVfxSocketNames.Resolve(socketSlot);
                if (string.IsNullOrEmpty(socketName))
                {
                    continue;
                }

                var socketTransform = FindDescendantNamed(_unitVisualRoot, socketName);
                if (socketTransform != null)
                {
                    _socketsBySlot[socketSlot] = socketTransform;
                }
            }
        }

        private void WarnMissingSocketOnce(TokenStatusVfxSocketSlot socketSlot)
        {
            if (!_missingSocketWarned.Add(socketSlot))
            {
                return;
            }

            var socketName = TokenStatusVfxSocketNames.Resolve(socketSlot);
            Debug.LogWarning(
                $"{nameof(CombatantTokenStatusVfxPresenter)}: combatant '{_combatantId}' visual " +
                $"'{_unitVisualRoot.name}' has no '{socketName}' — token VFX for that slot skipped.",
                _unitVisualRoot);
        }

        private static void RestartParticleSystems(GameObject vfxRoot)
        {
            var particleSystems = vfxRoot.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particleSystem in particleSystems)
            {
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }
        }

        private static Transform FindDescendantNamed(Transform root, string targetName)
        {
            for (var childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                var child = root.GetChild(childIndex);
                if (child.name == targetName)
                {
                    return child;
                }

                var nested = FindDescendantNamed(child, targetName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
