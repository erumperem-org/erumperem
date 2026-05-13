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
    /// Persists skill-tree unlocks under <see cref="Application.persistentDataPath"/> and enforces
    /// <see cref="SkillTreeRules"/> plus a maximum skill-point budget (default 12).
    /// </summary>
    public sealed class PlayerProgressionService : MonoBehaviour
    {
        public static PlayerProgressionService? Instance { get; private set; }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        [Header("Catalog (StreamingAssets)")]
        [SerializeField] private string _skillTreesResourceRelativePath = "Data/skill_trees.json";

        [Header("Budget")]
        [Tooltip("Total node cost the player may spend per character (each tier-1..3 node costs 1 in current data → 12 nodes max).")]
        [SerializeField] private int _maxSkillPoints = 12;

        private readonly Dictionary<string, Dictionary<string, bool>> _unlockedByCharacter =
            new(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyList<CharacterSkillTreesDefinition> _skillTreesCatalog = Array.Empty<CharacterSkillTreesDefinition>();

        public int MaxSkillPoints => _maxSkillPoints;

        /// <summary>Fires after a successful unlock or after reset. Argument is character id, or empty if reset all.</summary>
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
            LoadSkillTreesCatalogOrThrow();
            LoadFromDisk();
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
                return;
            }

            _skillTreesCatalog = CombatDataLoader.LoadSkillTrees(path);
        }

        public CharacterSkillTreesDefinition? GetCharacterDefinition(string characterId) =>
            SkillTreeLookup.FindCharacterTrees(_skillTreesCatalog, characterId);

        public IReadOnlyDictionary<string, bool> GetUnlockedNodesForCharacter(string characterId)
        {
            if (!_unlockedByCharacter.TryGetValue(characterId, out var dict))
            {
                return new Dictionary<string, bool>(StringComparer.Ordinal);
            }

            return dict.ToDictionary(static entry => entry.Key, static entry => entry.Value, StringComparer.Ordinal);
        }

        /// <summary>Total cost of unlocked nodes; used for UI (e.g. 7 / 12).</summary>
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

            var spentSoFar = SkillTreeLookup.SumUnlockedNodeCosts(character, snapshot);
            if (spentSoFar + nodeDefinition.Cost > _maxSkillPoints)
            {
                failureReason = "Sem pontos de skill livres.";
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
            if (!File.Exists(SaveFilePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(SaveFilePath);
                var dto = JsonSerializer.Deserialize<PlayerProgressionSaveDto>(json, JsonOptions);
                if (dto?.Characters == null)
                {
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
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"PlayerProgressionService: falha ao ler save — {ex.Message}");
            }
        }

        private void SaveToDisk()
        {
            var dto = new PlayerProgressionSaveDto
            {
                Version = 1,
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
    }
}
