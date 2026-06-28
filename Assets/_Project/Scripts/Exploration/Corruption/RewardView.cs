using Core.Exploration.Items;
using Core.Exploration.Items.Currencies;
using UnityEngine;
using UnityEngine.UI;

public class RewardView : MonoBehaviour
{
    public Image icon;
    public TMPro.TMP_Text rewardName, quantity;

    public void UpdateView(IStorageable storageable, int quantity)
    {
        this.rewardName.text = storageable is UnityEngine.Object obj ? obj.name : storageable.GetType().Name;
        this.quantity.text = quantity.ToString();
        if (storageable is IItem item) {this.icon.sprite = item.Sprite; }
        if (storageable is AnomalousArtifact artifact) {this.icon.sprite = artifact.Sprite; }
    }
}
