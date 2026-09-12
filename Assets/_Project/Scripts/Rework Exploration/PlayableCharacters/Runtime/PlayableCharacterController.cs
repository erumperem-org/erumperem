using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Core.CharacterStats;

/// <summary>
/// Controlador central do sistema de múltiplos personagens jogáveis. Guarda
/// o roster fixo de PlayableCharacters da cena, é a única fonte de verdade
/// de quem está Em Jogo e quem é Companheiro (todo o resto é Resting),
/// expõe os eventos de troca de papel, executa as operações de troca
/// centralizadas e dispara o carregamento inicial do save.
/// </summary>
public class PlayableCharacterController : MonoBehaviour
{
    [Header("Roster (sempre existem todos os personagens na cena)")]
    [SerializeField] private List<PlayableCharacters> characters = new List<PlayableCharacters>();

    [Header("Papéis iniciais (usados apenas se não houver arquivo de save)")]
    [SerializeField] private string initialInGameCharacterId;
    [SerializeField] private string initialCompanionCharacterId;

    public string InGameCharacterId { get; private set; }
    public string CompanionCharacterId { get; private set; }

    public PlayableCharacters InGameCharacter => GetCharacter(InGameCharacterId);
    public PlayableCharacters CompanionCharacter => GetCharacter(CompanionCharacterId);

    public event Action<PlayableCharacters> OnCharacterEnteredInGame;
    public event Action<PlayableCharacters> OnCharacterEnteredCompanion;
    public event Action<PlayableCharacters> OnCharacterEnteredResting;

    private void Start()
    {
        // Ponto de entrada do carregamento automático do save. async void é
        // intencional: este é um entry point de ciclo de vida do Unity, não
        // algo aguardado por outro código. Um wrapper futuro de save-game
        // (ex: um menu de "Continuar") pode chamar este mesmo método em vez
        // de depender do Start automático.
        LoadAndApplyCharactersAsync();
    }

    public PlayableCharacters GetCharacter(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return characters.FirstOrDefault(c => c.CharacterId == id);
    }

    /// <summary>Único ponto de execução de trocas de papel. Nenhum outro código deve chamar PlayableCharacters.SetState diretamente.</summary>
    public void ExecuteOperation(ICharacterSwitchOperation operation)
    {
        operation.Execute(this);
    }

    // ------------------------------------------------------------------
    // API interna para as operações (Operations/*.cs)
    // ------------------------------------------------------------------

    internal void AssignInGame(PlayableCharacters character)
    {
        InGameCharacterId = character.CharacterId;
        character.SetState(CharacterState.InGame);
        OnCharacterEnteredInGame?.Invoke(character);
    }

    internal void AssignCompanion(PlayableCharacters character)
    {
        CompanionCharacterId = character.CharacterId;
        character.SetState(CharacterState.Companion);
        character.SetFollowTarget(InGameCharacter != null ? InGameCharacter.transform : null);
        OnCharacterEnteredCompanion?.Invoke(character);
    }

    internal void AssignResting(PlayableCharacters character)
    {
        character.SetState(CharacterState.Resting);
        OnCharacterEnteredResting?.Invoke(character);
    }

    /// <summary>Atualiza o alvo de seguimento do Companheiro atual sem trocar de papel - usado quando o Em Jogo muda mas o Companheiro continua o mesmo.</summary>
    internal void RefreshCompanionFollowTarget()
    {
        var companion = CompanionCharacter;

        if (companion != null)
        {
            companion.SetFollowTarget(InGameCharacter != null ? InGameCharacter.transform : null);
        }
    }

    // ------------------------------------------------------------------
    // Save / Load
    // ------------------------------------------------------------------

    private async void LoadAndApplyCharactersAsync()
    {
        CharacterSaveData data = await CharacterPersistenceService.LoadAsync();
        CharacterVitalsSaveData vitalsData = await CharacterVitalsPersistenceService.LoadAsync();

        if (data == null)
        {
            ApplyInitialRolesWithoutSave();
            ApplyInitialPositionsWithoutSave();
        }
        else
        {
            ApplyLoadedData(data);
        }

        ApplyHealthForAllCharacters(vitalsData);
    }

