using System;
using System.IO;
using UnityEngine;

public sealed class SaveFileWiper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SaveSlotManager saveSlotManager;

    [Header("Directory Settings")]
    [SerializeField] private string saveFolderName = "Saves";

    private string SaveDirectory =>
        Path.Combine(Application.persistentDataPath, saveFolderName);

    // Deletes the currently selected slot through SaveSlotManager.
    public void DeleteCurrentSlot()
    {
        CurrentSlotData currentSlot = SaveSlotAccess.Data;

        if (currentSlot == null || !currentSlot.HasSlotSelected)
        {
            Debug.LogWarning(
                "[SaveFileWiper] No save slot is currently selected.",
                this);
            return;
        }

        if (saveSlotManager == null)
        {
            Debug.LogError(
                "[SaveFileWiper] SaveSlotManager is not assigned.",
                this);
            return;
        }

        int slotIndex = currentSlot.SlotIndex;

        saveSlotManager.DeleteSlot(slotIndex);

        Debug.Log(
            $"[SaveFileWiper] Deletion requested for slot {slotIndex}.",
            this);
    }

    // Deletes every file and subdirectory inside Saves, preserving Saves itself.
    public void ClearSavesDirectory()
    {
        if (!Directory.Exists(SaveDirectory))
        {
            Debug.LogWarning(
                $"[SaveFileWiper] Saves directory not found: {SaveDirectory}",
                this);
            return;
        }

        try
        {
            foreach (string file in Directory.GetFiles(
                SaveDirectory, "*", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }

            foreach (string directory in Directory.GetDirectories(
                SaveDirectory, "*", SearchOption.AllDirectories))
            {
                Directory.Delete(directory, false);
            }

            Debug.Log(
                $"[SaveFileWiper] Saves directory cleared: {SaveDirectory}",
                this);
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[SaveFileWiper] Failed to clear Saves directory: {ex.Message}",
                this);
        }
    }

    // Executes both operations.
    public void ClearAll()
    {
        DeleteCurrentSlot();
        ClearSavesDirectory();
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(SaveFileWiper))]
    private sealed class SaveFileWiperEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            UnityEditor.EditorGUILayout.Space(10);
            UnityEditor.EditorGUILayout.LabelField(
                "Save Cleanup",
                UnityEditor.EditorStyles.boldLabel);

            bool isPlaying = Application.isPlaying;
            GUI.enabled = isPlaying;

            Color previousColor = GUI.backgroundColor;

            GUI.backgroundColor = new Color(1f, 0.65f, 0.3f);

            if (GUILayout.Button("Delete Current Slot"))
            {
                ((SaveFileWiper)target).DeleteCurrentSlot();
            }

            if (GUILayout.Button("Clear Saves Directory"))
            {
                ((SaveFileWiper)target).ClearSavesDirectory();
            }

            UnityEditor.EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);

            if (GUILayout.Button("Clear All"))
            {
                ((SaveFileWiper)target).ClearAll();
            }

            GUI.backgroundColor = previousColor;
            GUI.enabled = true;

            if (!isPlaying)
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    "Enter Play Mode to use these buttons.",
                    UnityEditor.MessageType.Info);
            }
        }
    }
#endif
}
