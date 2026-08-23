using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Central controller for the torch system.
/// Responsible for maintaining the overall state (lit/unlit), notifying
/// subscribers via event, and saving/loading this state to/from disk.
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

    [Header("Persistence Configuration")]
    [Tooltip("Directory (relative to Application.persistentDataPath) where the state file will be saved.")]
    [SerializeField] private string saveDirectory = "TorchData";

    [Tooltip("Name of the torch state file.")]
    [SerializeField] private string saveFileName = "torch_state.json";

    [Header("Current State")]
    [SerializeField] private bool isTorchLit = false; // Default: torches unlit

    public bool IsTorchLit => isTorchLit;

    private string DirectoryPath => Path.Combine(Application.persistentDataPath, saveDirectory);
    private string FullFilePath => Path.Combine(DirectoryPath, saveFileName);

    [Serializable]
    private class TorchSaveData
    {
        public bool isTorchLit;
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

    private async void Start()
    {
        // Loads the saved state as soon as the manager is initialized.
        // If there is no file, the default state (unlit) is maintained.
        await LoadTorchStateAsync();
    }

    /// <summary>
    /// Public entry point for changing the overall torch state.
    /// Notifies subscribers.
    /// </summary>
    [ContextMenu("Test: Light Torches")]
    public void TestSetTorchStateLit() => SetTorchState(true);

    [ContextMenu("Test: Extinguish Torches")]
    public void TestSetTorchStateUnlit() => SetTorchState(false);

    public async void SetTorchState(bool lit)
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
    /// Writes the torch state information (lit/unlit) to a
    /// file inside the configured directory.
    /// </summary>
    private async Task WriteTorchStateToFileAsync(bool lit)
    {
        try
        {
            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            var data = new TorchSaveData { isTorchLit = lit };
            string json = JsonUtility.ToJson(data);

            await File.WriteAllTextAsync(FullFilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TorchManager] Failed to save torch state: {e.Message}");
        }
    }

    /// <summary>
    /// Loads the torch state from the saved file.
    /// If the file does not exist, is empty, or is invalid,
    /// the default state is assumed: torches unlit.
    /// </summary>
    public async Task LoadTorchStateAsync()
    {
        bool loadedState = false; // Default: unlit

        try
        {
            if (File.Exists(FullFilePath))
            {
                string json = await File.ReadAllTextAsync(FullFilePath);

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

        isTorchLit = loadedState;
        OnTorchStateChange?.Invoke(isTorchLit);
    }

    [ContextMenu("Test: Load State")]
    private async void TestLoadTorchState() => await LoadTorchStateAsync();
}