using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections;
using UnityEngine.UI;

public class SceneTransitionHandler : MonoBehaviour
{
    [Header("Configura��es do Fade")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 1f;

    [Header("Cores do Fade")]
    public Color fadeColor = Color.black;

    private static SceneTransitionHandler instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
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

    void CreateFadeCanvasGroup()
    {
        GameObject fadeCanvasGO = new GameObject("FadeCanvas");
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

        UnityEngine.UI.Image image = fadeImageGO.AddComponent<UnityEngine.UI.Image>();
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
        fadeCanvasGroup.alpha = 1f;
        yield return null; 

        yield return fadeCanvasGroup.DOFade(0f, fadeDuration).WaitForCompletion();
    }

    [Header("Controles Manuais")]
    public bool autoFadeOutOnDestroy = false; 

    public void FadeOutAndQuit()
    {
        StartCoroutine(FadeOutAndQuitCoroutine());
    }

    public void FadeOutAndQuit(float customDuration)
    {
        StartCoroutine(FadeOutAndQuitCoroutine(customDuration));
    }

    public void FadeOut(float duration = -1f)
    {
        float finalDuration = duration >= 0 ? duration : fadeDuration;
        fadeCanvasGroup.DOFade(1f, finalDuration);
    }
    public void FadeOutAndReloadScene()
    {
        StartCoroutine(FadeOutAndReloadSceneCoroutine());
    }

    private IEnumerator FadeOutAndQuitCoroutine(float customDuration = -1f)
    {
        float finalDuration = customDuration >= 0 ? customDuration : fadeDuration;

        yield return fadeCanvasGroup.DOFade(1f, finalDuration).WaitForCompletion();

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator FadeOutAndReloadSceneCoroutine()
    {
        yield return fadeCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();

        string currentScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentScene);
    }

    public static void LoadScene(string sceneName)
    {
        if (instance != null)
        {
            instance.StartCoroutine(instance.LoadSceneWithFade(sceneName));
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    public static void LoadScene(int sceneBuildIndex)
    {
        if (instance != null)
        {
            instance.StartCoroutine(instance.LoadSceneWithFade(sceneBuildIndex));
        }
        else
        {
            SceneManager.LoadScene(sceneBuildIndex);
        }
    }

    IEnumerator LoadSceneWithFade(string sceneName)
    {
        yield return fadeCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

    IEnumerator LoadSceneWithFade(int sceneBuildIndex)
    {
        yield return fadeCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneBuildIndex);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }
}