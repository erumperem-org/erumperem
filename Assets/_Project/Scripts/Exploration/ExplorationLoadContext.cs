using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class PlayableCharacterData
{
    public string characterName;
    public Vector3 position;
    public Quaternion rotation;
    public PlayableCharacterState state;

    public PlayableCharacterData(string name, Vector3 pos, Quaternion rot, PlayableCharacterState currentState)
    {
        characterName = name;
        position = pos;
        rotation = rot;
        state = currentState;
    }
}

public class ExplorationLoadContext : MonoBehaviour
{
    [SerializeField] private string explorationSceneName = "Exploration"; // Ajuste conforme necessário
    
    private List<PlayableCharacterData> charactersData;
    private string mainCharacterName;
    private string companionCharacterName;
    private bool hasDataToRestore = false;

    private PlayableCharactersManager cachedManager;
    private List<PlayableCharacter> cachedPlayables;
    private bool isExplorationScene = false;

    private static ExplorationLoadContext instance;

    private void Awake()
    {
        // Singleton - persiste entre cenas
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        charactersData = new List<PlayableCharacterData>();
        mainCharacterName = string.Empty;
        companionCharacterName = string.Empty;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Verifica se já estamos na cena de exploração no Start
        if (SceneManager.GetActiveScene().name == explorationSceneName)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[SAVE/LOAD] Cena exploração detectada no Start",
                LogCategory.Player);
            isExplorationScene = true;
            CacheManagerAndPlayables();
            RestoreState();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Verifica se é a cena de exploração
        if (scene.name == explorationSceneName)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[SAVE/LOAD] Cena exploração carregada via callback",
                LogCategory.Player);
            isExplorationScene = true;
            // Aguarda um frame para o PlayableCharactersManager inicializar
            StartCoroutine(RestoreStateNextFrame());
        }
    }

    private System.Collections.IEnumerator RestoreStateNextFrame()
    {
        yield return null;
        
        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[SAVE/LOAD] Iniciando restauração (Frame N+1)",
            LogCategory.Player);

        // Tenta encontrar o manager se não foi cacheado
        if (cachedManager == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[SAVE/LOAD] Cache vazio, buscando manager...",
                LogCategory.Player);
            CacheManagerAndPlayables();
        }

        RestoreState();
    }

    /// <summary>
    /// Encontra e cacheia o manager e playables
    /// </summary>
    private void CacheManagerAndPlayables()
    {
        cachedManager = FindFirstObjectByType<PlayableCharactersManager>();
        
        if (cachedManager != null)
        {
            cachedPlayables = GetAllPlayableCharacters(cachedManager);
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[SAVE/LOAD] Cache criado: Manager encontrado, {cachedPlayables?.Count ?? 0} personagens",
                LogCategory.Player);
        }
        else
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[SAVE/LOAD] Manager não encontrado durante cache",
                LogCategory.Player);
        }
    }

    /// <summary>
    /// Restaura o estado baseado nos dados salvos ou carrega padrão
    /// </summary>
    public void RestoreState()
    {
        if (cachedManager == null || cachedPlayables == null || cachedPlayables.Count == 0)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                "[SAVE/LOAD] PlayableCharactersManager ou personagens não encontrados",
                LogCategory.Player);
            return;
        }

        try
        {
            if (hasDataToRestore && CharactersDataIsValid())
            {
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[SAVE/LOAD] Dados salvos encontrados, restaurando...",
                    LogCategory.Player);
                RestorePlayableCharactersState();
            }
            else
            {
                // Se nenhum dado foi salvo, carrega padrão
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[SAVE/LOAD] Nenhum dado salvo, carregando padrão...",
                    LogCategory.Player);
                LoadDefaultState();
            }
        }
        catch (Exception e)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                $"[SAVE/LOAD] Erro ao restaurar estado: {e}",
                LogCategory.Player);
        }
    }

    /// <summary>
    /// Salva o estado atual dos personagens jogáveis
    /// </summary>
    public void SavePlayableCharactersState(PlayableCharactersManager manager, List<PlayableCharacter> playables)
    {
        if (manager == null || playables == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[SAVE/LOAD] Manager ou lista de personagens nula ao salvar estado",
                LogCategory.Player);
            return;
        }

        charactersData.Clear();

        // Salva dados de cada personagem
        foreach (var character in playables)
        {
            if (character != null)
            {
                var data = new PlayableCharacterData(
                    character.characterName,
                    character.transform.position,
                    character.transform.rotation,
                    character.CurrentState
                );
                charactersData.Add(data);
            }
        }

        // Salva referências do main e companion
        mainCharacterName = manager.MainCharacter != null ? manager.MainCharacter.characterName : string.Empty;
        companionCharacterName = manager.CompanionCharacter != null ? manager.CompanionCharacter.characterName : string.Empty;

        hasDataToRestore = true;

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[SAVE/LOAD] Estado salvo: {charactersData.Count} personagens | Main: {mainCharacterName} | Companion: {companionCharacterName}",
            LogCategory.Player);
    }

    /// <summary>
    /// Restaura o estado dos personagens salvos usando cached manager
    /// </summary>
    private void RestorePlayableCharactersState()
    {
        try
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[SAVE/LOAD] Restaurando {charactersData.Count} personagens...",
                LogCategory.Player);

            // Restaura posições e estados dos personagens
            foreach (var character in cachedPlayables)
            {
                var savedData = charactersData.Find(d => d.characterName == character.characterName);

                if (savedData != null)
                {
                    // Restaura posição e rotação
                    character.transform.position = savedData.position;
                    character.transform.rotation = savedData.rotation;

                    // Restaura estado via manager usando SetState
                    cachedManager.SetState(savedData.state, character);

                    LoggerService.PrintLogMessage(LogLevel.Debug,
                        $"[SAVE/LOAD] {character.characterName} restaurado | Estado: {savedData.state} | Pos: {savedData.position}",
                        LogCategory.Player);
                }
                else
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[SAVE/LOAD] {character.characterName}: dados não encontrados",
                        LogCategory.Player);
                }
            }

            LoggerService.PrintLogMessage(LogLevel.Debug,
                "[SAVE/LOAD] Todos os personagens restaurados com sucesso",
                LogCategory.Player);

            hasDataToRestore = false;
        }
        catch (Exception e)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                $"[SAVE/LOAD] Erro ao restaurar estado: {e}",
                LogCategory.Player);
        }
    }

    /// <summary>
    /// Obtém todos os personagens jogáveis do manager
    /// </summary>
    private List<PlayableCharacter> GetAllPlayableCharacters(PlayableCharactersManager manager)
    {
        // Usando reflexão para acessar a lista privada
        var field = typeof(PlayableCharactersManager).GetField("playables",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            var result = field.GetValue(manager) as List<PlayableCharacter>;
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[SAVE/LOAD]   └─ Reflexão OK: {result?.Count ?? 0} personagens encontrados",
                LogCategory.Player);
            return result;
        }

        LoggerService.PrintLogMessage(LogLevel.Error,
            "[SAVE/LOAD] Reflexão falhou: campo 'playables' não encontrado",
            LogCategory.Player);
        return null;
    }

    /// <summary>
    /// Verifica se os dados salvos são válidos
    /// </summary>
    private bool CharactersDataIsValid()
    {
        return charactersData != null && charactersData.Count > 0 && !string.IsNullOrEmpty(mainCharacterName);
    }

    /// <summary>
    /// Carrega o estado padrão quando nenhum dado foi salvo
    /// </summary>
    private void LoadDefaultState()
    {
        if (cachedManager == null || cachedPlayables == null || cachedPlayables.Count < 3)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                "[SAVE/LOAD] PlayableCharactersManager ou personagens insuficientes para padrão",
                LogCategory.Player);
            return;
        }

        try
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                "[SAVE/LOAD] Carregando configuração padrão...",
                LogCategory.Player);

            // Padrão: Wulfric Variant (Main), Buck Variant (Resting), Girl Variant (Resting)
            var wulfric = GameObject.Find("Wulfric Variant").GetComponent<PlayableCharacter>();
            var buck = GameObject.Find("Buck Variant").GetComponent<PlayableCharacter>();
            var girl = GameObject.Find("Girl Variant").GetComponent<PlayableCharacter>();

            if (wulfric != null)
            {
                cachedManager.SetState(PlayableCharacterState.Main, wulfric);
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    "[SAVE/LOAD] Wulfric Variant → Main",
                    LogCategory.Player);
            }
            else
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    "[SAVE/LOAD] Wulfric Variant não encontrado",
                    LogCategory.Player);
            }

            if (buck != null)
            {
                cachedManager.SetState(PlayableCharacterState.Resting, buck);
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    "[SAVE/LOAD] Buck Variant → Resting",
                    LogCategory.Player);
            }
            else
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    "[SAVE/LOAD] Buck Variant não encontrado",
                    LogCategory.Player);
            }

            if (girl != null)
            {
                cachedManager.SetState(PlayableCharacterState.Resting, girl);
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    "[SAVE/LOAD] Girl Variant → Resting",
                    LogCategory.Player);
            }
            else
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    "[SAVE/LOAD] Girl Variant não encontrado",
                    LogCategory.Player);
            }

            LoggerService.PrintLogMessage(LogLevel.Debug,
                "[SAVE/LOAD] Configuração padrão carregada",
                LogCategory.Player);
        }
        catch (Exception e)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                $"[SAVE/LOAD] Erro ao carregar padrão: {e}",
                LogCategory.Player);
        }
    }

    /// <summary>
    /// Limpa todos os dados (útil para novo jogo)
    /// </summary>
    public void ClearData()
    {
        charactersData.Clear();
        mainCharacterName = string.Empty;
        companionCharacterName = string.Empty;
        hasDataToRestore = false;

        LoggerService.PrintLogMessage(LogLevel.Debug,
            "[SAVE/LOAD] 🗑️ Todos os dados limpos",
            LogCategory.Player);
    }

    /// <summary>
    /// Verifica se há dados válidos salvos
    /// </summary>
    public bool HasValidData()
    {
        return CharactersDataIsValid();
    }

    /// <summary>
    /// Retorna a instância singleton
    /// </summary>
    public static ExplorationLoadContext Instance
    {
        get
        {
            if (instance == null)
            {
                var existingObject = FindFirstObjectByType<ExplorationLoadContext>();
                if (existingObject != null)
                {
                    instance = existingObject;
                }
            }
            return instance;
        }
    }
}