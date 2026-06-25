using Services.DebugUtilities;
using UnityEngine;

/// <summary>
/// Zona trigger da vila. Emite eventos quando o personagem Main entra ou sai.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public sealed class VillageArea : MonoBehaviour
{
    private const string TriggerEnterDetectionSource = "OnTriggerEnter";

    [SerializeField] private PlayableCharactersManager _playableCharactersManager;

    private SphereCollider _sphereCollider;
    private bool _isPlayerInside;

    public bool IsPlayerInside => _isPlayerInside;

    private void Awake()
    {
        _sphereCollider = GetComponent<SphereCollider>();
        _sphereCollider.isTrigger = true;

        if (_playableCharactersManager == null)
        {
            _playableCharactersManager = FindFirstObjectByType<PlayableCharactersManager>();
        }
    }

    private void OnEnable()
    {
        // Só verificamos "já dentro da vila" depois que a posição salva foi aplicada ao Main.
        // Esperar apenas um frame (como antes) criava um falso positivo pós-combate: o Main ainda
        // estava na posição padrão (dentro da esfera) e a party era curada, desfazendo a persistência.
        ExplorationLoadContext.OnExplorationStateApplied += HandleExplorationStateApplied;
    }

    private void OnDisable()
    {
        ExplorationLoadContext.OnExplorationStateApplied -= HandleExplorationStateApplied;
    }

    private void OnTriggerEnter(Collider otherCollider)
    {
        if (ExplorationLoadContext.IsApplyingSavedExplorationState)
        {
            return;
        }

        if (_isPlayerInside || !IsMainCharacterCollider(otherCollider))
        {
            return;
        }

        RaisePlayerEnteredVillage(TriggerEnterDetectionSource);
    }

    private void OnTriggerExit(Collider otherCollider)
    {
        if (!_isPlayerInside || !IsMainCharacterCollider(otherCollider))
        {
            return;
        }

        _isPlayerInside = false;
        ExplorationVillageEvents.RaisePlayerExitedVillage();
    }

    private void HandleExplorationStateApplied()
    {
        // Sincroniza o flag interno sem disparar cura: a cura só deve ocorrer quando o jogador
        // ENTRA na vila em runtime (OnTriggerEnter), não ao carregar posição salva ou pós-combate.
        var wasInsideBeforeSync = _isPlayerInside;
        _isPlayerInside = IsMainCharacterInsideSanctuarySphere();

        if (_isPlayerInside && !wasInsideBeforeSync)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[HEAL-DEBUG] [VILLAGE-AREA] Main dentro da esfera após load — flag sincronizado, " +
                "sem evento de cura (cura apenas em OnTriggerEnter).",
                LogCategory.Player);
        }
    }

    private void RaisePlayerEnteredVillage(string detectionSource)
    {
        _isPlayerInside = true;

        LogVillageEntry(detectionSource);
        ExplorationVillageEvents.RaisePlayerEnteredVillage();
    }

    private void LogVillageEntry(string detectionSource)
    {
        var mainTransform = _playableCharactersManager?.Main?.Transform;
        if (mainTransform == null || _sphereCollider == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[HEAL-DEBUG] [VILLAGE-AREA] Entrada na vila detectada por '{detectionSource}' (sem referência ao Main).",
                LogCategory.Player);
            return;
        }

        var sanctuaryCenter = transform.TransformPoint(_sphereCollider.center);
        var mainPosition = mainTransform.position;
        var distanceToCenter = Vector3.Distance(mainPosition, sanctuaryCenter);

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[HEAL-DEBUG] [VILLAGE-AREA] Entrada na vila detectada por '{detectionSource}'. " +
            $"Main em {mainPosition}, centro do santuário {sanctuaryCenter}, distância {distanceToCenter:F2} " +
            $"(raio {ResolveWorldRadius():F2}).",
            LogCategory.Player);
    }

    private bool IsMainCharacterInsideSanctuarySphere()
    {
        if (_playableCharactersManager?.Main?.Transform == null || _sphereCollider == null)
        {
            return false;
        }

        var sanctuaryCenter = transform.TransformPoint(_sphereCollider.center);
        var worldRadius = ResolveWorldRadius();
        var mainPosition = _playableCharactersManager.Main.Transform.position;
        return (mainPosition - sanctuaryCenter).sqrMagnitude <= worldRadius * worldRadius;
    }

    private float ResolveWorldRadius()
    {
        var lossyScale = transform.lossyScale;
        return _sphereCollider.radius * Mathf.Max(lossyScale.x, lossyScale.y, lossyScale.z);
    }

    private bool IsMainCharacterCollider(Collider otherCollider)
    {
        if (otherCollider == null || _playableCharactersManager?.Main == null)
        {
            return false;
        }

        var mainPlayableCharacter = _playableCharactersManager.Main as PlayableCharacter;
        if (mainPlayableCharacter != null)
        {
            var playableOnCollider = otherCollider.GetComponentInParent<PlayableCharacter>();
            if (playableOnCollider != null)
            {
                return ReferenceEquals(playableOnCollider, mainPlayableCharacter);
            }
        }

        if (!otherCollider.CompareTag("Player"))
        {
            return false;
        }

        var mainTransform = _playableCharactersManager.Main.Transform;
        return mainTransform != null &&
               (otherCollider.transform == mainTransform ||
                otherCollider.transform.IsChildOf(mainTransform));
    }
}
