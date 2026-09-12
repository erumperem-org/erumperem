using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Systems.Audio
{
    [Serializable]
    public class SkillAudioMapping
    {
        [Tooltip("A ID exata da skill (ex: 'Slash', 'Shoot', 'Fireball')")]
        public string skillId;
        
        [Tooltip("Variações do som. O sistema escolherá uma aleatoriamente para não ficar repetitivo.")]
        public AudioClip[] skillClips;
        
        [Range(0f, 2f)] public float volume = 1f;
        
        [Tooltip("Opcional: Arraste o osso ou a ponta da arma (ex: cano do revólver).")]
        public Transform emissionBone;
    }

    public class CombatantSkillAudio : MonoBehaviour
    {
        [Header("Mixer Routing")]
        [Tooltip("Arraste o grupo SFX do seu AudioMixer para respeitar as configurações de volume.")]
        public AudioMixerGroup sfxMixerGroup;

        [Header("Skill Sounds Dictionary")]
        [Tooltip("Mapeie as skills específicas DESTE personagem para os seus sons correspondentes.")]
        public SkillAudioMapping[] skillSounds;

        [Header("3D Settings")]
        public float minDistance = 2f;
        public float maxDistance = 20f;

        private AudioSource _audioSource;
        private Transform _emitterTransform;

        private void Awake()
        {
            // Cria um objeto filho dedicado apenas para o áudio. 
            // Assim podemos movê-lo para a arma sem mover o personagem inteiro.
            GameObject emitterGO = new GameObject("SkillAudioEmitter");
            _emitterTransform = emitterGO.transform;
            _emitterTransform.SetParent(transform);
            _emitterTransform.localPosition = Vector3.zero;

            _audioSource = emitterGO.AddComponent<AudioSource>();
            
            // Força a matemática do 3D Space
            _audioSource.spatialBlend = 1f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = minDistance;
            _audioSource.maxDistance = maxDistance;
            _audioSource.playOnAwake = false;
            _audioSource.outputAudioMixerGroup = sfxMixerGroup;
        }

        /// <summary>
        /// Pode ser chamado via Animation Events ou por um script visual do personagem.
        /// Procura a skill na lista deste personagem e toca o som se existir.
        /// </summary>
        public void PlaySkillSound(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return;

            SkillAudioMapping mapping = Array.Find(skillSounds, x => x.skillId == skillId);
            
            if (mapping != null && mapping.skillClips != null && mapping.skillClips.Length > 0)
            {
                // Move o emissor dinamicamente para o cano da arma (se definido)
                if (mapping.emissionBone != null)
                {
                    _emitterTransform.position = mapping.emissionBone.position;
                }
                else
                {
                    _emitterTransform.localPosition = Vector3.zero;
                }

                // Sorteia matematicamente uma variação da lista de sons desta skill
                AudioClip clipToPlay = mapping.skillClips[UnityEngine.Random.Range(0, mapping.skillClips.Length)];

                // Varia levemente o pitch para ataques repetidos não soarem robóticos
                _audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                _audioSource.PlayOneShot(clipToPlay, mapping.volume);
            }
        }
    }
}