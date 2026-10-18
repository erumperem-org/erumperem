using System;
using UnityEngine;

/// <summary>
/// Orquestrador de referências do sistema de chasers: quem é o player atual
/// e quais são as zonas da cena (limites do mapa, área segura, área de vagar).
///
/// Chasers e sistemas de spawn pedem tudo daqui em vez de cada um guardar sua
/// própria cópia de referência ou escutar o PlayableCharacterController.
/// Quando o personagem Em Jogo muda, <see cref="TargetChanged"/> avisa todos de uma vez.
/// </summary>
[DefaultExecutionOrder(-300)]
public class ChaserWorld : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayableCharacterController playableCharacterController;

    [Header("Zonas da cena")]
    [SerializeField] private MapLimits mapLimits;

    [Tooltip("A SafeArea/Hub: chasers nunca nascem dentro dela nem a atravessam.")]
    [SerializeField] private CircularZone safeArea;

    [Tooltip("Faixa extra ao redor da área segura. Única fonte desta margem: spawn, vagar e desvio de movimento " +
             "a usam (o WanderArea recebe MapLimits, área segura e margem daqui no Awake).")]
    [SerializeField] private float safeAreaMargin = 2f;

    [SerializeField] private WanderArea wanderArea;

    private Transform target;

    /// <summary>Disparado quando o alvo muda (inclui ficar nulo → válido).</summary>
    public event Action<Transform> TargetChanged;

    /// <summary>Transform do personagem Em Jogo atual (pode ser nulo antes de existir).</summary>
    public Transform Target => target;

    public MapLimits MapLimits => mapLimits;
    public CircularZone SafeArea => safeArea;
    public float SafeAreaMargin => Mathf.Max(0f, safeAreaMargin);
    public WanderArea WanderArea => wanderArea;

    private void Awake()
    {
        // O WanderArea não guarda mais cópia própria de mapa/área segura/margem: usa as do mundo.
        if (wanderArea != null)
        {
            wanderArea.Configure(mapLimits, safeArea, SafeAreaMargin);
        }
    }

    private void OnEnable()
    {
        if (playableCharacterController == null)
        {
            Debug.LogWarning("[ChaserWorld] PlayableCharacterController não atribuído: não haverá alvo " +
                             "e nenhum chaser será ativado.", this);
        }
        else
        {
            playableCharacterController.OnCharacterEnteredInGame += HandleCharacterEnteredInGame;
        }

        RefreshTarget();
    }

    private void OnDisable()
    {
        if (playableCharacterController != null)
        {
            playableCharacterController.OnCharacterEnteredInGame -= HandleCharacterEnteredInGame;
        }
    }

    private void Update()
    {
        // O personagem Em Jogo pode só passar a existir depois do Awake/Start.
        if (target == null)
        {
            RefreshTarget();
        }
    }

    private void HandleCharacterEnteredInGame(PlayableCharacters newInGameCharacter)
    {
        SetTarget(newInGameCharacter != null ? newInGameCharacter.transform : null);
    }

    private void RefreshTarget()
    {
        if (playableCharacterController == null)
        {
            return;
        }

        var inGame = playableCharacterController.InGameCharacter;
        SetTarget(inGame != null ? inGame.transform : null);
    }

    private void SetTarget(Transform newTarget)
    {
        if (newTarget == target)
        {
            return;
        }

        target = newTarget;
        TargetChanged?.Invoke(target);
    }

    public bool IsInsideMap(Vector3 point)
    {
        return mapLimits == null || mapLimits.Contains(point);
    }

    /// <summary>Dentro do raio da área segura (sem margem). Use para saber se o player está seguro.</summary>
    public bool IsInsideSafeArea(Vector3 point)
    {
        return safeArea != null && safeArea.Contains(point);
    }

    /// <summary>Dentro do raio da área segura + margem. Use para onde um chaser pode nascer ou vagar.</summary>
    public bool IsInsideSafeAreaWithMargin(Vector3 point)
    {
        if (safeArea == null)
        {
            return false;
        }

        Vector3 delta = point - safeArea.Center;
        delta.y = 0f;
        float effectiveRadius = safeArea.Radius + SafeAreaMargin;
        return delta.sqrMagnitude <= effectiveRadius * effectiveRadius;
    }
}
