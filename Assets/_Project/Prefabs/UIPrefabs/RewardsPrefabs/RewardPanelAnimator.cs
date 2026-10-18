using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class RewardPanelAnimator : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private RectTransform backgroundRect;
    [SerializeField] private RectTransform topPanelRect;
    [SerializeField] private RectTransform wontitleImageRect; // A imagem filha do wontitle
    [SerializeField] private RectTransform middlePanelRect;
    [SerializeField] private RectTransform contentRect;       // ScrollArea -> Content
    [SerializeField] private RectTransform continueButtonRect;

    [Header("Configurações de Animação")]
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float itemFadeDuration = 0.2f;
    [SerializeField] private float delayBetweenItems = 0.1f;

    private CanvasGroup backgroundCanvasGroup;
    private CanvasGroup topPanelCanvasGroup;
    private CanvasGroup middlePanelCanvasGroup;
    private CanvasGroup continueCanvasGroup;

    private void Awake()
    {
        backgroundCanvasGroup = GetOrAddCanvasGroup(backgroundRect);
        topPanelCanvasGroup = GetOrAddCanvasGroup(topPanelRect);
        middlePanelCanvasGroup = GetOrAddCanvasGroup(middlePanelRect);
        continueCanvasGroup = GetOrAddCanvasGroup(continueButtonRect);
    }

    private void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(PlayRewardSequence());
    }

    private IEnumerator PlayRewardSequence()
    {
        // 1. Resetar estados iniciais instantaneamente
        SetInitialStates();

        // Garante que o painel está visível para rodar as animações
        gameObject.SetActive(true);

        // 2. Background e TopPanel aparecem com fade + escala X do wontitle
        Sequence topSeq = DOTween.Sequence();
        
        if (backgroundCanvasGroup != null)
            topSeq.Join(backgroundCanvasGroup.DOFade(1f, fadeDuration));

        if (topPanelCanvasGroup != null)
            topSeq.Join(topPanelCanvasGroup.DOFade(1f, fadeDuration));

        if (wontitleImageRect != null)
        {
            topSeq.Join(wontitleImageRect.DOScaleX(1f, fadeDuration).SetEase(Ease.OutQuad));
        }

        // Aguarda a animação do topo terminar
        yield return topSeq.WaitForCompletion();

        // 3. MiddlePanel aparece com fade in simples
        if (middlePanelCanvasGroup != null)
        {
            middlePanelCanvasGroup.gameObject.SetActive(true);
            yield return middlePanelCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();
        }

        // 4. Itens do Content aparecem um após o outro ativando e fazendo fade in
        if (contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            int childCount = contentRect.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform item = contentRect.GetChild(i);
                if (item == null) continue;

                // Ativa o GameObject do item que estava desativado
                item.gameObject.SetActive(true);

                CanvasGroup itemCanvasGroup = GetOrAddCanvasGroup(item);
                itemCanvasGroup.alpha = 0f; // Começa invisível

                // Faz o fade in do item
                itemCanvasGroup.DOFade(1f, itemFadeDuration);

                yield return new WaitForSeconds(delayBetweenItems);
            }
        }

        // 5. Por fim, o Continue tem seu fade in
        if (continueCanvasGroup != null)
        {
            continueCanvasGroup.gameObject.SetActive(true);
            yield return continueCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();
        }
    }

    private void SetInitialStates()
    {
        if (backgroundCanvasGroup != null) backgroundCanvasGroup.alpha = 0f;
        if (topPanelCanvasGroup != null) topPanelCanvasGroup.alpha = 0f;
        
        if (wontitleImageRect != null)
        {
            Vector3 scale = wontitleImageRect.localScale;
            scale.x = 0f;
            wontitleImageRect.localScale = scale;
        }

        if (middlePanelCanvasGroup != null) middlePanelCanvasGroup.alpha = 0f;

        // Desativa os itens do content no início para que não apareçam antes da hora
        if (contentRect != null)
        {
            foreach (Transform child in contentRect)
            {
                if (child != null)
                {
                    CanvasGroup cg = GetOrAddCanvasGroup(child);
                    cg.alpha = 0f;
                    child.gameObject.SetActive(false); // Mantém desativado até chegar a vez dele
                }
            }
        }

        if (continueCanvasGroup != null) continueCanvasGroup.alpha = 0f;
    }

    private CanvasGroup GetOrAddCanvasGroup(Component component)
    {
        if (component == null) return null;
        return GetOrAddCanvasGroup(component.gameObject);
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
    {
        if (obj == null) return null;
        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = obj.AddComponent<CanvasGroup>();
        }
        return cg;
    }
}