using UnityEngine;

[ExecuteAlways] // Faz funcionar até mesmo no modo de edição
public class GrassEnvironmentController : MonoBehaviour
{
    [Header("Interação (Player/Objetos)")]
    public Transform player; 
    public float interactionRadius = 1.5f;
    public float interactionStrength = 0.8f;

    [Header("Vento Global")]
    public WindZone windZone;

    void Update()
    {
        // 1. Envia a posição do Player para as gramas
        if (player != null)
        {
            // SetGlobal permite que TODOS os materiais de grama leiam essa posição instantaneamente
            Shader.SetGlobalVector("_InteractorPos", player.position);
            Shader.SetGlobalFloat("_InteractionRadius", interactionRadius);
            Shader.SetGlobalFloat("_InteractionStrength", interactionStrength);
        }

        // 2. Envia os dados do WindZone para as gramas
        if (windZone != null)
        {
            // Pega a direção do vento multiplicada pela força principal
            Vector3 windForce = windZone.transform.forward * windZone.windMain;
            
            // Adiciona a turbulência usando o tempo
            float turbulence = Mathf.Sin(Time.time * windZone.windPulseFrequency) * windZone.windTurbulence;
            
            Shader.SetGlobalVector("_GlobalWindForce", windForce + (windZone.transform.forward * turbulence));
        }
    }
}