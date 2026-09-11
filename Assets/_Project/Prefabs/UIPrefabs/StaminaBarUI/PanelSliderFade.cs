using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class PanelSliderFade : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Arraste aqui o Slider que vai controlar a visibilidade do painel.")]
    public UnityEngine.UI.Slider sliderReferencia;

    [Header("Configurações do Fade")]
    [Tooltip("Tempo em segundos para aguardar antes de iniciar o fade out quando o slider chegar a 1.")]
    public float tempoEspera = 2.0f;

    [Tooltip("Duração do efeito de fade out em segundos.")]
    public float duracaoFade = 1.0f;

    private CanvasGroup canvasGroup;
    private Coroutine rotinaFade;

    void Start()
    {
        // Obtém ou adiciona automaticamente o CanvasGroup no painel
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        if (sliderReferencia == null) return;

        // Se o valor do slider for exatamente igual a 1 (ou o valor máximo definido no slider)
        if (Mathf.Approximately(sliderReferencia.value, sliderReferencia.maxValue))
        {
            // Se ainda não iniciamos a contagem para o fade out, inicia a corrotina
            if (rotinaFade == null && canvasGroup.alpha > 0f)
            {
                rotinaFade = StartCoroutine(RotinaFadeOut());
            }
        }
        else
        {
            // Se o valor for menor que 1, cancela o fade e deixa visível instantaneamente
            if (rotinaFade != null)
            {
                StopCoroutine(rotinaFade);
                rotinaFade = null;
            }

            // Torna visível instantaneamente
            canvasGroup.alpha = 1.0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    private IEnumerator RotinaFadeOut()
    {
        // Aguarda o tempo configurado antes de sumir
        yield return new WaitForSeconds(tempoEspera);

        float tempoDecorrido = 0f;
        float alphaInicial = canvasGroup.alpha;

        while (tempoDecorrido < duracaoFade)
        {
            tempoDecorrido += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(alphaInicial, 0f, tempoDecorrido / duracaoFade);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        rotinaFade = null;
    }
}
