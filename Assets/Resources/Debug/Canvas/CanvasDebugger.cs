using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Services.DebugUtilities
{
    // ══════════════════════════════════════════════════════════════════════════
    //  CanvasLoggerService  —  static API, mirrors LoggerService for on-screen
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// On-screen canvas logger. Accepts one or more <see cref="LogCategory"/> tags.
    /// Also forwards every message to the Unity console.
    /// </summary>
    public static class CanvasLoggerService
    {
        private static CanvasDebugger _ui;
        private static readonly StringBuilder _sb = new(256);

        internal static void Initialize(CanvasDebugger ui) => _ui = ui;

        // ── Single category ────────────────────────────────────────────

        public static void PrintLogMessage(LogLevel level, LogCategory category, string message)
            => PrintLogMessage(level, message, category);

        public static void PrintLogMessage(LogLevel level, LogCategory category, bool success, string message)
            => PrintLogMessage(level, success, message, category);

        // ── Multiple categories (params) ───────────────────────────────

        public static void PrintLogMessage(LogLevel level, string message, params LogCategory[] categories)
        {
            string formatted = Build(level, message, null, categories);
            Dispatch(level, formatted);
            _ui?.ShowMessage(formatted, level);
        }

        public static void PrintLogMessage(LogLevel level, bool success, string message, params LogCategory[] categories)
        {
            string formatted = Build(level, message, success, categories);
            Dispatch(level, formatted);
            _ui?.ShowMessage(formatted, level);
        }

        // ── Helpers ────────────────────────────────────────────────────

        private static string Build(LogLevel level, string message, bool? success, LogCategory[] categories)
        {
            _sb.Clear();

            // Level badge
            Color levelColor = level switch
            {
                LogLevel.Warning => new Color(1f, 0.85f, 0.1f),
                LogLevel.Error   => new Color(1f, 0.3f,  0.3f),
                _                => new Color(0.7f, 0.7f, 0.7f)
            };
            string levelHex = ColorUtility.ToHtmlStringRGB(levelColor);
            _sb.Append($"<color=#{levelHex}>[{level.ToString().ToUpper()}]</color> ");

            // Category badges
            if (categories != null)
                foreach (var cat in categories)
                {
                    string hex = ColorUtility.ToHtmlStringRGB(cat.Color);
                    _sb.Append($"<color=#{hex}>[{cat.Name.ToUpper()}]</color> ");
                }

            // Success badge
            if (success.HasValue)
            {
                Color  sc  = success.Value ? Color.green : Color.red;
                string sh  = ColorUtility.ToHtmlStringRGB(sc);
                string sl  = success.Value ? "[SUCCESSFUL]" : "[FAILURE]";
                _sb.Append($"<color=#{sh}>{sl}</color> ");
            }

            _sb.Append(message);
            return _sb.ToString();
        }

        private static void Dispatch(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Warning: UnityEngine.Debug.LogWarning(message); break;
                case LogLevel.Error:   UnityEngine.Debug.LogError(message);   break;
                default:               UnityEngine.Debug.Log(message);        break;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CanvasDebugger  —  MonoBehaviour that drives the on-screen panel
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attach to a Canvas GameObject. Displays scrollable, color-coded,
    /// auto-fading debug messages on screen.
    ///
    /// Setup:
    ///   • <see cref="container"/>      — a VerticalLayoutGroup RectTransform (messages stack here)
    ///   • <see cref="messagePrefab"/>  — a prefab with an Image + TMP_Text child named "Message text"
    ///   • <see cref="scrollRect"/>     — (optional) ScrollRect wrapping the container; enables auto-scroll
    /// </summary>
    public class CanvasDebugger : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("VerticalLayoutGroup rect where message rows are instantiated.")]
        public RectTransform container;

        [Tooltip("Prefab: root Image + child TMP_Text ('Message text').")]
        public GameObject messagePrefab;

        [Tooltip("(Optional) ScrollRect parent of the container. Enables auto-scroll to latest.")]
        public ScrollRect scrollRect;

        [Header("Config")]
        [Tooltip("Maximum messages visible simultaneously before the oldest is recycled.")]
        public int maxMessages = 8;

        [Tooltip("Seconds a message stays at full opacity before fading.")]
        public float messageLifetime = 4f;

        [Tooltip("Seconds the fade-out animation takes.")]
        public float fadeDuration = 0.6f;

        [Tooltip("Font size for message text.")]
        public int fontSize = 14;

        // ── Background tints per log level ─────────────────────────────
        [Header("Background tints (per level)")]
        public Color bgDebug   = new(0.08f, 0.08f, 0.10f, 0.82f);
        public Color bgWarning = new(0.20f, 0.14f, 0.02f, 0.88f);
        public Color bgError   = new(0.22f, 0.04f, 0.04f, 0.90f);

        // ── Runtime state ──────────────────────────────────────────────

        // Reusable pool — avoids Instantiate/Destroy churn at runtime.
        private readonly Queue<MessageEntry> _pool   = new();
        private readonly List<MessageEntry>  _active = new();

        private struct MessageEntry
        {
            public GameObject Root;
            public Image      Background;
            public TMP_Text   Text;
            public Coroutine  Lifetime;
        }

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            CanvasLoggerService.Initialize(this);
        }

        // ── Public API (called by CanvasLoggerService) ─────────────────

        internal void ShowMessage(string richText, LogLevel level)
        {
            // Recycle oldest if at capacity.
            if (_active.Count >= maxMessages)
                Recycle(_active[0]);

            MessageEntry entry = AcquireEntry();

            // Background tint by level.
            entry.Background.color = level switch
            {
                LogLevel.Warning => bgWarning,
                LogLevel.Error   => bgError,
                _                => bgDebug
            };

            // Text — rich text ON, no forced color override (categories paint via tags).
            entry.Text.richText     = true;
            entry.Text.text         = richText;
            entry.Text.fontSize     = fontSize;
            entry.Text.color        = Color.white;          // base; tags override per token
            entry.Text.enableAutoSizing      = false;
            entry.Text.textWrappingMode      = TextWrappingModes.Normal;
            entry.Text.overflowMode          = TextOverflowModes.Overflow;
            entry.Text.textWrappingMode    =  TextWrappingModes.Normal;

            // Make visible, move to bottom of layout.
            entry.Root.SetActive(true);
            entry.Root.transform.SetAsLastSibling();
            SetAlpha(entry, 1f);

            _active.Add(entry);

            // Auto-scroll to latest.
            if (scrollRect != null)
                Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;

            // Start lifetime coroutine.
            entry.Lifetime = StartCoroutine(LifetimeRoutine(entry));
        }

        // ── Pool helpers ───────────────────────────────────────────────

        private MessageEntry AcquireEntry()
        {
            if (_pool.Count > 0)
            {
                var e = _pool.Dequeue();
                e.Root.SetActive(true);
                e.Lifetime = null;
                return e;
            }

            // Instantiate new entry.
            GameObject root = Instantiate(messagePrefab, container);
            var entry = new MessageEntry
            {
                Root       = root,
                Background = root.GetComponent<Image>()
                          ?? root.GetComponentInChildren<Image>(),
                Text       = root.GetComponentInChildren<TMP_Text>()
            };
            return entry;
        }

        private void Recycle(MessageEntry entry)
        {
            if (entry.Lifetime != null)
                StopCoroutine(entry.Lifetime);

            _active.Remove(entry);
            entry.Root.SetActive(false);
            _pool.Enqueue(entry);
        }

        // ── Fade / lifetime ────────────────────────────────────────────

        private IEnumerator LifetimeRoutine(MessageEntry entry)
        {
            yield return new WaitForSeconds(messageLifetime);

            // Fade out.
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(entry, Mathf.Lerp(1f, 0f, elapsed / fadeDuration));
                yield return null;
            }

            Recycle(entry);
        }

        private static void SetAlpha(MessageEntry entry, float alpha)
        {
            if (entry.Background != null)
            {
                Color c = entry.Background.color;
                c.a = alpha * entry.Background.color.a;     // preserve configured alpha
                // simpler: just tint the root CanvasGroup if available
            }
            // Use CanvasGroup for a clean single-alpha on the whole row.
            var cg = entry.Root.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = alpha;
        }
    }
}
