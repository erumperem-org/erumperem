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
/// frame. Ativado uma única vez pelo <see cref="ChaserAI"/> ao nascer e
/// nunca mais desativado - todo Chaser fica com a percepção ligada 100% do
/// tempo. <see cref="Deactivate"/> continua existindo por completude (ex:
/// <c>OnDisable</c>) mas não faz parte do fluxo normal de nenhum estado.
///
/// O raio de percepção é multiplicado, a cada checagem, pelos fatores de
/// movimento e tocha do <see cref="CharacterStateExposed"/> do alvo atual
/// (ver <see cref="GetMovementFactor"/>/<see cref="GetTorchFactor"/>) -
/// alvo mais "barulhento" (correndo, tocha acesa) é percebido de mais
/// longe.
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
    /// Estado exposto do alvo atual (movimento/tocha), usado para calcular
    /// o multiplicador de percepção. Fica null se o alvo não tiver esse
    /// componente - nesse caso os fatores simplesmente não têm efeito
    /// (ver <see cref="GetMovementFactor"/>/<see cref="GetTorchFactor"/>).
    /// </summary>
    private CharacterStateExposed targetState;

    /// <summary>
    /// Liga o loop de percepção para o alvo e configurações fornecidos.
    /// Chamadas repetidas apenas atualizam o alvo/settings caso o loop já
    /// esteja rodando - também usado pelo <see cref="ChaserAI"/> para
    /// manter o alvo em dia quando ele muda em runtime (retarget), mesmo
    /// com o loop já ativo.
    /// </summary>
    public void Activate(Transform targetToTrack, ChaserSettings chaserSettings)
    {
        target = targetToTrack;
        settings = chaserSettings;
        targetState = target != null ? target.GetComponentInParent<CharacterStateExposed>() : null;

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
        targetState = null;
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

        // Raio efetivo de percepção: base * fator de movimento * fator de
        // tocha, lido do estado atual do alvo a cada checagem (sem assinar
        // eventos - reage naturalmente a troca de alvo via SetTarget).
        float effectiveRadius = settings.perceptionRadius * GetMovementFactor() * GetTorchFactor();

        // Fora do raio de percepção: nem tenta o raycast.
        if (sqrDistance > effectiveRadius * effectiveRadius)
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

    /// <summary>Multiplicador de percepção conforme o estado de movimento atual do alvo.</summary>
    private float GetMovementFactor()
    {
        if (targetState == null)
        {
            return 1f;
        }

        switch (targetState.MovementState)
        {
            case CharacterStateExposed.CharacterMovementState.Idle:
                return settings.movementFactorIdle;
            case CharacterStateExposed.CharacterMovementState.Walk:
                return settings.movementFactorWalk;
            case CharacterStateExposed.CharacterMovementState.Run:
                return settings.movementFactorRun;
            default:
                return 1f;
        }
    }

    /// <summary>Multiplicador de percepção conforme o estado atual da tocha do alvo.</summary>
    private float GetTorchFactor()
    {
        if (targetState == null)
        {
            return 1f;
        }

        return targetState.TorchState == CharacterStateExposed.CharacterTorchState.On
            ? settings.torchFactorOn
            : settings.torchFactorOff;
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