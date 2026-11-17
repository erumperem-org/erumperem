
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using InteractionSystem;
using InteractionSystem.Concrete;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Liga conversas às interações dos NPCs apenas no Play Mode.
/// O catálogo fica em Assets/Resources/ErumperemDialogueBindings.asset.
/// Não edita cenas nem prefabs.
/// </summary>
public sealed class DialogueRuntimeInstaller : MonoBehaviour
{
    private sealed class Binding
    {
        public InteractableBase Npc;
        public UnityEvent Original;
        public UnityEvent Replacement;
        public UnityAction Listener;
        public DialogueBindingCatalog.Entry Entry;
    }

    private static readonly FieldInfo InteractionEventField = typeof(InteractableBase).GetField("onInteractionStarted", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Binding> bindings = new List<Binding>();
    private DialogueBindingCatalog catalog;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameObject go = new GameObject("DialogueRuntimeInstaller");
        DontDestroyOnLoad(go);
        go.AddComponent<DialogueRuntimeInstaller>();
    }

    private IEnumerator Start()
    {
        catalog = Resources.Load<DialogueBindingCatalog>("ErumperemDialogueBindings");

        if (catalog == null)
        {
            Debug.LogWarning("[DialogueRuntimeInstaller] Crie Assets/Resources/ErumperemDialogueBindings.asset.");
            yield break;
        }

        if (InteractionEventField == null)
        {
            Debug.LogError("[DialogueRuntimeInstaller] onInteractionStarted não encontrado no InteractableBase.");
            yield break;
        }

        while (true)
        {
            InteractableBase[] npcs = FindObjectsByType<InteractableBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (InteractableBase npc in npcs)
            {
                if (npc == null || !npc.gameObject.scene.IsValid() || !(npc is NpcInteractable || npc is PlayableNpcInteractable) || IsBound(npc))
                {
                    continue;
                }

                foreach (DialogueBindingCatalog.Entry entry in catalog.Entries)
                {
                    if (entry == null || entry.Conversation == null || string.IsNullOrWhiteSpace(entry.NpcObjectName))
                    {
                        continue;
                    }

                    string actualName = npc.name.Replace("(Clone)", "").Trim();

                    if (actualName != entry.NpcObjectName.Trim())
                    {
                        continue;
                    }

                    Install(npc, entry);
                    break;
                }
            }

            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    private bool IsBound(InteractableBase npc)
    {
        foreach (Binding binding in bindings)
        {
            if (binding.Npc == npc)
            {
                return true;
            }
        }

        return false;
    }

    private void Install(InteractableBase npc, DialogueBindingCatalog.Entry entry)
    {
        UnityEvent original = InteractionEventField.GetValue(npc) as UnityEvent;
        UnityEvent replacement = new UnityEvent();
        Binding binding = new Binding { Npc = npc, Original = original, Replacement = replacement, Entry = entry };
        binding.Listener = () => Play(binding);
        replacement.AddListener(binding.Listener);
        InteractionEventField.SetValue(npc, replacement);
        bindings.Add(binding);
    }

    private void Play(Binding binding)
    {
        DialogueManager manager = EnsureDialogueManager();

        if (manager == null || manager.IsDialogueActive)
        {
            return;
        }

        System.Action ended = null;
        ended = () =>
        {
            manager.DialogueEnded -= ended;

            if (binding.Npc != null && binding.Entry.RunOriginalInteractionAfterDialogue)
            {
                binding.Original?.Invoke();
            }
        };

        manager.DialogueEnded += ended;
        manager.StartDialogue(binding.Entry.Conversation);

        if (!manager.IsDialogueActive)
        {
            manager.DialogueEnded -= ended;
        }
    }

    private void OnDestroy()
    {
        foreach (Binding binding in bindings)
        {
            if (binding.Npc != null)
            {
                InteractionEventField?.SetValue(binding.Npc, binding.Original);
            }
        }

        bindings.Clear();
    }

    private static DialogueManager EnsureDialogueManager()
    {
        if (DialogueManager.Instance != null)
        {
            return DialogueManager.Instance;
        }

        DialogueManager existing = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);

        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject("DialogueRuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.SetActive(false);
        DontDestroyOnLoad(root);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.layer = 5;
        panel.SetActive(false);
        panel.transform.SetParent(root.transform, false);
        Stretch(panel.GetComponent<RectTransform>(), new Vector2(0.10f, 0.03f), new Vector2(0.90f, 0.38f));
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.97f);
        panel.AddComponent<KeyboardNavigablePanel>();

        TMP_Text speaker = CreateText(panel.transform, "Speaker", 32f, new Vector2(0.20f, 0.76f), new Vector2(0.94f, 0.95f));
        TMP_Text text = CreateText(panel.transform, "DialogueText", 25f, new Vector2(0.20f, 0.35f), new Vector2(0.94f, 0.75f));
        text.enableWordWrapping = true;

        GameObject portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitObject.layer = 5;
        portraitObject.transform.SetParent(panel.transform, false);
        Stretch(portraitObject.GetComponent<RectTransform>(), new Vector2(0.02f, 0.33f), new Vector2(0.18f, 0.90f));
        Image portrait = portraitObject.GetComponent<Image>();
        portrait.preserveAspect = true;

        GameObject choices = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
        choices.layer = 5;
        choices.transform.SetParent(panel.transform, false);
        Stretch(choices.GetComponent<RectTransform>(), new Vector2(0.20f, 0.04f), new Vector2(0.76f, 0.34f));
        VerticalLayoutGroup layout = choices.GetComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 5f;

        Button next = CreateButton(panel.transform, "BtnContinue", "CONTINUAR");
        Stretch(next.GetComponent<RectTransform>(), new Vector2(0.78f, 0.05f), new Vector2(0.98f, 0.28f));

        Button choiceTemplate = CreateButton(root.transform, "ChoiceTemplate", "ESCOLHA");
        RectTransform choiceRect = choiceTemplate.GetComponent<RectTransform>();
        choiceRect.anchorMin = Vector2.zero;
        choiceRect.anchorMax = Vector2.zero;
        choiceRect.anchoredPosition = new Vector2(-5000f, -5000f);
        choiceRect.sizeDelta = new Vector2(500f, 55f);
        choiceTemplate.gameObject.AddComponent<LayoutElement>().preferredHeight = 55f;

        GameObject uiObject = new GameObject("DialogueUI");
        uiObject.SetActive(false);
        uiObject.transform.SetParent(root.transform, false);
        DialogueUI ui = uiObject.AddComponent<DialogueUI>();
        SetPrivate(ui, "dialoguePanel", panel);
        SetPrivate(ui, "speakerNameText", speaker);
        SetPrivate(ui, "portraitImage", portrait);
        SetPrivate(ui, "dialogueText", text);
        SetPrivate(ui, "continueButton", next);
        SetPrivate(ui, "choicesContainer", choices.transform);
        SetPrivate(ui, "choiceButtonPrefab", choiceTemplate);

        DialogueManager manager = root.AddComponent<DialogueManager>();
        SetPrivate(manager, "dialogueUI", ui);
        root.SetActive(true);
        uiObject.SetActive(true);
        return manager;
    }

    private static void SetPrivate(object owner, string field, object value)
    {
        FieldInfo info = owner.GetType().GetField(field, PrivateInstance);

        if (info == null)
        {
            Debug.LogError("[DialogueRuntimeInstaller] Campo ausente: " + owner.GetType().Name + "." + field);
            return;
        }

        info.SetValue(owner, value);
    }

    private static TMP_Text CreateText(Transform parent, string name, float size, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>(), min, max);
        TextMeshProUGUI result = go.GetComponent<TextMeshProUGUI>();
        result.font = TMP_Settings.defaultFontAsset;
        result.fontSize = size;
        result.color = Color.white;
        result.raycastTarget = false;
        return result;
    }

    private static Button CreateButton(Transform parent, string name, string value)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.25f, 0.23f, 0.28f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.84f, 0.45f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        TMP_Text label = CreateText(go.transform, "Text (TMP)", 20f, Vector2.zero, Vector2.one);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
