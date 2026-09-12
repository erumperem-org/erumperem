using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PanelTransitionController : MonoBehaviour
{
    [Header("Background")]

    [Tooltip("Animator responsável pela animação do papel abrindo/fechando.")]
    [SerializeField] private Animator backgroundAnimator;

    [Tooltip("Nome do estado da animação de abertura no Animator.")]
    [SerializeField] private string backgroundOpenAnimation = "Background_Open";

    [Tooltip("Nome do estado da animação de fechamento no Animator.")]
    [SerializeField] private string backgroundCloseAnimation = "Background_Close";

    [Tooltip("Duração da animação de abertura do Background.")]
    [SerializeField] private float backgroundOpenDuration = 0.8f;

    [Tooltip("Duração da animação de fechamento do Background.")]
    [SerializeField] private float backgroundCloseDuration = 0.8f;

    [Header("Side Buttons")]

    [Tooltip("Coloque aqui os RectTransforms dos botões que irão deslizar.")]
    [SerializeField] private RectTransform[] sideButtons;

    [Tooltip("Distância que os botões ficarão fora da posição original.")]
    [SerializeField] private float buttonSlideDistance = 300f;

    [Tooltip("Tempo da animação dos botões.")]
    [SerializeField] private float buttonSlideDuration = 0.35f;

    [Tooltip("Tempo entre a entrada de cada botão.")]
    [SerializeField] private float buttonStagger = 0.05f;

    [Tooltip("Se verdadeiro, os botões entram pela direita. Se falso, pela esquerda.")]
    [SerializeField] private bool buttonsEnterFromRight = true;

    [Header("Back Button")]

    [Tooltip("RectTransform do botão Back.")]
    [SerializeField] private RectTransform backButton;

    [Header("View Content")]

    [Tooltip("CanvasGroup opcional usado para controlar a interação do conteúdo.")]
    [SerializeField] private CanvasGroup contentCanvasGroup;

    [Tooltip("Duração da animação de entrada do conteúdo.")]
    [SerializeField] private float contentAnimationDuration = 0.25f;

    [Tooltip("Escala inicial do conteúdo.")]
    [SerializeField] private float contentInitialScale = 0.85f;

    // ESTADO

    private Vector2[] sideButtonsOriginalPositions;
    private Vector2 backButtonOriginalPosition;
    private bool hasBackButton;
    private bool isTransitioning;
    private void Awake()
    {
        SaveOriginalPositions();

        PrepareInitialState();
    }

    private void SaveOriginalPositions()
    {
        // Botões laterais

        if (sideButtons != null)
        {
            sideButtonsOriginalPositions = new Vector2[sideButtons.Length];

            for (int i = 0; i < sideButtons.Length; i++)
            {
                if (sideButtons[i] != null)
                {
                    sideButtonsOriginalPositions[i] =
                        sideButtons[i].anchoredPosition;
                }
            }
        }

        // Botão Back

        if (backButton != null)
        {
            hasBackButton = true;

            backButtonOriginalPosition =
                backButton.anchoredPosition;
        }
    }
    private void PrepareInitialState()
    {
        // Coloca os botões fora da posição original.

        PrepareButtonsForAnimation();

        // Prepara o conteúdo.

        if (contentCanvasGroup != null)
        {
            contentCanvasGroup.alpha = 0f;
            contentCanvasGroup.interactable = false;
            contentCanvasGroup.blocksRaycasts = false;

            RectTransform contentRect =
                contentCanvasGroup.GetComponent<RectTransform>();

            if (contentRect != null)
            {
                contentRect.localScale =
                    Vector3.one * contentInitialScale;
            }
        }

        // Desabilita interação durante o estado inicial.

        SetButtonsInteractable(false);
    }
    private void PrepareButtonsForAnimation()
    {
        // Botões laterais

        if (sideButtons != null)
        {
            for (int i = 0; i < sideButtons.Length; i++)
            {
                if (sideButtons[i] == null)
                    continue;

                float direction =
                    buttonsEnterFromRight ? 1f : -1f;

                sideButtons[i].anchoredPosition =
                    new Vector2(
                        sideButtonsOriginalPositions[i].x +
                        buttonSlideDistance * direction,

                        sideButtonsOriginalPositions[i].y
                    );
            }
        }

        // Back

        if (hasBackButton)
        {
            float direction =
                buttonsEnterFromRight ? 1f : -1f;

            backButton.anchoredPosition =
                new Vector2(
                    backButtonOriginalPosition.x +
                    buttonSlideDistance * direction,

                    backButtonOriginalPosition.y
                );
        }
    }
    public void OpenPanel()
    {
        // Evita iniciar duas transições ao mesmo tempo.

        if (isTransitioning)
            return;

        gameObject.SetActive(true);

        // Garante que o painel começa no estado correto.

        PrepareButtonsForAnimation();

        ResetContent();

        // Começa a sequência.

        StartCoroutine(OpenRoutine());
    }

    //ABERTURA
    private System.Collections.IEnumerator OpenRoutine()
    {
        isTransitioning = true;

        SetButtonsInteractable(false);

        //BACKGROUND ABRE

        if (backgroundAnimator != null)
        {
            backgroundAnimator.Play(
                backgroundOpenAnimation,
                0,
                0f
            );

            yield return new WaitForSeconds(
                backgroundOpenDuration
            );
        }

        //BOTÕES ENTRAM

        Sequence buttonsSequence =
            CreateButtonsEnterSequence();

        yield return buttonsSequence.WaitForCompletion();

        // CONTEÚDO APARECE

        Tween contentTween =
            CreateContentEnterTween();

        if (contentTween != null)
        {
            yield return contentTween.WaitForCompletion();
        }

        //LIBERA INTERAÇÃO

        SetButtonsInteractable(true);

        if (contentCanvasGroup != null)
        {
            contentCanvasGroup.interactable = true;
            contentCanvasGroup.blocksRaycasts = true;
        }


        isTransitioning = false;
    }

    // CRIA ANIMAÇÃO DE ENTRADA DOS BOTÕES
    private Sequence CreateButtonsEnterSequence()
    {
        Sequence sequence = DOTween.Sequence();

        // Botões laterais

        if (sideButtons != null)
        {
            for (int i = 0; i < sideButtons.Length; i++)
            {
                if (sideButtons[i] == null)
                    continue;

                RectTransform button =
                    sideButtons[i];

                float delay =
                    i * buttonStagger;

                sequence.Insert(
                    delay,

                    button.DOAnchorPos(
                        sideButtonsOriginalPositions[i],
                        buttonSlideDuration
                    )
                    .SetEase(Ease.OutBack)
                );
            }
        }

        // Back

        if (hasBackButton)
        {
            float delay =
                sideButtons != null
                    ? sideButtons.Length * buttonStagger
                    : 0f;

            sequence.Insert(
                delay,

                backButton.DOAnchorPos(
                    backButtonOriginalPosition,
                    buttonSlideDuration
                )
                .SetEase(Ease.OutBack)
            );
        }


        return sequence;
    }

    // ANIMAÇÃO DE ENTRADA
    private Tween CreateContentEnterTween()
    {
        if (contentCanvasGroup == null)
            return null;

        RectTransform contentRect =
            contentCanvasGroup.GetComponent<RectTransform>();

        if (contentRect == null)
            return null;


        contentCanvasGroup.alpha = 0f;

        contentRect.localScale =
            Vector3.one * contentInitialScale;


        Sequence sequence = DOTween.Sequence();

        sequence.Join(
            contentCanvasGroup
                .DOFade(1f, contentAnimationDuration)
        );

        sequence.Join(
            contentRect
                .DOScale(1f, contentAnimationDuration)
                .SetEase(Ease.OutBack)
        );

        return sequence;
    }

    // FECHAR PAINEL

    public void ClosePanel()
    {
        if (isTransitioning)
            return;

        StartCoroutine(CloseRoutine());
    }

    // ROTINA DE FECHAMENTO
    private System.Collections.IEnumerator CloseRoutine()
    {
        isTransitioning = true;

        SetButtonsInteractable(false);

        if (contentCanvasGroup != null)
        {
            contentCanvasGroup.interactable = false;
            contentCanvasGroup.blocksRaycasts = false;
        }

        //CONTEÚDO DESAPARECE

        Tween contentTween =
            CreateContentExitTween();

        if (contentTween != null)
        {
            yield return contentTween.WaitForCompletion();
        }

        //BOTÕES SAEM

        Sequence buttonsSequence =
            CreateButtonsExitSequence();

        yield return buttonsSequence.WaitForCompletion();

        //BACKGROUND FECHA

        if (backgroundAnimator != null)
        {
            backgroundAnimator.Play(
                backgroundCloseAnimation,
                0,
                0f
            );

            yield return new WaitForSeconds(
                backgroundCloseDuration
            );
        }

        isTransitioning = false;
        gameObject.SetActive(false);
    }

    // ANIMAÇÃO DE SAÍDA DOS BOTÕES
    private Sequence CreateButtonsExitSequence()
    {
        Sequence sequence = DOTween.Sequence();


        float direction =
            buttonsEnterFromRight ? 1f : -1f;

        // Botões laterais

        if (sideButtons != null)
        {
            for (int i = 0; i < sideButtons.Length; i++)
            {
                if (sideButtons[i] == null)
                    continue;

                RectTransform button =
                    sideButtons[i];

                Vector2 exitPosition =
                    new Vector2(
                        sideButtonsOriginalPositions[i].x +
                        buttonSlideDistance * direction,

                        sideButtonsOriginalPositions[i].y
                    );

                float delay =
                    i * buttonStagger;

                sequence.Insert(
                    delay,

                    button.DOAnchorPos(
                        exitPosition,
                        buttonSlideDuration
                    )
                    .SetEase(Ease.InBack)
                );
            }
        }

        // Back

        if (hasBackButton)
        {
            Vector2 exitPosition =
                new Vector2(
                    backButtonOriginalPosition.x +
                    buttonSlideDistance * direction,

                    backButtonOriginalPosition.y
                );

            float delay =
                sideButtons != null
                    ? sideButtons.Length * buttonStagger
                    : 0f;

            sequence.Insert(
                delay,

                backButton.DOAnchorPos(
                    exitPosition,
                    buttonSlideDuration
                )
                .SetEase(Ease.InBack)
            );
        }


        return sequence;
    }

    // ANIMAÇÃO DE SAÍDA DO CONTEÚDO
    private Tween CreateContentExitTween()
    {
        if (contentCanvasGroup == null)
            return null;

        RectTransform contentRect =
            contentCanvasGroup.GetComponent<RectTransform>();

        if (contentRect == null)
            return null;


        Sequence sequence = DOTween.Sequence();

        sequence.Join(
            contentCanvasGroup
                .DOFade(0f, contentAnimationDuration)
        );

        sequence.Join(
            contentRect
                .DOScale(
                    contentInitialScale,
                    contentAnimationDuration
                )
                .SetEase(Ease.InBack)
        );

        return sequence;
    }

    // RESET DO CONTEÚDO
    private void ResetContent()
    {
        if (contentCanvasGroup == null)
            return;

        contentCanvasGroup.alpha = 0f;
        contentCanvasGroup.interactable = false;
        contentCanvasGroup.blocksRaycasts = false;

        RectTransform contentRect =
            contentCanvasGroup.GetComponent<RectTransform>();

        if (contentRect != null)
        {
            contentRect.localScale =
                Vector3.one * contentInitialScale;
        }
    }

    // CONTROLE DE INTERAÇÃO DOS BOTÕES
    private void SetButtonsInteractable(bool value)
    {
        // Botões laterais

        if (sideButtons != null)
        {
            foreach (RectTransform buttonTransform in sideButtons)
            {
                if (buttonTransform == null)
                    continue;

                Button button =
                    buttonTransform.GetComponent<Button>();

                if (button != null)
                {
                    button.interactable = value;
                }
            }
        }

        // Back

        if (backButton != null)
        {
            Button button =
                backButton.GetComponent<Button>();

            if (button != null)
            {
                button.interactable = value;
            }
        }
    }
    public bool IsTransitioning()
    {
        return isTransitioning;
    }
}