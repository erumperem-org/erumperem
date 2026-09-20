using System;
using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Abstrai "quem é o instigador atual das interações" para o sistema de
    /// interação. Não sabe nada sobre personagens jogáveis ou qualquer
    /// conceito de troca de personagem - isso é responsabilidade de um
    /// adapter específico do projeto (ver Bridges/PlayableCharacterInstigatorProvider).
    /// </summary>
    public interface IInstigatorProvider
    {
        GameObject Current { get; }
        event Action<GameObject> InstigatorChanged;
    }
}
