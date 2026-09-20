using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class GlobalUiKeyboardNavigation : MonoBehaviour
{
    private static GlobalUiKeyboardNavigation _instance;

    [Header("Navigation")]
    [SerializeField, Range(0.1f, 1f)] private float navigationThreshold = 0.55f;
    [SerializeField, Min(0f)] private float firstRepeatDelay = 0.35f;
    [SerializeField, Min(0.02f)] private float repeatInterval = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugActivation;

    private UiKeyboardInputReader _inputReader;
    private UiNavigationSession _navigationSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (FindFirstObjectByType<GlobalUiKeyboardNavigation>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        var navigationObject = new GameObject(nameof(GlobalUiKeyboardNavigation));
        navigationObject.AddComponent<GlobalUiKeyboardNavigation>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _inputReader = new UiKeyboardInputReader(navigationThreshold, firstRepeatDelay, repeatInterval);
        _navigationSession = new UiNavigationSession(debugActivation);
    }

    private void OnEnable()
    {
        if (_inputReader == null || _navigationSession == null)
        {
            return;
        }

        _inputReader.NavigateRequested += HandleNavigateRequested;
        _inputReader.SubmitRequested += HandleSubmitRequested;
        _inputReader.CancelRequested += HandleCancelRequested;
        _inputReader.Enable();
    }

    private void OnDisable()
    {
        if (_inputReader != null)
        {
            _inputReader.NavigateRequested -= HandleNavigateRequested;
            _inputReader.SubmitRequested -= HandleSubmitRequested;
            _inputReader.CancelRequested -= HandleCancelRequested;
            _inputReader.Disable();
        }

        _navigationSession?.Shutdown();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }

        _inputReader?.Dispose();
        _inputReader = null;
        _navigationSession = null;
    }

    private void Update()
    {
        if (_inputReader == null || _navigationSession == null)
        {
            return;
        }

        _navigationSession.Tick();
        _inputReader.Tick(_navigationSession.HasActivePanel);
    }

    private void HandleNavigateRequested(UiNavigationDirection direction)
    {
        _navigationSession?.Navigate(direction);
    }

    private void HandleSubmitRequested()
    {
        _navigationSession?.Confirm();
    }

    private void HandleCancelRequested()
    {
        _navigationSession?.Cancel();
    }
}
