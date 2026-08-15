using UnityEngine;

namespace Erumperem.Combat
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CombatCapsuleTag))]
    public class CombatCapsuleAnimatorBridge : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private CombatSessionHub combatSessionHub;

        private Animator _animator;
        private CombatCapsuleTag _capsuleTag;

        // Hashes dos Triggers para melhor performance (evita alocação de string a cada frame)
        private static readonly int HitTakenTrigger = Animator.StringToHash("HitTaken");
        private static readonly int DeathTrigger = Animator.StringToHash("Death");
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        // Nota: O "Idle" geralmente é o estado padrão (Default State) no Animator,
        // mas se você precisar forçar via Trigger, descomente a linha abaixo:
        // private static readonly int IdleTrigger = Animator.StringToHash("Idle");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _capsuleTag = GetComponent<CombatCapsuleTag>();

            // Tenta encontrar o CombatSessionHub automaticamente se não foi arrastado no Inspector
            if (combatSessionHub == null)
            {
                combatSessionHub = FindFirstObjectByType<CombatSessionHub>(); // No Unity mais antigo, use FindObjectOfType
            }
        }

        private void OnEnable()
        {
            if (combatSessionHub == null) return;

            // Inscreve-se nos eventos relevantes do Hub
            combatSessionHub.OnCombatSkillExecutionPresentationStarted += HandleSkillStarted;
            combatSessionHub.OnCombatantPresentationDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (combatSessionHub == null) return;

            // Sempre desinscreva-se para evitar memory leaks
            combatSessionHub.OnCombatSkillExecutionPresentationStarted -= HandleSkillStarted;
            combatSessionHub.OnCombatantPresentationDeath -= HandleDeath;
        }

        private void HandleSkillStarted(string actorId, string targetId)
        {
            string meuId = _capsuleTag.combatantId;

            // 1. EU sou quem está atacando?
            if (actorId == meuId)
            {
                // Opcional: Se você quiser garantir que apenas ALIADOS ataquem por esse script:
                // if (meuId.StartsWith("ally_")) 
                
                _animator.SetTrigger(AttackTrigger);
                return;
            }

            // 2. EU sou o alvo que está recebendo o ataque?
            if (targetId == meuId)
            {
                _animator.SetTrigger(HitTakenTrigger);
            }
        }

        private void HandleDeath(string deadCombatantId)
        {
            // 3. EU morri?
            if (deadCombatantId == _capsuleTag.combatantId)
            {
                _animator.SetTrigger(DeathTrigger);
            }
        }
    }
}
