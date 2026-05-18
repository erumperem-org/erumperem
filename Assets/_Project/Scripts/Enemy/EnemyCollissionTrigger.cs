using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemyCollissionTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            SceneManager.LoadScene("CombatScene");
        }
    }
}
