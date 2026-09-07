using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Erumperem.Progression
{
    public enum SkillTreeNodeVisualState
    {
        Locked,
        AvailableToUnlock,
        Unlocked,
    }

    /// <summary>
    /// Add manually to each existing skill button; assign the matching <see cref="SkillTreeNodeAsset"/>.
    /// Visual decisions (colors, etc.) live in <see cref="SkillTreeView"/>; this script only forwards them.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class SkillTreeNodePresenter : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private SkillTreeNodeAsset _nodeAsset;

        private Button _button;
        private Image _resolvedTintTargetImage;
        private SkillTreeView _owner;

        public SkillTreeNodeAsset NodeAsset => _nodeAsset;

        private void Awake()
        {
            _button = GetComponent<Button>();
            ResolveTintTargetIfMissing();
        }

        private void ResolveTintTargetIfMissing()
        {
            if (_resolvedTintTargetImage != null || _button == null)
            {
                return;
            }

            _resolvedTintTargetImage = _button.targetGraphic as Image;
            if (_resolvedTintTargetImage == null)
            {
                _resolvedTintTargetImage = _button.image;
            }
        }

        internal void BindToOwner(SkillTreeView owner)
        {
            _owner = owner;
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }

            ResolveTintTargetIfMissing();

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonClicked);
                _button.onClick.AddListener(OnButtonClicked);
            }
        }

        internal void ApplyVisualState(SkillTreeNodeVisualState state, Color tintColor)
        {
            if (_button != null)
            {
                var canUnlock = state == SkillTreeNodeVisualState.AvailableToUnlock;
                // Overworld SkillTreeNonInteractPanel disables Buttons in the scene;
                // re-enable the component so Available nodes can actually unlock.
                _button.enabled = true;
                _button.interactable = canUnlock;
            }

            if (_resolvedTintTargetImage != null)
            {
                _resolvedTintTargetImage.color = tintColor;
            }
        }

        private void OnButtonClicked()
        {
            if (_owner == null || _nodeAsset == null)
            {
                return;
            }

            _owner.NotifyNodePointerActivated(_nodeAsset);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_owner == null || _nodeAsset == null)
            {
                return;
            }

            _owner.ShowDetails(_nodeAsset);
        }
    }
}
