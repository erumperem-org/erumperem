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

        [Header("Ícone (Opcional - resolvido automaticamente se não atribuído)")]
        [Tooltip("Sobrescreve diretamente o sprite de ícone exibido no painel de detalhes.")]
        [SerializeField] private Sprite _iconOverride;
        [Tooltip("Componente Image específico que contém o ícone da habilidade neste botão.")]
        [SerializeField] private Image _iconImage;

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

            _owner.ShowDetails(_nodeAsset, GetNodeIcon());
        }

        /// <summary>
        /// Obtém o Sprite de ícone desta habilidade a partir de override, da imagem do botão,
        /// de um filho 'Icon' ou do próprio nó.
        /// </summary>
        public Sprite GetNodeIcon()
        {
            if (_iconOverride != null)
            {
                return _iconOverride;
            }

            if (_iconImage != null && _iconImage.sprite != null)
            {
                return _iconImage.sprite;
            }

            var childIconTransform = transform.Find("Icon");
            if (childIconTransform != null && childIconTransform.TryGetComponent<Image>(out var childImg) && childImg.sprite != null)
            {
                return childImg.sprite;
            }

            ResolveTintTargetIfMissing();
            if (_resolvedTintTargetImage != null && _resolvedTintTargetImage.sprite != null)
            {
                return _resolvedTintTargetImage.sprite;
            }

            if (_button != null && _button.image != null && _button.image.sprite != null)
            {
                return _button.image.sprite;
            }

            return _nodeAsset != null ? _nodeAsset.Icon : null;
        }
    }
}