using UnityEngine;
using UnityEngine.UI;

public class PulseColor : MonoBehaviour
{
    public float frequencia = 2.0f;

    private Image imagem;
    private Color corOriginal = Color.white;
    private Color corAlvo = Color.red;

    void Start()
    {
        imagem = GetComponent<Image>();
    }

    void Update()
    {
        if (imagem != null)
        {
            float t = (Mathf.Sin(Time.time * frequencia) + 1.0f) / 2.0f;

            imagem.color = Color.Lerp(corOriginal, corAlvo, t);
        }
    }
}
