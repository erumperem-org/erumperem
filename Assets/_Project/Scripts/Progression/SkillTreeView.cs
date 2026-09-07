using System;
using System.Collections.Generic;
using Erumperem.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core.Models;
using Game.Core.Progression;

namespace Erumperem.Progression
{
    /// <summary>
    /// Skill tree UI for Wulfric and Buck with shared skill-point budget and arrow navigation.
    /// </summary>
    public sealed class SkillTreeView : MonoBehaviour
    {
        [Serializable]
        public struct SkillTreeCharacterUiProfile
        {
            public string ProgressionCharacterId;
            public string SkillTreeTitle;
            public Sprite PortraitSprite;
            public Color PanelBackgroundColor;
            public GameObject SkillTreeRoot;
        }

        [Serializable]
        public struct DetailUiBindings
        {
            public TMP_Text Title;
            public TMP_Text Body;

            public void Apply(SkillTreeNodeAsset nodeAsset)
            {
                if (Title != null)
                {
                    Title.text = nodeAsset.IsPassiveNode ? "Passive" : PlayerFacingText.TranslateToEnglish(nodeAsset.DisplayName);
                }

                if (Body != null)
                {
                    Body.text = PlayerFacingText.FormatSkillTreeNodeDescription(nodeAsset);
                }
            }

            public void Clear()
            {
                if (Title != null)
                {
                    Title.text = string.Empty;
                }

                if (Body != null)
                {
                    Body.text = string.Empty;
                }
            }
        }

        private static readonly Color DefaultWulfricBackground = new(0.17f, 0.21f, 0.23f, 0.996f);
        private static readonly Color DefaultBuckBackground = new(0.25f, 0.19f, 0.18f, 0.996f);

        [Header("Data")]
        [SerializeField] private PlayerProgressionService _progressionService;

        [Header("Navigation")]
        [SerializeField] private Button _arrowLeftButton;
        [SerializeField] private Button _arrowRightButton;

        [Header("Character profiles (order = arrow cycle)")]
        [SerializeField]
        private SkillTreeCharacterUiProfile[] _characterProfiles =
        {
            new()
            {
                ProgressionCharacterId = "wulfric",
                SkillTreeTitle = "Splintered Knight",
                PanelBackgroundColor = DefaultWulfricBackground,
                SkillTreeRoot = null
            },
            new()
            {
                ProgressionCharacterId = "buck",
                SkillTreeTitle = "The Gunslinger",
                PanelBackgroundColor = DefaultBuckBackground,
                SkillTreeRoot = null
            },
        };

        [Header("Panel chrome")]
        [SerializeField] private TMP_Text _skillTreeTitleText;
        [SerializeField] private Image _portraitImage;
        [SerializeField] private Image _panelBackgroundImage;

        [Header("Shared skill budget")]
        [SerializeField] private TMP_Text _levelTextValue;

        [Header("Reset")]
        [SerializeField] private Button _resetSkillsButton;

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
        private int _currentProfileIndex;
        private bool _subscribedToService;

        public string CurrentProgressionCharacterId =>
            _characterProfiles.Length > 0
                ? _characterProfiles[_currentProfileIndex].ProgressionCharacterId
                : string.Empty;

        private void Awake()
        {
            TryAutoBindHierarchyReferences();
            EnsureSharedSkillLevelClickCheatBound();
        }

        private void OnEnable()
        {
            EnsureProgressionServiceReady();
            EnsureSubscribed();
            BindNavigationButtons();
            ApplyCharacterSelection(_currentProfileIndex);
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnbindNavigationButtons();
        }

        private void Start()
        {
            EnsureProgressionServiceReady();
            EnsureSubscribed();
            ApplyCharacterSelection(_currentProfileIndex);
        }

        public void SelectNextCharacter()
        {
            if (_characterProfiles.Length == 0)
            {
                return;
            }

            var nextIndex = (_currentProfileIndex + 1) % _characterProfiles.Length;
            ApplyCharacterSelection(nextIndex);
        }

        public void SelectPreviousCharacter()
        {
            if (_characterProfiles.Length == 0)
            {
                return;
            }

            var previousIndex = (_currentProfileIndex - 1 + _characterProfiles.Length) % _characterProfiles.Length;
            ApplyCharacterSelection(previousIndex);
        }

        public void SelectCharacterByIndex(int profileIndex)
        {
            if (profileIndex < 0 || profileIndex >= _characterProfiles.Length)
            {
                return;
            }

            ApplyCharacterSelection(profileIndex);
        }

