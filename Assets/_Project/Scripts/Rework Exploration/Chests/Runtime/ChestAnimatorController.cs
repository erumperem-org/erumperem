using Services.DebugUtilities;
using UnityEngine;

namespace Core.Chests
{
    /// <summary>
    /// View do baú: traduz ChestState (Chest.OnChestStateChanged) para o bool
    /// "IsOpen" do Animator. Não contém lógica de gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChestAnimatorController : MonoBehaviour
    {
        private static readonly int IsOpenId = Animator.StringToHash("IsOpen");

        [Header("Integration")]
        [SerializeField] private Chest _chest;
        [SerializeField] private Animator _animator;

        [Header("Behaviour")]
        [Tooltip("Estado aplicado ao habilitar, antes de qualquer evento do Chest.")]
        [SerializeField] private ChestState _initialState = ChestState.Closed;

        [Tooltip("Se marcado, o estado inicial é aplicado sem tocar a animação de transição " +
                 "(pula direto para o fim do clip).")]
        [SerializeField] private bool _snapInitialState = true;

        private bool _hasAppliedState;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (_chest == null)
            {
                _chest = GetComponentInParent<Chest>();
                Log(LogLevel.Debug, _chest != null
                    ? $"Chest não atribuído - resolvido via GetComponentInParent: '{_chest.name}'."
                    : "Chest não atribuído e não encontrado nos pais.");
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                Log(LogLevel.Debug, _animator != null
                    ? $"Animator não atribuído - resolvido via GetComponentInChildren: '{_animator.name}'."
                    : "Animator não atribuído e não encontrado nos filhos.");
            }

            if (_chest == null)
                Log(LogLevel.Warning, "Sem Chest: o controller não reagirá a mudanças de estado.");

            if (_animator == null)
                Log(LogLevel.Warning, "Sem Animator: nenhuma animação será aplicada.");
        }

        private void OnEnable()
        {
            if (_chest != null)
                _chest.OnChestStateChanged += HandleChestStateChanged;

            // Sincroniza com o estado atual do baú, caso já tenha sido consumido
            // antes deste componente ser habilitado.
            bool consumed = _chest != null && _chest.IsConsumed;
            ChestState state = consumed ? ChestState.Open : _initialState;

            Log(LogLevel.Debug,
                $"Habilitado. Consumido: {consumed}, estado inicial resolvido: {state}, snap: {_snapInitialState}.");

            Apply(state, snap: _snapInitialState);
        }

        private void OnDisable()
        {
            if (_chest != null)
                _chest.OnChestStateChanged -= HandleChestStateChanged;

            Log(LogLevel.Debug, "Desabilitado - inscrição em OnChestStateChanged removida.");
        }

        private void HandleChestStateChanged(ChestState state)
        {
            Log(LogLevel.Debug, $"OnChestStateChanged recebido: {state}.");
            Apply(state, snap: false);
        }

        /// <summary>Também pode ser ligado a UnityEvents.</summary>
        public void SetOpen(bool open)
        {
            Log(LogLevel.Debug, $"SetOpen({open}) chamado externamente.");
            Apply(open ? ChestState.Open : ChestState.Closed, snap: false);
        }

        private void Apply(ChestState state, bool snap)
        {
            bool open = state == ChestState.Open;

            IsOpen = open;
            _hasAppliedState = true;

            if (_animator == null)
            {
                Log(LogLevel.Warning, $"Estado '{state}' registrado, mas não há Animator para aplicar.");
                return;
            }

            _animator.SetBool(IsOpenId, open);
            Log(LogLevel.Debug, $"Animator.SetBool(IsOpen, {open}) aplicado (snap: {snap}).");

            if (snap)
            {
                // Avança o animator para o estado final sem reproduzir a transição.
                _animator.Update(0f);
                _animator.Play(0, 0, 1f);
                _animator.Update(0f);
                Log(LogLevel.Debug, "Snap aplicado: animator avançado direto para o fim do clip.");
            }
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[ChestAnimatorController:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}