using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Player
{
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

        [Header("Estado inicial")]
        [Tooltip("No overworld, o Main começa com a tocha acesa.")]
        [SerializeField] private bool _startsWithTorchOn = true;

        private bool _isOn;
        private bool _hasAppliedInitialTorchState;
        private PlayableCharactersManager _charactersManager;

        /// <summary>
        /// Disparado quando a tocha do Main é ligada (true) ou desligada (false).
        /// </summary>
        public static event Action<bool> OnMainTorchChanged;

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_animationController == null)
                _animationController = GetComponentInChildren<PlayableAnimationController>();

            if (_character == null)
                _character = GetComponent<PlayableCharacter>();

            if (_inputReader == null)
                _inputReader = GetComponent<PlayerInputReader>();

            if (_inputReader == null)
                _inputReader = FindFirstObjectByType<PlayerInputReader>();

            _isOn = _startsWithTorchOn;
            _charactersManager = FindFirstObjectByType<PlayableCharactersManager>();
        }

        private void OnEnable()
        {
            if (_inputReader != null)
                _inputReader.OnTorch += Toggle;

            if (_charactersManager != null)
                _charactersManager.OnMainChanged += HandleMainCharacterChanged;

            StartCoroutine(ApplyInitialTorchStateWhenReady());
        }

        private void OnDisable()
        {
            if (_inputReader != null)
                _inputReader.OnTorch -= Toggle;

            if (_charactersManager != null)
                _charactersManager.OnMainChanged -= HandleMainCharacterChanged;
        }

        // ── Lógica ───────────────────────────────────────────────────────

        private void HandleMainCharacterChanged(IPlayableCharacter mainCharacter)
        {
            if (gameObject.GetComponent<PlayableCharacter>().CurrentStateExposed == PlayableCharacterState.Companion)
            {
                DeactivateTorch();
            }

            if (mainCharacter is not PlayableCharacter playableCharacter || playableCharacter != _character)
            {
                return;
            }

            TryApplyInitialTorchStateForMain();
        }

        private IEnumerator ApplyInitialTorchStateWhenReady()
        {
            const int maxFramesToWait = 180;
            for (var frameIndex = 0; frameIndex < maxFramesToWait; frameIndex++)
            {
                if (TryApplyInitialTorchStateForMain())
                {
                    yield break;
                }

                yield return null;
            }
        }

        private bool TryApplyInitialTorchStateForMain()
        {
            if (_hasAppliedInitialTorchState || !_startsWithTorchOn || _character == null)
            {
                return _hasAppliedInitialTorchState;
            }

            if (_character.CurrentState != PlayableCharacterState.Main)
            {
                return false;
            }

            _hasAppliedInitialTorchState = true;
            _isOn = true;
            ApplyTorchVisuals(notifyEvent: true);
            return true;
        }

        private void Toggle()
        {
            if (_character == null || _inputReader == null)
            {
                return;
            }

            if (_character.CurrentState != PlayableCharacterState.Main)
            {
                return;
            }

            _isOn = !_isOn;
            ApplyTorchVisuals(notifyEvent: true);
        }

        private void ApplyTorchVisuals(bool notifyEvent = false)
        {
            if (_naturalLightObject != null)
                _naturalLightObject.SetActive(!_isOn);

            if (_handItensObject != null)
            {
                foreach (var handItemObject in _handItensObject)
                {
                    if (handItemObject != null)
                        handItemObject.SetActive(!_isOn);
                }
            }

            if (_torchObject != null)
                _torchObject.SetActive(_isOn);

            _animationController?.SetIsTorchOn(_isOn);

            if (notifyEvent && _character?.CurrentState == PlayableCharacterState.Main)
            {
                OnMainTorchChanged?.Invoke(_isOn);
            }
        }

        private void DeactivateTorch()
        {
            _isOn = false;

            if (_naturalLightObject != null)
                _naturalLightObject.SetActive(true);

            if (_handItensObject != null)
            {
                foreach (var handItemObject in _handItensObject)
                {
                    if (handItemObject != null)
                        handItemObject.SetActive(true);
                }
            }

            if (_torchObject != null)
                _torchObject.SetActive(false);

            _animationController?.SetIsTorchOn(false);
        }
    }
}