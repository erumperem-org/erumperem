using System;
using Core.Exploration.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI.LootFeed
{
    public enum LootFeedEventKind
    {
        Inserted,
        Removed,
        Rejected
    }

    /// <summary>
    /// Um pop-up de loot (um prefab por evento). Ciclo de vida próprio:
    /// atraso inicial → entrada (expande + fade + pop) → permanência →
    /// saída (fade) → colapso da altura → devolve ao pool.
    /// Usa tempo não escalado.
    ///
    /// Raiz do prefab: CanvasGroup + LayoutElement (preferredHeight = altura da linha)
    /// + este componente. Filhos esperados (auto-bind por nome):
    /// Icon, Name, Quantity, Caption, AccentBar, Background.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(LayoutElement))]
    public sealed class LootFeedEntryView : MonoBehaviour
    {
        private enum Phase { Delay, Appearing, Holding, Disappearing, Collapsing }

        [Header("Referências (auto-bind por nome)")]
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private TMP_Text captionText;
        [SerializeField] private Image accentBar;
        [SerializeField] private Image background;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private LayoutElement layoutElement;

        [Header("Altura")]
        [Tooltip("Altura total da linha. Se 0, usa o preferredHeight do LayoutElement ou a altura do RectTransform.")]
        [SerializeField] private float fullHeight = 0f;

        private Phase _phase;
        private float _phaseTime;
        private float _delay;
        private float _fadeIn;
        private float _hold;
        private float _fadeOut;
        private float _collapse;
        private float _lifetime;
        private string _quantityFormat;
        private Action<LootFeedEventKind, LootFeedEntryView> _onFinished;

        public object ItemId { get; private set; }
        public LootFeedEventKind Kind { get; private set; }
        public int Quantity { get; private set; }
        public bool IsLeaving => _phase == Phase.Disappearing || _phase == Phase.Collapsing;

        private void Reset() => AutoBind();

        [ContextMenu("Auto Bind")]
        private void AutoBind()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            layoutElement = GetComponent<LayoutElement>();
            icon = FindChild<Image>("Icon");
            accentBar = FindChild<Image>("AccentBar");
            background = FindChild<Image>("Background");
            nameText = FindChild<TMP_Text>("Name");
            quantityText = FindChild<TMP_Text>("Quantity");
            captionText = FindChild<TMP_Text>("Caption");
        }

        private T FindChild<T>(string childName) where T : Component
        {
            foreach (var c in GetComponentsInChildren<T>(true))
                if (c.gameObject.name == childName) return c;
            return null;
        }

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (layoutElement == null) layoutElement = GetComponent<LayoutElement>();

            if (fullHeight <= 0f)
            {
                fullHeight = layoutElement.preferredHeight > 0f
                    ? layoutElement.preferredHeight
                    : ((RectTransform)transform).rect.height;
            }
        }

        public void Setup(
            IIITem item,
            LootFeedEventKind kind,
            int quantity,
            string quantityFormat,
            string caption,
            Color accentColor,
            Color nameColor,
            float startDelay,
            float lifetime,
            float fadeIn,
            float fadeOut,
            float collapse,
            Action<LootFeedEventKind, LootFeedEntryView> onFinished)
        {
            ItemId = item.StorageableId;
            Kind = kind;
            Quantity = quantity;
            _quantityFormat = quantityFormat;
            _delay = Mathf.Max(0f, startDelay);
            _lifetime = lifetime;
            _hold = lifetime;
            _fadeIn = Mathf.Max(0.01f, fadeIn);
            _fadeOut = Mathf.Max(0.01f, fadeOut);
            _collapse = Mathf.Max(0.01f, collapse);
            _onFinished = onFinished;

            if (icon != null)
            {
                icon.sprite = item.Sprite;
                icon.enabled = item.Sprite != null;
            }

            if (nameText != null)
            {
                nameText.text = item.DisplayName;
                nameText.color = nameColor;
            }

            if (accentBar != null) accentBar.color = accentColor;
            if (quantityText != null) quantityText.color = accentColor;

            if (captionText != null)
            {
                bool hasCaption = !string.IsNullOrEmpty(caption);
                captionText.gameObject.SetActive(hasCaption);
                captionText.text = caption;
                captionText.color = accentColor;
            }

            RefreshQuantity();

            _phase = Phase.Delay;
            _phaseTime = 0f;
            Apply(alpha: 0f, expand: 0f, scale: 0.9f);
        }

        /// <summary>Usado apenas com merge ligado: soma a quantidade e renova o tempo na tela.</summary>
        public void Merge(int extraQuantity)
        {
            Quantity += extraQuantity;
            RefreshQuantity();

            if (_phase == Phase.Holding)
                _phaseTime = 0f;
        }

        private void RefreshQuantity()
        {
            if (quantityText != null)
                quantityText.text = string.Format(_quantityFormat, Quantity);
        }

        private void Update()
        {
            _phaseTime += Time.unscaledDeltaTime;

            switch (_phase)
            {
                case Phase.Delay:
                    if (_phaseTime >= _delay) NextPhase(Phase.Appearing);
                    break;

                case Phase.Appearing:
                {
                    float t = Mathf.Clamp01(_phaseTime / _fadeIn);
                    float eased = 1f - (1f - t) * (1f - t);
                    Apply(t, eased, Mathf.Lerp(0.9f, 1f, eased));
                    if (t >= 1f) NextPhase(Phase.Holding);
                    break;
                }

                case Phase.Holding:
                    if (_phaseTime >= _hold) NextPhase(Phase.Disappearing);
                    break;

                case Phase.Disappearing:
                {
                    float t = Mathf.Clamp01(_phaseTime / _fadeOut);
                    Apply(1f - t, 1f, 1f);
                    if (t >= 1f) NextPhase(Phase.Collapsing);
                    break;
                }

                case Phase.Collapsing:
                {
                    float t = Mathf.Clamp01(_phaseTime / _collapse);
                    Apply(0f, 1f - t, 1f);
                    if (t >= 1f) _onFinished?.Invoke(Kind, this);
                    break;
                }
            }
        }

        private void NextPhase(Phase next)
        {
            _phase = next;
            _phaseTime = 0f;
        }

        private void Apply(float alpha, float expand, float scale)
        {
            if (canvasGroup != null) canvasGroup.alpha = alpha;
            if (layoutElement != null) layoutElement.preferredHeight = fullHeight * expand;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}