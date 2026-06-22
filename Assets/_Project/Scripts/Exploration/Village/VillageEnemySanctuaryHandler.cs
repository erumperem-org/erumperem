using Systems.NPC.Pool;
using Systems.NPC.Spawner;
using UnityEngine;

/// <summary>
/// Enquanto o jogador estiver na vila: para spawns, devolve inimigos ativos à pool
/// e desativa contactos de inimigos estáticos (fantasmas).
/// </summary>
public sealed class VillageEnemySanctuaryHandler : MonoBehaviour
{
    [SerializeField] private NpcEnemySpawner[] _enemySpawners;
    [SerializeField] private NpcEnemyPool _enemyPool;
    [SerializeField] private StaticExplorationEnemyContact[] _staticEnemyContacts;
    [SerializeField] private EnemyCollissionTrigger[] _staticEnemyCollisionTriggers;

    private bool _sanctuaryActive;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ExplorationVillageEvents.OnPlayerEnteredVillage += ActivateVillageSanctuary;
        ExplorationVillageEvents.OnPlayerExitedVillage += DeactivateVillageSanctuary;

        if (ExplorationVillageEvents.IsPlayerInsideVillage)
        {
            ActivateVillageSanctuary();
        }
    }

    private void OnDisable()
    {
        ExplorationVillageEvents.OnPlayerEnteredVillage -= ActivateVillageSanctuary;
        ExplorationVillageEvents.OnPlayerExitedVillage -= DeactivateVillageSanctuary;
    }

    private void ResolveDependencies()
    {
        if (_enemySpawners == null || _enemySpawners.Length == 0)
        {
            _enemySpawners = FindObjectsByType<NpcEnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        if (_enemyPool == null)
        {
            _enemyPool = FindFirstObjectByType<NpcEnemyPool>(FindObjectsInactive.Include);
        }

        if (_staticEnemyContacts == null || _staticEnemyContacts.Length == 0)
        {
            _staticEnemyContacts = FindObjectsByType<StaticExplorationEnemyContact>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        if (_staticEnemyCollisionTriggers == null || _staticEnemyCollisionTriggers.Length == 0)
        {
            _staticEnemyCollisionTriggers = FindObjectsByType<EnemyCollissionTrigger>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }
    }

    private void ActivateVillageSanctuary()
    {
        if (_sanctuaryActive)
        {
            return;
        }

        _sanctuaryActive = true;
        ResolveDependencies();

        if (_enemySpawners != null)
        {
            for (var spawnerIndex = 0; spawnerIndex < _enemySpawners.Length; spawnerIndex++)
            {
                var enemySpawner = _enemySpawners[spawnerIndex];
                if (enemySpawner != null)
                {
                    enemySpawner.StopSpawning();
                }
            }
        }

        _enemyPool?.ReturnAllActive();
        SetStaticEnemyContactsEnabled(false);
    }

    private void DeactivateVillageSanctuary()
    {
        if (!_sanctuaryActive)
        {
            return;
        }

        _sanctuaryActive = false;
        SetStaticEnemyContactsEnabled(true);

        if (_enemySpawners == null)
        {
            return;
        }

        for (var spawnerIndex = 0; spawnerIndex < _enemySpawners.Length; spawnerIndex++)
        {
            var enemySpawner = _enemySpawners[spawnerIndex];
            if (enemySpawner != null && enemySpawner.isActiveAndEnabled)
            {
                enemySpawner.StartSpawning();
            }
        }
    }

    private void SetStaticEnemyContactsEnabled(bool areContactsEnabled)
    {
        if (_staticEnemyContacts != null)
        {
            for (var contactIndex = 0; contactIndex < _staticEnemyContacts.Length; contactIndex++)
            {
                var staticEnemyContact = _staticEnemyContacts[contactIndex];
                if (staticEnemyContact != null)
                {
                    staticEnemyContact.enabled = areContactsEnabled;
                }
            }
        }

        if (_staticEnemyCollisionTriggers != null)
        {
            for (var triggerIndex = 0; triggerIndex < _staticEnemyCollisionTriggers.Length; triggerIndex++)
            {
                var collisionTrigger = _staticEnemyCollisionTriggers[triggerIndex];
                if (collisionTrigger != null)
                {
                    collisionTrigger.enabled = areContactsEnabled;
                }
            }
        }
    }
}
