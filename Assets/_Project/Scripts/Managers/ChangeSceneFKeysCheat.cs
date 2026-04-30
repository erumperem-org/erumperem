using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ChangeSceneFKeysCheat : MonoBehaviour
{
    public static ChangeSceneFKeysCheat Instance { get; private set; }

    private const int F1BuildIndex = 0;
    private const int F2BuildIndex = 1;
    private const int F3BuildIndex = 2;
    private const int F4BuildIndex = 3;
    private const int F5BuildIndex = 4;

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

    private void OnEnable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnSceneCheatPressed += HandleSceneCheatPressed;
        }
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnSceneCheatPressed -= HandleSceneCheatPressed;
        }
    }

    private void HandleSceneCheatPressed(int sceneSlotIndex)
    {
        switch (sceneSlotIndex)
        {
            case 0:
                TryLoadSceneByBuildIndex(F1BuildIndex);
                break;
            case 1:
                TryLoadSceneByBuildIndex(F2BuildIndex);
                break;
            case 2:
                TryLoadSceneByBuildIndex(F3BuildIndex);
                break; 
            case 3:
                TryLoadSceneByBuildIndex(F4BuildIndex);
                break;
            case 4:
                TryLoadSceneByBuildIndex(F5BuildIndex);
                break;
        }
    }

    private static void TryLoadSceneByBuildIndex(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning($"ChangeSceneFKeysCheat: build index {buildIndex} nao existe no Build Settings.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex);
    }
}
