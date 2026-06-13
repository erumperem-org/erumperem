using UnityEngine;

/// <summary>
/// Gatilho de combate para inimigos estáticos colocados na cena (ex.: fantasma da vila).
/// Os NPCs da pool usam <see cref="Systems.NPC.Enemy.NpcEnemyContactHandler"/>.
/// </summary>
public class EnemyCollissionTrigger : MonoBehaviour
{
    private const string CombatSceneName = "CombatScene";

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        SceneTransitionHandler.LoadScene(CombatSceneName);
    }

    private static bool IsPlayerCollider(Collider collider)
    {
        if (collider.CompareTag("Player"))
            return true;

        return collider.GetComponentInParent<PlayableCharacter>() != null;
    }
}
