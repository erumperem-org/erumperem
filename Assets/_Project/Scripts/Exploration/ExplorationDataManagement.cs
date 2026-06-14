using System;
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
    public void Save() => InvokeOnLoadContext(loadContext => loadContext.SaveState());

    /// <summary>Restaura o estado salvo (ou aplica o padrão configurado).</summary>
    public void Load() => InvokeOnLoadContext(loadContext => loadContext.RestoreState());

    /// <summary>Apaga o save (novo jogo).</summary>
    public void Reset() => InvokeOnLoadContext(loadContext => loadContext.ClearSave());

    /// <summary>Compatibilidade com botões de UI do Overworld.</summary>
    public void SaveExplorationState() => Save();

    /// <summary>Compatibilidade com botões de UI do Overworld.</summary>
    public void LoadExplorationContext() => Load();

    /// <summary>Apaga o save e restaura o estado padrão da cena.</summary>
    public void ResetExplorationContext()
    {
        var loadContext = ExplorationLoadContext.Instance;
        if (loadContext == null)
        {
            Debug.LogWarning("[ExplorationDataManagement] ExplorationLoadContext.Instance é nulo — reset ignorado.");
            return;
        }

        loadContext.ClearSave();
        loadContext.RestoreState();
    }

    private static void InvokeOnLoadContext(Action<ExplorationLoadContext> action)
    {
        var loadContext = ExplorationLoadContext.Instance;
        if (loadContext == null)
        {
            Debug.LogWarning("[ExplorationDataManagement] ExplorationLoadContext.Instance é nulo — operação ignorada.");
            return;
        }

        action(loadContext);
    }
}
