using UnityEngine;

/// <summary>
/// Combina múltiplos IMovementValidator em sequência - cada um refina a
/// direção produzida pelo anterior. Permite ao ChaserAI (ou qualquer outro
/// mover) aplicar várias regras de desvio independentes (obstáculos
/// físicos, exclusão de área segura, etc.) através do único slot de
/// validador exposto por PhysicsMovementService.SetValidator.
/// </summary>
public class CompositeMovementValidator : IMovementValidator
{
    private readonly IMovementValidator[] validators;

    public CompositeMovementValidator(params IMovementValidator[] validators)
    {
        this.validators = validators;
    }

    public Vector3 Validate(Vector3 desiredDirection, Vector3 origin, float castRadius, float checkDistance, LayerMask obstacleMask)
    {
        Vector3 direction = desiredDirection;

        foreach (var validator in validators)
        {
            if (validator == null)
            {
                continue;
            }

            direction = validator.Validate(direction, origin, castRadius, checkDistance, obstacleMask);
        }

        return direction;
    }
}