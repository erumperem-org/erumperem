using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    /// <summary>
    /// Lê e expõe o input direcional do jogador via New Input System.
    /// Requer uma InputActionAsset com um Action Map "Player" contendo:
    ///   - "Move"     : Vector2 (WASD / Left Stick)
    ///   - "Interact" : Button
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("Input Action Asset")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Action Map")]
        [SerializeField] private string _actionMapName = "Player";
        [SerializeField] private string _moveActionName = "Move";
        [SerializeField] private string _interactionActionName = "Interact";
        [SerializeField] private string _torchActionName = "Torch";

        public PlayerDetectionSystem playerDetectionSystem;

        /// <summary>Input direcional normalizado (WASD / Left Stick).</summary>
        public Vector2 MoveInput { get; private set; }

        private InputActionMap _actionMap;
        private InputAction _moveAction;
        private InputAction _interactionAction;
        private InputAction _torchAction;

        public bool IsPlayerInteracting;
        private void Awake()
        {
            if (_inputActions == null)
            {
                Debug.LogError("[PlayerInputReader] InputActionAsset não atribuído no Inspector.", this);
                enabled = false;
                return;
            }

            _actionMap = _inputActions.FindActionMap(_actionMapName, throwIfNotFound: true);
            _moveAction = _actionMap.FindAction(_moveActionName, throwIfNotFound: true);
            _interactionAction = _actionMap.FindAction(_interactionActionName, throwIfNotFound: true);
            _torchAction = _actionMap.FindAction(_torchActionName, throwIfNotFound: true);
        }

        private void OnEnable() => _actionMap?.Enable();

        private void OnDisable()
        {
            _actionMap?.Disable();
            MoveInput = Vector2.zero;
        }

        private void Update()
        {
            if (!IsPlayerInteracting)
            {
                MoveInput = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;

                if (_interactionAction.IsPressed() && playerDetectionSystem != null)
                    playerDetectionSystem.Interact();
            }

        }

        public void ResetInputs()
        {
            _actionMap.Disable();
            _actionMap.Enable();
        }
    }
}
