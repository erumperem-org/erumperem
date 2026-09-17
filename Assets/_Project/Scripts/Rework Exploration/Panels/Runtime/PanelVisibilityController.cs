using System;
using UnityEngine;

namespace Core.UI.Panels
{
    /// <summary>
    /// Central, static event bus for panel visibility changes. Static
    /// (rather than a scene singleton MonoBehaviour) specifically to avoid
    /// an initialization-order hazard: subscribing in OnEnable would
    /// otherwise depend on a singleton's Awake having already run in the
    /// same frame, which Unity does not guarantee across GameObjects. A
    /// static event has no such dependency — any script can subscribe the
    /// moment it exists, regardless of scene load order.
    /// </summary>
    public static class PanelVisibilityController
    {
        public static event Action<GameObject, bool> OnPanelVisibilityChanged;

        /// <summary>
        /// Broadcasts a panel visibility change to every subscriber. Does
        /// not itself change any GameObject's active state — callers are
        /// expected to have already applied SetActive before calling this.
        /// </summary>
        public static void NotifyVisibilityChanged(GameObject panel, bool isActive)
        {
            if (panel == null) return;
            OnPanelVisibilityChanged?.Invoke(panel, isActive);
        }
    }
}