using System;
using Services.DebugUtilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Core.Exploration.Items;

namespace Core.Inventory.UI
{
    /// <summary>
    /// Popup único de informações do item (nome, descrição, raridade, quantidade).
    /// Posiciona-se ao lado do slot em hover, trocando de lado e ficando
    /// dentro do canvas quando necessário.
    ///
    /// Montagem: coloque num objeto SEMPRE ATIVO dentro do Canvas, com um
    /// layout (ex.: VerticalLayoutGroup + ContentSizeFitter) para o tamanho
    /// se ajustar ao texto. A visibilidade é por CanvasGroup, não por SetActive.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ItemTooltipView : MonoBehaviour
    {
        public static ItemTooltipView Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [Header("Display")]
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _rarityText;
        [SerializeField] private TMP_Text _quantityText;
        [SerializeField] private string _quantityFormat = "x{0}";

        [Header("Position")]
        [Tooltip("Deslocamento em relação ao canto superior do slot (unidades do canvas).")]
        [SerializeField] private Vector2 _offset = new Vector2(12f, 0f);

        private readonly Vector3[] _corners = new Vector3[4];
        private CanvasGroup _group;
        private RectTransform _rect;
        private RectTransform _canvasRect;
        private Camera _camera;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Log(LogLevel.Warning, "Another ItemTooltipView already exists — the newest one replaces it.");

            Instance = this;

            _group = GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;   // nunca rouba o hover do slot (evita flicker)
            _group.interactable = false;

            _rect = (RectTransform)transform;
            _rect.pivot = new Vector2(0f, 1f); // posiciona pelo canto superior esquerdo

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                _canvasRect = (RectTransform)canvas.transform;
                _camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }
            else
            {
                Log(LogLevel.Error, "ItemTooltipView must be inside a Canvas.");
            }

            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(IIITem item, int quantity, RectTransform anchor)
        {
            if (item == null || anchor == null) { Hide(); return; }

            if (_icon != null)
            {
                _icon.sprite = item.Sprite;
                _icon.enabled = item.Sprite != null;
            }

            if (_titleText != null) _titleText.text = item.DisplayName ?? string.Empty;
            if (_descriptionText != null) _descriptionText.text = item.Description ?? string.Empty;
            if (_rarityText != null) _rarityText.text = item.Rarity.ToString();
            if (_quantityText != null) _quantityText.text = string.Format(_quantityFormat, quantity);

            transform.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
            PositionNextTo(anchor);

            _group.alpha = 1f;
        }

        public void Hide()
        {
            if (_group != null) _group.alpha = 0f;
        }

        private void PositionNextTo(RectTransform anchor)
        {
            if (_canvasRect == null) return;

            anchor.GetWorldCorners(_corners); // 0=BL, 1=TL, 2=TR, 3=BR
            Vector2 topLeft = ToCanvasLocal(_corners[1]);
            Vector2 topRight = ToCanvasLocal(_corners[2]);

            float scaleRatio = _rect.lossyScale.x / _canvasRect.lossyScale.x;
            Vector2 size = _rect.rect.size * scaleRatio;
            Rect bounds = _canvasRect.rect;

            // Tenta à direita do slot; se não couber, vai para a esquerda.
            Vector2 pos = new Vector2(topRight.x + _offset.x, topRight.y + _offset.y);
            if (pos.x + size.x > bounds.xMax)
                pos.x = topLeft.x - _offset.x - size.x;

            pos.x = Mathf.Clamp(pos.x, bounds.xMin, Mathf.Max(bounds.xMin, bounds.xMax - size.x));
            pos.y = Mathf.Clamp(pos.y, Mathf.Min(bounds.yMax, bounds.yMin + size.y), bounds.yMax);

            _rect.position = _canvasRect.TransformPoint(new Vector3(pos.x, pos.y, 0f));
        }

        private Vector2 ToCanvasLocal(Vector3 worldPoint)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(_camera, worldPoint);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, _camera, out var local);
            return local;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[ItemTooltipView:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}