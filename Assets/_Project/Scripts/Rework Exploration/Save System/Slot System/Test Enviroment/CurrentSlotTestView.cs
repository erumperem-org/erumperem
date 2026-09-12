using UnityEngine;

public class CurrentSlotTestView : MonoBehaviour
{
    public CurrentSlotData data;
    public TMPro.TMP_Text text;
    public void UpdateText()
    {
        text.text = "Current Slot: " + data.SlotDirectory;
    }
}
