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

            if (_character == null)
            {
                _character = GetComponent<PlayableCharacter>();
            }

            if (_inputReader == null)
            {
                _inputReader = GetComponent<PlayerInputReader>();
            }

            if (_inputReader == null)
            {
                _inputReader = FindFirstObjectByType<PlayerInputReader>();
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
            if (_character == null || _inputReader == null)
            {
                return;
            }

            var state = _character.CurrentState;
            if (state != PlayableCharacterState.Main && state != PlayableCharacterState.Companion)
                return;

            _isOn = !_isOn;
            if (_naturalLightObject != null)
            {
                _naturalLightObject.SetActive(!_isOn);
            }

            if (_handItensObject != null)
            {
                foreach (var handItemObject in _handItensObject)
                {
                    if (handItemObject != null)
                    {
                        handItemObject.SetActive(!_isOn);
                    }
                }
            }

            if (_torchObject != null)
            {
                _torchObject.SetActive(_isOn);
            }

            _animationController?.SetIsTorchOn(_isOn);
        }
    }
}
