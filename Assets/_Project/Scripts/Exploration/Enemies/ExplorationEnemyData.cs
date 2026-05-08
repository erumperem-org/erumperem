using System;
using UnityEngine;
using UnityEngine.AI;

// FIX: Changed from struct to class.
// Storing interface references (IEnemyStartegy, IEnemyStartegyContext) inside a
// struct causes boxing every time the struct is copied or passed by value, which
// generates GC pressure. A class stores the reference directly on the heap with
// no boxing overhead.
[Serializable]
public class ExplorationEnemyData
{
    [Header("Exposed strategy on inspector")]
    public string enemyStartegyExposed;
    public IEnemyStartegy _enemyStartegy;
    public IEnemyStartegyContext currentContext;

    [Header("Navmesh")]
    public NavMeshAgent agent;

    [Header("Identificator")]
    public string enemyId;

    [Header("Properties")]
    public ExplorationEnemyLevels enemyLevel;
    public float perceptionRadius;
    public float patrolRadius;
}