        public void ResetCurrentCharacterSkillTree()
        {
            var service = ResolveService();
            if (service == null || string.IsNullOrWhiteSpace(CurrentProgressionCharacterId))
            {
                return;
            }

            service.ResetCharacter(CurrentProgressionCharacterId);
        }

        private void ApplyCharacterSelection(int profileIndex)
        {
            if (_characterProfiles.Length == 0)
            {
                return;
            }

            _currentProfileIndex = profileIndex;
            var profile = _characterProfiles[_currentProfileIndex];

            for (var profileLoopIndex = 0; profileLoopIndex < _characterProfiles.Length; profileLoopIndex++)
            {
                var characterProfile = _characterProfiles[profileLoopIndex];
                if (characterProfile.SkillTreeRoot != null)
                {
                    characterProfile.SkillTreeRoot.SetActive(profileLoopIndex == _currentProfileIndex);
                }
            }

            if (_skillTreeTitleText != null)
            {
                _skillTreeTitleText.text = profile.SkillTreeTitle;
            }

            if (_portraitImage != null)
            {
                _portraitImage.sprite = profile.PortraitSprite;
                _portraitImage.enabled = profile.PortraitSprite != null;
            }

            if (_panelBackgroundImage != null)
            {
                _panelBackgroundImage.color = profile.PanelBackgroundColor;
            }

            _detailPanel.Clear();
            CollectPresenters();
            BindPresenters();
            CacheCharacterTreesOrWarn();
            RefreshAllPresenters();
        }

        private void BindNavigationButtons()
        {
            if (_arrowLeftButton != null)
            {
                _arrowLeftButton.onClick.RemoveListener(SelectPreviousCharacter);
                _arrowLeftButton.onClick.AddListener(SelectPreviousCharacter);
            }

            if (_arrowRightButton != null)
            {
                _arrowRightButton.onClick.RemoveListener(SelectNextCharacter);
                _arrowRightButton.onClick.AddListener(SelectNextCharacter);
            }

            if (_resetSkillsButton != null)
            {
                _resetSkillsButton.onClick.RemoveListener(ResetCurrentCharacterSkillTree);
                _resetSkillsButton.onClick.AddListener(ResetCurrentCharacterSkillTree);
            }
        }

        private void UnbindNavigationButtons()
        {
            if (_arrowLeftButton != null)
            {
                _arrowLeftButton.onClick.RemoveListener(SelectPreviousCharacter);
            }

            if (_arrowRightButton != null)
            {
                _arrowRightButton.onClick.RemoveListener(SelectNextCharacter);
            }

            if (_resetSkillsButton != null)
            {
                _resetSkillsButton.onClick.RemoveListener(ResetCurrentCharacterSkillTree);
            }
        }

        private void EnsureSharedSkillLevelClickCheatBound()
        {
            SharedSkillLevelClickCheat.EnsureBoundToLevelRoot(FindChildTransform("Level"));
        }

        private void TryAutoBindHierarchyReferences()
        {
            _skillTreeTitleText ??= FindChildComponent<TMP_Text>("SkillTreeTitle");
            _portraitImage ??= FindChildComponent<Image>("Portrait");
            _panelBackgroundImage ??= GetComponent<Image>();
            _levelTextValue ??= FindChildComponent<TMP_Text>("LevelTextValue");
            _arrowLeftButton ??= FindChildComponent<Button>("ArrowLeft");
            _arrowRightButton ??= FindChildComponent<Button>("ArrowRight");
            _resetSkillsButton ??= FindChildComponent<Button>("ResetSkills");

            if (_detailPanel.Title == null)
            {
                _detailPanel.Title = FindChildComponent<TMP_Text>("SkillTitle");
            }
            if (_detailPanel.Body == null)
            {
                _detailPanel.Body = FindChildComponent<TMP_Text>("SkillDescription");
            }

            if (_characterProfiles == null || _characterProfiles.Length < 2)
            {
                return;
            }

            var wulfricProfile = _characterProfiles[0];
            var buckProfile = _characterProfiles[1];

            wulfricProfile.SkillTreeRoot ??= FindChildTransform("SkillTree_Wulfric")?.gameObject
                ?? FindChildTransform("SkillTree")?.gameObject;
            buckProfile.SkillTreeRoot ??= FindChildTransform("SkillTree_Buck")?.gameObject;

            _characterProfiles[0] = wulfricProfile;
            _characterProfiles[1] = buckProfile;
        }

        private Transform FindChildTransform(string childName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            foreach (var childTransform in transforms)
            {
                if (string.Equals(childTransform.name, childName, System.StringComparison.Ordinal))
                {
                    return childTransform;
                }
            }

            return null;
        }

