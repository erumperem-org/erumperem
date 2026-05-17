using UnityEngine;

namespace Services.Navigation
{
    /// <summary>
    /// MonoBehaviour singleton responsável por hospedar as Coroutines do
    /// <see cref="NavMeshService"/>. Criado automaticamente em runtime e mantido
    /// vivo entre cenas via <see cref="Object.DontDestroyOnLoad"/>.
    ///
    /// O serviço em si não é um MonoBehaviour — este host existe apenas para
    /// fornecer o contexto de ciclo de vida que Coroutines exigem no Unity.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class NavMeshCoroutineHost : MonoBehaviour
    {
        private static NavMeshCoroutineHost _instance;

        // ─────────────────────────────────────────────────────────────
        // Factory
        // ─────────────────────────────────────────────────────────────

        internal static NavMeshCoroutineHost GetOrCreate()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("[NavMeshCoroutineHost]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            _instance = go.AddComponent<NavMeshCoroutineHost>();
            DontDestroyOnLoad(go);
            return _instance;
        }

        // ─────────────────────────────────────────────────────────────
        // API interna
        // ─────────────────────────────────────────────────────────────

        internal Coroutine Run(System.Collections.IEnumerator routine)
            => StartCoroutine(routine);

        internal void Stop(Coroutine coroutine)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }

        // ─────────────────────────────────────────────────────────────
        // Ciclo de vida Unity
        // ─────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
