using UnityEngine;
using UnityEngine.UI;

public class InfinitySkillScroll : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentPanelTransform;

    [Header("Settings")]
    [SerializeField] private float smoothSpeed = 10f;

    private float _targetAnchoredPositionX;

    private void Update()
    {
        if (scrollRect == null || contentPanelTransform == null) return;

        // Animação suave da posição X ancorada do Content
        Vector2 currentPos = contentPanelTransform.anchoredPosition;
        float newX = Mathf.Lerp(currentPos.x, _targetAnchoredPositionX, Time.deltaTime * smoothSpeed);
        contentPanelTransform.anchoredPosition = new Vector2(newX, currentPos.y);
    }

    /// <summary>
    /// Centraliza o item indicado pelo índice exatamente no centro do Viewport.
    /// </summary>
    public void ScrollToItem(int itemIndex)
    {
        if (scrollRect == null || contentPanelTransform == null || itemIndex < 0) return;

        int totalChildCount = contentPanelTransform.childCount;
        if (itemIndex >= totalChildCount) return;

        // Obtém o RectTransform do item selecionado
        RectTransform targetChild = contentPanelTransform.GetChild(itemIndex) as RectTransform;
        if (targetChild == null) return;

        // Garante a atualização de layout do Unity antes de calcular a posição do item
        Canvas.ForceUpdateCanvases();

        // Obtém a referência do Viewport (ou do próprio ScrollRect se a Viewport for nula)
        RectTransform viewportRect = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

        // Posição x local do elemento dentro do Content
        float childLocalX = targetChild.localPosition.x;

        // Posição X necessária no Content para alinhar o item ao centro do Viewport
        _targetAnchoredPositionX = -childLocalX;
    }
}
