using UnityEngine;
public sealed class ScavengerShop : Interactable
{
    [Header("Shop Panel")]
    [SerializeField] private GameObject shopPanelRoot;
    [SerializeField] private GameObject HUD;

    [Header("Objetos para desativar enquanto o shop estiver aberto")]
    [SerializeField] private GameObject[] objectsToDisableWhileOpen;

    public override bool CanInteract => true;

    private bool _isShopOpen;
    private bool _isProcessingInteraction;

    protected override void Awake()
    {
        base.Awake();

        if (shopPanelRoot != null)
        {
            shopPanelRoot.SetActive(false);
        }
    }

    public override void ExecuteInteraction(InteractionContext context)
    {
        if (_isProcessingInteraction)
        {
            return;
        }

        if (shopPanelRoot == null)
        {
            return;
        }

        _isProcessingInteraction = true;
        try
        {
            OpenShop();
        }
        finally
        {
            _isProcessingInteraction = false;
        }
    }

    private void ToggleShop()
    {
        SetShopOpen(!_isShopOpen);
    }

    public void OpenShop()
    {
        SetShopOpen(true);
    }

    public void CloseShop()
    {
        SetShopOpen(false);
    }

    private void SetShopOpen(bool isOpen)
    {
        _isShopOpen = isOpen;

        shopPanelRoot.SetActive(isOpen);
        SetOtherObjectsActive(false);
    }

    private void SetOtherObjectsActive(bool isActive)
    {
        if (objectsToDisableWhileOpen == null)
        {
            return;
        }

        foreach (var obj in objectsToDisableWhileOpen)
        {
            if (obj == null)
            {
                continue;
            }

            obj.SetActive(isActive);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            SetOtherObjectsActive(false);
            shopPanelRoot.SetActive(false);
            HUD.SetActive(true);
        }
    }
}