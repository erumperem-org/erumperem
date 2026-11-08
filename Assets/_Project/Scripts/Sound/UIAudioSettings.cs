using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class UIAudioSettings : MonoBehaviour
{
    [Header("Sliders")]
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Slider ambientSlider;

    private void Start()
    {
        if (ambientSlider == null)
        {
            foreach (var slider in FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (slider.gameObject.scene != gameObject.scene || slider.name != "AmbienceSlider") continue;
                ambientSlider = slider;
                break;
            }
        }
        if (AudioManager.instance == null) return;

        InitializeSlider(masterSlider, "PrefMasterVolume", AudioManager.instance.SetMasterVolume);
        InitializeSlider(bgmSlider, "PrefBGMVolume", AudioManager.instance.SetBGMVolume);
        InitializeSlider(sfxSlider, "PrefSFXVolume", AudioManager.instance.SetSFXVolume);
        InitializeSlider(ambientSlider, "PrefAmbientVolume", AudioManager.instance.SetAmbientVolume);
    }

    private void InitializeSlider(Slider slider, string prefKey, UnityAction<float> action)
    {
        if (slider == null) return;

        float savedValue = PlayerPrefs.GetFloat(prefKey, 1f);
        slider.SetValueWithoutNotify(savedValue);
        
        slider.onValueChanged.AddListener(action);
        action.Invoke(savedValue);
    }
}
