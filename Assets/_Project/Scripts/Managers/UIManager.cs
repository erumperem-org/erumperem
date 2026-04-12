using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void OpenPanel(GameObject panel)
    {
        if (panel == null)
        {
            Debug.LogWarning("UIManager: OpenPanel called with a null panel reference.");
            return;
        }

        if (panel.activeSelf)
        {
            return;
        }
        panel.SetActive(true);
    }

    public void ClosePanel(GameObject panel)
    {
        if (panel == null)
        {
            Debug.LogWarning("UIManager: ClosePanel called with a null panel reference.");
            return;
        }

        if (!panel.activeSelf)
        {
            return;
        }
        panel.SetActive(false);
    }
}
