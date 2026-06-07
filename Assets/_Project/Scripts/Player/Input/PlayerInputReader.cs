using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("Input Action Asset")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Action Map")]
        [SerializeField] private string _actionMapName = "Player";
        [SerializeField] private string _moveActionName = "Move";
        [SerializeField] private string _interactActionName = "Interact";
        [SerializeField] private string _torchActionName = "Torch";

        public Vector2 MoveInput { get; private set; }
        public bool IsBlocked { get; set; }

        public event System.Action OnInteract;
        public event System.Action OnTorch;

        private InputActionMap _actionMap;
        private InputAction _moveAction;
        private InputAction _interactAction;
        private InputAction _torchAction;

        private System.Action _currentDetectionSystemInteract;
        private bool _initialized;

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_inputActions == null)
            {
                Debug.LogError("[PlayerInputReader] InputActionAsset não atribuído.", this);
                enabled = false;
                return;
            }

            _actionMap = _inputActions.FindActionMap(_actionMapName, throwIfNotFound: true);
            _moveAction = _actionMap.FindAction(_moveActionName, throwIfNotFound: true);
            _interactAction = _actionMap.FindAction(_interactActionName, throwIfNotFound: true);
            _torchAction = _actionMap.FindAction(_torchActionName, throwIfNotFound: true);

            // performed: dispara uma vez por press — requer que a Action
            // esteja configurada como "Button" no InputActionAsset.
            // Se estiver como "Value/Vector2", troque por WasPressedThisFrame() no Update.
            _interactAction.performed += _ =>
            {
                OnInteract?.Invoke();
            };
            _torchAction.performed += _ => OnTorch?.Invoke();

            _initialized = true;

            // Caso BindDetectionSystem tenha sido chamado antes do Awake,
            // reaplicamos o bind pendente agora que as actions existem.
            if (_pendingDetectionSystem != null)
            {
                BindDetectionSystem(_pendingDetectionSystem);
                _pendingDetectionSystem = null;
            }
        }

        private void OnEnable() => _actionMap?.Enable();

        private void OnDisable()
        {
            _actionMap?.Disable();
            MoveInput = Vector2.zero;
        }

        private void Update()
        {
            MoveInput = (!IsBlocked && _moveAction != null)
                ? _moveAction.ReadValue<Vector2>()
                : Vector2.zero;
        }

        // ── API pública ───────────────────────────────────────────────────

        [SerializeField] private PlayerDetectionSystem _pendingDetectionSystem;

        public void BindDetectionSystem(PlayerDetectionSystem detectionSystem)
        {
            if (!_initialized)
            {
                // Awake ainda não rodou — guarda para aplicar depois.
                _pendingDetectionSystem = detectionSystem;
                return;
            }

            OnInteract -= _currentDetectionSystemInteract;
            _currentDetectionSystemInteract = detectionSystem != null
                ? detectionSystem.Interact
                : (System.Action)null;
            OnInteract += _currentDetectionSystemInteract;
        }
    }
}