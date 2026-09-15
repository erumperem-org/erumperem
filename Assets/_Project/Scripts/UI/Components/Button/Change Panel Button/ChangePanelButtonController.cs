using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Services.DebugUtilities;

public class ChangePanelButtonController : UiButtonController<ChangePanelButtonModel>
{
    private const string BackButtonName = "BtnBack";

    [Header("Transition")]
    [Tooltip("Used only by the BtnBack object. If empty, the transition is resolved from the panel being hidden.")]
    [SerializeField] private PanelTransition panelTransition;

    [SerializeField] private bool useCloseTransition = true;

    private bool isChangingPanel;
    private Button unityButton;

    private void Awake()
    {
        if (panelTransition == null)
            panelTransition = ResolvePanelTransition();

        unityButton = GetComponent<Button>();

        if (unityButton != null)
            unityButton.onClick.AddListener(HandleUnityButtonClick);
    }

    private void OnDestroy()
    {
        if (unityButton != null)
            unityButton.onClick.RemoveListener(HandleUnityButtonClick);
    }

    private void HandleUnityButtonClick()
    {
        if (isChangingPanel)
            return;

        ChangePanelAsync();
    }

    private PanelTransition ResolvePanelTransition()
    {
        if (!IsBackButton())
            return null;

        GameObject panelToHide = uiButtonModel.panelToHide;

        if (panelToHide == null)
            return null;

        Transform panelTransform = panelToHide.transform;
        bool buttonBelongsToPanel = transform == panelTransform ||
                                    transform.IsChildOf(panelTransform);

        if (!buttonBelongsToPanel)
            return null;

        return panelToHide.GetComponentInChildren<PanelTransition>(true) ??
               GetComponentInParent<PanelTransition>(true);
    }

    private bool IsBackButton()
    {
        return transform.name.Equals(BackButtonName, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldUseCloseTransition()
    {
        return useCloseTransition && panelTransition != null;
    }

    protected override void OnPointerDownHandled(PointerEventData eventData)
    {
        if (unityButton != null || isChangingPanel)
            return;

        ChangePanelAsync();
    }

    private async void ChangePanelAsync()
    {
        isChangingPanel = true;

        try
        {
            UIManager uiManager = UIManager.Instance;

            if (uiManager == null)
            {
                Debug.LogWarning(
                    $"{nameof(ChangePanelButtonController)}: " +
                    $"{nameof(UIManager)}.{nameof(UIManager.Instance)} is null.",
                    this
                );
                return;
            }

            if (ShouldUseCloseTransition())
            {
                panelTransition ??= ResolvePanelTransition();

                if (panelTransition != null)
                {
                    await panelTransition.CloseAsync();
                }
                else
                {
                    Debug.LogWarning(
                        $"{nameof(ChangePanelButtonController)} on '{name}' could not find a " +
                        $"{nameof(PanelTransition)} in its parent hierarchy. The panel will close without animation.",
                        this
                    );
                }
            }

            if (uiButtonModel.panelToHide != null)
                uiManager.ClosePanel(uiButtonModel.panelToHide);

            if (uiButtonModel.panelToOpen != null)
                uiManager.OpenPanel(uiButtonModel.panelToOpen);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            isChangingPanel = false;
        }
    }
}