using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.Collections;
using Unity.Cinemachine;

public class SceneTransitionHandler : MonoBehaviour
{
    [Header("Configurações do Fade")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 1f;

    [Header("Cores do Fade")]
    public Color fadeColor = Color.black;

    [Header("Controles Manuais")]
    public bool autoFadeOutOnDestroy = false;

    [Header("Combat Transition")]
    [SerializeField, Min(0f)] private float _combatHitStopSeconds = 0.08f;
    [SerializeField, Min(0.01f)] private float _combatZoomSeconds = 0.28f;
    [SerializeField, Min(0.01f)] private float _combatFadeSeconds = 0.25f;
    [SerializeField, Min(0.01f)] private float _sceneRevealSeconds = 0.35f;
    [SerializeField, Range(0.5f, 1f)] private float _combatZoomRatio = 0.88f;
    [SerializeField, Range(-10f, 10f)] private float _combatCameraRoll = -3f;

    private static SceneTransitionHandler instance;
    private static bool isSceneLoadInProgress;
    public static bool IsTransitioning => isSceneLoadInProgress || (instance != null && instance._isRevealing);

    private bool _isRevealing;
    private Coroutine _revealCoroutine;
    private Sequence _cameraTransition;
    private Camera _transitionCamera;
    private CinemachineBrain _transitionBrain;
    private bool _brainWasEnabled;
    private float _originalFieldOfView;
    private float _originalOrthographicSize;
    private Quaternion _originalCameraRotation;
    private float _previousTimeScale;
    private bool _ownsTimeScale;
    private Sequence _screenAudioTransition;
    private float _originalListenerVolume;
    private bool _ownsListenerVolume;

    public static bool IsSceneLoadInProgress => isSceneLoadInProgress;

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

            var canvas = fadeCanvasGroup.GetComponentInParent<Canvas>();
            if (canvas != null) canvas.sortingOrder = short.MaxValue;
            if (fadeCanvasGroup.transform is RectTransform fadeRect)
            {
                fadeRect.localScale = Vector3.one;
                fadeRect.anchorMin = Vector2.zero;
                fadeRect.anchorMax = Vector2.one;
                fadeRect.offsetMin = Vector2.zero;
                fadeRect.offsetMax = Vector2.zero;
            }

            _revealCoroutine = StartCoroutine(FadeIn());
        }
        else
        {
            Destroy(this);
        }
    }

    void OnEnable()
    {
        if (instance == this) SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance != this) return;
        StopAllCoroutines();
        RestoreTransitionState();
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.DOKill();
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
        isSceneLoadInProgress = false;
        _isRevealing = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isSceneLoadInProgress) return;
        if (_revealCoroutine != null) StopCoroutine(_revealCoroutine);
        _revealCoroutine = StartCoroutine(FadeIn());
    }

    void CreateFadeCanvasGroup()
    {
        GameObject fadeCanvasGO = new GameObject("FadeCanvas");
        fadeCanvasGO.transform.SetParent(transform, false);

        Canvas canvas = fadeCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvas.pixelPerfect = false;

        var canvasScaler = fadeCanvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject fadeImageGO = new GameObject("FadeImage");
        fadeImageGO.transform.SetParent(fadeCanvasGO.transform, false);

        var image = fadeImageGO.AddComponent<UnityEngine.UI.Image>();
        image.color = fadeColor;
        image.raycastTarget = true;
        fadeCanvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

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

        _isRevealing = true;
        fadeCanvasGroup.DOKill();
        fadeCanvasGroup.alpha = 1f;
        fadeCanvasGroup.blocksRaycasts = true;
        yield return WaitForSceneReady();

        if (fadeCanvasGroup == null)
            yield break;

        string sceneName = SceneManager.GetActiveScene().name;
        float revealDuration = sceneName == "CombatScene" || sceneName == "Overworld"
            || sceneName == "REWORKING_Overworld" ? _sceneRevealSeconds : fadeDuration;
        yield return fadeCanvasGroup
            .DOFade(0f, revealDuration).SetUpdate(true)
            .WaitForCompletion();
        fadeCanvasGroup.blocksRaycasts = false;
        _isRevealing = false;
        _revealCoroutine = null;
    }

    public void FadeOut(float duration = -1f)
    {
        if (fadeCanvasGroup == null) return;

        float finalDuration = duration >= 0 ? duration : fadeDuration;

        fadeCanvasGroup.DOKill();
        fadeCanvasGroup.DOFade(1f, finalDuration).SetUpdate(true);
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
            .SetUpdate(true)
            .WaitForCompletion();

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator FadeOutAndReloadSceneCoroutine()
    {
        LoadScene(SceneManager.GetActiveScene().name);
        yield break;
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
            if (ScenesManager.IsCombatSceneName(sceneName))
            {
                CombatSceneLoadCoordinator.CancelCombatSceneLoadAttempt();
            }

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneTransitionHandler] Cena indisponível: {sceneName}.");
            return;
        }
        if (instance == null)
        {
            new GameObject(nameof(SceneTransitionHandler)).AddComponent<SceneTransitionHandler>();
            instance.fadeCanvasGroup.alpha = 0f;
        }
        isSceneLoadInProgress = true;
        if (instance._revealCoroutine != null) instance.StopCoroutine(instance._revealCoroutine);
        instance.StartCoroutine(instance.LoadSceneWithFade(sceneName));
    }

    public static void LoadScene(int sceneBuildIndex)
    {
        if (sceneBuildIndex < 0 || sceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"[SceneTransitionHandler] Build index inválido: {sceneBuildIndex}.");
            return;
        }

        LoadScene(SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex));
    }

    public static bool TryRunCoveredTransition(IEnumerator operation, float durationSeconds = 2f)
    {
        if (IsTransitioning || operation == null) return false;
        if (instance == null)
        {
            new GameObject(nameof(SceneTransitionHandler)).AddComponent<SceneTransitionHandler>();
            instance.fadeCanvasGroup.alpha = 0f;
        }
        if (instance._revealCoroutine != null) instance.StopCoroutine(instance._revealCoroutine);
        isSceneLoadInProgress = true;
        instance.StartCoroutine(instance.RunCoveredTransition(operation, durationSeconds));
        return true;
    }

    private IEnumerator RunCoveredTransition(IEnumerator operation, float durationSeconds)
    {
        float halfDuration = Mathf.Max(0f, durationSeconds) * 0.5f;
        _previousTimeScale = Time.timeScale;
        _ownsTimeScale = true;
        _originalListenerVolume = AudioListener.volume;
        _ownsListenerVolume = true;
        Time.timeScale = 0f;
        fadeCanvasGroup.DOKill();
        fadeCanvasGroup.blocksRaycasts = true;
        try
        {
            yield return FadeScreenAndAudio(1f, 0f, halfDuration);
            while (operation.MoveNext())
                yield return operation.Current;
            yield return WaitForSceneReady();
            yield return FadeScreenAndAudio(0f, _originalListenerVolume, halfDuration);
        }
        finally
        {
            (operation as System.IDisposable)?.Dispose();
            RestoreTransitionState();
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            isSceneLoadInProgress = false;
            _isRevealing = false;
        }
    }

    private IEnumerator FadeScreenAndAudio(float alpha, float volume, float duration)
    {
        _screenAudioTransition = DOTween.Sequence().SetUpdate(true);
        _screenAudioTransition.Append(fadeCanvasGroup.DOFade(alpha, duration).SetEase(Ease.InOutSine));
        _screenAudioTransition.Join(DOTween.To(() => AudioListener.volume,
            value => AudioListener.volume = value, volume, duration).SetEase(Ease.InOutSine));
        yield return _screenAudioTransition.WaitForCompletion();
    }

    IEnumerator LoadSceneWithFade(string sceneName)
    {
        bool enteringCombat = System.IO.Path.GetFileNameWithoutExtension(sceneName) == "CombatScene"
            && SceneManager.GetActiveScene().name != "CombatScene";
        float outgoingDuration = enteringCombat
            ? _combatHitStopSeconds + Mathf.Max(_combatZoomSeconds, _combatFadeSeconds)
            : SceneManager.GetActiveScene().name == "CombatScene" ? _combatFadeSeconds : fadeDuration;
        try
        {
            fadeCanvasGroup.DOKill();
            fadeCanvasGroup.blocksRaycasts = true;
            AudioManager.instance?.FadeOutBGM(outgoingDuration);

            if (enteringCombat)
            {
                yield return PlayCombatEntrance();
            }
            else
            {
                yield return fadeCanvasGroup.DOFade(1f, outgoingDuration).SetUpdate(true)
                    .WaitForCompletion();
            }

            AudioManager.instance?.StopAmbientLoop();
            if (ScenesManager.IsCombatSceneName(sceneName))
            {
                CombatOverworldFlowDiagnostics.LogPhase(
                    "SceneTransitionHandler",
                    $"LoadSceneAsync('{sceneName}') a iniciar (fade concluído)");
            }

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (asyncLoad == null)
            {
                CombatOverworldFlowDiagnostics.LogError(
                    "SceneTransitionHandler",
                    $"LoadSceneAsync falhou para '{sceneName}'");
                yield break;
            }

            while (!asyncLoad.isDone)
                yield return null;

            if (ScenesManager.IsCombatSceneName(sceneName))
            {
                CombatOverworldFlowDiagnostics.LogActiveScene("SceneTransitionHandler load completo");
            }

            RestoreTransitionState();
            yield return FadeIn();
        }
        finally
        {
            RestoreTransitionState();
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.DOKill();
                fadeCanvasGroup.alpha = 0f;
                fadeCanvasGroup.blocksRaycasts = false;
            }
            isSceneLoadInProgress = false;
            _isRevealing = false;

            if (ScenesManager.IsCombatSceneName(sceneName))
            {
                CombatSceneLoadCoordinator.NotifyCombatSceneLoadFinished(SceneManager.GetActiveScene());
            }
        }
    }

    private IEnumerator PlayCombatEntrance()
    {
        _previousTimeScale = Time.timeScale;
        _ownsTimeScale = true;
        Time.timeScale = 0f;
        _transitionCamera = Camera.main;
        _cameraTransition = DOTween.Sequence().SetUpdate(true);
        _cameraTransition.AppendInterval(_combatHitStopSeconds);
        if (_transitionCamera != null)
        {
            _transitionBrain = _transitionCamera.GetComponent<CinemachineBrain>();
            if (_transitionBrain != null)
            {
                _brainWasEnabled = _transitionBrain.enabled;
                _transitionBrain.enabled = false;
            }
            _originalFieldOfView = _transitionCamera.fieldOfView;
            _originalOrthographicSize = _transitionCamera.orthographicSize;
            _originalCameraRotation = _transitionCamera.transform.rotation;
            Tween zoom = _transitionCamera.orthographic
                ? _transitionCamera.DOOrthoSize(_originalOrthographicSize * _combatZoomRatio, _combatZoomSeconds)
                : _transitionCamera.DOFieldOfView(_originalFieldOfView * _combatZoomRatio, _combatZoomSeconds);
            _cameraTransition.Append(zoom.SetEase(Ease.InQuad));
            _cameraTransition.Join(_transitionCamera.transform.DORotateQuaternion(
                _originalCameraRotation * Quaternion.Euler(0f, 0f, _combatCameraRoll), _combatZoomSeconds)
                .SetEase(Ease.InQuad));
        }
        _cameraTransition.Insert(_combatHitStopSeconds,
            fadeCanvasGroup.DOFade(1f, _combatFadeSeconds).SetEase(Ease.InQuad));
        yield return _cameraTransition.WaitForCompletion();
    }

    private void RestoreTransitionState()
    {
        _screenAudioTransition?.Kill();
        _screenAudioTransition = null;
        if (_ownsListenerVolume)
        {
            AudioListener.volume = _originalListenerVolume;
            _ownsListenerVolume = false;
        }
        _cameraTransition?.Kill();
        _cameraTransition = null;
        if (_transitionCamera != null)
        {
            _transitionCamera.fieldOfView = _originalFieldOfView;
            _transitionCamera.orthographicSize = _originalOrthographicSize;
            _transitionCamera.transform.rotation = _originalCameraRotation;
        }
        if (_transitionBrain != null) _transitionBrain.enabled = _brainWasEnabled;
        _transitionCamera = null;
        _transitionBrain = null;
        if (_ownsTimeScale)
        {
            Time.timeScale = _previousTimeScale;
            _ownsTimeScale = false;
        }
    }

    private IEnumerator WaitForSceneReady()
    {
        yield return null;
        var characters = FindFirstObjectByType<PlayableCharacterController>();
        var battle = FindFirstObjectByType<Erumperem.Combat.CombatPrototypeController>();
        float deadline = Time.realtimeSinceStartup + 15f;
        while ((ExplorationLoadContext.Instance != null && ExplorationLoadContext.Instance.IsRestoringState)
            || (characters != null && characters.isActiveAndEnabled && characters.IsLoadingState)
            || (battle != null && battle.isActiveAndEnabled && !battle.IsSceneReady))
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Debug.LogWarning("[SceneTransitionHandler] A inicialização da cena excedeu 15 segundos.");
                break;
            }
            yield return null;
        }
        foreach (var cameraTarget in FindObjectsByType<CinemachineCameraTargetUpdate>(FindObjectsSortMode.None))
            cameraTarget.SnapToCurrentMain();
        foreach (var cameraTarget in FindObjectsByType<InGameCharacterCamera>(FindObjectsSortMode.None))
            cameraTarget.SnapToCurrentCharacter();
        yield return null;
    }
}
