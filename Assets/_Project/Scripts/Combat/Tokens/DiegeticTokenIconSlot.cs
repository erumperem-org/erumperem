using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Erumperem.Combat.Tokens
{
    /// <summary>
    /// One horizontal cell: icon + stack label (+ opcional tooltip via <see cref="TokenIconHoverDescriptionView"/>).
    /// </summary>
    public sealed class DiegeticTokenIconSlot : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI stackLabel;

        [Tooltip("Opcional: se o prefab tem um tooltip animado, esta referência é resolvida em Awake.")]
        [SerializeField] private TokenIconHoverDescriptionView hoverDescriptionView;

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

            if (hoverDescriptionView == null)
            {
                hoverDescriptionView = GetComponent<TokenIconHoverDescriptionView>();
            }
        }

        public void ApplyVisual(
            Sprite sprite,
            Color spriteColor,
            Color backgroundColor,
            int stacks,
            bool showBackgroundTint,
            string authoredHoverDescription = null)
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

            if (hoverDescriptionView != null)
            {
                hoverDescriptionView.Configure(authoredHoverDescription);
            }
        }

        public void SetCellActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}
