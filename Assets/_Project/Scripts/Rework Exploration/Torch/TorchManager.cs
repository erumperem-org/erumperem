using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Central controller for the torch system.
/// Responsible for maintaining the overall state (lit/unlit), notifying
/// subscribers via event, and saving/loading this state to/from disk.
///
/// Also listens to a configurable New Input System action (assigned in the
/// Inspector) to toggle the torches - same subscription pattern used by
/// PlayerInputMovementController (explicit Enable/Disable and
/// performed/canceled subscription, not polling).
///
/// The save file now lives inside the active save slot
/// (SaveSlotAccess.Data.SlotDirectory) instead of a fixed subfolder under
/// Application.persistentDataPath - so torch state is per save slot, same
/// as character data.
/// </summary>
public class TorchManager : MonoBehaviour
{
    public static TorchManager Instance { get; private set; }

    /// <summary>
    /// Triggered whenever the overall torch state changes.
    /// true  = torches lit
    /// false = torches unlit
    /// </summary>
    public event Action<bool> OnTorchStateChange;

    [Header("Input")]
    [Tooltip("Optional - action that toggles the torches on/off. If left unassigned, the torches can still be toggled/set through code (SetTorchState/ToggleTorchState).")]
    [SerializeField] private InputActionReference toggleTorchAction;

    [Header("Persistence Configuration")]
    [Tooltip("Subfolder inside the active slot's directory where the state file will be saved.")]
    [SerializeField] private string saveSubdirectory = "TorchData";

    [Tooltip("Name of the torch state file.")]
    [SerializeField] private string saveFileName = "torch_state.json";

    [Header("Current State")]
    [SerializeField] private bool isTorchLit = false; // Default: torches unlit

    public bool IsTorchLit => isTorchLit;

    [Serializable]
    private class TorchSaveData
    {
        public bool isTorchLit;
    }

    private bool TryGetSaveFilePath(out string path)
    {
        var slotData = SaveSlotAccess.Data;

        if (slotData == null || !slotData.HasSlotSelected)
        {
            Debug.LogError($"[{nameof(TorchManager)}] Nenhum slot selecionado (SaveSlotAccess.Data) - operação abortada.");
            path = null;
            return false;
        }

        string directoryPath = Path.Combine(slotData.SlotDirectory, saveSubdirectory);
        path = Path.Combine(directoryPath, saveFileName);
        return true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (toggleTorchAction == null)
        {
            return;
        }

        // TorchManager é singleton (uma única instância viva por vez), então
        // diferente de PlayerInputMovementController - que precisa evitar
        // Disable() por a action ser compartilhada entre vários personagens
        // trocando de papel - aqui não há esse risco: só esta instância liga
        // e desliga a action, então Enable/Disable simétrico é seguro.
        toggleTorchAction.action.Enable();
        toggleTorchAction.action.performed += OnToggleTorchPerformed;
    }

    private void OnDisable()
    {
        if (toggleTorchAction == null)
        {
            return;
        }

        toggleTorchAction.action.performed -= OnToggleTorchPerformed;
        toggleTorchAction.action.Disable();
    }

    private void OnToggleTorchPerformed(InputAction.CallbackContext context)
    {
        ToggleTorchState();
    }

    private async void Start()
    {
        // Loads the saved state as soon as the manager is initialized.
        // If there is no file (or no slot selected yet), the default state
        // (unlit) is maintained.
        await LoadTorchStateAsync();
    }

    /// <summary>Flips the current torch state - the action this component's input toggle button performs.</summary>
    public void ToggleTorchState() => SetTorchState(!isTorchLit);

    /// <summary>
    /// Public entry point for changing the overall torch state.
    /// Notifies subscribers.
    /// </summary>
    [ContextMenu("Test: Light Torches")]
    public void TestSetTorchStateLit() => SetTorchState(true);

    [ContextMenu("Test: Extinguish Torches")]
    public void TestSetTorchStateUnlit() => SetTorchState(false);

    public void SetTorchState(bool lit)
    {
        if (isTorchLit == lit)
            return;

        isTorchLit = lit;
        OnTorchStateChange?.Invoke(isTorchLit);
    }

    /// <summary>
    /// Saves the current torch state.
    /// Orchestration point: currently only delegates to file writing,
    /// but this is the appropriate place for additional rules (e.g. saving
    /// to multiple slots, notifying analytics, etc.) before persisting.
    /// </summary>
    public async Task SaveTorchStateAsync()
    {
        await WriteTorchStateToFileAsync(isTorchLit);
    }

    [ContextMenu("Test: Save State")]
    private async void TestSaveTorchState() => await SaveTorchStateAsync();

    /// <summary>
    /// Writes the torch state information (lit/unlit) to a file inside the
    /// active slot's directory. Does nothing if no slot is selected.
    /// </summary>
    private async Task WriteTorchStateToFileAsync(bool lit)
    {
        if (!TryGetSaveFilePath(out string fullPath))
        {
            return;
        }

        try
        {
            string directoryPath = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            var data = new TorchSaveData { isTorchLit = lit };
            string json = JsonUtility.ToJson(data);

            await File.WriteAllTextAsync(fullPath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TorchManager] Failed to save torch state: {e.Message}");
        }
    }

    /// <summary>
    /// Loads the torch state from the saved file inside the active slot's
    /// directory. If no slot is selected, the file does not exist, is
    /// empty, or is invalid, the default state is assumed: torches unlit.
    /// </summary>
    public async Task LoadTorchStateAsync()
    {
        bool loadedState = false; // Default: unlit

        if (TryGetSaveFilePath(out string fullPath))
        {
            try
            {
                if (File.Exists(fullPath))
                {
                    string json = await File.ReadAllTextAsync(fullPath);

                    if (!string.IsNullOrEmpty(json))
                    {
                        var data = JsonUtility.FromJson<TorchSaveData>(json);
                        if (data != null)
                            loadedState = data.isTorchLit;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TorchManager] Failed to load torch state: {e.Message}");
            }
        }

        isTorchLit = loadedState;
        OnTorchStateChange?.Invoke(isTorchLit);
    }

    [ContextMenu("Test: Load State")]
    private async void TestLoadTorchState() => await LoadTorchStateAsync();
}