#if UNITY_EDITOR
using Erumperem.Combat.Tokens;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Erumperem.Editor.Combat
{
    /// <summary>
    /// One-click setup for world-space token strip (horizontal) used by <see cref="CombatDiegeticTokenStripsBinder"/>.
    /// </summary>
    public static class DiegeticTokenStripPrefabMenu
    {
        private const string DefaultPrefabPath = "Assets/_Project/Prefabs/Combat/DiegeticTokenStripRoot.prefab";

        [MenuItem("Erumperem/Combat/Generate Diegetic Token Strip Prefab")]
        private static void GeneratePrefab()
        {
            var folder = System.IO.Path.GetDirectoryName(DefaultPrefabPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                var parts = folder.Split('/');
                var current = "Assets";
                for (var i = 1; i < parts.Length; i++)
                {
                    var next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }

                    current = next;
                }
            }

            var root = new GameObject("DiegeticTokenStripRoot");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(2f, 0.35f);
            rootRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var strip = new GameObject("TokensPanel");
            strip.transform.SetParent(root.transform, false);
            var stripRect = strip.AddComponent<RectTransform>();
            stripRect.anchorMin = Vector2.zero;
            stripRect.anchorMax = Vector2.one;
            stripRect.offsetMin = Vector2.zero;
            stripRect.offsetMax = Vector2.zero;
            var horizontalLayout = strip.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.spacing = 6f;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;

            var presenter = strip.AddComponent<DiegeticTokenStripPresenter>();

            var iconPrefab = new GameObject("TokenIconPrefab");
            var iconRect = iconPrefab.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(28f, 28f);
            var backgroundImage = iconPrefab.AddComponent<Image>();
            backgroundImage.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
            var foregroundGo = new GameObject("Icon");
            foregroundGo.transform.SetParent(iconPrefab.transform, false);
            var fgRect = foregroundGo.AddComponent<RectTransform>();
            fgRect.anchorMin = new Vector2(0.15f, 0.15f);
            fgRect.anchorMax = new Vector2(0.85f, 0.85f);
            fgRect.offsetMin = Vector2.zero;
            fgRect.offsetMax = Vector2.zero;
            var fgImage = foregroundGo.AddComponent<Image>();
            fgImage.color = Color.white;
            fgImage.raycastTarget = false;

            var countGo = new GameObject("TokenCount");
            countGo.transform.SetParent(iconPrefab.transform, false);
            var countRect = countGo.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.5f, 0f);
            countRect.anchorMax = new Vector2(1f, 0.35f);
            countRect.offsetMin = Vector2.zero;
            countRect.offsetMax = Vector2.zero;
            var tmp = countGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 10;
            tmp.alignment = TextAlignmentOptions.BottomRight;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            var slot = iconPrefab.AddComponent<DiegeticTokenIconSlot>();

            var serializedSlot = new SerializedObject(slot);
            serializedSlot.FindProperty("iconImage").objectReferenceValue = fgImage;
            serializedSlot.FindProperty("backgroundImage").objectReferenceValue = backgroundImage;
            serializedSlot.FindProperty("stackLabel").objectReferenceValue = tmp;
            serializedSlot.ApplyModifiedPropertiesWithoutUndo();

            iconPrefab.SetActive(false);
            var iconAssetPath = "Assets/_Project/Prefabs/Combat/DiegeticTokenIconSlot.prefab";
            var iconFolder = System.IO.Path.GetDirectoryName(iconAssetPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(iconFolder) && !AssetDatabase.IsValidFolder(iconFolder))
            {
                var parts = iconFolder.Split('/');
                var current = "Assets";
                for (var i = 1; i < parts.Length; i++)
                {
                    var next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }

                    current = next;
                }
            }

            Object.DestroyImmediate(iconPrefab);

            var slotPrefabAsset = AssetDatabase.LoadAssetAtPath<DiegeticTokenIconSlot>(iconAssetPath);
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.FindProperty("stripContentRoot").objectReferenceValue = stripRect;
            serializedPresenter.FindProperty("tokenIconSlotPrefab").objectReferenceValue = slotPrefabAsset;
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, DefaultPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(DefaultPrefabPath));
        }
    }
}
#endif
