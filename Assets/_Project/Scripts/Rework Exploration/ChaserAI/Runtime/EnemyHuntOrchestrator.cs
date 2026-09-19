using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Camada acima da IA de cada inimigo: escuta uma ou mais áreas seguras
/// (<c>Hub</c>, pacote AreaZones) e decide, de forma agregada, se o alvo
/// está disponível para ser perseguido.
///
/// Não conhece <c>ChaserAI</c> nem qualquer outro tipo concreto de inimigo -
/// comunica a decisão via <see cref="UnityEvent"/>, para que qualquer
/// inimigo (atual ou futuro) se conecte pelo Inspector, sem exigir nenhuma
/// referência de código entre este pacote e o de IA.
///
/// Suporta múltiplas áreas seguras simultâneas: o alvo só volta a ficar
/// "disponível para caça" quando sai de TODAS elas - evita disparar
/// "disponível" prematuramente ao sair de apenas uma área, em casos de
/// sobreposição.
/// </summary>
public class EnemyHuntOrchestrator : MonoBehaviour
{
    [Tooltip("Todas as áreas seguras que tornam o alvo indisponível para caça " +
             "enquanto ele estiver dentro de qualquer uma delas.")]
    [SerializeField] private Hub[] safeAreas;

    [Header("Eventos (conecte os inimigos aqui pelo Inspector)")]
    [Tooltip("Disparado quando o alvo deixa de estar em qualquer área segura monitorada.")]
    public UnityEvent OnTargetAvailableForHunt;

    [Tooltip("Disparado quando o alvo entra em uma área segura monitorada (a primeira, em caso de sobreposição).")]
    public UnityEvent OnTargetUnavailableForHunt;

    private int areasContainingTarget;

    /// <summary>True quando o alvo não está em nenhuma área segura monitorada.</summary>
    public bool IsTargetAvailableForHunt => areasContainingTarget == 0;

    private void OnEnable()
    {
        areasContainingTarget = 0;

        foreach (var area in safeAreas)
        {
            if (area == null)
            {
                continue;
            }

            area.OnPlayerEnteredSafeArea += HandleAreaEntered;
            area.OnPlayerExitedSafeArea += HandleAreaExited;

            // Sincroniza o contador com o estado atual de cada área, caso o
            // alvo já esteja dentro dela no momento em que este componente
            // é ativado (ex: cena carregada com o jogador já no HUB).
            if (area.IsTargetInside)
            {
                areasContainingTarget++;
            }
        }

        // Propaga o estado inicial, para que inimigos que já nasçam
        // "caçando" por padrão sejam corrigidos imediatamente se o alvo já
        // estiver seguro ao entrar em cena.
        if (areasContainingTarget > 0)
        {
            OnTargetUnavailableForHunt?.Invoke();
        }
    }

    private void OnDisable()
    {
        foreach (var area in safeAreas)
        {
            if (area == null)
            {
                continue;
            }

            area.OnPlayerEnteredSafeArea -= HandleAreaEntered;
            area.OnPlayerExitedSafeArea -= HandleAreaExited;
        }
    }

    private void HandleAreaEntered()
    {
        areasContainingTarget++;

        if (areasContainingTarget == 1)
        {
            OnTargetUnavailableForHunt?.Invoke();
        }
    }

    private void HandleAreaExited()
    {
        areasContainingTarget = Mathf.Max(0, areasContainingTarget - 1);

        if (areasContainingTarget == 0)
        {
            OnTargetAvailableForHunt?.Invoke();
        }
    }
}