using UnityEngine;

public class ScavengerAnimator : MonoBehaviour
{
    public Animator animator;
    public System.Random random = new System.Random();

    void Start()
    {
        this.transform.localScale = new Vector3(1f, 1f, 1f);
    }
    void Update()
    {
        if (random.Next(0, 100) > 50)
        {
            SetWave();
        }
        else
        {
            animator.ResetTrigger("Wave");
        }
    }
    void SetWave()
    {
        animator.SetTrigger("Wave");
    }

    void OnDestroy()
    {
        animator.ResetTrigger("Wave");
    }
    void OnEnable()
    {
        animator.ResetTrigger("Wave");
    }
}
