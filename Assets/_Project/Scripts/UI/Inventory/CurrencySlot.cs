using Core.Exploration.Items;
using Core.Exploration.Items.Currencies;
using UnityEngine;

public class DeterministicInventorySlotView : MonoBehaviour
{
    public ScriptableObject item;
    public PlayerInventorySystem inventorySystem;
    public UnityEngine.UI.Image icon;
    public TMPro.TMP_Text quantity;

    void Awake()
    {
        if (item is AnomalousArtifact item1)
        {
            inventorySystem.OnItemAdded += HandleItemView;
            inventorySystem.OnItemRemoved += HandleItemView;
            icon.sprite = item1.Sprite;
            quantity.text = "0";
        }
    }

    void OnEnable()
    {
        if (this.item is AnomalousArtifact item)
        {
            int total = inventorySystem.GetAmount(item);
            quantity.text = total.ToString();
        }
    }
    void OnDestroy()
    {
        if (item is AnomalousArtifact item1)
        {
            inventorySystem.OnItemAdded -= HandleItemView;
            inventorySystem.OnItemRemoved -= HandleItemView;
        }
    }

    private void HandleItemView(IStorageable storageable, int amount)
    {
        if (storageable is not AnomalousArtifact item) return;
        if (this.item is AnomalousArtifact itemTest)
        {
            if(storageable != this.item ){return;}
        }
        
        int total = inventorySystem.GetAmount(item);
        quantity.text = total.ToString();
    }
}