    /// <summary>
    /// Aplica a vida de cada personagem do roster: combina o stat efetivo
    /// (base + modificadores de item, via CharacterEffectiveStatResolver) com
    /// a porcentagem salva de vitalidade. Roda independente de ApplyLoadedData/
    /// ApplyInitialRolesWithoutSave, porque a vitalidade vive num arquivo
    /// separado do de posição/papel.
    /// </summary>
    private void ApplyHealthForAllCharacters(CharacterVitalsSaveData vitalsData)
    {
        foreach (var character in characters)
        {
            if (character.HealthBar == null)
            {
                continue;
            }

            float effectiveMax = CharacterEffectiveStatResolver.GetEffectiveStat(character.CharacterId, StatType.Health);

            CharacterVitalsRecord record = vitalsData != null
                ? CharacterVitalsPersistenceService.GetRecordForCharacter(vitalsData, character.CharacterId)
                : null;

            // Sem registro salvo (primeira sessão, ou personagem novo no
            // roster): assume 100% de vida. Sinalizando como decisão, não como
            // certeza - me avise se preferir outro padrão de fallback aqui.
            float normalized = record?.HealthNormalized ?? 1f;

            character.HealthBar.LoadState(effectiveMax, effectiveMax * normalized);
        }
    }



    private void ApplyInitialRolesWithoutSave()
    {
        foreach (var character in characters)
        {
            if (character.CharacterId == initialInGameCharacterId)
            {
                AssignInGame(character);
            }
            else if (character.CharacterId == initialCompanionCharacterId)
            {
                AssignCompanion(character);
            }
            else
            {
                AssignResting(character);
            }
        }
    }

    private void ApplyInitialPositionsWithoutSave()
    {
        foreach (var character in characters)
        {
            character.transform.position = character.RestingPoint.position;
        }
    }

    private void ApplyLoadedData(CharacterSaveData data)
    {
        var recordsById = data.Records.ToDictionary(r => r.Id, r => r);

        foreach (var character in characters)
        {
            if (recordsById.TryGetValue(character.CharacterId, out var record))
            {
                character.Teleport(record.Position, record.Rotation);
            }

            if (character.CharacterId == data.InGameCharacterId)
            {
                AssignInGame(character);
            }
            else if (character.CharacterId == data.CompanionCharacterId)
            {
                AssignCompanion(character);
            }
            else
            {
                AssignResting(character);
            }
        }
    }

    /// <summary>
    /// Monta o CharacterSaveData atual e salva no disco. Sem gatilho
    /// automático - chame isso de onde fizer sentido no seu fluxo de jogo
    /// (checkpoint, saída de área segura, encerramento de sessão, etc.).
    /// </summary>
    public async Task SaveAsync()
    {
        var data = new CharacterSaveData
        {
            InGameCharacterId = InGameCharacterId,
            CompanionCharacterId = CompanionCharacterId
        };

        var vitalsData = new CharacterVitalsSaveData();

        foreach (var character in characters)
        {
            data.Records.Add(new CharacterSaveRecord
            {
                Id = character.CharacterId,
                State = character.CurrentState.ToString(),
                Position = character.transform.position,
                Rotation = character.transform.rotation
            });

            if (character.HealthBar != null)
            {
                vitalsData.Records.Add(new CharacterVitalsRecord
                {
                    CharacterId = character.CharacterId,
                    HealthNormalized = character.HealthBar.Model.Normalized
                });
            }
        }

        await CharacterPersistenceService.SaveAsync(data);
        await CharacterVitalsPersistenceService.SaveAsync(vitalsData);
    }

#if UNITY_EDITOR
    /// <summary>Uso exclusivo do editor de testes - força um novo carregamento do save. Compilado apenas em Editor.</summary>
    public void Editor_ForceReload()
    {
        LoadAndApplyCharactersAsync();
    }
#endif
}
