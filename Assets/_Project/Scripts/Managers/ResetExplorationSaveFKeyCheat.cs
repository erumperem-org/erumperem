using UnityEngine;

/// <summary>
/// Cheat F5: mesmo comportamento que o botão Reset Save (<see cref="ExplorationDataManagement.ResetExplorationContext"/>).
/// </summary>
public sealed class ResetExplorationSaveFKeyCheat : MonoBehaviour
{
    public static ResetExplorationSaveFKeyCheat Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureResetExplorationSaveFKeyCheatExists()
    {
        if (Instance != null)
        {
            return;
        }

        var cheatGameObject = new GameObject(nameof(ResetExplorationSaveFKeyCheat));
        cheatGameObject.AddComponent<ResetExplorationSaveFKeyCheat>();
    }

    private bool _isSubscribedToInputManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable() => TrySubscribeToInputManager();

    private void Start() => TrySubscribeToInputManager();

    private void OnDisable() => UnsubscribeFromInputManager();

    private void OnDestroy()
    {
        UnsubscribeFromInputManager();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void TrySubscribeToInputManager()
    {
        if (_isSubscribedToInputManager || InputManager.Instance == null)
        {
            return;
        }

        InputManager.Instance.OnExplorationCheatResetSavePressed += HandleExplorationCheatResetSavePressed;
        _isSubscribedToInputManager = true;
    }

    private void UnsubscribeFromInputManager()
    {
        if (!_isSubscribedToInputManager || InputManager.Instance == null)
        {
            return;
        }

        InputManager.Instance.OnExplorationCheatResetSavePressed -= HandleExplorationCheatResetSavePressed;
        _isSubscribedToInputManager = false;
    }

    private static void HandleExplorationCheatResetSavePressed()
    {
        Debug.Log("Cheat F5 acionado: reset do save de exploração.");
        ExplorationDataManagement.ResetExplorationSave();
        ExplorationDataManagement.ResetInventorySave();
    }
}
