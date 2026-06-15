using Core.Exploration.Items;
using Core.Exploration.Items.Currencies;
using UnityEngine;

public class CurrencySlot : MonoBehaviour
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

    private void HandleItemView(IStorageable storageable, int amount)
    {
        if (storageable is not IItem item) return;
        int total = inventorySystem.GetAmount(item);
        quantity.text = total.ToString();
    }
}
