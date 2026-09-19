using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using DG.Tweening;
using UnityEngine;

public class PanelTransition : MonoBehaviour
{
    private const float HiddenAlpha = 0f;
    private const float VisibleAlpha = 1f;
    private const float InitialViewScale = 0.95f;

    private const int AnimatorLayer = 0;
    private const int DelayMillisecondsMultiplier = 1000;
    private const float DefaultPaperOpenDuration = 0.875f;
    private const float DefaultPaperCloseDuration = 0.833333f;

    [Header("Paper")]
    [SerializeField] private Animator paperAnimator;
    [SerializeField] private string paperOpenState = "Paper_Open";
    [SerializeField] private string paperCloseState = "Paper_Close";
    [SerializeField, Min(0f)] private float paperOpenDuration = DefaultPaperOpenDuration;
    [SerializeField, Min(0f)] private float paperCloseDuration = DefaultPaperCloseDuration;

    [Header("View Panel")]
    [SerializeField] private GameObject viewPanelObject;
    [SerializeField] private CanvasGroup viewPanel;

    [Header("Buttons")]
    [SerializeField] private CanvasGroup buttonExtra;
    [SerializeField] private RectTransform buttonExtraRect;
    [SerializeField] private CanvasGroup btnBack;
    [SerializeField] private RectTransform btnBackRect;

    [Header("Legacy Button References")]
    [SerializeField] private RectTransform[] sideButtons;
    [SerializeField] private RectTransform backButton;
    [SerializeField] private CanvasGroup contentCanvasGroup;

    [SerializeField] private bool autoFindSideButtons = true;

    [SerializeField, Min(0f)] private float openDelay = 0.15f;
    [SerializeField, Min(0f)] private float closeDelay = 0.15f;
    [SerializeField, Min(0f)] private float buttonDuration = 0.4f;
    [SerializeField, Min(0f)] private float viewDuration = 0.35f;
    [SerializeField, Min(0f)] private float buttonMoveDistance = 180f;

    [Header("Lifecycle")]
    [SerializeField] private bool openOnEnable = true;

    private Vector2 buttonExtraStartPosition;

    private Vector2 btnBackStartPosition;
    private RectTransform viewPanelRect;
    private TaskCompletionSource<bool> paperCompletion;
    private CanvasGroup[] sideButtonCanvasGroups;
    private Vector2[] sideButtonStartPositions;

    private bool isReady;
    private bool isAnimating;
    private bool isOpen;
    private int transitionVersion;

    private void Awake()
    {
        ResolveReferences();
        viewPanelRect = viewPanel != null
            ? viewPanel.GetComponent<RectTransform>()
            : null;
        isReady = ValidateReferences();

        if (!isReady)
            return;

        // viewPanelRect is resolved before validation.
        buttonExtraStartPosition = buttonExtraRect != null
            ? buttonExtraRect.anchoredPosition
            : Vector2.zero;
        btnBackStartPosition = btnBackRect != null
            ? btnBackRect.anchoredPosition
            : Vector2.zero;
        SetClosedState();
    }

    /// <summary>
    /// Opens the paper first, then reveals the buttons and view panel together without fading the buttons during movement.
    /// </summary>
    public async Task OpenAsync()
    {
        if (!EnsureReady() || isOpen || isAnimating)
            return;

        int version = ++transitionVersion;
        isAnimating = true;

        try
        {
            await PlayPaperAnimationAsync(
                paperOpenState,
                paperOpenDuration,
                DefaultPaperOpenDuration
            );

            if (!IsCurrentTransition(version))
                return;

            await Delay(openDelay);

            if (!IsCurrentTransition(version))
                return;

            ActivateElements();

            await Task.WhenAll(
                AnimateButtonsOpenAsync(),
                AnimateViewOpenAsync()
            );

            if (!IsCurrentTransition(version))
                return;

            SetElementsInteractable(true);
            isOpen = true;
        }
        catch (Exception exception)
        {
            if (IsCurrentTransition(version))
            {
                Debug.LogException(exception, this);
                SetClosedState();
            }
        }
        finally
        {
            if (IsCurrentTransition(version))
                isAnimating = false;
        }
    }

