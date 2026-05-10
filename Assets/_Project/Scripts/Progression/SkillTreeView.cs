using System;
using System.Collections.Generic;
using Game.Core.Models;
using Game.Core.Progression;
using TMPro;
using UnityEngine;

namespace Erumperem.Progression
{
    /// <summary>
    /// Drop on any GameObject in the skill tree scene. Discovers every <see cref="SkillTreeNodePresenter"/>
    /// in the scene (active or inactive) and syncs interactability + tint with <see cref="PlayerProgressionService"/>.
    /// </summary>
    public sealed class SkillTreeView : MonoBehaviour
    {
        [Serializable]
        public struct DetailUiBindings
        {
            public TMP_Text Title;
            public TMP_Text Body;

            public void Apply(SkillTreeNodeAsset nodeAsset)
            {
                if (Title != null)
                {
                    Title.text = nodeAsset.DisplayName;
                }

                if (Body != null)
                {
                    Body.text = nodeAsset.DescriptionForUi;
                }
            }
        }

        [Header("Data")]
        [SerializeField] private string _characterId = "wulfric";
        [SerializeField] private PlayerProgressionService _progressionService;

        [Header("Tints (applied to each button image)")]
        [SerializeField] private Color _lockedTint = new(0.55f, 0.55f, 0.6f, 1f);
        [SerializeField] private Color _availableTint = Color.white;
        [SerializeField] private Color _unlockedTint = new(0.4f, 0.85f, 0.45f, 1f);

        [Header("UI (optional)")]
        [SerializeField] private TMP_Text _pointsLabel;
        [SerializeField] private DetailUiBindings _detailPanel;

        [Header("Diagnostics")]
        [Tooltip("Logs why each node ends up Locked.")]
        [SerializeField] private bool _logVisualStateDecisions;

        private readonly List<SkillTreeNodePresenter> _presenters = new();
        private CharacterSkillTreesDefinition _characterTrees;
        private bool _subscribedToService;

        private void OnEnable()
        {
            EnsureProgressionServiceExists();
            EnsureSubscribed();
            CollectPresenters();
            BindPresenters();
            CacheCharacterTreesOrWarn();
            RefreshAllPresenters();
        }

        private void OnDisable() => Unsubscribe();

        private void Start()
        {
            EnsureProgressionServiceExists();
            EnsureSubscribed();
            CollectPresenters();
            BindPresenters();
            CacheCharacterTreesOrWarn();
            RefreshAllPresenters();
        }

        private static void EnsureProgressionServiceExists()
        {
            if (PlayerProgressionService.Instance != null)
            {
                return;
            }

            if (FindFirstObjectByType<PlayerProgressionService>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var serviceRoot = new GameObject(nameof(PlayerProgressionService));
            serviceRoot.AddComponent<PlayerProgressionService>();
        }

        private void EnsureSubscribed()
        {
            if (_subscribedToService)
            {
                return;
            }

            var service = ResolveService();
            if (service == null)
            {
                return;
            }

            service.OnUnlockedNodesChanged += HandleUnlockedNodesChanged;
            _subscribedToService = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribedToService)
            {
                return;
            }

            var service = ResolveService();
            if (service != null)
            {
                service.OnUnlockedNodesChanged -= HandleUnlockedNodesChanged;
            }

            _subscribedToService = false;
        }

        private void CollectPresenters()
        {
            _presenters.Clear();
            var found = FindObjectsByType<SkillTreeNodePresenter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            _presenters.AddRange(found);

            if (_presenters.Count == 0)
            {
                Debug.LogWarning(
                    "SkillTreeView: nenhum SkillTreeNodePresenter na cena. " +
                    "Confirma que adicionaste o componente em cada botão da árvore.",
                    this);
            }
        }

        private void BindPresenters()
        {
            foreach (var presenter in _presenters)
            {
                presenter.BindToOwner(this);
            }
        }

        private void CacheCharacterTreesOrWarn()
        {
            var service = ResolveService();
            _characterTrees = service != null
                ? service.GetCharacterDefinition(_characterId)
                : null;

            if (_characterTrees == null)
            {
                Debug.LogError(
                    $"SkillTreeView: personagem '{_characterId}' não está em skill_trees.json " +
                    "(ou PlayerProgressionService falhou a carregar StreamingAssets/Data/skill_trees.json).",
                    this);
            }
        }

