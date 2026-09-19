using System.Collections;
using UnityEngine;

/// <summary>
/// Detecta se um alvo está perceptível: dentro de um raio de percepção 360°
/// E com linha de visão livre até ele (sem obstáculos da <c>obstacleMask</c>
/// no caminho). Os dois critérios juntos definem "perceber" - sair do raio
/// OU ter a visão bloqueada já derruba o resultado.
///
/// Roda em loop próprio (coroutine, intervalo configurável em
/// <see cref="ChaserSettings.perceptionCheckInterval"/>) em vez de todo
/// frame, e pode ser completamente desativado via <see cref="Deactivate"/>
/// - usado pelo <see cref="ChaserAI"/> ao entrar em Resting, para não gastar
/// raycasts à toa enquanto a IA está desligada.
/// </summary>
public class PerceptionSensor : MonoBehaviour
{
    [Header("Debug")]
    [Tooltip("Desenha o raycast de percepção no Scene view (visível em Play Mode). " +
             "Verde = percebido, vermelho = bloqueado por obstáculo, cinza = fora do raio.")]
    [SerializeField] private bool enablePerceptionGizmos = true;

    /// <summary>Resultado da última checagem de percepção.</summary>
    public bool CanSeeTarget { get; private set; }

    private Transform target;
    private ChaserSettings settings;
    private Coroutine loopRoutine;

    /// <summary>
    /// Liga o loop de percepção para o alvo e configurações fornecidos.
    /// Chamadas repetidas apenas atualizam o alvo/settings caso o loop já
    /// esteja rodando.
    /// </summary>
    public void Activate(Transform targetToTrack, ChaserSettings chaserSettings)
    {
        target = targetToTrack;
        settings = chaserSettings;

        if (loopRoutine == null)
        {
            loopRoutine = StartCoroutine(PerceptionLoop());
        }
    }

    /// <summary>Para completamente o loop de percepção e zera o resultado.</summary>
    public void Deactivate()
    {
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        CanSeeTarget = false;
    }

    private IEnumerator PerceptionLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.01f, settings.perceptionCheckInterval));

        while (true)
        {
            CanSeeTarget = EvaluatePerception();
            yield return wait;
        }
    }

    private bool EvaluatePerception()
    {
        if (target == null || settings == null)
        {
            return false;
        }

        Vector3 origin = transform.position + Vector3.up * settings.eyeHeight;
        Vector3 toTarget = target.position - origin;
        float sqrDistance = toTarget.sqrMagnitude;

        // Fora do raio de percepção: nem tenta o raycast.
        if (sqrDistance > settings.perceptionRadius * settings.perceptionRadius)
        {
            DrawPerceptionRay(origin, target.position, Color.gray);
            return false;
        }

        // Dentro do raio: checa linha de visão contra a obstacleMask.
        if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, toTarget.magnitude, settings.obstacleMask))
        {
            // Desenha até o ponto de impacto (mostra onde a visão foi bloqueada),
            // não até o alvo - facilita ver qual obstáculo está no caminho.
            DrawPerceptionRay(origin, hit.point, Color.red);
            return false;
        }

        DrawPerceptionRay(origin, target.position, Color.green);
        return true;
    }

    private void DrawPerceptionRay(Vector3 from, Vector3 to, Color color)
    {
        if (!enablePerceptionGizmos)
        {
            return;
        }

        // Duration = intervalo de checagem, para a linha ficar visível de
        // forma contínua entre uma checagem e outra, em vez de piscar por
        // um único frame.
        Debug.DrawLine(from, to, color, settings.perceptionCheckInterval);
    }

    private void OnDisable()
    {
        Deactivate();
    }
}