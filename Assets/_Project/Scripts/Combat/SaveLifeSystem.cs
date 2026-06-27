using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct AllyHealthSaveEntry
{
    public string CharacterId;
    public float CurrentHealth;
}

public class SaveLifeSystem : MonoBehaviour
{
    public List<AllyHealthSaveEntry> healthEntries = new();
    public ExplorationLoadContext context;

    public void OnEnable()
    {
        if (context == null)
        {
            return;
        }

        foreach (var healthEntry in healthEntries)
        {
            context.SaveAllyCurrentHealth(healthEntry.CharacterId, healthEntry.CurrentHealth);
        }
    }
}
