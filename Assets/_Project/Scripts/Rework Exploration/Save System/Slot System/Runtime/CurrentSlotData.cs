using UnityEngine;

/// <summary>
/// ScriptableObject that exposes, at runtime, which save slot is currently
/// selected in the active session, along with the corresponding physical
/// directory.
///
/// Acts as the "single source of truth" for any system that needs to know
/// where to read/write data (save system, UI screens, autoload, per-slot
/// audio/config systems, etc). Those systems should only READ the values
/// exposed here (SlotIndex / SlotDirectory / HasSlotSelected).
/// The only one allowed to write to this asset is <see cref="SaveSlotManager"/>.
///
/// IMPORTANT — PERSISTENCE:
/// The values held by this ScriptableObject only exist in memory, for the
/// duration of the current game session. They are NOT automatically written
/// to disk and do NOT represent the save data itself — they simply indicate
/// "which folder am I using right now". When the game is closed, these
/// values are lost (this is intentional).
/// See the system's README.md for a full explanation.
/// </summary>
[CreateAssetMenu(fileName = "CurrentSlotData", menuName = "Save System/Current Slot Data")]
public class CurrentSlotData : ScriptableObject
{
    [Header("Current slot state (set at runtime by SaveSlotManager)")]
    [SerializeField, Tooltip("Index of the currently selected slot. -1 = no slot selected in this session.")]
    private int slotIndex = -1;

    [SerializeField, Tooltip("Absolute path on disk for the current slot's folder (Application.persistentDataPath/Slot 000).")]
    private string slotDirectory = string.Empty;

    /// <summary>Index of the current slot. -1 means no slot has been set for this session.</summary>
    public int SlotIndex => slotIndex;

    /// <summary>Absolute path (Application.persistentDataPath/Slot 000) of the current slot.</summary>
    public string SlotDirectory => slotDirectory;

    /// <summary>True if a slot has already been selected in this session.</summary>
    public bool HasSlotSelected => slotIndex >= 0 && !string.IsNullOrEmpty(slotDirectory);

    /// <summary>Raised whenever the active slot changes (index, new directory).</summary>
    public event System.Action<int, string> OnSlotChanged;

    /// <summary>
    /// Sets the active slot. Should only be called by SaveSlotManager.
    /// Other systems must not call this method directly — they should only
    /// read the properties above.
    /// </summary>
    public void SetSlot(int newSlotIndex, string newSlotDirectory)
    {
        slotIndex = newSlotIndex;
        slotDirectory = newSlotDirectory;
        OnSlotChanged?.Invoke(slotIndex, slotDirectory);
    }

    /// <summary>
    /// Clears the asset's state, returning to "no slot selected".
    /// </summary>
    public void ClearSlot()
    {
        SetSlot(-1, string.Empty);
    }

    /// <summary>
    /// Session safety net: guarantees the slot never "leaks" from one run
    /// into the next. In a normal build this wouldn't even be necessary
    /// (each new process loads the asset's original serialized values), but
    /// in the Editor, if "Enter Play Mode Options" is configured with Domain
    /// Reload disabled, changes made during Play Mode can remain on the
    /// asset between runs. This reset runs before any scene loads, covering
    /// both cases.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAllInstancesOnGameStart()
    {
        var allInstances = Resources.FindObjectsOfTypeAll<CurrentSlotData>();
        foreach (var instance in allInstances)
        {
            instance.slotIndex = -1;
            instance.slotDirectory = string.Empty;
        }
    }
}