using System;
using UnityEngine;

/// <summary>
/// Detecta contato físico e só avisa, informando com QUAL collider encostou. Não decide nada:
/// quem decide se o contato vira captura (é o alvo atual? o chaser já começou?) é o
/// <see cref="ChaserAI"/>, que conhece o alvo do <see cref="ChaserWorld"/>.
///
/// Dispara em OnCollisionEnter e também em OnCollisionStay: se o chaser já estava encostado no
/// player quando passou a poder capturar (ex.: acabou de ser ativado, ou a captura foi liberada por
/// ResetCatchStateForCombatRetry), o contato contínuo ainda é percebido.
/// Precisa ficar no mesmo GameObject do Collider/Rigidbody para receber os callbacks.
/// </summary>
[DisallowMultipleComponent]
public class ChaserContactSensor : MonoBehaviour
{
    /// <summary>Disparado enquanto o collider deste objeto encosta em outro collider.</summary>
    public event Action<Collider> Contact;

    private void OnCollisionEnter(Collision collision)
    {
        Contact?.Invoke(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        Contact?.Invoke(collision.collider);
    }
}
