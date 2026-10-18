using System;
using UnityEngine;

[Serializable]
public class PeriodicHuntSettings
{
    [Tooltip("Menor intervalo, em segundos, entre uma caçada forçada e a próxima.")]
    [Min(1f)] public float minInterval = 20f;

    [Tooltip("Maior intervalo. Cada espera é sorteada entre min e max para os chasers não agirem em sincronia.")]
    [Min(1f)] public float maxInterval = 40f;
}

/// <summary>
/// Variante "caçador": a cada intervalo sorteado, se o player estiver FORA da área segura,
/// força o chaser a ir até a posição do player naquele instante (Investigating).
/// Se o player estiver na área segura quando o tempo acaba, a caçada é pulada e um novo
/// intervalo começa (assim, ao sair do Hub, os caçadores não disparam todos juntos).
/// Ignorado enquanto o chaser já está perseguindo.
///
/// Pode ser adicionado manualmente a um prefab ou ligado pelo ChaserSpawner (arquétipo "periodicHunt").
/// </summary>
[RequireComponent(typeof(ChaserAI))]
public class PeriodicHuntBehaviour : MonoBehaviour
{
    [SerializeField] private PeriodicHuntSettings settings = new PeriodicHuntSettings();

    private ChaserAI chaser;
    private float timer;

    public void Configure(PeriodicHuntSettings newSettings)
    {
        settings = newSettings ?? new PeriodicHuntSettings();

        if (isActiveAndEnabled)
        {
            ScheduleNext();
        }
    }

    private void Awake()
    {
        chaser = GetComponent<ChaserAI>();
    }

    private void OnEnable()
    {
        ScheduleNext();
    }

    private void Update()
    {
        if (!chaser.HasBegun)
        {
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f)
        {
            return;
        }

        TryHunt();
        ScheduleNext();
    }

    private void TryHunt()
    {
        Transform target = chaser.Target;
        if (target == null)
        {
            return;
        }

        ChaserWorld world = chaser.World;
        if (world != null && world.IsInsideSafeArea(target.position))
        {
            return;
        }

        chaser.ForceInvestigate(target.position);
    }

    private void ScheduleNext()
    {
        float min = Mathf.Min(settings.minInterval, settings.maxInterval);
        float max = Mathf.Max(settings.minInterval, settings.maxInterval);
        timer = UnityEngine.Random.Range(min, max);
    }
}
