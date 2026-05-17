/// <summary>
/// Define como itens são armazenados dentro do inventário.
/// </summary>
public enum StorageMode
{
    /// <summary>
    /// Apenas uma instância do item pode existir no inventário.
    /// </summary>

    Unique,

    /// <summary>
    /// Múltiplas unidades do mesmo item ocupam o mesmo slot,
    /// </summary>
    Stackable,

    /// <summary>
    /// Cada item ocupa um slot individual,
    /// mesmo sendo do mesmo tipo.
    /// </summary>
    SingleSlot,

    /// <summary>
    /// Não possui limite de quantidade ou slots.
    /// Normalmente usado para sistemas simplificados.
    /// </summary>
    Unlimited,
}