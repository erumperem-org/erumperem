using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections;
using UnityEngine.UI;

public class SceneTransitionHandler : MonoBehaviour
{
    [Header("Configurações do Fade")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 1f;

    [Header("Cores do Fade")]
    public Color fadeColor = Color.black;

    [Header("Controles Manuais")]
    public bool autoFadeOutOnDestroy = false;

    private static SceneTransitionHandler instance;
    private static bool isSceneLoadInProgress;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;

            // Mantém este objeto entre cenas
            DontDestroyOnLoad(gameObject);

            if (fadeCanvasGroup == null)
            {
                CreateFadeCanvasGroup();
            }

            StartCoroutine(FadeIn());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(FadeIn());
    }

    void CreateFadeCanvasGroup()
    {
        GameObject fadeCanvasGO = new GameObject("FadeCanvas");

        // Mantém o canvas entre cenas
        DontDestroyOnLoad(fadeCanvasGO);

        Canvas canvas = fadeCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvas.pixelPerfect = false;

        CanvasScaler canvasScaler = fadeCanvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject fadeImageGO = new GameObject("FadeImage");
        fadeImageGO.transform.SetParent(fadeCanvasGO.transform, false);

        Image image = fadeImageGO.AddComponent<Image>();
        image.color = fadeColor;
        image.raycastTarget = false;

        RectTransform rectTransform = fadeImageGO.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = Vector2.one * 0.5f;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;

        fadeCanvasGroup = fadeImageGO.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 1f;
    }

    IEnumerator FadeIn()
    {
        if (fadeCanvasGroup == null)
            yield break;

        fadeCanvasGroup.alpha = 1f;

        yield return null;

        if (fadeCanvasGroup == null)
            yield break;

        yield return fadeCanvasGroup
            .DOFade(0f, fadeDuration)
            .WaitForCompletion();
    }

    public void FadeOut(float duration = -1f)
    {
        if (fadeCanvasGroup == null) return;

        float finalDuration = duration >= 0 ? duration : fadeDuration;

        fadeCanvasGroup.DOFade(1f, finalDuration);
    }

    public void FadeOutAndQuit()
    {
        StartCoroutine(FadeOutAndQuitCoroutine());
    }

    public void FadeOutAndQuit(float customDuration)
    {
        StartCoroutine(FadeOutAndQuitCoroutine(customDuration));
    }

    public void FadeOutAndReloadScene()
    {
        StartCoroutine(FadeOutAndReloadSceneCoroutine());
    }

    private IEnumerator FadeOutAndQuitCoroutine(float customDuration = -1f)
    {
        float finalDuration = customDuration >= 0 ? customDuration : fadeDuration;

        yield return fadeCanvasGroup
            .DOFade(1f, finalDuration)
            .WaitForCompletion();

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator FadeOutAndReloadSceneCoroutine()
    {
        yield return fadeCanvasGroup
            .DOFade(1f, fadeDuration)
            .WaitForCompletion();

        string currentScene = SceneManager.GetActiveScene().name;

        SceneManager.LoadScene(currentScene);
    }

    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[SceneTransitionHandler] Nome de cena vazio — load cancelado.");
            return;
        }

        if (isSceneLoadInProgress)
        {
            Debug.LogWarning($"[SceneTransitionHandler] Load de '{sceneName}' ignorado — outro load já está em curso.");
            return;
        }

        if (instance != null)
        {
            isSceneLoadInProgress = true;
            instance.StartCoroutine(
                instance.LoadSceneWithFade(sceneName)
            );
            return;
        }

        isSceneLoadInProgress = true;
        SceneManager.LoadScene(sceneName);
        isSceneLoadInProgress = false;
    }

    public static void LoadScene(int sceneBuildIndex)
    {
        if (sceneBuildIndex < 0 || sceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"[SceneTransitionHandler] Build index inválido: {sceneBuildIndex}.");
            return;
        }

        if (isSceneLoadInProgress)
        {
            Debug.LogWarning($"[SceneTransitionHandler] Load do build index {sceneBuildIndex} ignorado — outro load já está em curso.");
            return;
        }

        if (instance != null)
        {
            isSceneLoadInProgress = true;
            instance.StartCoroutine(
                instance.LoadSceneWithFade(sceneBuildIndex)
            );
            return;
        }

        isSceneLoadInProgress = true;
        SceneManager.LoadScene(sceneBuildIndex);
        isSceneLoadInProgress = false;
    }

    IEnumerator LoadSceneWithFade(string sceneName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneTransitionHandler] Nome de cena vazio — load cancelado.");
                yield break;
            }

            if (fadeCanvasGroup != null)
            {
                yield return fadeCanvasGroup
                    .DOFade(1f, fadeDuration)
                    .WaitForCompletion();
            }

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (asyncLoad == null)
            {
                Debug.LogError($"[SceneTransitionHandler] Falha ao carregar cena '{sceneName}'.");
                yield break;
            }

            while (!asyncLoad.isDone)
                yield return null;
        }
        finally
        {
            isSceneLoadInProgress = false;
        }
    }

    IEnumerator LoadSceneWithFade(int sceneBuildIndex)
    {
        try
        {
            if (fadeCanvasGroup == null)
            {
                SceneManager.LoadScene(sceneBuildIndex);
                yield break;
            }

            yield return fadeCanvasGroup
                .DOFade(1f, fadeDuration)
                .WaitForCompletion();

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneBuildIndex, LoadSceneMode.Single);
            if (asyncLoad == null)
            {
                Debug.LogError($"[SceneTransitionHandler] Falha ao carregar build index {sceneBuildIndex}.");
                yield break;
            }

            while (!asyncLoad.isDone)
                yield return null;
        }
        finally
        {
            isSceneLoadInProgress = false;
        }
    }
}