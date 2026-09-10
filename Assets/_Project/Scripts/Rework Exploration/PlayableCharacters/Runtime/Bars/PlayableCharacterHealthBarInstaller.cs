using UnityEngine;
using BarSystem.Core;
using BarSystem.Config;
using BarSystem.Behaviors;
using BarSystem.View;

namespace BarSystem.Bars.Health
{
    /// <summary>
    /// Variante de HealthBarInstaller usada pelo PlayableCharacters: monta a
    /// mesma composição de BarModel + RegenBehavior opcional + View, mas NÃO
    /// persiste o próprio estado via IBarStateRepository. A vida faz parte
    /// dos dados salvos do personagem (junto com posição/rotação/etc.),
    /// gerenciada por um sistema externo - ver LoadState/GetSaveState
    /// abaixo, que esse sistema deve usar (a ser discutido depois).
    ///
    /// Sempre inicia nos valores padrão do BarConfigSO. Se houver estado
    /// salvo para restaurar, o sistema externo deve chamar LoadState()
    /// logo após este componente inicializar.
    /// </summary>
    public class PlayableCharacterHealthBarInstaller : MonoBehaviour
    {
        [SerializeField] private BarConfigSO _config;
        [SerializeField] private UISliderBarView _sliderView;

        [Header("Regeneration (Optional)")]
        [SerializeField] private bool _useRegen = false;
        [SerializeField] private float _regenPerSecond = 2f;

        [Header("Smoothing (Optional)")]
        [SerializeField] private bool _useSmoothing = true;
        [SerializeField] private float _smoothingSpeed = 6f;

        private BarController _controller;
        private SmoothedBarView _smoothedView;

        public BarModel Model { get; private set; }

        private void Awake()
        {
            // Sem IBarStateRepository aqui: sempre começa nos valores
            // padrão do BarConfigSO. Restaurar um estado salvo é
            // responsabilidade do sistema externo, via LoadState().
            Model = new BarModel(_config.Id, _config.MinDefault, _config.MaxDefault, _config.CurrentDefault);
            _controller = new BarController(Model);

            if (_useRegen)
                _controller.AddBehavior(new RegenBehavior(_regenPerSecond));

            IBarView view = _sliderView;
            if (_useSmoothing)
            {
                _smoothedView = new SmoothedBarView(_sliderView, _smoothingSpeed);
                view = _smoothedView;
            }

            _controller.AddView(view);
        }

        private void Update()
        {
            _controller.Tick(Time.deltaTime);
            _smoothedView?.Tick(Time.deltaTime);
        }

        public void ApplyDamage(float amount) => Model.ApplyDelta(-amount);
        public void ApplyHeal(float amount) => Model.ApplyDelta(amount);

        /// <summary>
        /// Ponto de entrada para o sistema externo de save/load restaurar o
        /// estado de vida deste personagem. Max é aplicado antes de Current,
        /// para o clamping respeitar corretamente um Max diferente do
        /// configurado no BarConfigSO no momento do save.
        /// </summary>
        public void LoadState(float max, float current)
        {
            Model.SetMax(max);
            Model.SetCurrent(current);
        }

        /// <summary>
        /// Ponto de saída para o sistema externo de save/load obter o
        /// estado atual a persistir.
        /// </summary>
        public BarSaveData GetSaveState()
        {
            return new BarSaveData(Model.Id, Model.Current, Model.Max);
        }

        private void OnDisable()
        {
            // Sem repository.Save() aqui - o sistema externo decide quando
            // e onde persistir, chamando GetSaveState() no momento certo.
            _controller.Dispose();
        }
    }
}