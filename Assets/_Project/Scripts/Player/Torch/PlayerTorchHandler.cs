using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerTorchHandler : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private GameObject torchObject;
        [SerializeField] private Animator animator;

        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private string _actionMapName = "Player";
        [SerializeField] private string _torchActionName = "Torch";

        private static readonly int IsTorchOn = Animator.StringToHash("IsTorchOn");

        private InputActionMap _actionMap;
        private InputAction _torchAction;
        private bool _isOn;

        private void Awake()
        {
            _actionMap = _inputActions.FindActionMap(_actionMapName, throwIfNotFound: true);
            _torchAction = _actionMap.FindAction(_torchActionName, throwIfNotFound: true);

            _torchAction.performed += OnTorchPerformed;
        }

        private void OnEnable() => _actionMap?.Enable();

        private void OnDisable() => _actionMap?.Disable();

        private void OnDestroy() => _torchAction.performed -= OnTorchPerformed;

        private void OnTorchPerformed(InputAction.CallbackContext _)
        {
            _isOn = !_isOn;
            torchObject.SetActive(_isOn);
            animator.SetBool(IsTorchOn, _isOn);
        }
    }
}