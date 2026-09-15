using System.IO;
using UnityEngine;

/// <summary>
/// Single entry point for "choosing a save slot".
///
/// Responsibilities:
/// 1) Translate a slot number (0, 1, 2, ...) into a physical path in the
///    format "Application.persistentDataPath/Slot 000";
/// 2) Ensure that slot's folder exists on disk;
/// 3) Write the result into <see cref="CurrentSlotData"/> (ScriptableObject),
///    which is the reference the rest of the game should consult.
///
/// This component is the ONLY writer of CurrentSlotData. Any other system
/// (slot selection UI, save/load system, etc.) should call SelectSlot(int)
/// and then read the result through CurrentSlotData.
/// </summary>
public class SaveSlotManager : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("ScriptableObject that stores the current session's slot. " +
             "Must be the SAME asset referenced by all other save systems.")]
    [SerializeField]
    private CurrentSlotData currentSlotData;

    [Header("Configuration")]
    [Tooltip("Prefix used to name each slot's folder. " +
             "Final result: <persistentDataPath>/<prefix><N in 000 format> (e.g. \"Slot 000\", \"Slot 001\"...)")]
    [SerializeField]
    private string slotFolderPrefix = "Slot ";

    [Tooltip("If checked, a slot is automatically selected on this component's Awake.")]
    [SerializeField]
    private bool selectSlotOnAwake = false;

    [Tooltip("Slot used automatically if 'Select slot on Awake' is checked.")]
    [SerializeField]
    private int defaultSlotIndex = 0;

    /// <summary>Root path where all slots are stored on this platform.</summary>
    public string SlotsRootPath => Application.persistentDataPath;

    private void Awake()
    {
        if (currentSlotData == null)
        {
            Debug.LogError($"[{nameof(SaveSlotManager)}] No CurrentSlotData assigned in the Inspector.", this);
            return;
        }

        if (selectSlotOnAwake)
        {
            SelectSlot(defaultSlotIndex);
        }
    }

    /// <summary>
    /// Selects the session's active slot: builds the path
    /// "Application.persistentDataPath/Slot 000", creates the folder if it
    /// doesn't exist yet, and updates CurrentSlotData with the index and directory.
    /// </summary>
    /// <param name="slotIndex">Slot number (0, 1, 2, ...).</param>
    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0)
        {
            Debug.LogError($"[{nameof(SaveSlotManager)}] Invalid slot index: {slotIndex}. Must be >= 0.");
            return;
        }

        if (currentSlotData == null)
        {
            Debug.LogError($"[{nameof(SaveSlotManager)}] CurrentSlotData not assigned in the Inspector.", this);
            return;
        }

        string fullPath = GetSlotPath(slotIndex);
        EnsureDirectoryExists(fullPath);

        currentSlotData.SetSlot(slotIndex, fullPath);

        Debug.Log($"[{nameof(SaveSlotManager)}] Slot {slotIndex} selected. Directory: {fullPath}");
    }

    /// <summary>
    /// Returns the path a given slot would have on disk, WITHOUT selecting
    /// it and WITHOUT creating the folder. Useful for "choose a slot"
    /// screens that need to check if a save already exists before the
    /// player confirms.
    /// </summary>
    public string GetSlotPath(int slotIndex)
    {
        string folderName = $"{slotFolderPrefix} {slotIndex:000}";
        return Path.Combine(Application.persistentDataPath, folderName);
    }

    /// <summary>Checks whether a folder already exists on disk for the given slot.</summary>
    public bool SlotExistsOnDisk(int slotIndex)
    {
        return Directory.Exists(GetSlotPath(slotIndex));
    }

    /// <summary>
    /// Permanently deletes the folder and all contents of a slot.
    /// If the deleted slot is the currently active one, CurrentSlotData is
    /// also cleared (back to "no slot selected").
    /// </summary>
    public void DeleteSlot(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            Debug.Log($"[{nameof(SaveSlotManager)}] Slot {slotIndex} deleted ({path}).");
        }

        if (currentSlotData != null && currentSlotData.SlotIndex == slotIndex)
        {
            currentSlotData.ClearSlot();
        }
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}