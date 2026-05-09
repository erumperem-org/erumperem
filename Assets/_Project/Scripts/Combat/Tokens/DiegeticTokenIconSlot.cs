using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Erumperem.Combat.Tokens
{
    /// <summary>
    /// One horizontal cell: icon + stack label. Parent should sit under a HorizontalLayoutGroup.
    /// </summary>
    public sealed class DiegeticTokenIconSlot : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI stackLabel;

        private void Awake()
        {
            if (iconImage == null)
            {
                iconImage = GetComponent<Image>();
            }

            if (stackLabel == null)
            {
                stackLabel = GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        public void ApplyVisual(Sprite sprite, Color spriteColor, Color backgroundColor, int stacks, bool showBackgroundTint)
        {
            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.color = spriteColor;
                iconImage.enabled = sprite != null;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = backgroundColor;
                backgroundImage.enabled = showBackgroundTint;
            }

            if (stackLabel != null)
            {
                stackLabel.text = stacks.ToString();
                stackLabel.enabled = true;
            }
        }

        public void SetCellActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}
