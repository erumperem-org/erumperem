using UnityEditor;
using UnityEngine;

/// <summary>
/// Testbed isolado do sistema de persistência: salva e lê um
/// CharacterSaveData fictício direto no disco, sem depender de nenhum
/// PlayableCharacter real na cena - prova que a serialização/desserialização
/// funciona isoladamente, antes de integrar com o resto do sistema.
/// </summary>
public static class CharacterPersistenceTestbed
{
    [MenuItem("Tools/Character Persistence/Save Dados Fictícios")]
    private static async void SaveFakeData()
    {
        var data = new CharacterSaveData
        {
            InGameCharacterId = "test-character-a",
            CompanionCharacterId = "test-character-b"
        };

        data.Records.Add(new CharacterSaveRecord
        {
            Id = "test-character-a",
            State = "InGame",
            Position = new Vector3(1f, 0f, 2f),
            Rotation = Quaternion.identity
        });

        data.Records.Add(new CharacterSaveRecord
        {
            Id = "test-character-b",
            State = "Companion",
            Position = new Vector3(3f, 0f, 4f),
            Rotation = Quaternion.Euler(0f, 90f, 0f)
        });

        data.Records.Add(new CharacterSaveRecord
        {
            Id = "test-character-c",
            State = "Resting",
            Position = new Vector3(-5f, 0f, 0f),
            Rotation = Quaternion.identity
        });

        await CharacterPersistenceService.SaveAsync(data);
        Debug.Log("[CharacterPersistenceTestbed] Dados fictícios salvos.");
    }

    [MenuItem("Tools/Character Persistence/Load e Logar")]
    private static async void LoadAndLog()
    {
        CharacterSaveData data = await CharacterPersistenceService.LoadAsync();

        if (data == null)
        {
            Debug.LogWarning("[CharacterPersistenceTestbed] Nenhum save encontrado.");
            return;
        }

        Debug.Log($"[CharacterPersistenceTestbed] InGame: {data.InGameCharacterId}, Companion: {data.CompanionCharacterId}, Records: {data.Records.Count}");

        foreach (var record in data.Records)
        {
            Debug.Log($"  - {record.Id}: {record.State} @ {record.Position}, rot {record.Rotation.eulerAngles}");
        }
    }
}