    /// <summary>
    /// Slides the contents out, hides button graphics when the paper close animation starts, and closes the view.
    /// </summary>
    public async Task CloseAsync()
    {
        if (!EnsureReady())
            return;

        int version = ++transitionVersion;
        KillTweens();
        paperCompletion?.TrySetCanceled();
        paperCompletion = null;
        isAnimating = true;

        try
        {
            SetElementsInteractable(false);

            await Task.WhenAll(
                AnimateButtonsCloseAsync(),
                AnimateViewCloseAsync()
            );

            if (!IsCurrentTransition(version))
                return;

            await Delay(closeDelay);

            if (!IsCurrentTransition(version))
                return;

            SetButtonsAlpha(HiddenAlpha);

            await PlayPaperAnimationAsync(
                paperCloseState,
                paperCloseDuration,
                DefaultPaperCloseDuration
            );

            if (!IsCurrentTransition(version))
                return;

            SetClosedState();
        }
        catch (Exception exception)
        {
            if (IsCurrentTransition(version))
            {
                Debug.LogException(exception, this);
                SetClosedState();
            }
        }
        finally
        {
            if (IsCurrentTransition(version))
            {
                isOpen = false;
                isAnimating = false;
            }
        }
    }

    private void OnEnable()
    {
        PrepareClosedState();

        if (Application.isPlaying && openOnEnable)
            _ = OpenAsync();
    }

    /// <summary>
    /// Restores every animated element to the closed state so the next opening starts cleanly.
    /// </summary>
    public void ResetToClosedState()
    {
        if (!isReady)
            return;

        transitionVersion++;
        KillTweens();
        paperCompletion?.TrySetCanceled();
        paperCompletion = null;
        isAnimating = false;
        SetClosedState();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying || !isReady)
            return;

