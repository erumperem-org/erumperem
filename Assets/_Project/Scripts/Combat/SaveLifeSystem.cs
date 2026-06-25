using System.Collections.Generic;
using UnityEngine;
using Erumperem.Characters;
public class SaveLifeSystem : MonoBehaviour
{
    public List<AllyCharacterStatDefinition> definitions;
    public ExplorationLoadContext context;
    public void OnEnable()
    {
        foreach (var data in definitions)
        {
            context.SaveLifeFromDefinition(data);
        }
    }
}
