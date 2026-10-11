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
                _chest = GetComponentInParent<Chest>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            if (_chest != null)
                _chest.OnChestStateChanged += HandleChestStateChanged;

            // Sincroniza com o estado atual do baú, caso já tenha sido consumido
            // antes deste componente ser habilitado.
            ChestState state = _chest != null && _chest.IsConsumed
                ? ChestState.Open
                : _initialState;

            Apply(state, snap: _snapInitialState);
        }

        private void OnDisable()
        {
            if (_chest != null)
                _chest.OnChestStateChanged -= HandleChestStateChanged;
        }

        private void HandleChestStateChanged(ChestState state)
        {
            Apply(state, snap: false);
        }

        /// <summary>Também pode ser ligado a UnityEvents.</summary>
        public void SetOpen(bool open)
        {
            Apply(open ? ChestState.Open : ChestState.Closed, snap: false);
        }

        private void Apply(ChestState state, bool snap)
        {
            bool open = state == ChestState.Open;

            if (_hasAppliedState && IsOpen == open && !snap)
                return;

            IsOpen = open;
            _hasAppliedState = true;

            if (_animator == null) return;

            _animator.SetBool(IsOpenId, open);

            if (snap)
            {
                // Avança o animator para o estado final sem reproduzir a transição.
                _animator.Update(0f);
                _animator.Play(0, 0, 1f);
                _animator.Update(0f);
            }
        }
    }
}