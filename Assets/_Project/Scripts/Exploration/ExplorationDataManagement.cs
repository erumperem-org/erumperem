using System.Collections.Generic;
using UnityEngine;

public class ExplorationDataManagement : MonoBehaviour
{
    [SerializeField] private List<PlayableCharacter> playableCharacters;
    [SerializeField] private PlayableCharactersManager playableCharactersManager;

    public void SaveExplorationState()
    {
        var context = ExplorationLoadContext.Instance;
        context.SavePlayableCharactersState(playableCharactersManager, playableCharacters);
    }

    public void ResetExplorationContext()
    {
        var context = ExplorationLoadContext.Instance;
        context.ClearData();
    }

    public void LoadExplorationContext()
    {
        var context = ExplorationLoadContext.Instance;
        context.RestoreState();
    }
}
