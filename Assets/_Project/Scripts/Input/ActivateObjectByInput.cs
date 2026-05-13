using UnityEngine;
using UnityEngine.InputSystem;

namespace Erumperem.Input
{
    /// <summary>
    /// Liga um <see cref="GameObject"/> da cena quando uma tecla configurável é pressionada.
    /// Usa o novo Input System por evento (sem polling em <c>Update</c>) e cria a sua própria
    /// <see cref="InputAction"/>, sem precisar de tocar no <see cref="InputManager"/> global.
    /// </summary>
    public sealed class ActivateObjectByInput : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Tecla que dispara a activação. Mude no Inspector.")]
        [SerializeField] private Key _activationKey = Key.E;

        [Header("Alvo")]
        [Tooltip("GameObject a activar/desactivar quando a tecla for pressionada.")]
        [SerializeField] private GameObject _objectToToggleActivation;

        [Tooltip("Se verdadeiro, alterna entre activo/inactivo a cada press. " +
                 "Se falso, força para o valor de 'Set Active To' a cada press.")]
        [SerializeField] private bool _toggleOnEachPress = false;

        [Tooltip("Estado aplicado ao GameObject quando 'Toggle On Each Press' está desligado.")]
        [SerializeField] private bool _setActiveTo = true;

        private InputAction _activationInputAction;

        private void OnEnable()
        {
            _activationInputAction = BuildActivationInputAction(_activationKey);
            _activationInputAction.performed += HandleActivationKeyPerformed;
            _activationInputAction.Enable();
        }

        private void OnDisable()
        {
            if (_activationInputAction == null)
            {
                return;
            }

            _activationInputAction.performed -= HandleActivationKeyPerformed;
            _activationInputAction.Disable();
            _activationInputAction.Dispose();
            _activationInputAction = null;
        }

        private void HandleActivationKeyPerformed(InputAction.CallbackContext callbackContext)
        {
            if (_objectToToggleActivation == null)
            {
                Debug.LogWarning(
                    $"{nameof(ActivateObjectByInput)} em '{name}': nenhum GameObject atribuído em 'Object To Toggle Activation'.",
                    this);
                return;
            }

            if (_toggleOnEachPress)
            {
                _objectToToggleActivation.SetActive(!_objectToToggleActivation.activeSelf);
                return;
            }

            _objectToToggleActivation.SetActive(_setActiveTo);
        }

        private static InputAction BuildActivationInputAction(Key activationKey)
        {
            var keyboardBindingPath = $"<Keyboard>/{activationKey.ToString().ToLowerInvariant()}";
            var inputAction = new InputAction(name: "ActivateObjectByInput", type: InputActionType.Button);
            inputAction.AddBinding(keyboardBindingPath);
            return inputAction;
        }
    }
}
