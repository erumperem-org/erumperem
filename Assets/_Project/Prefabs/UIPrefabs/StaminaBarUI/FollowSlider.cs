using UnityEngine;
using UnityEngine.UI;

public class FollowSlider : MonoBehaviour
{
    public Slider sliderAlvo;
    public float velocidade = 0.1f;

    private Slider slider;

    void Start()
    {
        slider = GetComponent<Slider>();
    }

    void Update()
    {
        if (slider != null && sliderAlvo != null)
        {
            if (sliderAlvo.value == 1)
            {
                slider.value = sliderAlvo.value;
                SetSliderVisible(false);
            }
            else
            {
                SetSliderVisible(true);
                slider.value = Mathf.MoveTowards(slider.value, sliderAlvo.value, velocidade * Time.deltaTime);
            }
            
        }
    }

    private void SetSliderVisible(bool visible)
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }
}
