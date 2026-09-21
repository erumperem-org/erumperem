using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Camada acima da IA de cada Chaser: agrega a captura do alvo por
/// qualquer um dos ChaserAI monitorados num único UnityEvent, para outros
/// sistemas (troca de cena, UI de derrota, etc.) encadearem suas próprias
/// ações sem precisar conhecer ChaserAI diretamente nem se inscrever em
/// cada instância individualmente.
///
/// Não conhece nenhum tipo de "consumidor" concreto - comunica a captura
/// via <see cref="UnityEvent"/>, para que qualquer sistema (atual ou
/// futuro) se conecte pelo Inspector, sem exigir nenhuma referência de
/// código entre este pacote e o de destino (ex: carregamento de cena).
/// </summary>
public class EnemyHuntOrchestrator : MonoBehaviour
{
    [Tooltip("Todos os ChaserAI cuja captura do alvo deve disparar OnPreyCaught.")]
    [SerializeField] private ChaserAI[] chasers;

    [Header("Eventos (conecte pelo Inspector)")]
    [Tooltip("Disparado quando qualquer um dos Chasers monitorados captura o alvo.")]
    public UnityEvent OnPreyCaught;

    private void OnEnable()
    {
        foreach (var chaser in chasers)
        {
            if (chaser == null)
            {
                continue;
            }

            chaser.OnTargetCaught += HandlePreyCaught;
        }
    }

    private void OnDisable()
    {
        foreach (var chaser in chasers)
        {
            if (chaser == null)
            {
                continue;
            }

            chaser.OnTargetCaught -= HandlePreyCaught;
        }
    }

    private void HandlePreyCaught()
    {
        OnPreyCaught?.Invoke();
    }
}
