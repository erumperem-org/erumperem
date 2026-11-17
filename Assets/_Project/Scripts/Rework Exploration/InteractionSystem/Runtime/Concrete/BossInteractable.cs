using System;
using System.IO;
using UnityEngine;
using InteractionSystem;

/// <summary>
/// Boss interagível cujo estado (derrotado ou não) é salvo na pasta do slot atual.
///
/// Arquivo: {SlotDirectory}/Bosses/{bossId}.json
///   - Sem arquivo ........................ não derrotado  -> disponível (valor de startAvailable)
///   - Arquivo com defeated = false ....... não derrotado  -> disponível
///   - Arquivo com defeated = true ........ derrotado      -> indisponível
///
/// O bossId é gerado/validado pelo BossInteractableEditor (único na cena),
/// mas pode ser ajustado manualmente no Inspector.
/// </summary>
public class BossInteractable : InteractableBase
{
    private const string FolderName = "Bosses";

    [Header("Boss Save")]
    [Tooltip("ID único deste boss. Gerado pelo editor; pode ser ajustado manualmente, mas não pode se repetir.")]
    [SerializeField] private string bossId;

    [Serializable]
    private class BossSaveData
    {
        public bool defeated;
    }

    public string BossId => bossId;
    public bool IsDefeated { get; private set; }

    // Start (e não Awake/OnEnable) para garantir que SaveSlotAccess já existe
    // e que sensores/UI já assinaram AvailabilityChanged.
    private void Start()
    {
        RefreshFromSave();
    }

    /// <summary>Lê o arquivo do slot atual e ajusta a disponibilidade.</summary>
    public void RefreshFromSave()
    {
        string path = GetFilePath();
        IsDefeated = false;

        if (path == null || !File.Exists(path))
            return; // sem arquivo = não derrotado, mantém disponibilidade padrão

        try
        {
            var data = JsonUtility.FromJson<BossSaveData>(File.ReadAllText(path));
            IsDefeated = data != null && data.defeated;
        }
        catch (Exception e)
        {
            Debug.LogError($"[{nameof(BossInteractable)}] Falha ao ler '{path}': {e.Message}", this);
        }

        // Há arquivo: derrotado = indisponível, não derrotado = disponível.
        SetAvailable(!IsDefeated);
    }

    /// <summary>Chame quando o boss for vencido. Salva no slot e bloqueia a interação.</summary>
    public void MarkAsDefeated()
    {
        IsDefeated = true;
        WriteState(true);
        SetAvailable(false);
    }

    protected override void OnInteractionStarted(IInteractionContext context)
    {
        base.OnInteractionStarted(context);

        // Registra o encontro (arquivo com defeated = false), se ainda não existir.
        string path = GetFilePath();
        if (path != null && !File.Exists(path))
            WriteState(false);

        StartCombat();
    }

    /// <summary>Inicia o combate contra este boss. Implementar depois.</summary>
    public void StartCombat()
    {
    }

    private string GetFilePath()
    {
        var slot = SaveSlotAccess.Data;
        if (slot == null || !slot.HasSlotSelected || string.IsNullOrWhiteSpace(bossId))
            return null;

        string safeId = bossId;
        foreach (char c in Path.GetInvalidFileNameChars())
            safeId = safeId.Replace(c, '_');

        return Path.Combine(slot.SlotDirectory, FolderName, safeId + ".json");
    }

    private void WriteState(bool defeated)
    {
        string path = GetFilePath();
        if (path == null)
        {
            Debug.LogWarning($"[{nameof(BossInteractable)}] Sem slot selecionado ou bossId vazio - estado não salvo.", this);
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new BossSaveData { defeated = defeated }));
        }
        catch (Exception e)
        {
            Debug.LogError($"[{nameof(BossInteractable)}] Falha ao salvar '{path}': {e.Message}", this);
        }
    }
}