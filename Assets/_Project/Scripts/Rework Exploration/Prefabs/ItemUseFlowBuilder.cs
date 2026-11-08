#if UNITY_EDITOR
using System.Collections.Generic;
using Erumperem.Characters;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Core.Inventory.UI.ItemUse.Authoring
{
    /// <summary>
    /// Builds the whole item-use UI so nothing is assembled by hand:
    ///  1) CharacterTargetButton.prefab  (background, icon, name, reason; wired into CharacterTargetButtonView)
    ///  2) ItemUseFlow.prefab            (controller + view + full panel hierarchy, every view field wired)
    ///  3) An instance of ItemUseFlow under a Canvas in the active scene
    ///  4) Fills `_useFlow` on every SelectedItemPanelView / TransferItemPanelView in the scene that has it empty
    ///
    /// Existing prefabs are never overwritten (delete one to rebuild it).
    /// If the scene already has an ItemUseFlowController, no second instance is created.
    /// Must live in an Editor folder.
    /// </summary>
    public static class ItemUseFlowBuilder
    {
        private const string Folder = "Assets/_Project/Scripts/Rework Exploration/Prefabs";
        private const string TargetButtonPath = Folder + "/CharacterTargetButton.prefab";
        private const string FlowPath = Folder + "/ItemUseFlow.prefab";

        private static readonly Color WindowColor = new(0.12f, 0.12f, 0.15f, 1f);
        private static readonly Color BlockerColor = new(0f, 0f, 0f, 0.6f);
        private static readonly Color ButtonColor = new(0.22f, 0.22f, 0.28f, 1f);
        private static readonly Color MutedText = new(0.7f, 0.7f, 0.75f, 1f);
        private static readonly Color ErrorText = new(1f, 0.55f, 0.55f, 1f);

        [MenuItem("Tools/Item Use Flow/Build Item Use Flow (prefabs + scene)")]
        public static void BuildAll()
        {
            EnsureFolders();

            var targetButtonPrefab = LoadOrCreate(TargetButtonPath, CreateTargetButton);
            var flowPrefab = LoadOrCreate(FlowPath, () => CreateFlow(targetButtonPrefab));

            var flow = PlaceInScene(flowPrefab);
            WirePanels(flow);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ── Prefab 1: character target button ────────────────────────

        private static GameObject CreateTargetButton()
        {
            var root = new GameObject("CharacterTargetButton", typeof(RectTransform), typeof(Image), typeof(Button));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 80f);

            var background = root.GetComponent<Image>();
            background.color = ButtonColor;

            SetHeight(root.GetComponent<RectTransform>(), 80f);

            var button = root.GetComponent<Button>();
            ConfigureButton(button, background);

            var iconRect = NewRect("Icon", root.transform);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(64f, 64f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);

            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false; // the view enables it when a sprite is bound

            var nameText = NewText("NameText", root.transform, string.Empty, 24f, Color.white, TextAlignmentOptions.MidlineLeft);
            nameText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameText.rectTransform.offsetMin = new Vector2(80f, 0f);
            nameText.rectTransform.offsetMax = new Vector2(-8f, -4f);
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            var reasonText = NewText("ReasonText", root.transform, string.Empty, 14f, ErrorText, TextAlignmentOptions.MidlineLeft);
            reasonText.rectTransform.anchorMin = new Vector2(0f, 0f);
            reasonText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            reasonText.rectTransform.offsetMin = new Vector2(80f, 4f);
            reasonText.rectTransform.offsetMax = new Vector2(-8f, 0f);
            reasonText.overflowMode = TextOverflowModes.Ellipsis;

            var view = root.AddComponent<CharacterTargetButtonView>();
            var so = new SerializedObject(view);
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_nameText").objectReferenceValue = nameText;
            so.FindProperty("_reasonText").objectReferenceValue = reasonText;
            so.FindProperty("_button").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ── Prefab 2: the whole flow ─────────────────────────────────

        private static GameObject CreateFlow(GameObject targetButtonPrefab)
        {
            // Always-active host: controller + view. The view toggles ItemUsePanel, never itself.
            var host = NewRect("ItemUseFlow", null);
            Stretch(host);
            var controller = host.gameObject.AddComponent<ItemUseFlowController>();
            var view = host.gameObject.AddComponent<ItemUseFlowView>();

            // Panel (hidden while Idle), on its own sorting layer so it sits above the inventory UI.
            var panel = NewRect("ItemUsePanel", host);
            Stretch(panel);
            var canvas = panel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            panel.gameObject.AddComponent<GraphicRaycaster>();

            // Blocker: swallows clicks so other slots can't be clicked while the flow is open.
            var blocker = NewRect("Blocker", panel);
            Stretch(blocker);
            var blockerImage = blocker.gameObject.AddComponent<Image>();
            blockerImage.color = BlockerColor;
            blockerImage.raycastTarget = true;

            // Window
            var window = NewRect("Window", panel);
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.pivot = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(560f, 640f);
            window.gameObject.AddComponent<Image>().color = WindowColor;
            AddVertical(window.gameObject, spacing: 12f, padding: 24);

            // Header: icon + (name / step)
            var header = NewRect("Header", window);
            SetHeight(header, 80f);
            var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 12f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            var iconRect = NewRect("ItemIcon", header);
            var itemIcon = iconRect.gameObject.AddComponent<Image>();
            itemIcon.preserveAspect = true;
            itemIcon.raycastTarget = false;
            itemIcon.enabled = false;
            SetSize(iconRect, 72f, 72f);

            var textColumn = NewRect("TextColumn", header);
            AddVertical(textColumn.gameObject, spacing: 2f, padding: 0);
            Flex(textColumn, flexibleWidth: 1f);

            var itemName = NewText("ItemName", textColumn, string.Empty, 28f, Color.white, TextAlignmentOptions.MidlineLeft);
            itemName.fontStyle = FontStyles.Bold;
            var stepLabel = NewText("StepLabel", textColumn, string.Empty, 20f, MutedText, TextAlignmentOptions.MidlineLeft);

            // Target panel (shown in SelectingTarget)
            var targetPanel = NewRect("TargetPanel", window);
            Flex(targetPanel, flexibleHeight: 1f, minHeight: 220f);

            var buttonContainer = NewRect("ButtonContainer", targetPanel);
            Stretch(buttonContainer);
            AddVertical(buttonContainer.gameObject, spacing: 8f, padding: 0);

            // Confirm panel (shown in AwaitingConfirmation)
            var confirmPanel = NewRect("ConfirmPanel", window);
            Flex(confirmPanel, flexibleHeight: 1f, minHeight: 220f);
            AddVertical(confirmPanel.gameObject, spacing: 12f, padding: 0);

            var confirmText = NewText("ConfirmText", confirmPanel, string.Empty, 26f, Color.white, TextAlignmentOptions.Center);
            Flex(confirmText.rectTransform, flexibleHeight: 1f, minHeight: 100f);
            var confirmButton = NewButton("ConfirmButton", confirmPanel, "Confirm", 56f);
            var changeTargetButton = NewButton("ChangeTargetButton", confirmPanel, "Change character", 56f);

            // Common
            var feedback = NewText("FeedbackText", window, string.Empty, 18f, ErrorText, TextAlignmentOptions.Center);
            Flex(feedback.rectTransform, minHeight: 40f);
            var cancelButton = NewButton("CancelButton", window, "Cancel", 56f);

            panel.gameObject.SetActive(false); // the view also hides it while Idle

            // Wire the view
            var so = new SerializedObject(view);
            so.FindProperty("_controller").objectReferenceValue = controller;
            so.FindProperty("_root").objectReferenceValue = panel.gameObject;
            so.FindProperty("_itemIcon").objectReferenceValue = itemIcon;
            so.FindProperty("_itemNameText").objectReferenceValue = itemName;
            so.FindProperty("_stepText").objectReferenceValue = stepLabel;
            so.FindProperty("_targetPanel").objectReferenceValue = targetPanel.gameObject;
            so.FindProperty("_targetButtonContainer").objectReferenceValue = buttonContainer;
            so.FindProperty("_targetButtonPrefab").objectReferenceValue = targetButtonPrefab.GetComponent<CharacterTargetButtonView>();
            so.FindProperty("_confirmPanel").objectReferenceValue = confirmPanel.gameObject;
            so.FindProperty("_confirmText").objectReferenceValue = confirmText;
            so.FindProperty("_confirmButton").objectReferenceValue = confirmButton;
            so.FindProperty("_changeTargetButton").objectReferenceValue = changeTargetButton;
            so.FindProperty("_cancelButton").objectReferenceValue = cancelButton;
            so.FindProperty("_feedbackText").objectReferenceValue = feedback;

            var allies = FindAllies();
            var alliesProp = so.FindProperty("_allies");
            alliesProp.arraySize = allies.Count;
            for (int i = 0; i < allies.Count; i++)
                alliesProp.GetArrayElementAtIndex(i).objectReferenceValue = allies[i];

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[ItemUseFlowBuilder] {allies.Count} AllyCharacterStatDefinition asset(s) added to the target list" +
                      (allies.Count == 0 ? " — fill `_allies` on ItemUseFlowView by hand." : "."));

            return host.gameObject;
        }

        private static List<AllyCharacterStatDefinition> FindAllies()
        {
            var result = new List<AllyCharacterStatDefinition>();

            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(AllyCharacterStatDefinition)}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<AllyCharacterStatDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) result.Add(asset);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        // ── Scene placement and wiring ───────────────────────────────

        private static ItemUseFlowController PlaceInScene(GameObject flowPrefab)
        {
            var existing = Object.FindObjectsOfType<ItemUseFlowController>(true);
            if (existing.Length > 0)
            {
                Debug.Log("[ItemUseFlowBuilder] The scene already has an ItemUseFlowController — no new instance created.");
                return existing[0];
            }

            var canvas = FindOrCreateCanvas();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(flowPrefab, canvas.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Create Item Use Flow");

            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log($"[ItemUseFlowBuilder] ItemUseFlow instantiated under '{canvas.name}'.");
            return instance.GetComponent<ItemUseFlowController>();
        }

        private static void WirePanels(ItemUseFlowController flow)
        {
            if (flow == null) return;

            foreach (var panel in Object.FindObjectsOfType<SelectedItemPanelView>(true))
                WirePanel(panel, flow);

            foreach (var panel in Object.FindObjectsOfType<TransferItemPanelView>(true))
                WirePanel(panel, flow);
        }

        private static void WirePanel(MonoBehaviour panel, ItemUseFlowController flow)
        {
            var so = new SerializedObject(panel);
            var prop = so.FindProperty("_useFlow");

            if (prop == null)
            {
                Debug.LogWarning($"[ItemUseFlowBuilder] '{panel.name}' ({panel.GetType().Name}) has no `_useFlow` field — update the script first.");
                return;
            }

            if (prop.objectReferenceValue != null) return; // keep whatever was set by hand

            prop.objectReferenceValue = flow;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);
            Debug.Log($"[ItemUseFlowBuilder] `_useFlow` set on '{panel.name}' ({panel.GetType().Name}).");
        }

        private static Canvas FindOrCreateCanvas()
        {
            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                var parent = selected.GetComponentInParent<Canvas>();
                if (parent != null) return parent.rootCanvas;
            }

            foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
                if (canvas.isRootCanvas) return canvas;

            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var created = go.GetComponent<Canvas>();
            created.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            Debug.LogWarning("[ItemUseFlowBuilder] No Canvas found — one was created. Make sure the scene has an EventSystem.");
            return created;
        }

        // ── UI helpers ───────────────────────────────────────────────

        private static GameObject LoadOrCreate(string path, System.Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                Debug.Log($"[ItemUseFlowBuilder] '{path}' already exists — kept as is.");
                return existing;
            }

            var temp = build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);

            Debug.Log($"[ItemUseFlowBuilder] Prefab created at '{path}'.");
            return prefab;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddVertical(GameObject go, float spacing, int padding)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static LayoutElement GetLayoutElement(RectTransform rect)
        {
            var element = rect.GetComponent<LayoutElement>();
            return element != null ? element : rect.gameObject.AddComponent<LayoutElement>();
        }

        private static void SetHeight(RectTransform rect, float height)
        {
            var element = GetLayoutElement(rect);
            element.minHeight = height;
            element.preferredHeight = height;
        }

        private static void SetSize(RectTransform rect, float width, float height)
        {
            var element = GetLayoutElement(rect);
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = height;
            element.preferredHeight = height;
        }

        private static void Flex(RectTransform rect, float flexibleWidth = -1f, float flexibleHeight = -1f, float minHeight = -1f)
        {
            var element = GetLayoutElement(rect);
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0f) element.flexibleHeight = flexibleHeight;
            if (minHeight >= 0f) element.minHeight = minHeight;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
        {
            var rect = NewRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button NewButton(string name, Transform parent, string label, float height)
        {
            var rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;

            var button = rect.gameObject.AddComponent<Button>();
            ConfigureButton(button, image);
            SetHeight(rect, height);

            var text = NewText("Label", rect, label, 24f, Color.white, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);

            return button;
        }

        private static void ConfigureButton(Button button, Graphic target)
        {
            button.targetGraphic = target;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            button.colors = colors;
        }

        private static void EnsureFolders()
        {
            string current = "Assets";

            foreach (string part in Folder.Substring("Assets/".Length).Split('/'))
            {
                string next = $"{current}/{part}";

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);

                current = next;
            }
        }
    }
}
#endif