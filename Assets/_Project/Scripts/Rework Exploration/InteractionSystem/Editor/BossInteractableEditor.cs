#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector do BossInteractable. Gera um bossId único ("cena_boss_001")
/// olhando os outros BossInteractable existentes, e avisa se houver IDs
/// repetidos (ex: depois de Ctrl+D). O campo continua editável à mão.
/// Colocar esta pasta como "Editor".
/// </summary>
[CustomEditor(typeof(BossInteractable), true)]
[CanEditMultipleObjects]
public class BossInteractableEditor : Editor
{
    private SerializedProperty _bossId;

    private void OnEnable()
    {
        _bossId = serializedObject.FindProperty("bossId");

        // Só gera automaticamente para objetos em cena e quando o ID está vazio.
        if (targets.Length == 1 && string.IsNullOrWhiteSpace(_bossId.stringValue) && IsSceneObject((BossInteractable)target))
        {
            serializedObject.Update();
            _bossId.stringValue = GenerateUniqueId((BossInteractable)target);
            serializedObject.ApplyModifiedProperties();
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (targets.Length != 1) return;
        var boss = (BossInteractable)target;
        if (!IsSceneObject(boss)) return;

        serializedObject.Update();
        EditorGUILayout.Space();

        if (string.IsNullOrWhiteSpace(_bossId.stringValue))
            EditorGUILayout.HelpBox("bossId vazio: o estado deste boss não será salvo.", MessageType.Error);
        else if (IsDuplicate(boss, _bossId.stringValue))
            EditorGUILayout.HelpBox("Existe outro boss com este mesmo ID. Gere um novo ou ajuste manualmente.", MessageType.Warning);

        if (GUILayout.Button("Gerar novo ID único"))
        {
            _bossId.stringValue = GenerateUniqueId(boss);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static bool IsSceneObject(BossInteractable boss)
    {
        return boss != null && boss.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(boss);
    }

    private static BossInteractable[] FindAllBosses()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<BossInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<BossInteractable>(true);
#endif
    }

    private static bool IsDuplicate(BossInteractable self, string id)
    {
        foreach (var other in FindAllBosses())
        {
            if (other != self && other.BossId == id)
                return true;
        }
        return false;
    }

    private static string GenerateUniqueId(BossInteractable self)
    {
        var used = new HashSet<string>();
        foreach (var other in FindAllBosses())
        {
            if (other != self && !string.IsNullOrWhiteSpace(other.BossId))
                used.Add(other.BossId);
        }

        string scene = Regex.Replace(self.gameObject.scene.name, @"[^A-Za-z0-9]+", "_").Trim('_');
        if (string.IsNullOrEmpty(scene)) scene = "scene";
        string prefix = $"{scene}_boss_";

        for (int i = 1; i < 100000; i++)
        {
            string candidate = prefix + i.ToString("000");
            if (!used.Contains(candidate))
                return candidate;
        }

        return prefix + System.Guid.NewGuid().ToString("N");
    }
}
#endif