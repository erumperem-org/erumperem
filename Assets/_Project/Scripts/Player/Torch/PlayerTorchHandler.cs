using System.Collections.Generic;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Controla a tocha do personagem jogável.
    ///
    /// Recebe o toggle via <see cref="PlayerInputReader.OnTorch"/> — não lê
    /// InputActionAsset diretamente, eliminando o input duplicado.
    ///
    /// Só age quando o personagem é Main ou Companion.
    /// </summary>
    public sealed class PlayerTorchHandler : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private GameObject _torchObject;
        [SerializeField] private GameObject _naturalLightObject;
#nullable enable
        [SerializeField] private List<GameObject> _handItensObject;
#nullable disable

        [SerializeField] private PlayableAnimationController _animationController;
        [SerializeField] private PlayableCharacter _character;
        [SerializeField] private PlayerInputReader _inputReader;

        private bool _isOn;

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_animationController == null)
            {
                _animationController = GetComponentInChildren<PlayableAnimationController>();
            }

            if (_inputReader == null)
            {
                _inputReader = GetComponent<PlayerInputReader>();
            }
        }

        private void OnEnable()
        {
            if (_inputReader == null) return;
            _inputReader.OnTorch += Toggle;
        }

        private void OnDisable()
        {
            if (_inputReader == null) return;
            _inputReader.OnTorch -= Toggle;
        }

        // ── Lógica ───────────────────────────────────────────────────────

        private void Toggle()
        {
            var state = _character.CurrentState;
            if (state != PlayableCharacterState.Main && state != PlayableCharacterState.Companion)
                return;

            _isOn = !_isOn;
            _naturalLightObject.SetActive(!_isOn);
            foreach (var itens in _handItensObject)
            {
                itens.SetActive(!_isOn);
            }
            _torchObject.SetActive(_isOn);
            _animationController?.SetIsTorchOn(_isOn);
        }
    }
}