        private T FindChildComponent<T>(string childName) where T : Component
        {
            var childTransform = FindChildTransform(childName);
            return childTransform != null ? childTransform.GetComponent<T>() : null;
        }

        private PlayerProgressionService EnsureProgressionServiceReady()
        {
            var service = _progressionService != null
                ? _progressionService
                : PlayerProgressionService.Instance;

            if (service == null)
            {
                service = UnityEngine.Object.FindFirstObjectByType<PlayerProgressionService>(FindObjectsInactive.Include);
            }

            if (service == null)
            {
                var serviceRoot = new GameObject(nameof(PlayerProgressionService));
                service = serviceRoot.AddComponent<PlayerProgressionService>();
            }

            service.EnsureSkillTreesCatalogLoaded();
            _progressionService = service;
            return service;
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
            var activeRoot = _characterProfiles.Length > 0
                ? _characterProfiles[_currentProfileIndex].SkillTreeRoot
                : null;

            if (activeRoot != null)
            {
                _presenters.AddRange(activeRoot.GetComponentsInChildren<SkillTreeNodePresenter>(true));
            }
            else
            {
                var found = FindObjectsByType<SkillTreeNodePresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                _presenters.AddRange(found);
            }

            if (_presenters.Count == 0)
            {
                Debug.LogWarning(
                    "SkillTreeView: nenhum SkillTreeNodePresenter no root activo. " +
                    "Confirma SkillTreeRoot e SkillTreeNodePresenter nos botões.",
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
            var characterId = CurrentProgressionCharacterId;
            _characterTrees = service != null && !string.IsNullOrWhiteSpace(characterId)
                ? service.GetCharacterDefinition(characterId)
                : null;

            if (_characterTrees == null)
            {
                var catalogLoaded = service.IsSkillTreesCatalogLoaded;
                Debug.LogError(
                    catalogLoaded
                        ? $"SkillTreeView: personagem '{characterId}' não está em skill_trees.json."
                        : $"SkillTreeView: skill_trees.json não carregou " +
                          $"(verifica Assets/StreamingAssets/Data/skill_trees.json). Personagem pedido: '{characterId}'.",
                    this);
            }
        }

        private void HandleUnlockedNodesChanged(string characterIdWhoseSaveChanged)
        {
            var service = ResolveService();
            if (service != null &&
                !string.IsNullOrEmpty(characterIdWhoseSaveChanged) &&
                !service.IsSharedSkillBudgetCharacter(characterIdWhoseSaveChanged) &&
                !string.Equals(characterIdWhoseSaveChanged, CurrentProgressionCharacterId, StringComparison.OrdinalIgnoreCase))
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
            var characterId = CurrentProgressionCharacterId;
            if (service == null || _characterTrees == null || string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }

            if (!service.TryUnlock(characterId, nodeAsset.NodeId, out var failureReason))
            {
                Debug.Log($"SkillTreeView: nó '{nodeAsset.NodeId}' bloqueado — {failureReason}", this);
            }
        }

        public void ShowDetails(SkillTreeNodeAsset nodeAsset)
        {
            _detailPanel.Apply(nodeAsset);
        }

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

            var characterId = CurrentProgressionCharacterId;
            var unlocked = service.GetUnlockedNodesForCharacter(characterId);
            var sharedSkillLevel = service.GetSharedSkillLevel();
            var characterPointsSpent = service.GetPointsSpent(characterId);
            var budgetLabel = $"{sharedSkillLevel} / {service.MaxSkillPoints}";

            if (_levelTextValue != null)
            {
                _levelTextValue.text = budgetLabel;
            }

            if (_pointsLabel != null)
            {
                _pointsLabel.text = budgetLabel;
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

                var state = ComputeVisualState(asset, service, unlocked, sharedSkillLevel, characterPointsSpent);
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
            int sharedSkillLevel,
            int currentCharacterPointsSpent)
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
                        $"SkillTreeView: nodeId '{asset.NodeId}' não está na árvore de '{CurrentProgressionCharacterId}'.",
                        asset);
                }

                return SkillTreeNodeVisualState.Locked;
            }

            if (unlocked.TryGetValue(asset.NodeId, out var isOn) && isOn)
            {
                return SkillTreeNodeVisualState.Unlocked;
            }

            if (currentCharacterPointsSpent + nodeDef.Cost > sharedSkillLevel)
            {
                if (_logVisualStateDecisions)
                {
                    Debug.Log(
                        $"SkillTreeView: '{asset.NodeId}' inactivo — {CurrentProgressionCharacterId} sem pontos " +
                        $"(gasto {currentCharacterPointsSpent}, level partilhado {sharedSkillLevel}/{service.MaxSkillPoints}).",
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