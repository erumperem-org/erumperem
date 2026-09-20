using System;
using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Marca um campo de referência (UnityEngine.Object, tipicamente um
    /// MonoBehaviour) como devendo implementar a interface informada.
    /// Puramente para o Editor (ver RequireInterfaceDrawer) - não valida
    /// nada em runtime, então quem consome o campo ainda deve castar com
    /// "as" e logar erro se vier null.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class RequireInterfaceAttribute : PropertyAttribute
    {
        public Type InterfaceType { get; }

        public RequireInterfaceAttribute(Type interfaceType)
        {
            InterfaceType = interfaceType;
        }
    }
}
