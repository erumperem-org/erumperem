#if UNITY_EDITOR
using System;
using Erumperem.Progression;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Erumperem.Editor.Progression
{
    /// <summary>
    /// Duplicates the Wulfric skill-tree root for Buck (b_* node assets) and wires <see cref="SkillTreeView"/>.
    /// Run once after pulling: Erumperem/Setup SkillTreePanel (Wulfric + Buck).
    /// </summary>
    public static class SkillTreePanelMultiCharacterSetup
    {
        private const string SkillTreePanelPrefabPath = "Assets/_Project/Prefabs/UIPrefabs/SkillTreePanel.prefab";
        private const string KnightSkillTreePanelPrefabPath = "Assets/_Project/Prefabs/UIPrefabs/KnightSkillTreePanel.prefab";
        private const string SkillTreeNodesFolder = "Assets/_Project/Resources/SkillTreeNodes";

        private static readonly Color WulfricPanelBackground = new(0.17f, 0.21f, 0.23f, 0.996f);
        private static readonly Color BuckPanelBackground = new(0.25f, 0.19f, 0.18f, 0.996f);

        [MenuItem("Erumperem/Setup SkillTreePanel (Wulfric + Buck)")]
        public static void SetupSkillTreePanelForWulfricAndBuck()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(SkillTreePanelPrefabPath);
            try
            {
                var contentTransform = FindChildTransform(prefabRoot.transform, "Content");
                if (contentTransform == null)
                {
                    Debug.LogError("SkillTreePanelMultiCharacterSetup: Content not found.");
                    return;
                }

                var wulfricRootTransform = FindChildTransform(contentTransform, "SkillTree_Wulfric")
                    ?? FindChildTransform(contentTransform, "SkillTree");
                if (wulfricRootTransform == null)
                {
                    Debug.LogError("SkillTreePanelMultiCharacterSetup: SkillTree / SkillTree_Wulfric not found.");
                    return;
                }

                wulfricRootTransform.name = "SkillTree_Wulfric";

                var buckRootTransform = FindChildTransform(contentTransform, "SkillTree_Buck");
                if (buckRootTransform == null)
                {
                    var buckRootObject = UnityEngine.Object.Instantiate(
                        wulfricRootTransform.gameObject,
                        contentTransform);
                    buckRootTransform = buckRootObject.transform;
                    buckRootTransform.name = "SkillTree_Buck";
                    RemapPresentersToBuckNodeAssets(buckRootTransform);
                }

                wulfricRootTransform.gameObject.SetActive(true);
                buckRootTransform.gameObject.SetActive(false);

                var skillTreeView = prefabRoot.GetComponent<SkillTreeView>();
                if (skillTreeView == null)
                {
                    Debug.LogError("SkillTreePanelMultiCharacterSetup: SkillTreeView missing on prefab root.");
                    return;
                }

                var wulfricPortraitSprite = LoadPortraitSpriteFromPrefab(KnightSkillTreePanelPrefabPath);
                var buckPortraitSprite = FindChildComponent<Image>(prefabRoot.transform, "Portrait")?.sprite;

                WireSkillTreeView(
                    skillTreeView,
                    prefabRoot.transform,
                    wulfricRootTransform.gameObject,
                    buckRootTransform.gameObject,
                    wulfricPortraitSprite,
                    buckPortraitSprite);

                WireResetButton(prefabRoot, skillTreeView);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, SkillTreePanelPrefabPath);
                Debug.Log(
                    "SkillTreePanelMultiCharacterSetup: SkillTreePanel updated with SkillTree_Wulfric + SkillTree_Buck.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void WireSkillTreeView(
            SkillTreeView skillTreeView,
            Transform prefabRootTransform,
            GameObject wulfricSkillTreeRoot,
            GameObject buckSkillTreeRoot,
            Sprite wulfricPortraitSprite,
            Sprite buckPortraitSprite)
        {
            var skillTreeViewSerializedObject = new SerializedObject(skillTreeView);

            skillTreeViewSerializedObject.FindProperty("_arrowLeftButton").objectReferenceValue =
                FindChildComponent<Button>(prefabRootTransform, "ArrowLeft");
            skillTreeViewSerializedObject.FindProperty("_arrowRightButton").objectReferenceValue =
                FindChildComponent<Button>(prefabRootTransform, "ArrowRight");
            skillTreeViewSerializedObject.FindProperty("_resetSkillsButton").objectReferenceValue =
                FindChildComponent<Button>(prefabRootTransform, "ResetSkills");
            skillTreeViewSerializedObject.FindProperty("_skillTreeTitleText").objectReferenceValue =
                FindChildComponent<TMP_Text>(prefabRootTransform, "SkillTreeTitle");
            skillTreeViewSerializedObject.FindProperty("_portraitImage").objectReferenceValue =
                FindChildComponent<Image>(prefabRootTransform, "Portrait");
            skillTreeViewSerializedObject.FindProperty("_panelBackgroundImage").objectReferenceValue =
                prefabRootTransform.GetComponent<Image>();
            skillTreeViewSerializedObject.FindProperty("_levelTextValue").objectReferenceValue =
                FindChildComponent<TMP_Text>(prefabRootTransform, "LevelTextValue");

            var detailPanelProperty = skillTreeViewSerializedObject.FindProperty("_detailPanel");
            detailPanelProperty.FindPropertyRelative("Title").objectReferenceValue =
                FindChildComponent<TMP_Text>(prefabRootTransform, "SkillDescription")?.transform
                    .GetComponentInChildren<TMP_Text>(true);
            detailPanelProperty.FindPropertyRelative("Body").objectReferenceValue =
                FindChildTransform(prefabRootTransform, "SkillDescription")
                    ?.GetComponentInChildren<TMP_Text>(true);

            var profilesProperty = skillTreeViewSerializedObject.FindProperty("_characterProfiles");
            profilesProperty.arraySize = 2;

            WriteCharacterProfile(
                profilesProperty.GetArrayElementAtIndex(0),
                "wulfric",
                "Splintered Knight",
                wulfricPortraitSprite,
                WulfricPanelBackground,
                wulfricSkillTreeRoot);
            WriteCharacterProfile(
                profilesProperty.GetArrayElementAtIndex(1),
                "buck",
                "El Pistolero",
                buckPortraitSprite,
                BuckPanelBackground,
                buckSkillTreeRoot);

            skillTreeViewSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteCharacterProfile(
            SerializedProperty profileProperty,
            string progressionCharacterId,
            string skillTreeTitle,
            Sprite portraitSprite,
            Color panelBackgroundColor,
            GameObject skillTreeRoot)
        {
            profileProperty.FindPropertyRelative("ProgressionCharacterId").stringValue = progressionCharacterId;
            profileProperty.FindPropertyRelative("SkillTreeTitle").stringValue = skillTreeTitle;
            profileProperty.FindPropertyRelative("PortraitSprite").objectReferenceValue = portraitSprite;
            profileProperty.FindPropertyRelative("PanelBackgroundColor").colorValue = panelBackgroundColor;
            profileProperty.FindPropertyRelative("SkillTreeRoot").objectReferenceValue = skillTreeRoot;
        }

        private static void WireResetButton(GameObject prefabRoot, SkillTreeView skillTreeView)
        {
            var resetButton = prefabRoot.GetComponentInChildren<PlayerProgressionResetButton>(true);
            if (resetButton == null)
            {
                return;
            }

            var resetButtonSerializedObject = new SerializedObject(resetButton);
            resetButtonSerializedObject.FindProperty("_skillTreeView").objectReferenceValue = skillTreeView;
            resetButtonSerializedObject.FindProperty("_resetEntireFile").boolValue = false;
            resetButtonSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemapPresentersToBuckNodeAssets(Transform buckSkillTreeRoot)
        {
            foreach (var presenter in buckSkillTreeRoot.GetComponentsInChildren<SkillTreeNodePresenter>(true))
            {
                var presenterSerializedObject = new SerializedObject(presenter);
                var nodeAssetProperty = presenterSerializedObject.FindProperty("_nodeAsset");
                var wulfricNodeAsset = nodeAssetProperty.objectReferenceValue as SkillTreeNodeAsset;
                if (wulfricNodeAsset == null || string.IsNullOrWhiteSpace(wulfricNodeAsset.NodeId))
                {
                    continue;
                }

                var buckNodeId = wulfricNodeAsset.NodeId.StartsWith("b_", StringComparison.Ordinal)
                    ? wulfricNodeAsset.NodeId
                    : $"b_{wulfricNodeAsset.NodeId}";

                var buckNodeAssetPath = $"{SkillTreeNodesFolder}/{buckNodeId}.asset";
                var buckNodeAsset = AssetDatabase.LoadAssetAtPath<SkillTreeNodeAsset>(buckNodeAssetPath);
                if (buckNodeAsset == null)
                {
                    Debug.LogWarning(
                        $"SkillTreePanelMultiCharacterSetup: missing Buck node asset at {buckNodeAssetPath}.");
                    continue;
                }

                nodeAssetProperty.objectReferenceValue = buckNodeAsset;
                presenterSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Sprite LoadPortraitSpriteFromPrefab(string prefabPath)
        {
            var knightPrefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                return FindChildComponent<Image>(knightPrefabRoot.transform, "Portrait")?.sprite;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(knightPrefabRoot);
            }
        }

        private static Transform FindChildTransform(Transform parent, string childName)
        {
            foreach (var childTransform in parent.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(childTransform.name, childName, StringComparison.Ordinal))
                {
                    return childTransform;
                }
            }

            return null;
        }

        private static T FindChildComponent<T>(Transform parent, string childName) where T : Component
        {
            var childTransform = FindChildTransform(parent, childName);
            return childTransform != null ? childTransform.GetComponent<T>() : null;
        }
    }
}
#endif
