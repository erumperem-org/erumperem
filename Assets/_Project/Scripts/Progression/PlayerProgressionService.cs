using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.Core.Data;
using Game.Core.Models;
using Game.Core.Progression;
using UnityEngine;

namespace Erumperem.Progression
{
    /// <summary>
    /// Persists skill-tree unlocks and a <see cref="SharedSkillLevel"/> cap (default max 12).
    /// Wulfric and Buck share the same level value (e.g. 4/12): each may spend up to that many
    /// points in their own tree; spending on one does not reduce the other's budget.
    /// </summary>
    public sealed class PlayerProgressionService : MonoBehaviour
    {
        public static PlayerProgressionService? Instance { get; private set; }

        private static readonly string[] DefaultSharedSkillLevelCharacterIds = { "wulfric", "buck" };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        [Header("Catalog (StreamingAssets)")]
        [SerializeField] private string _skillTreesResourceRelativePath = "Data/skill_trees.json";

        [Header("Shared skill level")]
        [Tooltip("Maximum shared skill level (UI denominator, e.g. 12 in 4/12).")]
        [SerializeField] private int _maxSkillPoints = 12;

        [Tooltip("Level assigned on a brand-new save before any external progression sets it.")]
        [SerializeField] private int _initialSharedSkillLevel = 12;

        private readonly Dictionary<string, Dictionary<string, bool>> _unlockedByCharacter =
            new(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyList<CharacterSkillTreesDefinition> _skillTreesCatalog = Array.Empty<CharacterSkillTreesDefinition>();
        private bool _isSkillTreesCatalogLoaded;
        private int _sharedSkillLevel;

        public int MaxSkillPoints => _maxSkillPoints;

        public bool IsSkillTreesCatalogLoaded => _isSkillTreesCatalogLoaded && _skillTreesCatalog.Count > 0;

        public IReadOnlyList<string> SharedSkillBudgetCharacterIds => DefaultSharedSkillLevelCharacterIds;

        /// <summary>
        /// Current shared level shown in UI (e.g. 4 in 4/12). Each shared-level character may spend
        /// up to this many points in their own tree.
        /// </summary>
        public int GetSharedSkillLevel() => _sharedSkillLevel;

        public int GetRemainingPointsForCharacter(string characterId) =>
            Math.Max(0, _sharedSkillLevel - GetPointsSpent(characterId));

        public bool IsSharedSkillBudgetCharacter(string characterId) =>
            !string.IsNullOrWhiteSpace(characterId) &&
            DefaultSharedSkillLevelCharacterIds.Any(sharedCharacterId =>
                string.Equals(sharedCharacterId, characterId, StringComparison.OrdinalIgnoreCase));

        /// <summary>Raises or lowers the shared level cap (both characters benefit together).</summary>
        public bool TrySetSharedSkillLevel(int newLevel)
        {
            var clampedLevel = Math.Clamp(newLevel, 0, _maxSkillPoints);
            if (clampedLevel == _sharedSkillLevel)
            {
                return false;
            }

            _sharedSkillLevel = clampedLevel;
            SaveToDisk();
            OnUnlockedNodesChanged?.Invoke(string.Empty);
            return true;
        }

        /// <summary>Fires after a successful unlock, level change, or reset. Empty = refresh all shared-level UIs.</summary>
        public event Action<string>? OnUnlockedNodesChanged;

        private string SaveFilePath => Path.Combine(Application.persistentDataPath, "player_skill_progression.json");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSkillTreesCatalogLoaded();
            LoadFromDisk();
        }

