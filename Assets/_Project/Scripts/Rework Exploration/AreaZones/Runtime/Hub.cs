using System;
using UnityEngine;

/// <summary>
/// Área segura específica (ex: o HUB do jogo) que detecta o alvo via
/// trigger de collider (SphereCollider, raio sincronizado com o raio da
/// zona definido em CircularZone) em vez de polling por distância. Expõe
/// eventos de entrada/saída para um orquestrador de estados externo.
///
/// O alvo rastreado é mantido em sincronia com o personagem Em Jogo atual
/// via PlayableCharacterController.OnCharacterEnteredInGame - não precisa
/// mais ser arrastado manualmente no Inspector (embora o campo continue
/// serializado, como fallback caso o controller não esteja configurado).
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class Hub : SafeArea
{
    [SerializeField] private Transform target;

    [Header("Sincronização com o personagem Em Jogo")]
    [Tooltip("Opcional - se atribuído, o alvo do Hub é atualizado automaticamente a cada troca de personagem Em Jogo, sobrescrevendo o campo 'target' acima.")]
    [SerializeField] private PlayableCharacterController playableCharacterController;

    private SphereCollider triggerCollider;
    private bool isTargetInside;

    /// <summary>Disparado quando o alvo passa a estar dentro do Hub.</summary>
    public event Action OnPlayerEnteredSafeArea;

    /// <summary>Disparado quando o alvo deixa de estar dentro do Hub.</summary>
    public event Action OnPlayerExitedSafeArea;

    /// <summary>Estado atual, para consulta direta sem precisar assinar os eventos.</summary>
    public bool IsTargetInside => isTargetInside;

    private void Awake()
    {
        triggerCollider = GetComponent<SphereCollider>();
        SyncColliderWithRadius();
    }

    private void OnEnable()
    {
        if (playableCharacterController == null)
        {
            return;
        }

        playableCharacterController.OnCharacterEnteredInGame += HandleInGameCharacterChanged;

        // Sincroniza imediatamente com quem já é o Em Jogo atual, caso o
        // Hub seja habilitado depois da troca inicial já ter acontecido
        // (ex: load de save já aplicado antes deste componente ligar).
        if (playableCharacterController.InGameCharacter != null)
        {
            target = playableCharacterController.InGameCharacter.transform;
            ReevaluateContainment();
        }
    }

    private void OnDisable()
    {
        if (playableCharacterController != null)
        {
            playableCharacterController.OnCharacterEnteredInGame -= HandleInGameCharacterChanged;
        }
    }

    private void HandleInGameCharacterChanged(PlayableCharacters character)
    {
        if (character == null)
        {
            return;
        }

        target = character.transform;

        // O trigger físico (OnTriggerEnter/Exit) só dispara quando um
        // collider CRUZA a fronteira do Hub. Se o novo alvo já estava
        // fisicamente dentro do Hub antes da troca (ex: era Companheiro,
        // parado lá dentro, e virou Em Jogo agora), nenhum evento novo de
        // física ocorre - então o estado precisa ser reconciliado
        // manualmente aqui, com base na posição atual.
        ReevaluateContainment();
    }

    /// <summary>Reavalia com base na posição atual do alvo (não depende de física ter disparado).</summary>
    private void ReevaluateContainment()
    {
        if (target == null)
        {
            return;
        }

        SetInsideState(Contains(target.position));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsTarget(other))
        {
            return;
        }

        SetInsideState(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsTarget(other))
        {
            return;
        }

        SetInsideState(false);
    }

    /// <summary>
    /// ÚNICO ponto que altera isTargetInside e dispara os eventos - tanto
    /// OnTriggerEnter/Exit (física) quanto ReevaluateContainment (troca de
    /// alvo) passam por aqui. Isso evita a lógica de "mudou de estado →
    /// dispara evento" ficar duplicada e divergir entre os dois caminhos.
    /// Idempotente: chamar com o mesmo valor do estado atual não faz nada.
    /// </summary>
    private void SetInsideState(bool isInside)
    {
        if (isInside == isTargetInside)
        {
            return;
        }

        isTargetInside = isInside;

        if (isInside)
        {
            OnPlayerEnteredSafeArea?.Invoke();
        }
        else
        {
            OnPlayerExitedSafeArea?.Invoke();
        }
    }

    private void OnValidate()
    {
        // Mantém o SphereCollider sempre igual ao raio definido em
        // CircularZone, para não ter dois lugares diferentes controlando o
        // "tamanho" do Hub (um no Inspector via radius, outro no collider).
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<SphereCollider>();
        }

        SyncColliderWithRadius();
    }

    private void SyncColliderWithRadius()
    {
        if (triggerCollider == null)
        {
            return;
        }

        triggerCollider.isTrigger = true;
        triggerCollider.center = Vector3.zero;
        triggerCollider.radius = Radius;
    }

    private bool IsTarget(Collider other)
    {
        return target != null && (other.transform == target || other.transform.IsChildOf(target));
    }

#if UNITY_EDITOR
    /// <summary>
    /// Uso exclusivo do editor de testes (HubEditor) para simular a entrada
    /// do alvo sem precisar movê-lo até o Hub de verdade. Compilado apenas
    /// em Editor - não existe em builds de player.
    /// </summary>
    public void Editor_SimulateEnter()
    {
        SetInsideState(true);
    }

    /// <summary>Equivalente a <see cref="Editor_SimulateEnter"/>, para simular a saída.</summary>
    public void Editor_SimulateExit()
    {
        SetInsideState(false);
    }
#endif
}