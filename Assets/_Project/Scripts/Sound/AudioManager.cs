using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    public Playlist[] bgmPlaylists; 
    public Sound[] sfxClips;        
    [InspectorName("Ambience")] public Sound[] ambientLoops;

    public AudioSource bgmSource;
    public AudioSource sfxSource;
    public AudioSource ambientSource;

    private Playlist currentPlaylist;
    private int lastBGMIndex = -1;
    private bool isBGMPlayingState = false;
    private Tween _bgmFade;
    private readonly List<AudioSource> _musicTails = new();
    private AudioSource _sliderVoice;
    private readonly List<AudioSource> _enemySpotVoices = new();
    [SerializeField, Min(0.1f)] private float _enemySpotFadeSeconds = 1.2f;

    public void PlaySliderTick(string soundName)
    {
        var sound = sfxClips == null ? null : Array.Find(sfxClips, item => item != null && item.name == soundName);
        var clip = sound?.clips == null ? null : Array.Find(sound.clips, item => item != null);
        if (clip == null || sfxSource == null) return;
        if (_sliderVoice == null)
        {
            var emitter = new GameObject("SliderAudio");
            emitter.transform.SetParent(transform, false);
            _sliderVoice = emitter.AddComponent<AudioSource>();
            _sliderVoice.playOnAwake = false;
        }
        if (_sliderVoice.isPlaying) return;
        _sliderVoice.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
        _sliderVoice.volume = sound.volume * sfxSource.volume * 0.4f;
        _sliderVoice.mute = sfxSource.mute;
        _sliderVoice.pitch = sound.pitch;
        _sliderVoice.clip = clip;
        _sliderVoice.Play();
    }

    public void FadeEnemyDetectionSounds()
    {
        foreach (var voice in _enemySpotVoices)
        {
            if (voice == null || !voice.isPlaying) continue;
            voice.spatialBlend = 0f;
            var reverb = voice.GetComponent<AudioReverbFilter>();
            reverb.reverbPreset = AudioReverbPreset.Hallway;
            reverb.enabled = true;
            voice.DOFade(0f, _enemySpotFadeSeconds).SetUpdate(true)
                .SetLink(voice.gameObject).OnComplete(() =>
                {
                    if (voice != null) Destroy(voice.gameObject);
                });
        }
        _enemySpotVoices.Clear();
    }
    public int BgmRevision { get; private set; }
    public string CurrentPlaylistName => currentPlaylist?.name;

    public bool HasBGM(string playlistName)
    {
        if (string.IsNullOrWhiteSpace(playlistName)) return false;
        var playlist = bgmPlaylists == null ? null : Array.Find(bgmPlaylists, item => item != null && item.name == playlistName);
        return playlist?.clips != null && Array.Exists(playlist.clips, clip => clip != null);
    }
    private readonly List<AudioSource> _sfxVoices = new();

    [Header("Music Transitions")]
    [SerializeField, Min(0f)] private float _musicFadeInSeconds = 0.45f;
    [SerializeField, Min(0f)] private float _battleEndFadeSeconds = 0.8f;
    [SerializeField, Min(0.05f)] private float _musicTransitionFadeSeconds = 0.65f;
    [SerializeField, Range(0.1f, 5f)] private float _musicTransitionReverbSeconds = 1.2f;
    [SerializeField, Range(-10000f, 0f)] private float _musicTransitionReverbLevel = -1700f;

    private void FadeCurrentMusicWithReverb(float duration)
    {
        if (bgmSource == null || !bgmSource.isPlaying || bgmSource.clip == null) return;
        if (bgmSource.volume <= 0.001f)
        {
            bgmSource.Stop();
            return;
        }

        _musicTails.RemoveAll(source => source == null);
        if (_musicTails.Count >= 2)
        {
            var oldest = _musicTails[0];
            oldest.DOKill();
            Destroy(oldest.gameObject);
            _musicTails.RemoveAt(0);
        }

        var emitter = new GameObject("MusicTransitionTail");
        emitter.transform.SetParent(transform, false);
        var tail = emitter.AddComponent<AudioSource>();
        tail.playOnAwake = false;
        tail.outputAudioMixerGroup = bgmSource.outputAudioMixerGroup;
        tail.clip = bgmSource.clip;
        tail.volume = bgmSource.volume;
        tail.pitch = bgmSource.pitch;
        tail.mute = bgmSource.mute;
        tail.priority = bgmSource.priority;
        tail.panStereo = bgmSource.panStereo;
        tail.ignoreListenerPause = bgmSource.ignoreListenerPause;
        tail.loop = false;
        tail.spatialBlend = 0f;
        var reverb = emitter.AddComponent<AudioReverbFilter>();
        reverb.reverbPreset = AudioReverbPreset.User;
        reverb.decayTime = _musicTransitionReverbSeconds;
        reverb.room = -1000f;
        reverb.reverbLevel = _musicTransitionReverbLevel;
        tail.timeSamples = Mathf.Clamp(bgmSource.timeSamples, 0, tail.clip.samples - 1);
        bgmSource.Stop();
        tail.Play();
        _musicTails.Add(tail);
        tail.DOFade(0f, Mathf.Max(_musicTransitionFadeSeconds, duration))
            .SetEase(Ease.OutSine).SetUpdate(true).SetLink(emitter)
            .OnComplete(() =>
            {
                _musicTails.Remove(tail);
                if (tail != null) Destroy(tail.gameObject);
            });
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            EnsureMixerRouting();
            EnsureExplorationAmbientEmitter();
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            instance.MergeConfigurationFrom(this);
            Destroy(gameObject);
        }
    }

    private void MergeConfigurationFrom(AudioManager source)
    {
        if (source == null || source == this) return;

        if (mainMixer == null) mainMixer = source.mainMixer;
        bgmPlaylists = MergePlaylists(bgmPlaylists, source.bgmPlaylists);
        sfxClips = MergeSounds(sfxClips, source.sfxClips);
        ambientLoops = MergeSounds(ambientLoops, source.ambientLoops);

        EnsureMixerRouting();
        EnsureExplorationAmbientEmitter();
    }

    private void EnsureExplorationAmbientEmitter()
    {
        if (!HasAmbientProfile("ExplorationOneShots")) return;
        if (GetComponent<ExplorationAmbientEmitter>() == null)
            gameObject.AddComponent<ExplorationAmbientEmitter>();
    }

    private bool HasAmbientProfile(string soundName)
    {
        return ambientLoops != null && Array.Exists(ambientLoops,
            item => item != null && item.name == soundName);
    }

    private static Playlist[] MergePlaylists(Playlist[] current, Playlist[] source)
    {
        if (source == null || source.Length == 0) return current;
        var merged = current == null ? new List<Playlist>() : new List<Playlist>(current);
        foreach (var item in source)
        {
            if (item == null || string.IsNullOrEmpty(item.name)) continue;
            if (!merged.Exists(existing => existing != null && existing.name == item.name))
                merged.Add(item);
        }
        return merged.ToArray();
    }

    private static Sound[] MergeSounds(Sound[] current, Sound[] source)
    {
        if (source == null || source.Length == 0) return current;
        var merged = current == null ? new List<Sound>() : new List<Sound>(current);
        foreach (var item in source)
        {
            if (item == null || string.IsNullOrEmpty(item.name)) continue;
            if (!merged.Exists(existing => existing != null && existing.name == item.name))
                merged.Add(item);
        }
        return merged.ToArray();
    }

    private void EnsureMixerRouting()
    {
        if (!TryResolveMainMixer()) return;

        AssignMixerGroup(bgmSource, "BGM");
        AssignMixerGroup(sfxSource, "SFX");
        AssignMixerGroup(ambientSource, "Ambient");
    }

    private bool TryResolveMainMixer()
    {
        if (mainMixer != null) return true;

        var sources = new[] { bgmSource, sfxSource, ambientSource };
        foreach (var source in sources)
        {
            if (source == null || source.outputAudioMixerGroup == null) continue;
            mainMixer = source.outputAudioMixerGroup.audioMixer;
            if (mainMixer != null) return true;
        }

        var loadedSources = Resources.FindObjectsOfTypeAll<AudioSource>();
        foreach (var source in loadedSources)
        {
            var group = source != null ? source.outputAudioMixerGroup : null;
            var mixer = group != null ? group.audioMixer : null;
            if (mixer == null || mixer.name != "MainMixer") continue;
            mainMixer = mixer;
            return true;
        }

        var mixers = Resources.FindObjectsOfTypeAll<AudioMixer>();
        mainMixer = Array.Find(mixers, mixer => mixer != null && mixer.name == "MainMixer");
        return mainMixer != null;
    }

    private void AssignMixerGroup(AudioSource source, string groupName)
    {
        if (source == null || mainMixer == null) return;

        var groups = mainMixer.FindMatchingGroups($"Master/{groupName}");
        var group = Array.Find(groups, item => item != null && item.name == groupName);
        if (group == null)
        {
            groups = mainMixer.FindMatchingGroups(groupName);
            group = Array.Find(groups, item => item != null && item.name == groupName);
        }
        if (group != null) source.outputAudioMixerGroup = group;
    }

    private AudioMixerGroup GetAmbientMixerGroup()
    {
        if (ambientSource != null && ambientSource.outputAudioMixerGroup != null)
        {
            var groupMixer = ambientSource.outputAudioMixerGroup.audioMixer;
            if (ambientSource.outputAudioMixerGroup.name == "Ambient"
                && (mainMixer == null || groupMixer == mainMixer))
                return ambientSource.outputAudioMixerGroup;
        }

        if (mainMixer == null) return null;

        var groups = mainMixer.FindMatchingGroups("Master/Ambient");
        var group = Array.Find(groups, item => item != null && item.name == "Ambient");
        if (group != null) return group;

        groups = mainMixer.FindMatchingGroups("Ambient");
        return Array.Find(groups, item => item != null && item.name == "Ambient");
    }

    public bool TryRouteAmbientSource(AudioSource source)
    {
        if (source == null) return false;

        EnsureMixerRouting();
        var group = GetAmbientMixerGroup();
        if (group == null) return false;

        source.outputAudioMixerGroup = group;
        return true;
    }

    private void Start()
    {
        if (instance != this) return;
        ApplyMixerVolume("MasterVolume", PlayerPrefs.GetFloat("PrefMasterVolume", 1f));
        ApplyMixerVolume("BGMVolume", PlayerPrefs.GetFloat("PrefBGMVolume", 1f));
        ApplyMixerVolume("SFXVolume", PlayerPrefs.GetFloat("PrefSFXVolume", 1f));
        ApplyMixerVolume("AmbientVolume", PlayerPrefs.GetFloat("PrefAmbientVolume", 1f));
    }

    private void Update()
    {
        if (isBGMPlayingState && bgmSource != null && !bgmSource.isPlaying && currentPlaylist != null)
        {
            PlayNextRandomBGM();
        }
    }

    public void PlayBGM(string playlistName)
    {
        if (bgmSource == null || !HasBGM(playlistName))
        {
            StopBGM();
            return;
        }
        Playlist p = Array.Find(bgmPlaylists, x => x != null && x.name == playlistName);
        if (p != null && p.clips != null && p.clips.Length > 0)
        {
            BgmRevision++;
            _bgmFade?.Kill();
            FadeCurrentMusicWithReverb(_musicTransitionFadeSeconds);
            currentPlaylist = p;
            bgmSource.volume = p.volume;
            bgmSource.pitch = p.pitch;
            bgmSource.loop = false; 
            isBGMPlayingState = true;
            lastBGMIndex = -1; 

            PlayNextRandomBGM();
        }
    }

    public void FadeInBGM(string playlistName)
    {
        PlayBGM(playlistName);
        if (bgmSource == null || currentPlaylist == null || currentPlaylist.name != playlistName) return;
        bgmSource.volume = 0f;
        _bgmFade = bgmSource.DOFade(currentPlaylist.volume, _musicFadeInSeconds).SetUpdate(true);
    }

    public void FadeOutBGM(float duration)
    {
        BgmRevision++;
        _bgmFade?.Kill();
        isBGMPlayingState = false;
        currentPlaylist = null;
        lastBGMIndex = -1;
        if (bgmSource == null) return;
        FadeCurrentMusicWithReverb(Mathf.Max(0f, duration));
        bgmSource.volume = 0f;
    }

    public void PlayCombatOutcome(bool victory)
    {
        FadeOutBGM(_battleEndFadeSeconds);
        if (victory) PlaySFX("CombatVictory");
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        _bgmFade?.Kill();
        foreach (var tail in _musicTails)
            if (tail != null) tail.DOKill();
        instance = null;
    }

    private void PlayNextRandomBGM()
    {
        var clips = currentPlaylist?.clips;
        int validCount = clips == null ? 0 : Array.FindAll(clips, clip => clip != null).Length;
        if (validCount == 0)
        {
            StopBGM();
            return;
        }
        bool skipLast = validCount > 1 && lastBGMIndex >= 0 && lastBGMIndex < clips.Length && clips[lastBGMIndex] != null;
        int selection = UnityEngine.Random.Range(0, validCount - (skipLast ? 1 : 0));
        for (int index = 0; index < clips.Length; index++)
        {
            if (clips[index] == null || (skipLast && index == lastBGMIndex)) continue;
            if (selection-- != 0) continue;
            lastBGMIndex = index;
            bgmSource.clip = clips[index];
            bgmSource.Play();
            return;
        }
    }

    public void PlaySFX(string soundName, float volumeMultiplier = 1f)
        => PlaySFXInternal(soundName, volumeMultiplier, null);

    public void PlaySFXAtPosition(string soundName, Vector3 position, float volumeMultiplier = 1f)
        => PlaySFXInternal(soundName, volumeMultiplier, position);

    private void PlaySFXInternal(string soundName, float volumeMultiplier, Vector3? position)
    {
        if (sfxClips == null || sfxSource == null || string.IsNullOrEmpty(soundName)) return;

        Sound s = Array.Find(sfxClips, x => x != null && x.name == soundName);
        if (s != null && s.clips != null && s.clips.Length > 0)
        {
            int randomIndex = 0;

            if (s.clips.Length > 1)
            {
                do
                {
                    randomIndex = UnityEngine.Random.Range(0, s.clips.Length);
                } while (randomIndex == s.lastPlayedIndex);
            }

            s.lastPlayedIndex = randomIndex;
            if (s.clips[randomIndex] != null)
            {
                var voice = soundName == "EnemySpot" ? null : GetAvailableSfxVoice(position.HasValue);
                if (soundName == "EnemySpot")
                {
                    var emitter = new GameObject("EnemySpotAudio");
                    emitter.transform.SetParent(transform, false);
                    voice = emitter.AddComponent<AudioSource>();
                    voice.playOnAwake = false;
                    voice.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
                    voice.volume = sfxSource.volume;
                    voice.mute = sfxSource.mute;
                    voice.dopplerLevel = 0f;
                    emitter.AddComponent<AudioReverbFilter>().enabled = false;
                    _enemySpotVoices.RemoveAll(item => item == null);
                    _enemySpotVoices.Add(voice);
                    Destroy(emitter, s.clips[randomIndex].length / Mathf.Max(0.01f, Mathf.Abs(s.pitch)) + 3f);
                }
                if (position.HasValue)
                {
                    voice.transform.position = position.Value;
                    voice.spatialBlend = 1f;
                    voice.spread = 0f;
                    voice.rolloffMode = AudioRolloffMode.Linear;
                    voice.minDistance = 2f;
                    voice.maxDistance = 40f;
                }
                else
                {
                    voice.spatialBlend = 0f;
                    if (voice != sfxSource) voice.transform.localPosition = Vector3.zero;
                }
                voice.pitch = s.pitch;
                voice.PlayOneShot(s.clips[randomIndex], s.volume * volumeMultiplier);
            }
        }
    }

    private AudioSource GetAvailableSfxVoice(bool positional)
    {
        AudioSource voice = null;
        if (!positional && !sfxSource.isPlaying)
            voice = sfxSource;
        else
        {
            foreach (var candidate in _sfxVoices)
            {
                if (candidate != null && !candidate.isPlaying)
                {
                    voice = candidate;
                    break;
                }
            }
        }

        if (voice == null)
        {
            var emitter = new GameObject("SfxVoice");
            emitter.transform.SetParent(sfxSource.transform, false);
            voice = emitter.AddComponent<AudioSource>();
            _sfxVoices.Add(voice);
        }

        voice.playOnAwake = false;
        voice.loop = false;
        voice.dopplerLevel = 0f;
        if (voice != sfxSource)
        {
            voice.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
            voice.volume = sfxSource.volume;
            voice.mute = sfxSource.mute;
            voice.priority = sfxSource.priority;
            voice.panStereo = sfxSource.panStereo;
            voice.spatialBlend = sfxSource.spatialBlend;
            voice.spatialize = sfxSource.spatialize;
            voice.spread = sfxSource.spread;
            voice.minDistance = sfxSource.minDistance;
            voice.maxDistance = sfxSource.maxDistance;
            voice.rolloffMode = sfxSource.rolloffMode;
            voice.reverbZoneMix = sfxSource.reverbZoneMix;
            voice.ignoreListenerPause = sfxSource.ignoreListenerPause;
            voice.ignoreListenerVolume = sfxSource.ignoreListenerVolume;
            if (sfxSource.rolloffMode == AudioRolloffMode.Custom)
                voice.SetCustomCurve(AudioSourceCurveType.CustomRolloff,
                    sfxSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
        }
        return voice;
    }

    public bool TryConfigureAmbientSource(string soundName, AudioSource source, bool randomClip = false)
    {
        if (source == null || ambientLoops == null) return false;
        var sound = Array.Find(ambientLoops, item => item != null && item.name == soundName);
        if (sound == null || sound.clips == null) return false;
        var clip = Array.Find(sound.clips, item => item != null);
        if (clip == null) return false;
        if (randomClip)
        {
            var candidates = Array.FindAll(sound.clips, item => item != null && item != source.clip);
            if (candidates.Length > 0) clip = candidates[UnityEngine.Random.Range(0, candidates.Length)];
        }
        source.clip = clip;
        source.volume = sound.volume;
        source.pitch = sound.pitch;
        TryRouteAmbientSource(source);
        return true;
    }


    public void PlayAmbientLoop(string soundName)
    {
        Sound s = Array.Find(ambientLoops, x => x.name == soundName);
        if (s != null && s.clips.Length > 0)
        {
            TryRouteAmbientSource(ambientSource);
            ambientSource.clip = s.clips[0]; 
            ambientSource.volume = s.volume;
            ambientSource.pitch = s.pitch;
            ambientSource.loop = true;
            ambientSource.Play();
        }
    }

    public void StopBGM()
    {
        BgmRevision++;
        _bgmFade?.Kill();
        isBGMPlayingState = false;
        currentPlaylist = null;
        lastBGMIndex = -1;
        if (bgmSource != null)
        {
            FadeCurrentMusicWithReverb(_musicTransitionFadeSeconds);
            bgmSource.Stop();
            bgmSource.clip = null;
        }
    }

    public void StopAmbientLoop()
    {
        ambientSource.Stop();
    }
    [Header("Audio Mixer")]
    public AudioMixer mainMixer;

    public void SetMasterVolume(float sliderValue)
    {
        ApplyMixerVolume("MasterVolume", sliderValue);
        PlayerPrefs.SetFloat("PrefMasterVolume", sliderValue);
    }

    public void SetBGMVolume(float sliderValue)
    {
        ApplyMixerVolume("BGMVolume", sliderValue);
        PlayerPrefs.SetFloat("PrefBGMVolume", sliderValue);
    }

    public void SetSFXVolume(float sliderValue)
    {
        ApplyMixerVolume("SFXVolume", sliderValue);
        PlayerPrefs.SetFloat("PrefSFXVolume", sliderValue);
    }

    public void SetAmbientVolume(float sliderValue)
    {
        ApplyMixerVolume("AmbientVolume", sliderValue);
        PlayerPrefs.SetFloat("PrefAmbientVolume", sliderValue);
    }

    private void ApplyMixerVolume(string exposedParameter, float sliderValue)
    {
        EnsureMixerRouting();
        if (mainMixer == null)
        {
            Debug.LogWarning($"[{nameof(AudioManager)}] {nameof(mainMixer)} is not assigned; cannot set '{exposedParameter}'.", this);
            return;
        }

        float dbVolume = sliderValue <= 0.001f ? -80f : Mathf.Log10(sliderValue) * 20f;
        mainMixer.SetFloat(exposedParameter, dbVolume);
    }
}