        private void HandleUnlockedNodesChanged(string characterIdWhoseSaveChanged)
        {
            if (!string.IsNullOrEmpty(characterIdWhoseSaveChanged) &&
                !string.Equals(characterIdWhoseSaveChanged, _characterId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RefreshAllPresenters();
        }

        private PlayerProgressionService ResolveService() =>
            _progressionService != null ? _progressionService : PlayerProgressionService.Instance;

        public void NotifyNodePointerActivated(SkillTreeNodeAsset nodeAsset)
        {
            var service = ResolveService();
            if (service == null || _characterTrees == null)
            {
                return;
            }

            if (!service.TryUnlock(_characterId, nodeAsset.NodeId, out var failureReason))
            {
                Debug.Log($"SkillTreeView: nó '{nodeAsset.NodeId}' bloqueado — {failureReason}", this);
            }
        }

        public void ShowDetails(SkillTreeNodeAsset nodeAsset)
        {
            _detailPanel.Apply(nodeAsset);
        }

        /// <summary>Hook para botões de debug ou para revalidar manualmente.</summary>
        public void RefreshNow() => RefreshAllPresenters();

        private void RefreshAllPresenters()
        {
            var service = ResolveService();
            if (service == null || _characterTrees == null)
            {
                foreach (var presenter in _presenters)
                {
                    presenter.ApplyVisualState(SkillTreeNodeVisualState.Locked, _lockedTint);
                }

                return;
            }

            var unlocked = service.GetUnlockedNodesForCharacter(_characterId);
            var spent = SkillTreeLookup.SumUnlockedNodeCosts(_characterTrees, unlocked);
            if (_pointsLabel != null)
            {
                _pointsLabel.text = $"{spent} / {service.MaxSkillPoints}";
            }

            foreach (var presenter in _presenters)
            {
                var asset = presenter.NodeAsset;
                if (asset == null)
                {
                    if (_logVisualStateDecisions)
                    {
                        Debug.LogWarning(
                            $"SkillTreeView: presenter em '{presenter.name}' sem SkillTreeNodeAsset atribuído.",
                            presenter);
                    }

                    presenter.ApplyVisualState(SkillTreeNodeVisualState.Locked, _lockedTint);
                    continue;
                }

                var state = ComputeVisualState(asset, service, unlocked, spent);
                presenter.ApplyVisualState(state, GetTintFor(state));
            }
        }

        private Color GetTintFor(SkillTreeNodeVisualState state) => state switch
        {
            SkillTreeNodeVisualState.Unlocked => _unlockedTint,
            SkillTreeNodeVisualState.AvailableToUnlock => _availableTint,
            _ => _lockedTint,
        };

        private SkillTreeNodeVisualState ComputeVisualState(
            SkillTreeNodeAsset asset,
            PlayerProgressionService service,
            IReadOnlyDictionary<string, bool> unlocked,
            int currentlySpent)
        {
            if (_characterTrees == null)
            {
                return SkillTreeNodeVisualState.Locked;
            }

            if (string.IsNullOrWhiteSpace(asset.NodeId))
            {
                if (_logVisualStateDecisions)
                {
                    Debug.LogWarning(
                        $"SkillTreeView: SO '{asset.name}' tem _nodeId vazio.",
                        asset);
                }

                return SkillTreeNodeVisualState.Locked;
            }

            if (!SkillTreeLookup.TryFindNode(_characterTrees, asset.NodeId, out var elementType, out var nodeDef))
            {
                if (_logVisualStateDecisions)
                {
                    Debug.LogWarning(
                        $"SkillTreeView: nodeId '{asset.NodeId}' não está na árvore de '{_characterId}'.",
                        asset);
                }

                return SkillTreeNodeVisualState.Locked;
            }

            if (unlocked.TryGetValue(asset.NodeId, out var isOn) && isOn)
            {
                return SkillTreeNodeVisualState.Unlocked;
            }

            if (currentlySpent + nodeDef.Cost > service.MaxSkillPoints)
            {
                if (_logVisualStateDecisions)
                {
                    Debug.Log(
                        $"SkillTreeView: '{asset.NodeId}' inactivo — sem pontos ({currentlySpent}/{service.MaxSkillPoints}).",
                        asset);
                }

                return SkillTreeNodeVisualState.Locked;
            }

            var canUnlock = SkillTreeRules.CanUnlockNode(
                _characterTrees,
                elementType.ToString(),
                asset.NodeId,
                unlocked);

            if (!canUnlock && _logVisualStateDecisions)
            {
                Debug.Log(
                    $"SkillTreeView: '{asset.NodeId}' inactivo — requisitos da árvore não cumpridos " +
                    $"(elemento {elementType}, requires=[{string.Join(",", nodeDef.Requires)}]).",
                    asset);
            }

            return canUnlock
                ? SkillTreeNodeVisualState.AvailableToUnlock
                : SkillTreeNodeVisualState.Locked;
        }
    }
}
