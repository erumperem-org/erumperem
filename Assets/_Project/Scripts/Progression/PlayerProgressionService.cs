
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
    /// Salva os nós desbloqueados de cada personagem.
    /// Wulfric, Buck e Matsuda possuem o mesmo nível compartilhado,
    /// mas os pontos gastos em cada árvore são independentes.
    /// </summary>
    public sealed class PlayerProgressionService : MonoBehaviour
    {
        public static PlayerProgressionService? Instance { get; private set; }

        private static readonly string[] DefaultSharedSkillLevelCharacterIds =
        {
            "wulfric",
            "buck",
            "matsuda"
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        [Header("Catalog (StreamingAssets)")]
        [SerializeField] private string _skillTreesResourceRelativePath = "Data/skill_trees.json";

        [Header("Shared skill level")]
        [Tooltip("Maximum shared skill level.")]
        [SerializeField] private int _maxSkillPoints = 12;

        [Tooltip("Initial shared level on a new save.")]
        [SerializeField] private int _initialSharedSkillLevel = 12;

        private readonly Dictionary<string, Dictionary<string, bool>> _unlockedByCharacter =
            new(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyList<CharacterSkillTreesDefinition> _skillTreesCatalog = Array.Empty<CharacterSkillTreesDefinition>();
        private bool _isSkillTreesCatalogLoaded;
        private int _sharedSkillLevel;
        private CurrentSlotData _watchedSlot;
        private string _loadedSlotDirectory;

        public int MaxSkillPoints => _maxSkillPoints;

        public bool IsSkillTreesCatalogLoaded => _isSkillTreesCatalogLoaded && _skillTreesCatalog.Count > 0;

        public IReadOnlyList<string> SharedSkillBudgetCharacterIds => DefaultSharedSkillLevelCharacterIds;

        public int GetSharedSkillLevel()
        {
            return _sharedSkillLevel;
        }

        public int GetRemainingPointsForCharacter(string characterId)
        {
            return Math.Max(0, _sharedSkillLevel - GetPointsSpent(characterId));
        }

        public bool IsSharedSkillBudgetCharacter(string characterId)
        {
            return !string.IsNullOrWhiteSpace(characterId) &&
                   DefaultSharedSkillLevelCharacterIds.Any(sharedCharacterId => string.Equals(sharedCharacterId, characterId, StringComparison.OrdinalIgnoreCase));
        }

        public event Action<string>? OnUnlockedNodesChanged;

        private string SaveFilePath
        {
            get
            {
                CurrentSlotData slot = SaveSlotAccess.Data;

                return slot != null && slot.HasSlotSelected
                    ? Path.Combine(slot.SlotDirectory, "player_skill_progression.json")
                    : null;
            }
        }

        private string ActiveSlotDirectory
        {
            get
            {
                CurrentSlotData slot = SaveSlotAccess.Data;
                return slot != null && slot.HasSlotSelected ? slot.SlotDirectory : null;
            }
        }

        public bool TrySetSharedSkillLevel(int newLevel)
        {
            if (SaveFilePath == null)
            {
                return false;
            }

            int clampedLevel = Math.Clamp(newLevel, 0, _maxSkillPoints);

            if (clampedLevel == _sharedSkillLevel)
            {
                return false;
            }

            _sharedSkillLevel = clampedLevel;
            SaveToDisk();
            OnUnlockedNodesChanged?.Invoke(string.Empty);
            return true;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureSkillTreesCatalogLoaded();
            SubscribeToSlot();
            _loadedSlotDirectory = ActiveSlotDirectory;
            LoadFromDisk();
        }

        private void LateUpdate()
        {
            SubscribeToSlot();

            if (!string.Equals(_loadedSlotDirectory, ActiveSlotDirectory, StringComparison.Ordinal))
            {
                ReloadActiveSlot();
            }
        }

        private void SubscribeToSlot()
        {
            CurrentSlotData slot = SaveSlotAccess.Data;

            if (ReferenceEquals(_watchedSlot, slot))
            {
                return;
            }

            if (_watchedSlot != null)
            {
                _watchedSlot.OnSlotChanged -= HandleSlotChanged;
            }

            _watchedSlot = slot;

            if (_watchedSlot != null)
            {
                _watchedSlot.OnSlotChanged += HandleSlotChanged;
            }
        }

        private void HandleSlotChanged(int index, string directory)
        {
            ReloadActiveSlot();
        }

        private void ReloadActiveSlot()
        {
            _loadedSlotDirectory = ActiveSlotDirectory;
            LoadFromDisk();
            OnUnlockedNodesChanged?.Invoke(string.Empty);
        }

        private void OnDestroy()
        {
            if (_watchedSlot != null)
            {
                _watchedSlot.OnSlotChanged -= HandleSlotChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void EnsureSkillTreesCatalogLoaded()
        {
            if (_isSkillTreesCatalogLoaded && _skillTreesCatalog.Count > 0)
            {
                return;
            }

            LoadSkillTreesCatalogOrThrow();
        }

        private void LoadSkillTreesCatalogOrThrow()
        {
            string path = Path.Combine(Application.streamingAssetsPath, _skillTreesResourceRelativePath);

            if (!File.Exists(path))
            {
                Debug.LogError("PlayerProgressionService: missing " + path + ". Run Erumperem/Combat/Export Catalog.");
                _skillTreesCatalog = Array.Empty<CharacterSkillTreesDefinition>();
                _isSkillTreesCatalogLoaded = false;
                return;
            }

            try
            {
                _skillTreesCatalog = CombatDataLoader.LoadSkillTrees(path);
                _isSkillTreesCatalogLoaded = _skillTreesCatalog.Count > 0;
            }
            catch (Exception exception)
            {
                Debug.LogError("PlayerProgressionService: failed to parse skill_trees.json — " + exception.Message, this);
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
            if (!_unlockedByCharacter.TryGetValue(characterId, out Dictionary<string, bool> map))
            {
                return new Dictionary<string, bool>(StringComparer.Ordinal);
            }

            return map.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        }

        public int GetPointsSpent(string characterId)
        {
            CharacterSkillTreesDefinition character = GetCharacterDefinition(characterId);

            if (character == null)
            {
                return 0;
            }

            IReadOnlyDictionary<string, bool> unlocked = GetUnlockedNodesForCharacter(characterId);
            return SkillTreeLookup.SumUnlockedNodeCosts(character, unlocked);
        }

        public bool TryUnlock(string characterId, string nodeId, out string failureReason)
        {
            failureReason = string.Empty;

            if (SaveFilePath == null)
            {
                failureReason = "Nenhum save slot selecionado.";
                return false;
            }

            CharacterSkillTreesDefinition character = GetCharacterDefinition(characterId);

            if (character == null)
            {
                failureReason = "Personagem desconhecido.";
                return false;
            }

            if (!SkillTreeLookup.TryFindNode(character, nodeId, out var elementType, out var nodeDefinition))
            {
                failureReason = "Nó desconhecido.";
                return false;
            }

            Dictionary<string, bool> unlocked = GetOrCreateMutableCharacterMap(characterId);

            if (unlocked.TryGetValue(nodeId, out bool isOn) && isOn)
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

            int characterPointsSpent = GetPointsSpent(characterId);

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
            if (SaveFilePath == null)
            {
                return;
            }

            _unlockedByCharacter.Clear();
            DeleteSaveFile();
            SaveToDisk();
            OnUnlockedNodesChanged?.Invoke(string.Empty);
            Debug.Log("PlayerProgressionService: save resetado. Ficheiro: " + SaveFilePath);
        }

        public void ResetCharacter(string characterId)
        {
            if (SaveFilePath == null)
            {
                return;
            }

            _unlockedByCharacter.Remove(characterId);
            SaveToDisk();
            OnUnlockedNodesChanged?.Invoke(characterId);
            Debug.Log("PlayerProgressionService: progressão resetada para '" + characterId + "'.");
        }

        private void DeleteSaveFile()
        {
            if (SaveFilePath == null)
            {
                return;
            }

            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("PlayerProgressionService: falha ao apagar save — " + exception.Message);
            }
        }

        private Dictionary<string, bool> GetOrCreateMutableCharacterMap(string characterId)
        {
            if (!_unlockedByCharacter.TryGetValue(characterId, out Dictionary<string, bool> map))
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

            if (SaveFilePath == null || !File.Exists(SaveFilePath))
            {
                ApplyDefaultSharedSkillLevelForNewSave();
                return;
            }

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                PlayerProgressionSaveDto dto = JsonSerializer.Deserialize<PlayerProgressionSaveDto>(json, JsonOptions);

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

                    Dictionary<string, bool> map = new Dictionary<string, bool>(StringComparer.Ordinal);

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
            catch (Exception exception)
            {
                Debug.LogWarning("PlayerProgressionService: falha ao ler save — " + exception.Message);
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

            int highestCharacterSpend = 0;

            foreach (string characterId in DefaultSharedSkillLevelCharacterIds)
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
            string path = SaveFilePath;

            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("PlayerProgressionService: sem save slot selecionado.");
                return;
            }

            PlayerProgressionSaveDto dto = new PlayerProgressionSaveDto
            {
                Version = 2,
                SharedSkillLevel = _sharedSkillLevel,
                Characters = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase)
            };

            foreach (var characterEntry in _unlockedByCharacter)
            {
                Dictionary<string, bool> inner = new Dictionary<string, bool>(StringComparer.Ordinal);

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
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string json = JsonSerializer.Serialize(dto, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception exception)
            {
                Debug.LogError("PlayerProgressionService: falha ao gravar save — " + exception.Message);
            }
        }

        public void GiveProgressionPoints(int pointsToGive)
        {
            _maxSkillPoints += pointsToGive;
        }
    }
}
