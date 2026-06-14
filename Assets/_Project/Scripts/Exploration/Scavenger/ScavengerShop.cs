using UnityEngine;

public class ScavengerShop : MonoBehaviour
{
    public GameObject skillTreePanel;
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            skillTreePanel.SetActive(true);
        }
    }
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            skillTreePanel.SetActive(false);
        }
    }
}
