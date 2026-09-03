using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Exemplo de controlador "plugado" no PhysicsMovementService.
///
/// Responsabilidade deste script: ler input de teclado (via New Input System,
/// lendo o estado do dispositivo diretamente, sem Input Actions asset),
/// transformar em uma direção world-space relativa à câmera, e repassar
/// para o serviço via sua API pública. O serviço não sabe nada sobre
/// teclado nem sobre câmera — toda essa tradução acontece aqui.
///
/// Para usar com outro esquema de câmera (primeira pessoa, top-down, etc.),
/// basta ajustar como `inputDir` é calculado — o resto do script não muda.
/// </summary>
[RequireComponent(typeof(PhysicsMovementService))]
public class KeyboardMovementController : MonoBehaviour
{
    [SerializeField] private PhysicsMovementService movement;
    [SerializeField] private Key sprintKey = Key.LeftShift;

    private void Reset()
    {
        movement = GetComponent<PhysicsMovementService>();
    }

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<PhysicsMovementService>();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return; // nenhum teclado conectado neste frame

        float h = 0f;
        float v = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) h -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) h += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v += 1f;

        Vector3 inputDir = new Vector3(h, 0f, v);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        movement.SetMoveDirection(inputDir);
        movement.SetSprinting(keyboard[sprintKey].isPressed);
    }
}