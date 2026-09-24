using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenesManager : MonoBehaviour
{
    public static ScenesManager Instance { get; private set; }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Destrói só este componente para não levar junto outros scripts
            // no mesmo GameObject (ex.: SceneTransitionHandler no CombatScene).
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RestartScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        LoadSceneByName(currentScene.name);
    }

    public void LoadNextScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            LoadSceneByBuildIndex(nextSceneIndex);
        }
        else
        {
            LoadMainMenu();
        }
    }

    public void LoadPreviousScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int previousSceneIndex = currentSceneIndex - 1;
        if (previousSceneIndex >= 0)
        {
            LoadSceneByBuildIndex(previousSceneIndex);
        }
    }

    public void LoadSceneByName(string sceneName)
    {
        if (SceneTransitionHandler.IsTransitioning) return;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[ScenesManager] Nome de cena vazio — load cancelado.");
            return;
        }

        Time.timeScale = 1f;
        SceneTransitionHandler.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void LoadMainMenu()
    {
        LoadSceneByName("_MainMenu");
    }

    public void LoadSceneByBuildIndex(int buildIndex)
    {
        if (SceneTransitionHandler.IsTransitioning) return;
        Time.timeScale = 1f;
        SceneTransitionHandler.LoadScene(buildIndex);
    }

    public String GetCurrentLevelName()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        return currentScene.name;
    }
}
