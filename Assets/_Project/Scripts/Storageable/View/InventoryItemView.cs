using TMPro;
using UnityEngine;

public class InventoryItemView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventorySystem inventory;
    [SerializeField] private ScriptableObject targetItem;
    [SerializeField] private TMP_Text amountText;

    private IStorageable _target;
    private int _currentAmount;

    private void Awake()
    {
        _target = targetItem as IStorageable;

        if (_target == null)
            Debug.LogWarning($"[InventoryItemView] '{targetItem?.name}' não implementa IStorageable.", this);
    }

    private void OnEnable()
    {
        inventory.OnItemAdded   += HandleItemAdded;
        inventory.OnItemRemoved += HandleItemRemoved;
        Refresh();
    }

    private void OnDisable()
    {
        inventory.OnItemAdded   -= HandleItemAdded;
        inventory.OnItemRemoved -= HandleItemRemoved;
    }

    private void HandleItemAdded(IStorageable item, int amount)
    {
        if (_target == null || item != _target) return;
        _currentAmount += amount;
        UpdateText();
    }

    private void HandleItemRemoved(IStorageable item, int amount)
    {
        if (_target == null || item != _target) return;
        _currentAmount = Mathf.Max(0, _currentAmount - amount);
        UpdateText();
    }

    private void Refresh()
    {
        if (_target == null) return;
        _currentAmount = inventory.GetAmount(_target);
        UpdateText();
    }

    private void UpdateText() => amountText.text = _currentAmount.ToString();
}
