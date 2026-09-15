using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Serviço genérico de leitura/escrita do save de personagens em JSON. Não
/// conhece PlayableCharacter nem qualquer outro tipo de personagem - só
/// serializa/desserializa CharacterSaveData de/para disco. Reutilizável por
/// qualquer sistema que precise persistir uma lista de
/// "id + estado + posição + rotação" por GameObject.
///
/// O arquivo agora vive dentro do slot ativo (SaveSlotAccess.Data.SlotDirectory),
/// não mais direto em Application.persistentDataPath - assim, cada slot tem
/// seu próprio roster de personagens salvos, independente dos demais.
/// </summary>
public static class CharacterPersistenceService
{
    private const string SaveFileName = "characters_save.json";

    private static bool TryGetSaveFilePath(out string path)
    {
        var slotData = SaveSlotAccess.Data;

        if (slotData == null || !slotData.HasSlotSelected)
        {
            Debug.LogError("[CharacterPersistenceService] Nenhum slot selecionado (SaveSlotAccess.Data) - operação abortada.");
            path = null;
            return false;
        }

        path = Path.Combine(slotData.SlotDirectory, SaveFileName);
        return true;
    }

    /// <summary>
    /// Lê o arquivo de save do disco de forma assíncrona. Retorna null se
    /// não houver slot selecionado, ou se o arquivo não existir - quem
    /// chama decide o fallback (ex: manter a posição autoral da cena, como
    /// PlayableCharacterController faz).
    /// </summary>
    public static async Task<CharacterSaveData> LoadAsync()
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
            return JsonUtility.FromJson<CharacterSaveData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[CharacterPersistenceService] Falha ao carregar save em '{path}': {exception}");
            return null;
        }
    }

    /// <summary>
    /// Escreve o save no disco de forma assíncrona, sobrescrevendo o
    /// arquivo anterior dentro do slot ativo. Sem gatilho automático - quem
    /// chama decide quando salvar (ver PlayableCharacterController.SaveAsync).
    /// Não escreve nada se não houver slot selecionado.
    /// </summary>
    public static async Task SaveAsync(CharacterSaveData data)
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
            Debug.LogError($"[CharacterPersistenceService] Falha ao salvar em '{path}': {exception}");
        }
    }
}