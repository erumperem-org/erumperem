using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Carrossel infinito da barra de skills. A skill do cursor fica sempre no centro (sob a moldura fixa do HUD)
/// e os vizinhos dão a volta na lista: depois da última skill aparece a primeira de novo, e vice-versa.
/// Os slots ficam em posições fixas (offset * <see cref="slotSpacing"/>); quando o cursor anda, o conteúdo
/// é deslocado pelo passo e volta ao repouso, dando a impressão de que as skills deslizam.
/// O repouso do conteúdo é alinhado à moldura central, então a skill do cursor fica sempre dentro dela.
/// </summary>
public class InfinitySkillScroll : MonoBehaviour
{
    private const string FocusFrameObjectName = "Image (2)";

    [Header("UI References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentPanelTransform;

    [Tooltip("Moldura que marca a skill do cursor. Se vazio, procura um irmão chamado \"Image (2)\"; sem moldura, usa o centro do viewport.")]
    [SerializeField] private RectTransform focusFrame;

    [Header("Carousel")]
    [SerializeField, Min(1f)] private float slotSpacing = 56f;
    [Tooltip("Máximo de skills mostradas de cada lado da skill central.")]
    [SerializeField, Range(1, 3)] private int maxSideSlots = 3;
    [SerializeField, Min(0f)] private float slideDuration = 0.18f;

    private Vector2 _contentRestPosition;
    private bool _configured;
    private Tweener _slideTween;

    /// <summary>Quantos slots cabem (mesmo que cortados pela máscara) de cada lado do centro.</summary>
    public int SideSlotCount
    {
        get
        {
            var viewport = ResolveViewport();
            if (viewport == null || viewport.rect.width <= 1f)
            {
                return maxSideSlots;
            }

            var halfWidth = viewport.rect.width * 0.5f;
            var fitting = Mathf.CeilToInt((halfWidth - slotSpacing * 0.5f) / slotSpacing);
            return Mathf.Clamp(fitting, 1, maxSideSlots);
        }
    }

    /// <summary>
    /// Desliga o arrasto do ScrollRect e o layout automático do conteúdo: as posições passam a ser do carrossel.
    /// </summary>
    public void Configure(RectTransform content)
    {
        if (content != null)
        {
            contentPanelTransform = content;
        }

        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.enabled = false;
        }

        if (contentPanelTransform == null)
        {
            return;
        }

        if (contentPanelTransform.TryGetComponent<LayoutGroup>(out var layoutGroup))
        {
            layoutGroup.enabled = false;
        }

        if (contentPanelTransform.TryGetComponent<ContentSizeFitter>(out var sizeFitter))
        {
            sizeFitter.enabled = false;
        }

        if (!_configured)
        {
            _contentRestPosition = ComputeRestPositionCenteredOnFocus();
            contentPanelTransform.anchoredPosition = _contentRestPosition;
            _configured = true;
        }
    }

    /// <summary>Desloca o conteúdo em X até o centro dele cair no centro da moldura (ou do viewport).</summary>
    private Vector2 ComputeRestPositionCenteredOnFocus()
    {
        Canvas.ForceUpdateCanvases();

        var restPosition = contentPanelTransform.anchoredPosition;
        var focusTarget = ResolveFocusFrame();
        if (focusTarget == null)
        {
            focusTarget = ResolveViewport();
        }

        var contentParent = contentPanelTransform.parent;
        if (focusTarget == null || contentParent == null)
        {
            return restPosition;
        }

        var focusCenterWorld = focusTarget.TransformPoint(focusTarget.rect.center);
        var contentCenterWorld = contentPanelTransform.TransformPoint(contentPanelTransform.rect.center);
        var offsetInParent = contentParent.InverseTransformVector(focusCenterWorld - contentCenterWorld);
        restPosition.x += offsetInParent.x;
        return restPosition;
    }

    private RectTransform ResolveFocusFrame()
    {
        if (focusFrame != null)
        {
            return focusFrame;
        }

        var skillsSection = transform.parent;
        if (skillsSection == null)
        {
            return null;
        }

        var frame = skillsSection.Find(FocusFrameObjectName);
        if (frame == null)
        {
            frame = skillsSection.Find("FrontGround/" + FocusFrameObjectName);
        }

        focusFrame = frame as RectTransform;
        return focusFrame;
    }

    public void PlaceSlot(RectTransform slot, int offsetFromCenter)
    {
        if (slot == null)
        {
            return;
        }

        var center = new Vector2(0.5f, 0.5f);
        slot.anchorMin = center;
        slot.anchorMax = center;
        slot.pivot = center;
        slot.anchoredPosition = new Vector2(offsetFromCenter * slotSpacing, 0f);
    }

    /// <summary>Anima o deslize depois que o cursor andou <paramref name="step"/> slots (positivo = para a direita).</summary>
    public void PlaySlide(int step)
    {
        if (contentPanelTransform == null || !_configured)
        {
            return;
        }

        _slideTween?.Kill();
        contentPanelTransform.anchoredPosition = _contentRestPosition;

        if (step == 0 || slideDuration <= 0f)
        {
            return;
        }

        contentPanelTransform.anchoredPosition = _contentRestPosition + new Vector2(step * slotSpacing, 0f);
        _slideTween = contentPanelTransform
            .DOAnchorPos(_contentRestPosition, slideDuration)
            .SetEase(Ease.OutCubic)
            .SetLink(contentPanelTransform.gameObject);
    }

    private RectTransform ResolveViewport()
    {
        if (scrollRect != null)
        {
            return scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;
        }

        return transform as RectTransform;
    }

    private void OnDisable()
    {
        _slideTween?.Kill();
        _slideTween = null;

        if (contentPanelTransform != null && _configured)
        {
            contentPanelTransform.anchoredPosition = _contentRestPosition;
        }
    }
}
