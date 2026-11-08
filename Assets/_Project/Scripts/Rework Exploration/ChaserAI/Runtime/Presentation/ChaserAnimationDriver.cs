using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Alimenta o(s) Animator(s) do chaser: velocidade de andar e gatilho de "pegar".
/// Não cria nem exige nenhuma animação: basta arrastar o Animator (ou deixar vazio para
/// detectar nos filhos) e criar no Animator Controller os parâmetros com os nomes abaixo.
/// Campo de nome vazio = aquele parâmetro é ignorado. Parâmetro inexistente no controller
/// é ignorado com um único aviso.
///
/// O rig é o mesmo nos modelos de todos os tiers: um único Animator Controller serve a todos.
/// Os Animators dos modelos inativos (ChaserAIView) são pulados e passam a ser alimentados
/// quando o modelo é ativado.
///
/// Parâmetros:
/// - Speed (Float): velocidade horizontal real em unidades/s (ou 0..1, se normalizar).
///   Medida pelo deslocamento, então independe do tipo de movimento/física usado.
/// - Grab (Trigger): disparado na captura do player (ChaserAI.OnTargetCaught).
/// </summary>
[DisallowMultipleComponent]
public class ChaserAnimationDriver : MonoBehaviour
{
    [Header("Parâmetros do Animator (vazio = ignorar)")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string grabTrigger = "Grab";

    [Header("Velocidade")]
    [Tooltip("Suavização do parâmetro de velocidade, em segundos (0 = sem suavização).")]
    [SerializeField] private float speedDampTime = 0.1f;

    [Tooltip("Se ligado, envia velocidade / Normalize Max Speed (útil para blend trees 0..1).")]
    [SerializeField] private bool normalizeSpeed;
    [SerializeField] private float normalizeMaxSpeed = 6f;

    [Header("Animators")]
    [Tooltip("Vazio = todos os Animators nos filhos (inclusive modelos inativos).")]
    [SerializeField] private Animator[] animators;

    private sealed class Binding
    {
        public Animator animator;
        public bool resolved;
        public bool hasSpeed;
        public bool hasGrab;
    }

    private static bool warnedMissingSpeed;
    private static bool warnedMissingGrab;

    private readonly List<Binding> bindings = new List<Binding>();
    private ChaserAI chaser;
    private int speedHash;
    private int grabHash;
    private Vector3 lastPosition;
    private bool hasLastPosition;

    private void Awake()
    {
        chaser = GetComponent<ChaserAI>();

        speedHash = string.IsNullOrEmpty(speedParameter) ? 0 : Animator.StringToHash(speedParameter);
        grabHash = string.IsNullOrEmpty(grabTrigger) ? 0 : Animator.StringToHash(grabTrigger);

        Animator[] source = (animators != null && animators.Length > 0)
            ? animators
            : GetComponentsInChildren<Animator>(true);

        foreach (Animator animator in source)
        {
            if (animator != null)
            {
                bindings.Add(new Binding { animator = animator });
            }
        }
    }

    private void OnEnable()
    {
        hasLastPosition = false;

        if (chaser != null)
        {
            chaser.OnTargetCaught += HandleCaught;
            chaser.OnCatchStateReset += HandleCatchReset;
            chaser.OnRelocated += HandleRelocated;
        }
    }

    private void OnDisable()
    {
        if (chaser != null)
        {
            chaser.OnTargetCaught -= HandleCaught;
            chaser.OnCatchStateReset -= HandleCatchReset;
            chaser.OnRelocated -= HandleRelocated;
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        Vector3 position = transform.position;
        float speed = 0f;

        if (hasLastPosition)
        {
            Vector3 delta = position - lastPosition;
            delta.y = 0f;
            speed = delta.magnitude / deltaTime;
        }

        lastPosition = position;
        hasLastPosition = true;

        if (speedHash == 0)
        {
            return;
        }

        if (normalizeSpeed && normalizeMaxSpeed > 0f)
        {
            speed /= normalizeMaxSpeed;
        }

        foreach (Binding binding in bindings)
        {
            if (!Resolve(binding) || !binding.hasSpeed)
            {
                continue;
            }

            if (speedDampTime > 0f)
            {
                binding.animator.SetFloat(speedHash, speed, speedDampTime, deltaTime);
            }
            else
            {
                binding.animator.SetFloat(speedHash, speed);
            }
        }
    }

    private void HandleCaught()
    {
        if (grabHash == 0)
        {
            return;
        }

        foreach (Binding binding in bindings)
        {
            if (Resolve(binding) && binding.hasGrab)
            {
                binding.animator.SetTrigger(grabHash);
            }
        }
    }

    private void HandleCatchReset()
    {
        if (grabHash == 0)
        {
            return;
        }

        foreach (Binding binding in bindings)
        {
            if (Resolve(binding) && binding.hasGrab)
            {
                binding.animator.ResetTrigger(grabHash);
            }
        }
    }

    // O teleporte não pode virar um pico de velocidade na animação.
    private void HandleRelocated()
    {
        hasLastPosition = false;
    }

    /// <summary>True se o Animator está pronto (ativo e com controller) e seus parâmetros foram checados.</summary>
    private bool Resolve(Binding binding)
    {
        Animator animator = binding.animator;

        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        if (binding.resolved)
        {
            return true;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (speedHash != 0 && parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                binding.hasSpeed = true;
            }

            if (grabHash != 0 && parameter.nameHash == grabHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                binding.hasGrab = true;
            }
        }

        if (speedHash != 0 && !binding.hasSpeed && !warnedMissingSpeed)
        {
            warnedMissingSpeed = true;
            Debug.LogWarning($"[ChaserAnimationDriver] O Animator Controller não tem o parâmetro Float '{speedParameter}'. " +
                             "A velocidade não será enviada.", animator);
        }

        if (grabHash != 0 && !binding.hasGrab && !warnedMissingGrab)
        {
            warnedMissingGrab = true;
            Debug.LogWarning($"[ChaserAnimationDriver] O Animator Controller não tem o parâmetro Trigger '{grabTrigger}'. " +
                             "O gatilho de captura não será enviado.", animator);
        }

        binding.resolved = true;
        return true;
    }
}
