using System.Collections.Generic;
using UnityEngine;

namespace Core.Exploration.Items
{
    /// <summary>
    /// Validação estrutural de um NewItemRegistry: tipagem, presença e unicidade
    /// de StorageableId. Não depende de UnityEditor, podendo ser reutilizada
    /// em testes automatizados (EditMode/PlayMode) ou em um passo de CI.
    /// </summary>
    public static class NewItemRegistryValidator
    {
        public readonly struct ValidationError
        {
            public readonly string Message;
            public readonly ScriptableObject Context;

            public ValidationError(string message, ScriptableObject context)
            {
                Message = message;
                Context = context;
            }
        }

        public static IReadOnlyList<ValidationError> Validate(NewItemRegistry registry)
        {
            var errors = new List<ValidationError>();
            var seenIds = new Dictionary<string, ScriptableObject>();

            foreach (var obj in registry.Items)
            {
                if (obj == null)
                {
                    errors.Add(new ValidationError("Elemento nulo na lista de itens.", registry));
                    continue;
                }

                if (obj is not IIITem item)
                {
                    errors.Add(new ValidationError($"'{obj.name}' não implementa IIITem.", obj));
                    continue;
                }

                if (string.IsNullOrEmpty(item.StorageableId))
                {
                    errors.Add(new ValidationError($"'{obj.name}' tem StorageableId vazio.", obj));
                    continue;
                }

                if (seenIds.TryGetValue(item.StorageableId, out var existing))
                {
                    errors.Add(new ValidationError(
                        $"StorageableId duplicado '{item.StorageableId}': '{existing.name}' e '{obj.name}'.", obj));
                    continue;
                }

                seenIds[item.StorageableId] = obj;
            }

            return errors;
        }
    }
}
