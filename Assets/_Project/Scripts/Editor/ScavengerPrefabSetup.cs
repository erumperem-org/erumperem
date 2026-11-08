using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ScavengerPrefabSetup
{
    private const string ScenePath = "Assets/__Scenes/Overworld.unity";
    private const string PrefabPath = "Assets/_Project/Prefabs/Scavenger.prefab";

    [InitializeOnLoadMethod]
    private static void ScheduleSetup()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    private static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        CreatePrefab();
    }

    [MenuItem("Tools/Overworld/Create Scavenger Prefab")]
    public static void CreatePrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded)
        {
            var scavenger = FindScavenger(scene);
            if (scavenger != null) SavePrefab(scavenger, true);
            return;
        }

        var preview = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var scavenger = FindScavenger(preview);
            if (scavenger != null) SavePrefab(scavenger, false);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static GameObject FindScavenger(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var shop in root.GetComponentsInChildren<ScavengerShop>(true))
                if (shop.name == "Scavenger") return shop.gameObject;
        }
        return null;
    }

    private static void SavePrefab(GameObject root, bool connect)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(root)) return;
        var outline = root.GetComponent<InteractionOutline>();
        if (outline == null) return;

        var references = new List<(Component Component, string Path, Object Value)>();
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var value = property.objectReferenceValue;
                var target = value is Component targetComponent ? targetComponent.transform
                    : value is GameObject targetObject ? targetObject.transform : null;
                if (target == null || target == root.transform || target.IsChildOf(root.transform)) continue;
                if (EditorUtility.IsPersistent(value)) continue;
                references.Add((component, property.propertyPath, value));
            }
        }

        var outlineData = new SerializedObject(outline);
        outlineData.FindProperty("_outlineWidth").floatValue = 0.0005f;
        outlineData.ApplyModifiedProperties();

        if (connect)
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        else
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

        if (!connect) return;
        foreach (var reference in references)
        {
            if (reference.Component == null) continue;
            var serialized = new SerializedObject(reference.Component);
            var property = serialized.FindProperty(reference.Path);
            if (property == null) continue;
            property.objectReferenceValue = reference.Value;
            serialized.ApplyModifiedProperties();
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
    }
}