        transitionVersion++;
        KillTweens();
        isAnimating = false;
        isOpen = false;
        paperCompletion?.TrySetCanceled();
        paperCompletion = null;
    }

    private void KillTweens()
    {
        buttonExtraRect?.DOKill();
        btnBackRect?.DOKill();
        buttonExtra?.DOKill();
        btnBack?.DOKill();
        viewPanelRect?.DOKill();
        if (sideButtons != null)
        {
            foreach (RectTransform sideButton in sideButtons)
                sideButton?.DOKill();
        }

        if (sideButtonCanvasGroups != null)
        {
            foreach (CanvasGroup sideButtonCanvasGroup in sideButtonCanvasGroups)
                sideButtonCanvasGroup?.DOKill();
        }
    }

    private void ResolveReferences()
    {
        paperAnimator ??= FindChildComponent<Animator>("PaperBG");
        buttonExtraRect ??= FindChildRectTransform("ButtonExtra");
        btnBackRect ??= FindChildRectTransform("BtnBack");

        viewPanelObject ??= FindChildObject("ViewPanel") ?? FindChildObject("Content");
        viewPanel ??= contentCanvasGroup;
        viewPanel ??= GetOrAddCanvasGroup(viewPanelObject != null ? viewPanelObject.transform : null);
        buttonExtra ??= GetOrAddCanvasGroup(buttonExtraRect);
        btnBackRect ??= backButton;
        btnBack ??= GetOrAddCanvasGroup(btnBackRect);
        if (autoFindSideButtons && (sideButtons == null || sideButtons.Length == 0))
            sideButtons = FindSideButtons();

        NormalizeButtonReferences();
        ResolveSideButtonCanvasGroups();
    }

    private void NormalizeButtonReferences()
    {
        if (buttonExtra == null || buttonExtraRect == null || btnBackRect == null)
            return;

        if (buttonExtraRect != btnBackRect)
            return;

        buttonExtraRect = buttonExtra.GetComponent<RectTransform>();
    }
    private RectTransform[] FindSideButtons()

    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        List<RectTransform> foundButtons = new();

        foreach (Transform child in children)
        {
            if (!child.name.StartsWith("Buttons", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (Transform button in child)
            {
                if (button.name.Equals("BtnBack", StringComparison.OrdinalIgnoreCase))
                    continue;

                RectTransform buttonRect = button as RectTransform;

                if (buttonRect != null)
                    foundButtons.Add(buttonRect);
            }
        }

        return foundButtons.ToArray();
    }

    private void ResolveSideButtonCanvasGroups()
    {
        if (sideButtons == null || sideButtons.Length == 0)
            return;

        sideButtonCanvasGroups = new CanvasGroup[sideButtons.Length];
        sideButtonStartPositions = new Vector2[sideButtons.Length];

        for (int index = 0; index < sideButtons.Length; index++)
        {
            RectTransform sideButton = sideButtons[index];
            sideButtonCanvasGroups[index] = GetOrAddCanvasGroup(sideButton);
            sideButtonStartPositions[index] = sideButton != null
                ? sideButton.anchoredPosition
                : Vector2.zero;
        }
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == childName)
                return child.GetComponent<T>();
        }

        return null;
    }

    private RectTransform FindChildRectTransform(string childName)
    {
        return FindChildComponent<RectTransform>(childName);
    }

    private GameObject FindChildObject(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == childName)
                return child.gameObject;
        }

        return null;
    }

    private static CanvasGroup GetOrAddCanvasGroup(Component target)
    {
        if (target == null)
            return null;

        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
        return canvasGroup != null ? canvasGroup : target.gameObject.AddComponent<CanvasGroup>();
    }

    private bool HasButtonAnimationReferences()
    {
        return HasExtraButtonAnimationReferences() || HasBackButtonAnimationReferences();
    }

    private bool HasBackButtonAnimationReferences()
    {
        return btnBack != null && btnBackRect != null;
    }

    private bool HasExtraButtonAnimationReferences()
    {
        bool hasStandaloneButton = buttonExtra != null &&
                                   buttonExtraRect != null &&
                                   !IsSideButtonContainer();
        bool hasSideButtons = sideButtons != null &&
                              sideButtonCanvasGroups != null &&
                              sideButtons.Length == sideButtonCanvasGroups.Length;

        return hasStandaloneButton || hasSideButtons;
    }

    private bool HasSideButtonContainerAnimationReferences()
    {
        return buttonExtra != null &&
               buttonExtraRect != null &&
               IsSideButtonContainer();
    }

    private bool ShouldAnimateSideButtonsIndividually()
    {
        return sideButtons != null &&
               sideButtonCanvasGroups != null &&
               sideButtons.Length == sideButtonCanvasGroups.Length &&
               !IsSideButtonContainer();
    }
    private bool HasStandaloneButtonExtraAnimationReferences()
    {
        return buttonExtra != null &&
               buttonExtraRect != null &&
               !IsSideButtonContainer();
    }


    private bool IsSideButtonContainer()

    {
        if (buttonExtraRect == null || sideButtons == null)
            return false;

        foreach (RectTransform sideButton in sideButtons)
        {
            if (sideButton == null)
                continue;

            if (sideButton == buttonExtraRect || sideButton.IsChildOf(buttonExtraRect))
                return true;
        }

        return false;
    }

    private bool IsCurrentTransition(int version)
    {
        return version == transitionVersion;
    }

    private bool EnsureReady()
    {
        if (isReady)
            return true;

        ResolveReferences();
        viewPanelRect = viewPanel != null
            ? viewPanel.GetComponent<RectTransform>()
            : null;
        isReady = ValidateReferences();

        if (!isReady)
            return false;

        buttonExtraStartPosition = buttonExtraRect != null
            ? buttonExtraRect.anchoredPosition
            : Vector2.zero;
        btnBackStartPosition = btnBackRect != null
            ? btnBackRect.anchoredPosition
            : Vector2.zero;

        return true;
    }

    private bool ValidateReferences()
    {
        bool hasValidReferences = paperAnimator != null &&
                                  viewPanelObject != null &&
                                  viewPanel != null &&
                                  viewPanelRect != null;

        if (!hasValidReferences)
        {
            Debug.LogError(
                $"{nameof(PanelTransition)} requires all paper, panel, button and RectTransform references to be assigned.",
                this
            );
        }

        return hasValidReferences;
    }

    private void PrepareClosedState()
    {
        if (!isReady)
            return;

        SetClosedState();
    }

    private void SetClosedState()
    {
        viewPanelObject.SetActive(false);

        viewPanel.alpha = HiddenAlpha;
        viewPanelRect.localScale = Vector3.one * InitialViewScale;

        ResetAnimatedButtonState();

        SetButtonsAlpha(HiddenAlpha);
        SetElementsInteractable(false);
        isOpen = false;
    }

    private void SetButtonsAlpha(float alpha)
    {
        if (buttonExtra != null)
            buttonExtra.alpha = alpha;

        if (sideButtonCanvasGroups != null)
        {
            foreach (CanvasGroup sideButtonCanvasGroup in sideButtonCanvasGroups)
            {
                if (sideButtonCanvasGroup != null)
                    sideButtonCanvasGroup.alpha = alpha;
            }
        }

        if (btnBack != null)
            btnBack.alpha = alpha;
    }

    private void ActivateElements()
    {
        viewPanelObject.SetActive(true);

        viewPanel.alpha = HiddenAlpha;
        viewPanelRect.localScale = Vector3.one * InitialViewScale;

        ResetAnimatedButtonState();
        SetButtonsAlpha(VisibleAlpha);

        SetElementsInteractable(false);
    }

    private void ResetAnimatedButtonState()
    {
        if (HasSideButtonContainerAnimationReferences())
        {
            buttonExtraRect.anchoredPosition = GetButtonExtraHiddenPosition();
        }
        else if (HasStandaloneButtonExtraAnimationReferences())
        {
            buttonExtra.alpha = HiddenAlpha;
            buttonExtraRect.anchoredPosition = GetButtonExtraHiddenPosition();
        }

        if (ShouldAnimateSideButtonsIndividually() && sideButtons != null && sideButtonCanvasGroups != null)
        {
            for (int index = 0; index < sideButtons.Length; index++)
            {
                RectTransform buttonRect = sideButtons[index];
                CanvasGroup buttonCanvasGroup = sideButtonCanvasGroups[index];

                if (buttonRect == null || buttonCanvasGroup == null)
                    continue;

                buttonCanvasGroup.alpha = HiddenAlpha;
                buttonRect.anchoredPosition = GetButtonExtraHiddenPosition(index);
            }
        }

        if (btnBack != null && btnBackRect != null)
        {
            btnBack.alpha = HiddenAlpha;
            btnBackRect.anchoredPosition = GetBtnBackHiddenPosition();
        }
    }


    private Vector2 GetButtonExtraHiddenPosition()
    {
        return buttonExtraStartPosition + Vector2.right * buttonMoveDistance;
    }

    private Vector2 GetButtonExtraHiddenPosition(int index)
    {
        return sideButtonStartPositions[index] + Vector2.right * buttonMoveDistance;
    }

    private Vector2 GetBtnBackHiddenPosition()
    {
        return btnBackStartPosition + Vector2.left * buttonMoveDistance;
    }

    private void SetElementsInteractable(bool interactable)
    {
        if (!HasButtonAnimationReferences())
            return;

        if (buttonExtra != null)
        {
            buttonExtra.interactable = interactable;
            buttonExtra.blocksRaycasts = interactable;
        }

        if (sideButtonCanvasGroups != null)
        {
            foreach (CanvasGroup buttonCanvasGroup in sideButtonCanvasGroups)
            {
                if (buttonCanvasGroup == null)
                    continue;

                buttonCanvasGroup.interactable = interactable;
                buttonCanvasGroup.blocksRaycasts = interactable;
            }
        }

        if (btnBack != null)
        {
            btnBack.interactable = interactable;
            btnBack.blocksRaycasts = interactable;
        }
    }

    private async Task AnimateButtonsOpenAsync()
    {
        if (!HasButtonAnimationReferences())
            return;

        List<Task> animationTasks = new();

        if (HasSideButtonContainerAnimationReferences() || HasStandaloneButtonExtraAnimationReferences())
        {
            animationTasks.Add(
                buttonExtraRect
                    .DOAnchorPos(buttonExtraStartPosition, buttonDuration)
                    .SetEase(Ease.OutCubic)
                    .AsyncWaitForCompletion()
            );
        }

        if (ShouldAnimateSideButtonsIndividually() && sideButtons != null && sideButtonCanvasGroups != null)
        {
            for (int index = 0; index < sideButtons.Length; index++)
            {
                RectTransform buttonRect = sideButtons[index];

                if (buttonRect == null)
                    continue;

                animationTasks.Add(
                    buttonRect
                        .DOAnchorPos(sideButtonStartPositions[index], buttonDuration)
                        .SetEase(Ease.OutCubic)
                        .AsyncWaitForCompletion()
                );
            }
        }

        if (HasBackButtonAnimationReferences())
        {
            animationTasks.Add(
                btnBackRect
                    .DOAnchorPos(btnBackStartPosition, buttonDuration)
                    .SetEase(Ease.OutCubic)
                    .AsyncWaitForCompletion()
            );
        }

        await Task.WhenAll(animationTasks);
    }

    private async Task AnimateButtonsCloseAsync()
    {
        if (!HasButtonAnimationReferences())
            return;

        List<Task> animationTasks = new();

        if (HasSideButtonContainerAnimationReferences() || HasStandaloneButtonExtraAnimationReferences())
        {
            animationTasks.Add(
                buttonExtraRect
                    .DOAnchorPos(GetButtonExtraHiddenPosition(), buttonDuration)
                    .SetEase(Ease.InCubic)
                    .AsyncWaitForCompletion()
            );
        }

        if (ShouldAnimateSideButtonsIndividually() && sideButtons != null && sideButtonCanvasGroups != null)
        {
            for (int index = 0; index < sideButtons.Length; index++)
            {
                RectTransform buttonRect = sideButtons[index];

                if (buttonRect == null)
                    continue;

                animationTasks.Add(
                    buttonRect
                        .DOAnchorPos(GetButtonExtraHiddenPosition(index), buttonDuration)
                        .SetEase(Ease.InCubic)
                        .AsyncWaitForCompletion()
                );
            }
        }

        if (HasBackButtonAnimationReferences())
        {
            animationTasks.Add(
                btnBackRect
                    .DOAnchorPos(GetBtnBackHiddenPosition(), buttonDuration)
                    .SetEase(Ease.InCubic)
                    .AsyncWaitForCompletion()
            );
        }

        await Task.WhenAll(animationTasks);

        if (btnBackRect != null)
            btnBackRect.anchoredPosition = GetBtnBackHiddenPosition();
    }

    private async Task AnimateViewOpenAsync()
    {
        Tween fadeTween = viewPanel
            .DOFade(VisibleAlpha, viewDuration)
            .SetEase(Ease.OutQuad);

        Tween scaleTween = viewPanelRect
            .DOScale(Vector3.one, viewDuration)
            .SetEase(Ease.OutBack);

        await Task.WhenAll(
            fadeTween.AsyncWaitForCompletion(),
            scaleTween.AsyncWaitForCompletion()
        );
    }

    private async Task AnimateViewCloseAsync()
    {
        Tween fadeTween = viewPanel
            .DOFade(HiddenAlpha, viewDuration)
            .SetEase(Ease.InQuad);

        Tween scaleTween = viewPanelRect
            .DOScale(Vector3.one * InitialViewScale, viewDuration)
            .SetEase(Ease.InQuad);

        await Task.WhenAll(
            fadeTween.AsyncWaitForCompletion(),
            scaleTween.AsyncWaitForCompletion()
        );

        viewPanelObject.SetActive(false);
    }

    private async Task PlayPaperAnimationAsync(
        string stateName,
        float configuredDuration,
        float fallbackDuration)
    {
        float duration = GetPaperAnimationDuration(configuredDuration, fallbackDuration);
        bool canPlayState = TryPlayPaperState(stateName);

        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        paperCompletion = completion;

        try
        {
            if (canPlayState)
                await Task.WhenAny(completion.Task, Delay(duration));
            else
                await Delay(duration);
        }
        finally
        {
            if (ReferenceEquals(paperCompletion, completion))
                paperCompletion = null;
        }
    }

    private bool TryPlayPaperState(string stateName)
    {
        if (paperAnimator.runtimeAnimatorController == null)
        {
            Debug.LogWarning(
                $"{nameof(PanelTransition)}: no RuntimeAnimatorController is assigned; using the configured paper duration.",
                this
            );
            return false;
        }

        int stateHash = Animator.StringToHash(stateName);

        if (!paperAnimator.HasState(AnimatorLayer, stateHash))
        {
            Debug.LogWarning(
                $"{nameof(PanelTransition)}: animator state '{stateName}' was not found; using the configured paper duration.",
                this
            );
            return false;
        }

        paperAnimator.Play(stateHash, AnimatorLayer, 0f);
        return true;
    }

    private float GetPaperAnimationDuration(float configuredDuration, float fallbackDuration)
    {
        if (configuredDuration > 0f)
            return configuredDuration;

        return fallbackDuration;
    }

    /// <summary>
    /// Completes a paper transition when an Animator Animation Event is present.
    /// The code also has a duration fallback, so a missing event cannot deadlock the UI.
    /// </summary>
    public void OnPaperAnimationFinished()
    {
        paperCompletion?.TrySetResult(true);
    }

    /// <summary>
    /// Completes the currently playing paper animation.
    /// </summary>
    public void OnPaperOpenFinished()
    {
        OnPaperAnimationFinished();
    }

    /// <summary>
    /// Completes the currently playing paper animation.
    /// </summary>
    public void OnPaperCloseFinished()
    {
        OnPaperAnimationFinished();
    }

    private static Task Delay(float seconds)
    {
        if (seconds <= 0f)
            return Task.CompletedTask;

        int milliseconds = Mathf.CeilToInt(seconds * DelayMillisecondsMultiplier);
        return Task.Delay(milliseconds);
    }
}

public sealed class PanelTransitionController : PanelTransition
{
}
