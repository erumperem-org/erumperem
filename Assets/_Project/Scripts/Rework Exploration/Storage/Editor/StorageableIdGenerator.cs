#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.Storage.Editor
{
    /// <summary>
    /// Shared editor utility to auto-generate StorageableId values for
    /// ScriptableObjects that don't have one yet. Never overwrites an
    /// existing id — doing so would break any save data referencing it.
    /// Assumes the target's serialized field is named "_storageableId"
    /// (true for both ItemDefinition and CoinDefinition).
    /// </summary>
    public static class StorageableIdGenerator
    {
        private const string SerializedFieldName = "_storageableId";

        /// <summary>
        /// Fills in a generated id (format: "{prefix}_{8-char uppercase hex}")
        /// for every candidate whose current id (as read via <paramref name="getCurrentId"/>)
        /// is null or empty. Returns how many ids were generated.
        /// </summary>
        public static int GenerateMissingIds(
            IEnumerable<ScriptableObject> candidates,
            string prefix,
            Func<ScriptableObject, string> getCurrentId)
        {
            var candidateList = candidates.Where(c => c != null).ToList();

            var existingIds = new HashSet<string>(
                candidateList
                    .Select(getCurrentId)
                    .Where(id => !string.IsNullOrEmpty(id)));

            int generatedCount = 0;

            foreach (var obj in candidateList)
            {
                string current = getCurrentId(obj);
                if (!string.IsNullOrEmpty(current)) continue; // never overwrite

                string newId = GenerateUniqueId(prefix, existingIds);
                existingIds.Add(newId);

                if (AssignId(obj, newId))
                    generatedCount++;
            }

            return generatedCount;
        }

        private static string GenerateUniqueId(string prefix, HashSet<string> existingIds)
        {
            string id;
            do
            {
                id = $"{prefix}_{GenerateHex()}";
            } while (existingIds.Contains(id));

            return id;
        }

        private static string GenerateHex()
        {
            var bytes = new byte[4];
            Guid.NewGuid().ToByteArray().AsSpan(0, 4).CopyTo(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToUpperInvariant();
        }

        private static bool AssignId(ScriptableObject obj, string id)
        {
            var serializedObject = new SerializedObject(obj);
            var property = serializedObject.FindProperty(SerializedFieldName);

            if (property == null)
            {
                Debug.LogError($"[StorageableIdGenerator] Field '{SerializedFieldName}' not found on '{obj.name}'.", obj);
                return false;
            }

            property.stringValue = id;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(obj);
            return true;
        }
    }
}
#endif