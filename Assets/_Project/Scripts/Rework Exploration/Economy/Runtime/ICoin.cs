using UnityEngine;
using Core.Storage;

namespace Core.Economy.Currency
{
    /// <summary>
    /// Contract for a currency. Structurally equivalent to IIITem, but
    /// without a use effect — coins are storageable (IStorageable) and have
    /// a visual presentation (Sprite), but are not "used" like items are.
    /// </summary>
    public interface ICoin : InterfaceStorageable
    {
        Sprite Sprite { get; }
    }
}
