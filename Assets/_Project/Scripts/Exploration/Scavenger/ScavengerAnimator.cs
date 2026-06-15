using UnityEngine;

public class ScavengerAnimator : MonoBehaviour
{
    public Animator animator;
    public System.Random random = new System.Random();
    
    void Update()
    {
        if(random.Next(0,100) > 50)
        {
            SetWave();
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