        /// <summary>
        /// Loads skill_trees.json from StreamingAssets. Retries when the catalog is still empty
        /// (e.g. DontDestroyOnLoad service created before JSON was present).
        /// </summary>
        public void EnsureSkillTreesCatalogLoaded()
        {
            if (_isSkillTreesCatalogLoaded && _skillTreesCatalog.Count > 0)
            {
                return;
            }

            LoadSkillTreesCatalogOrThrow();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LoadSkillTreesCatalogOrThrow()
        {
            var path = Path.Combine(Application.streamingAssetsPath, _skillTreesResourceRelativePath);
            if (!File.Exists(path))
            {
                Debug.LogError($"PlayerProgressionService: missing {path}. Copy skill_trees.json to StreamingAssets/Data.");
                _skillTreesCatalog = Array.Empty<CharacterSkillTreesDefinition>();
                _isSkillTreesCatalogLoaded = false;
                return;
            }

            try
            {
                _skillTreesCatalog = CombatDataLoader.LoadSkillTrees(path);
                _isSkillTreesCatalogLoaded = _skillTreesCatalog.Count > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"PlayerProgressionService: failed to parse skill_trees.json at {path} — {ex.Message}",
                    this);
                _skillTreesCatalog = Array.Empty<CharacterSkillTreesDefinition>();
                _isSkillTreesCatalogLoaded = false;
            }
        }

        public CharacterSkillTreesDefinition? GetCharacterDefinition(string characterId)
        {
            EnsureSkillTreesCatalogLoaded();
            return SkillTreeLookup.FindCharacterTrees(_skillTreesCatalog, characterId);
        }

        public IReadOnlyDictionary<string, bool> GetUnlockedNodesForCharacter(string characterId)
        {
            if (!_unlockedByCharacter.TryGetValue(characterId, out var dict))
            {
                return new Dictionary<string, bool>(StringComparer.Ordinal);
            }

            return dict.ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal);
        }

        /// <summary>Total cost of unlocked nodes for one character.</summary>
        public int GetPointsSpent(string characterId)
        {
            var character = GetCharacterDefinition(characterId);
            if (character is null)
            {
                return 0;
            }

            var unlocked = GetUnlockedNodesForCharacter(characterId);
            return SkillTreeLookup.SumUnlockedNodeCosts(character, unlocked);
        }

        public bool TryUnlock(string characterId, string nodeId, out string failureReason)
        {
            failureReason = string.Empty;
            var character = GetCharacterDefinition(characterId);
            if (character is null)
            {
                failureReason = "Personagem desconhecido.";
                return false;
            }

            if (!SkillTreeLookup.TryFindNode(character, nodeId, out var elementType, out var nodeDefinition))
            {
                failureReason = "Nó desconhecido.";
                return false;
            }

            var unlocked = GetOrCreateMutableCharacterMap(characterId);
            if (unlocked.TryGetValue(nodeId, out var isOn) && isOn)
            {
                failureReason = "Já desbloqueado.";
                return false;
            }

            IReadOnlyDictionary<string, bool> snapshot = unlocked;
            if (!SkillTreeRules.CanUnlockNode(character, elementType.ToString(), nodeId, snapshot))
            {
                failureReason = "Requisitos não cumpridos.";
                return false;
            }

            var characterPointsSpent = GetPointsSpent(characterId);
            if (characterPointsSpent + nodeDefinition.Cost > _sharedSkillLevel)
            {
                failureReason = "Sem pontos de skill livres para este personagem.";
                return false;
            }

            unlocked[nodeId] = true;
            SaveToDisk();
            OnUnlockedNodesChanged?.Invoke(characterId);
            return true;
        }

        public void ResetAllCharacters()
        {
            _unlockedByCharacter.Clear();
            //_sharedSkillLevel = Math.Clamp(_initialSharedSkillLevel, 0, _maxSkillPoints);
            DeleteSaveFile();
            SaveToDisk();
            OnUnlockedNodesChanged?.Invoke(string.Empty);
            Debug.Log($"PlayerProgressionService: save resetado (todos os personagens). Ficheiro: {SaveFilePath}");
        }

        public void ResetCharacter(string characterId)
        {
            _unlockedByCharacter.Remove(characterId);
            SaveToDisk();
            OnUnlockedNodesChanged?.Invoke(characterId);
            Debug.Log($"PlayerProgressionService: save resetado para '{characterId}'. Ficheiro: {SaveFilePath}");
        }

