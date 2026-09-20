namespace InteractionSystem.Concrete
{
    /// <summary>
    /// Não adiciona nenhum comportamento próprio hoje - existe como tipo
    /// nomeado para clareza no Hierarchy/Inspector e como ponto de extensão
    /// futuro (ex: sobrescrever CreateContext para carregar uma tabela de
    /// loot). Toda a integração externa é feita via onInteractionStarted
    /// (herdado), e a reativação fica por conta de um
    /// ProximityReactivationSensor separado, pareado com este componente -
    /// o baú não sabe reativar a si mesmo.
    /// </summary>
    public class ChestInteractable : InteractableBase
    {
    }
}
