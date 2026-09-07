using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;
using Core.Inventory;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Inventory.Testing
{
    /// <summary>
    /// Editor-only test harness: adds or removes N units of a reference
    /// item on a target InventorySystem via inspector buttons. Not meant
    /// for production scenes — exists purely to exercise InventorySystem
    /// (and, by extension, InventoryManager migration) in isolation.
    /// </summary>
    public sealed class InventoryTestbed : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventorySystem _inventory;

        [Tooltip("Must implement IIITem.")]
        [SerializeField] private ScriptableObject _itemAsset;

        [Header("Amount")]
        [SerializeField] private int _amount = 1;

        private IIITem Item => _itemAsset as IIITem;

        public void Add()
        {
            if (!Validate()) return;

            int added = _inventory.AddAsMuchAsPossible(Item, _amount);

            if (added == _amount)
                Log(LogLevel.Debug, $"Added {added} of '{Item.StorageableId}'.");
            else if (added > 0)
                Log(LogLevel.Warning, $"Partial add: {added}/{_amount} of '{Item.StorageableId}' fit.");
            else
                Log(LogLevel.Warning, $"Failed to add '{Item.StorageableId}' — no space/capacity available.");
        }

        public void Remove()
        {
            if (!Validate()) return;

            int removed = _inventory.TryRemoveItem(Item, _amount);

            if (removed == _amount)
                Log(LogLevel.Debug, $"Removed {removed} of '{Item.StorageableId}'.");
            else if (removed > 0)
                Log(LogLevel.Warning, $"Partial remove: {removed}/{_amount} of '{Item.StorageableId}' were available.");
            else
                Log(LogLevel.Warning, $"Failed to remove '{Item.StorageableId}' — none found in inventory.");
        }

        private bool Validate()
        {
            if (_inventory == null) { Log(LogLevel.Error, "InventorySystem not assigned."); return false; }
            if (Item == null) { Log(LogLevel.Error, "Item asset not assigned or does not implement IIITem."); return false; }
            if (_amount <= 0) { Log(LogLevel.Error, "Amount must be greater than 0."); return false; }
            return true;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[InventoryTestbed:{gameObject.name}] {msg}", LogCategory.Inventory);
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(InventoryTestbed))]
    public sealed class InventoryTestbedEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var testbed = (InventoryTestbed)target;

            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to add/remove.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add"))
                    testbed.Add();

                if (GUILayout.Button("Remove"))
                    testbed.Remove();
            }
        }
    }
#endif
}