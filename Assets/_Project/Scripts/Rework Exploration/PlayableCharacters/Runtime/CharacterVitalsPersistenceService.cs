using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Persistência própria e isolada da vitalidade dos personagens (hoje só
/// vida, guardada como porcentagem normalizada). Deliberadamente separada
/// de CharacterPersistenceService/CharacterSaveData (pacote
/// CharacterPersistence) - aquele pacote continua 100% agnóstico de
/// BarSystem/conceitos de vida; este é um segundo arquivo próprio, vivendo
/// dentro de PlayableCharacters.
///
/// O arquivo agora vive dentro do slot ativo (SaveSlotAccess.Data.SlotDirectory),
/// mesmo padrão aplicado a CharacterPersistenceService.
/// </summary>
public static class CharacterVitalsPersistenceService
{
    private const string SaveFileName = "character_vitals_save.json";

    private static bool TryGetSaveFilePath(out string path)
    {
        var slotData = SaveSlotAccess.Data;

        if (slotData == null || !slotData.HasSlotSelected)
        {
            Debug.LogError("[CharacterVitalsPersistenceService] Nenhum slot selecionado (SaveSlotAccess.Data) - operação abortada.");
            path = null;
            return false;
        }

        path = Path.Combine(slotData.SlotDirectory, SaveFileName);
        return true;
    }

    public static async Task<CharacterVitalsSaveData> LoadAsync()
    {
        if (!TryGetSaveFilePath(out string path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = await Task.Run(() => File.ReadAllText(path));
            return JsonUtility.FromJson<CharacterVitalsSaveData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[CharacterVitalsPersistenceService] Falha ao carregar em '{path}': {exception}");
            return null;
        }
    }

    public static async Task SaveAsync(CharacterVitalsSaveData data)
    {
        if (!TryGetSaveFilePath(out string path))
        {
            return;
        }

        string json = JsonUtility.ToJson(data, prettyPrint: true);

        try
        {
            await Task.Run(() => File.WriteAllText(path, json));
        }
        catch (Exception exception)
        {
            Debug.LogError($"[CharacterVitalsPersistenceService] Falha ao salvar em '{path}': {exception}");
        }
    }

    /// <summary>
    /// Descompactação por id, pronta para uso futuro caso mais campos de
    /// vitalidade sejam adicionados - mesmo formato de
    /// CharacterStatsRepository.GetStatsForCharacter.
    /// </summary>
    public static CharacterVitalsRecord GetRecordForCharacter(CharacterVitalsSaveData data, string characterId)
    {
        return data?.Records.FirstOrDefault(r => r.CharacterId == characterId);
    }
}