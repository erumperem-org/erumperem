using Services.DebugUtilities.Console;
using UnityEngine;

namespace DetectionSystem.Core
{
    /// <summary>
    /// Adicione a qualquer objeto que deva ser monitorado pelo <see cref="DetectionUsageExample"/>.
    /// Registra e desregistra o próprio collider automaticamente no detector mais próximo.
    /// Sobrescreva OnDetectionEnter/Exit ou assine os eventos para reagir.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DetectionReceiver : MonoBehaviour
    {
        public System.Action<Detector, string, int> OnEnter;
        public System.Action<Detector, string, int> OnExit;

        private Detector _detector;
        private Collider _col;

        void Awake()
        {
            _col = GetComponent<Collider>();
            _detector = FindAnyObjectByType<Detector>();
        }

        void OnEnable()
        {
            _detector?.Register(_col);
        }

        void OnDisable()
        {
            _detector?.Unregister(_col);
        }

        public void NotifyEnter(Detector detector, string shapeLabel, int shapeIndex)
        {
            OnEnter?.Invoke(detector, shapeLabel, shapeIndex);
            OnDetectionEnter(detector, shapeLabel, shapeIndex);
        }

        public void NotifyExit(Detector detector, string shapeLabel, int shapeIndex)
        {
            OnExit?.Invoke(detector, shapeLabel, shapeIndex);
            OnDetectionExit(detector, shapeLabel, shapeIndex);
        }

        protected virtual void OnDetectionEnter(Detector detector, string shapeLabel, int shapeIndex)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.World, $"[Receiver] {name} ENTER '{shapeLabel}' (index {shapeIndex}) via {detector.name}");
        }

        protected virtual void OnDetectionExit(Detector detector, string shapeLabel, int shapeIndex)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.World, $"[Receiver] {name} EXIT  '{shapeLabel}' (index {shapeIndex}) via {detector.name}");
        }
    }
}