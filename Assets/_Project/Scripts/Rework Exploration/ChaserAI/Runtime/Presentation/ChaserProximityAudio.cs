using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Som contínuo (loop) 3D do chaser, audível só quando o player está perto.
/// O volume pela distância é feito pela própria AudioSource (som 3D com rolloff linear,
/// chega a zero em Max Distance); este componente só liga/desliga a reprodução por
/// distância, para que dezenas de chasers distantes não ocupem vozes de áudio.
///
/// Independe do áudio de "avistou" (EnemySpot) do ChaserAI, que continua como estava.
/// Sem clip atribuído, o componente não faz nada.
/// </summary>
[DisallowMultipleComponent]
public class ChaserProximityAudio : MonoBehaviour
{
    [Header("Áudio")]
    [Tooltip("Loop tocado enquanto o player estiver perto (ex.: passos, rosnado).")]
    [SerializeField] private AudioClip loopClip;
    [SerializeField] private AudioMixerGroup outputMixerGroup;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("Proximidade")]
    [Tooltip("Até esta distância o volume é máximo.")]
    [SerializeField] private float minDistance = 2f;

    [Tooltip("Além desta distância o som é inaudível (e a reprodução é desligada).")]
    [SerializeField] private float maxDistance = 25f;

    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;

    [Tooltip("A reprodução só para depois de passar de Max Distance + esta margem (evita liga/desliga na borda).")]
    [SerializeField] private float stopMargin = 3f;

    [Tooltip("Intervalo, em segundos, entre as checagens de distância.")]
    [SerializeField] private float evaluationInterval = 0.25f;

    private ChaserAI chaser;
    private AudioSource source;
    private float timer;

    private void Awake()
    {
        chaser = GetComponent<ChaserAI>();

        if (loopClip == null)
        {
            enabled = false;
            return;
        }

        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        source.clip = loopClip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = rolloffMode;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = volume;
        source.outputAudioMixerGroup = outputMixerGroup;
    }

    private void OnEnable()
    {
        // Desfasa os chasers entre si para as checagens não caírem todas no mesmo frame.
        timer = Random.Range(0f, evaluationInterval);
    }

    private void OnDisable()
    {
        if (source != null)
        {
            source.Stop();
        }
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
        {
            return;
        }

        timer = evaluationInterval;
        Evaluate();
    }

    private void Evaluate()
    {
        Transform target = chaser != null ? chaser.Target : null;

        if (target == null)
        {
            if (source.isPlaying)
            {
                source.Stop();
            }

            return;
        }

        float sqrDistance = (target.position - transform.position).sqrMagnitude;

        if (!source.isPlaying)
        {
            if (sqrDistance <= maxDistance * maxDistance)
            {
                // Começa em um ponto aleatório do loop: vários chasers não tocam em fase.
                source.time = Random.Range(0f, loopClip.length * 0.99f);
                source.Play();
            }
        }
        else
        {
            float stopDistance = maxDistance + stopMargin;
            if (sqrDistance > stopDistance * stopDistance)
            {
                source.Stop();
            }
        }
    }

    private void OnValidate()
    {
        minDistance = Mathf.Max(0f, minDistance);
        maxDistance = Mathf.Max(minDistance + 0.1f, maxDistance);
        stopMargin = Mathf.Max(0f, stopMargin);
        evaluationInterval = Mathf.Max(0.05f, evaluationInterval);
    }
}
