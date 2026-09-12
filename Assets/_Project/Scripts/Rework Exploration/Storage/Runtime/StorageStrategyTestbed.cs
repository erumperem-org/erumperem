using UnityEngine;

namespace Core.Storage
{
    /// <summary>
    /// Componente exclusivo para testagem em editor de uma IStorageStrategy
    /// isolada, sem precisar de um item real ou de um InventorySystem completo.
    /// Não deve ser usado em builds de produção.
    /// </summary>
    public sealed class StorageStrategyTestbed : MonoBehaviour
    {
        [SerializeReference] private IStorageStrategy _strategy = new StackableStorageStrategy();

        public IStorageStrategy Strategy => _strategy;
    }
}
