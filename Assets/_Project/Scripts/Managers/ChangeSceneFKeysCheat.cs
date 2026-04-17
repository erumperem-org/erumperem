using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public sealed class ChangeSceneFKeysCheat : MonoBehaviour
{
    public static ChangeSceneFKeysCheat Instance { get; private set; }

    private const int F1BuildIndex = 0;
    private const int F2BuildIndex = 1;
    private const int F3BuildIndex = 2;

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

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.f1Key.wasPressedThisFrame)
        {
            TryLoadSceneByBuildIndex(F1BuildIndex);
        }
        else if (keyboard.f2Key.wasPressedThisFrame)
        {
            TryLoadSceneByBuildIndex(F2BuildIndex);
        }
        else if (keyboard.f3Key.wasPressedThisFrame)
        {
            TryLoadSceneByBuildIndex(F3BuildIndex);
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
