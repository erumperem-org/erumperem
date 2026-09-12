using System.IO;
using UnityEngine;
using BarSystem.Core;

namespace BarSystem.Persistence
{
    /// <summary>
    /// Reference implementation of IBarStateRepository: one .json file
    /// per bar, saved in Application.persistentDataPath. Each Id becomes a
    /// separate file, avoiding write conflicts between different bars.
    /// </summary>
    public class JsonFileBarStateRepository : IBarStateRepository
    {
        private readonly string _folder;

        public JsonFileBarStateRepository(string folderName = "BarSystemSaves")
        {
            _folder = Path.Combine(Application.persistentDataPath, folderName);
            Directory.CreateDirectory(_folder);
        }

        public void Save(BarSaveData data)
        {
            string path = GetPath(data.Id);
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(path, json);
        }

        public BarSaveData Load(string id)
        {
            string path = GetPath(id);
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<BarSaveData>(json);
        }

        public bool HasSavedState(string id) => File.Exists(GetPath(id));

        private string GetPath(string id) => Path.Combine(_folder, $"{id}.json");
    }
}