        private void DeleteSaveFile()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"PlayerProgressionService: falha a apagar save — {ex.Message}");
            }
        }

        private Dictionary<string, bool> GetOrCreateMutableCharacterMap(string characterId)
        {
            if (!_unlockedByCharacter.TryGetValue(characterId, out var map))
            {
                map = new Dictionary<string, bool>(StringComparer.Ordinal);
                _unlockedByCharacter[characterId] = map;
            }

            return map;
        }

        private void LoadFromDisk()
        {
            _unlockedByCharacter.Clear();
            _sharedSkillLevel = 0;

            if (!File.Exists(SaveFilePath))
            {
                ApplyDefaultSharedSkillLevelForNewSave();
                return;
            }

            try
            {
                var json = File.ReadAllText(SaveFilePath);
                var dto = JsonSerializer.Deserialize<PlayerProgressionSaveDto>(json, JsonOptions);
                if (dto?.Characters == null)
                {
                    ApplyDefaultSharedSkillLevelForNewSave();
                    return;
                }

                foreach (var characterEntry in dto.Characters)
                {
                    if (string.IsNullOrWhiteSpace(characterEntry.Key) || characterEntry.Value == null)
                    {
                        continue;
                    }

                    var map = new Dictionary<string, bool>(StringComparer.Ordinal);
                    foreach (var nodeEntry in characterEntry.Value)
                    {
                        if (!string.IsNullOrEmpty(nodeEntry.Key) && nodeEntry.Value)
                        {
                            map[nodeEntry.Key] = true;
                        }
                    }

                    _unlockedByCharacter[characterEntry.Key] = map;
                }

                _sharedSkillLevel = dto.SharedSkillLevel;
                ReconcileSharedSkillLevelAfterLoad(dto.Version);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"PlayerProgressionService: falha ao ler save — {ex.Message}");
                ApplyDefaultSharedSkillLevelForNewSave();
            }
        }

        private void ReconcileSharedSkillLevelAfterLoad(int saveVersion)
        {
            if (saveVersion >= 2 && _sharedSkillLevel > 0)
            {
                _sharedSkillLevel = Math.Clamp(_sharedSkillLevel, 0, _maxSkillPoints);
                return;
            }

            // v1 saves had no SharedSkillLevel field: keep at least enough level for existing unlocks.
            var highestCharacterSpend = 0;
            foreach (var characterId in DefaultSharedSkillLevelCharacterIds)
            {
                highestCharacterSpend = Math.Max(highestCharacterSpend, GetPointsSpent(characterId));
            }

            if (highestCharacterSpend > 0)
            {
                _sharedSkillLevel = Math.Clamp(highestCharacterSpend, 0, _maxSkillPoints);
                return;
            }

            ApplyDefaultSharedSkillLevelForNewSave();
        }

        private void ApplyDefaultSharedSkillLevelForNewSave()
        {
            _sharedSkillLevel = Math.Clamp(_initialSharedSkillLevel, 0, _maxSkillPoints);
        }

        private void SaveToDisk()
        {
            var dto = new PlayerProgressionSaveDto
            {
                Version = 2,
                SharedSkillLevel = _sharedSkillLevel,
                Characters = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase),
            };

            foreach (var characterEntry in _unlockedByCharacter)
            {
                var inner = new Dictionary<string, bool>(StringComparer.Ordinal);
                foreach (var nodeEntry in characterEntry.Value)
                {
                    if (nodeEntry.Value)
                    {
                        inner[nodeEntry.Key] = true;
                    }
                }

                if (inner.Count > 0)
                {
                    dto.Characters[characterEntry.Key] = inner;
                }
            }

            try
            {
                var json = JsonSerializer.Serialize(dto, JsonOptions);
                File.WriteAllText(SaveFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"PlayerProgressionService: falha ao gravar save — {ex.Message}");
            }
        }

        public void GiveProgressionPoints(int pointsToGive)
        {
            _maxSkillPoints += pointsToGive;
        }
    }
}
