using System.Collections.Generic;

namespace BarSystem.Core
{
    /// <summary>
    /// Orchestrates a bar: keeps the BarModel updated, runs its behaviors
    /// on each tick, and propagates value changes to one or more views.
    /// It is the only point in the system that knows about Model + Behaviors + View
    /// at the same time.
    /// </summary>
    public class BarController
    {
        private readonly BarModel _model;
        private readonly List<IBarBehavior> _behaviors;
        private readonly List<IBarView> _views = new List<IBarView>();

        public BarModel Model => _model;

        public BarController(BarModel model, IEnumerable<IBarBehavior> behaviors = null)
        {
            _model = model;
            _behaviors = behaviors != null
                ? new List<IBarBehavior>(behaviors)
                : new List<IBarBehavior>();

            _model.OnValueChanged += HandleModelChanged;
            _model.OnMaxChanged += HandleMaxChanged;
        }

        public void AddView(IBarView view)
        {
            _views.Add(view);
            view.SetNormalizedValue(_model.Normalized);
        }

        public void RemoveView(IBarView view) => _views.Remove(view);

        public void AddBehavior(IBarBehavior behavior) => _behaviors.Add(behavior);

        public void RemoveBehavior(IBarBehavior behavior) => _behaviors.Remove(behavior);

        /// <summary>
        /// Call every frame (or every simulation step) to run the behaviors.
        /// </summary>
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _behaviors.Count; i++)
                _behaviors[i].Tick(deltaTime, _model);
        }

        private void HandleModelChanged(float _)
        {
            for (int i = 0; i < _views.Count; i++)
                _views[i].SetNormalizedValue(_model.Normalized);
        }

        private void HandleMaxChanged(float _) => HandleModelChanged(_model.Current);

        /// <summary>
        /// Unsubscribes from events. Call when the owner of the controller is destroyed.
        /// </summary>
        public void Dispose()
        {
            _model.OnValueChanged -= HandleModelChanged;
            _model.OnMaxChanged -= HandleMaxChanged;
        }
    }
}