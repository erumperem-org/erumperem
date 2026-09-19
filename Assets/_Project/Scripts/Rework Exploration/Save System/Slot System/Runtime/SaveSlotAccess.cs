using UnityEngine;

/// <summary>
/// Ponto de acesso global e somente-leitura ao CurrentSlotData. Não
/// seleciona, cria nem deleta slots — essa continua sendo
/// responsabilidade exclusiva do SaveSlotManager. Este componente só
/// guarda a referência ao mesmo asset, para que qualquer código no jogo
/// possa lê-la via SaveSlotAccess.Data, sem precisar arrastar uma
/// referência no Inspector de cada consumidor.
///
/// Sobrevive a trocas de cena (DontDestroyOnLoad). Se mais de uma
/// instância existir (ex: cena de bootstrap recarregada por engano), a
/// mais nova se autodestrói, preservando a primeira.
///
/// IMPORTANTE: o campo currentSlotData aqui precisa apontar para o MESMO
/// asset arrastado no SaveSlotManager - são dois componentes referenciando
/// o mesmo ScriptableObject, não duas fontes de dado separadas.
/// </summary>
public class SaveSlotAccess : MonoBehaviour
{
    public static SaveSlotAccess Instance { get; private set; }

    [Header("Reference")]
    [Tooltip("Deve ser o MESMO asset CurrentSlotData referenciado pelo SaveSlotManager.")]
    [SerializeField] private CurrentSlotData currentSlotData;

    /// <summary>Acesso estático global ao CurrentSlotData, de qualquer lugar do código.</summary>
    public static CurrentSlotData Data => Instance != null ? Instance.currentSlotData : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(SaveSlotAccess)}] Instância duplicada encontrada em '{gameObject.name}' - destruindo esta e mantendo a original.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (currentSlotData == null)
        {
            Debug.LogError($"[{nameof(SaveSlotAccess)}] No CurrentSlotData assigned in the Inspector.", this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}