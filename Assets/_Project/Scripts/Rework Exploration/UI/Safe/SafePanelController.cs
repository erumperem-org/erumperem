using UnityEngine;

public class SafePanelController : MonoBehaviour
{
    [SerializeField] private GameObject safePanel;

    void Update()
    {
        if (safePanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseSafe();
        }
    }

    public void OpenSafe()
    {
        safePanel.SetActive(true);
        Time.timeScale = 0f; 
    }

    public void CloseSafe()
    {
        safePanel.SetActive(false);
        Time.timeScale = 1f;
    }
}
