using UnityEngine;

/// <summary>
/// Fachada de cena para operações de save/load de exploração.
/// Invocada por botões de UI, triggers de cena, etc.
///
/// MUDANÇAS:
///   - Não recebe mais <c>List&lt;PlayableCharacter&gt;</c> externamente —
///     o Manager expõe <c>Playables</c> diretamente.
///   - Métodos renomeados para refletir semântica clara.
/// </summary>
public sealed class ExplorationDataManagement : MonoBehaviour
{
    // ExplorationLoadContext é um singleton DontDestroyOnLoad — acesso via Instance.

    /// <summary>Persiste o estado atual dos personagens antes de trocar de cena.</summary>
    public void Save()  => ExplorationLoadContext.Instance?.SaveState();

    /// <summary>Restaura o estado salvo (ou aplica o padrão configurado).</summary>
    public void Load()  => ExplorationLoadContext.Instance?.RestoreState();

    /// <summary>Apaga o save (novo jogo).</summary>
    public void Reset() => ExplorationLoadContext.Instance?.ClearSave();
}
