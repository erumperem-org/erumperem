using Core.Economy.Currency;
using Core.Exploration.Items;
using Core.Rewards.Distribution;
using Core.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Rewards.UI
{
    /// <summary>
    /// One reward element on screen: icon + amount. Display only — it never
    /// touches wallet or inventory. Turns red when part (or all) of the
    /// reward could not be stored.
    /// </summary>
    public sealed class RewardView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _amountText;

        [Tooltip("Graphics tinted when the reward was rejected. Empty = every Graphic in this prefab.")]
        [SerializeField] private Graphic[] _tintTargets;

        [SerializeField] private Color _rejectedColor = Color.red;

        private Color[] _originalColors;

        private void Awake()
        {
            if (_tintTargets == null || _tintTargets.Length == 0)
                _tintTargets = GetComponentsInChildren<Graphic>(true);

            _originalColors = new Color[_tintTargets.Length];
            for (int i = 0; i < _tintTargets.Length; i++)
                _originalColors[i] = _tintTargets[i] != null ? _tintTargets[i].color : Color.white;
        }

        public void Show(RewardLine line)
        {
            if (_originalColors == null) Awake(); // in case Show runs before Awake

            if (_icon != null)
            {
                _icon.sprite = ResolveSprite(line.Storageable);
                _icon.enabled = _icon.sprite != null;
            }

            if (_amountText != null)
                _amountText.text = $"x{line.Requested}";

            if (_nameText != null)
                if(line.Storageable is IIITem item)
                    _nameText.text = item.DisplayName;
                else if(line.Storageable is ICoin coin)
                    _nameText.text = coin.DisplayName;
                else
                    _nameText.text = line.Storageable.GetType().Name;
            ApplyTint(line.HasRejection);
        }

        private void ApplyTint(bool rejected)
        {
            for (int i = 0; i < _tintTargets.Length; i++)
            {
                if (_tintTargets[i] == null) continue;
                _tintTargets[i].color = rejected ? _rejectedColor : _originalColors[i];
            }
        }

        private static Sprite ResolveSprite(InterfaceStorageable storageable) => storageable switch
        {
            ICoin coin => coin.Sprite,
            IIITem item => item.Sprite,
            _ => null
        };
    }
}
