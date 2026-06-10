using Core.Exploration.Items;
using Core.Exploration.Items.Currencies;
using TMPro;
using UnityEngine;

public class PlayerCurrencyView : MonoBehaviour
{
    [SerializeField] private AnomalousArtifact _currency;
    [SerializeField] private PlayerInventorySystem _inventorySystem;
    [SerializeField] private TMP_Text _quantity;

    private void Awake()
    {
        _quantity.text = "0";
        _inventorySystem.OnItemAdded   += OnInventoryChanged;
        _inventorySystem.OnItemRemoved += OnInventoryChanged;
        UpdateView();
    }

    private void OnDestroy()
    {
        _inventorySystem.OnItemAdded   -= OnInventoryChanged;
        _inventorySystem.OnItemRemoved -= OnInventoryChanged;
    }

    private void OnInventoryChanged(IStorageable item, int _)
    {
        if (item == _currency)
            UpdateView();
    }

    private void UpdateView()
    {
        _quantity.text = _inventorySystem.GetAmount(_currency).ToString();
    }
}