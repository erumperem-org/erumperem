
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingsBindingsRuntime : MonoBehaviour
{
    private const string InstallerName = "RuntimeSettingsBindingsInstaller";
    private const string SettingsName = "SettingsPanel";
    private const string LauncherName = "KeyBindingsOption";
    private const float KeyButtonWidth = 88f;
    private const float KeyButtonHeight = 44f;

    private Button buttonStyle;
    private TMP_Text buttonTextStyle;
    private TMP_Text optionTextStyle;
    private TMP_Text headingTextStyle;
    private Image paperStyle;
    private GameObject overlay;
    private RectTransform rowsContent;
    private ScrollRect rowsScroll;
    private TMP_Text statusText;
    private bool installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallScanner()
    {
        if (FindFirstObjectByType<SettingsBindingsRuntime>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject installer = new GameObject(InstallerName);
        DontDestroyOnLoad(installer);
        installer.AddComponent<SettingsBindingsRuntime>();
    }

    private void Awake()
    {
        if (gameObject.name == InstallerName)
        {
            StartCoroutine(FindSettingsPanels());
            return;
        }

        InstallInSettings();
    }

    private void OnEnable()
    {
        if (gameObject.name != InstallerName && !installed)
        {
            InstallInSettings();
        }
    }

    private void OnDisable()
    {
        if (gameObject.name != InstallerName)
        {
            InputBindingsService.Cancel();
        }
    }

    private IEnumerator FindSettingsPanels()
    {
        WaitForSecondsRealtime delay = new WaitForSecondsRealtime(0.5f);

        while (true)
        {
            Panel[] panels = FindObjectsByType<Panel>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (Panel panel in panels)
            {
                if (panel != null && panel.gameObject.scene.IsValid() && panel.gameObject.name == SettingsName && panel.GetComponent<SettingsBindingsRuntime>() == null)
                {
                    panel.gameObject.AddComponent<SettingsBindingsRuntime>();
                }
            }

            yield return delay;
        }
    }

    private void InstallInSettings()
    {
        if (installed)
        {
            return;
        }

        Transform content = transform.Find("PaperBG/ViewPanel/GameplayPanel/Viewport/Content");
        Transform styleButton = transform.Find("ButtonsSettings/BtnGameplay");
        Transform paper = transform.Find("PaperBG");
        Transform optionTitle = transform.Find("PaperBG/ViewPanel/GameplayPanel/Viewport/Content/UI/Title");
        Transform heading = transform.Find("PaperBG/ViewPanel/GameplayPanel/Viewport/Content/GameTxt");

        buttonStyle = styleButton != null ? styleButton.GetComponent<Button>() : null;
        buttonTextStyle = buttonStyle != null ? buttonStyle.GetComponentInChildren<TMP_Text>(true) : null;
        optionTextStyle = optionTitle != null ? optionTitle.GetComponent<TMP_Text>() : null;
        headingTextStyle = heading != null ? heading.GetComponent<TMP_Text>() : null;
        paperStyle = paper != null ? paper.GetComponent<Image>() : null;

        if (content == null || buttonStyle == null || buttonTextStyle == null || optionTextStyle == null || headingTextStyle == null || paperStyle == null)
        {
            Debug.LogWarning("[SettingsBindingsRuntime] Original Settings references not available.", this);
            return;
        }

        if (content.Find(LauncherName) != null)
        {
            installed = true;
            return;
        }

        CreateLauncher(content);
        installed = true;
    }

    private void CreateLauncher(Transform content)
    {
        GameObject row = new GameObject(LauncherName, typeof(RectTransform), typeof(LayoutElement));
        row.layer = 5;
        row.transform.SetParent(content, false);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, 122f);

        LayoutElement rowElement = row.GetComponent<LayoutElement>();
        rowElement.minHeight = 122f;
        rowElement.preferredHeight = 122f;
        rowElement.flexibleWidth = 1f;

        TMP_Text label = CreateText(row.transform, "Label", "Key Bindings", optionTextStyle, 35f, TextAlignmentOptions.MidlineLeft);
        Stretch(label.rectTransform, new Vector2(0.04f, 0.06f), new Vector2(0.55f, 0.94f));
        EnableAutosize(label, 21f, 35f);

        Button configure = CreateButton(row.transform, "BtnConfigureKeyBindings", "CONFIGURE", 26f);
        Stretch(configure.GetComponent<RectTransform>(), new Vector2(0.57f, 0.12f), new Vector2(0.97f, 0.88f));
        EnableAutosize(configure.GetComponentInChildren<TMP_Text>(), 15f, 26f);
        configure.onClick.AddListener(Open);

        row.transform.SetSiblingIndex(Mathf.Min(1, content.childCount - 1));

        RectTransform contentRect = content as RectTransform;

        if (contentRect != null && content.GetComponent<ContentSizeFitter>() == null)
        {
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 0f;
            contentRect.sizeDelta += new Vector2(0f, 122f + spacing);
        }
    }

    private void Open()
    {
        if (overlay == null)
        {
            BuildOverlay();
        }

        RebuildRows();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();

        Canvas.ForceUpdateCanvases();
        rowsScroll.verticalNormalizedPosition = 1f;
    }

    private void Close()
    {
        InputBindingsService.Cancel();

        if (overlay != null)
        {
            overlay.SetActive(false);
        }
    }

    private void BuildOverlay()
    {
        overlay = new GameObject("KeyBindingsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.layer = 5;
        overlay.SetActive(false);
        overlay.transform.SetParent(transform, false);

        Stretch(overlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.392f);
        overlay.AddComponent<KeyboardNavigablePanel>();

        GameObject paper = new GameObject("PaperBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        paper.layer = 5;
        paper.transform.SetParent(overlay.transform, false);

        Stretch(paper.GetComponent<RectTransform>(), new Vector2(0.14f, 0.02f), new Vector2(0.86f, 0.98f));
        CopyImageStyle(paperStyle, paper.GetComponent<Image>());

        TMP_Text title = CreateText(paper.transform, "Title", "KEY BINDINGS", headingTextStyle, 40f, TextAlignmentOptions.Center);
        Stretch(title.rectTransform, new Vector2(0.09f, 0.86f), new Vector2(0.91f, 0.95f));

        statusText = CreateText(paper.transform, "Status", "", optionTextStyle, 19f, TextAlignmentOptions.Center);
        Stretch(statusText.rectTransform, new Vector2(0.09f, 0.78f), new Vector2(0.91f, 0.85f));
        EnableAutosize(statusText, 14f, 19f);

        GameObject scrollObject = new GameObject("BindingsScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollObject.layer = 5;
        scrollObject.transform.SetParent(paper.transform, false);

        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(70f, 108f);
        scrollRect.offsetMax = new Vector2(-70f, -130f);

        rowsScroll = scrollObject.GetComponent<ScrollRect>();

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        viewport.layer = 5;
        viewport.transform.SetParent(scrollObject.transform, false);

        Stretch(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0f);
        viewportImage.raycastTarget = true;

        GameObject list = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        list.layer = 5;
        list.transform.SetParent(viewport.transform, false);

        rowsContent = list.GetComponent<RectTransform>();
        rowsContent.anchorMin = new Vector2(0f, 1f);
        rowsContent.anchorMax = new Vector2(1f, 1f);
        rowsContent.pivot = new Vector2(0.5f, 1f);
        rowsContent.anchoredPosition = Vector2.zero;
        rowsContent.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = list.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 4, 4);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        rowsScroll.viewport = viewport.GetComponent<RectTransform>();
        rowsScroll.content = rowsContent;
        rowsScroll.vertical = true;
        rowsScroll.horizontal = false;
        rowsScroll.movementType = ScrollRect.MovementType.Clamped;
        rowsScroll.scrollSensitivity = 35f;

        Button restore = CreateButton(paper.transform, "BtnRestoreDefaults", "RESTORE DEFAULTS", 18f);
        Stretch(restore.GetComponent<RectTransform>(), new Vector2(0.22f, 0.05f), new Vector2(0.45f, 0.14f));
        EnableAutosize(restore.GetComponentInChildren<TMP_Text>(), 11f, 17f);

        restore.onClick.AddListener(() =>
        {
            InputBindingsService.RestoreDefaults();
            RebuildRows();
            statusText.text = "Default bindings restored.";
        });

        Button back = CreateButton(paper.transform, "BtnBack", "BACK", 20f);
        Stretch(back.GetComponent<RectTransform>(), new Vector2(0.55f, 0.05f), new Vector2(0.78f, 0.14f));
        EnableAutosize(back.GetComponentInChildren<TMP_Text>(), 14f, 20f);
        back.onClick.AddListener(Close);
    }

    private void RebuildRows()
    {
        if (rowsContent == null)
        {
            return;
        }

        for (int index = rowsContent.childCount - 1; index >= 0; index--)
        {
            GameObject oldRow = rowsContent.GetChild(index).gameObject;
            oldRow.SetActive(false);
            Destroy(oldRow);
        }

        int count = 0;

        foreach (InputActionMap map in InputBindingsService.RegisteredMaps)
        {
            if (map == null)
            {
                continue;
            }

            foreach (InputAction action in map.actions)
            {
                if (action.name.IndexOf("Cheat", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                for (int index = 0; index < action.bindings.Count; index++)
                {
                    InputBinding binding = action.bindings[index];

                    if (binding.isComposite || !InputBindingsService.IsKeyboardBinding(binding))
                    {
                        continue;
                    }

                    CreateBindingRow(map, action, index);
                    count++;
                }
            }
        }

        statusText.text = count > 0 ? "Select a binding to change it. Press Esc to cancel." : "No keyboard bindings are currently available.";
        Canvas.ForceUpdateCanvases();
        rowsScroll.verticalNormalizedPosition = 1f;
    }

    private void CreateBindingRow(InputActionMap map, InputAction action, int index)
    {
        GameObject row = new GameObject("Binding_" + action.name + "_" + index, typeof(RectTransform), typeof(LayoutElement));
        row.layer = 5;
        row.transform.SetParent(rowsContent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 58f;

        TMP_Text label = CreateText(row.transform, "Action", GetActionLabel(map, action, index), optionTextStyle, 20f, TextAlignmentOptions.MidlineLeft);

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 3f);
        labelRect.offsetMax = new Vector2(-(KeyButtonWidth + 18f), -3f);
        EnableAutosize(label, 12f, 20f);

        Button keyButton = CreateButton(row.transform, "KeyButton", action.GetBindingDisplayString(index), 18f);

        RectTransform keyRect = keyButton.GetComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(1f, 0.5f);
        keyRect.anchorMax = new Vector2(1f, 0.5f);
        keyRect.pivot = new Vector2(1f, 0.5f);
        keyRect.sizeDelta = new Vector2(KeyButtonWidth, KeyButtonHeight);
        keyRect.anchoredPosition = new Vector2(-6f, 0f);
        EnableAutosize(keyButton.GetComponentInChildren<TMP_Text>(), 12f, 18f);

        InputAction selectedAction = action;
        int selectedIndex = index;

        keyButton.onClick.AddListener(() => BeginRebinding(selectedAction, selectedIndex));
    }

    private void BeginRebinding(InputAction action, int index)
    {
        statusText.text = "Press a new key (Esc to cancel).";

        bool started = InputBindingsService.Begin(action, index, success =>
        {
            RebuildRows();
            statusText.text = success ? "Binding updated and saved." : "Binding change cancelled.";
        });

        if (!started)
        {
            statusText.text = "This binding cannot be changed.";
        }
    }

    private static string GetActionLabel(InputActionMap map, InputAction action, int index)
    {
        string section = map.name == "GlobalUiKeyboard" ? "Menu" : map.name == "Player" ? "Exploration" : map.name;
        string name = section + " / " + SplitName(action.name);

        if (!string.IsNullOrWhiteSpace(action.bindings[index].name))
        {
            name += " / " + SplitName(action.bindings[index].name);
        }

        return name;
    }

    private Button CreateButton(Transform parent, string objectName, string value, float fontSize)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);

        Image image = obj.GetComponent<Image>();
        CopyImageStyle(buttonStyle.GetComponent<Image>(), image);

        Button button = obj.GetComponent<Button>();
        button.transition = buttonStyle.transition;
        button.colors = buttonStyle.colors;
        button.spriteState = buttonStyle.spriteState;
        button.targetGraphic = image;

        TMP_Text label = CreateText(obj.transform, "Text (TMP)", value, buttonTextStyle, fontSize, TextAlignmentOptions.Center);

        Stretch(label.rectTransform, Vector2.zero, Vector2.one);
        label.rectTransform.offsetMin = new Vector2(6f, 2f);
        label.rectTransform.offsetMax = new Vector2(-6f, -2f);

        return button;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, TMP_Text style, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);

        TMP_Text label = obj.GetComponent<TextMeshProUGUI>();
        label.font = style.font;
        label.fontSharedMaterial = style.fontSharedMaterial;
        label.fontStyle = style.fontStyle;
        label.fontSize = fontSize;
        label.color = style.color;
        label.alignment = alignment;
        label.text = value;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;

        return label;
    }

    private static void EnableAutosize(TMP_Text text, float minimum, float maximum)
    {
        if (text == null)
        {
            return;
        }

        text.enableAutoSizing = true;
        text.fontSizeMin = minimum;
        text.fontSizeMax = maximum;
    }

    private static void CopyImageStyle(Image source, Image target)
    {
        target.sprite = source.sprite;
        target.type = source.type;
        target.color = source.color;
        target.material = source.material;
        target.preserveAspect = source.preserveAspect;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
    }

    private static string SplitName(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? name : System.Text.RegularExpressions.Regex.Replace(name, "([a-z0-9])([A-Z])", "$1 $2");
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
