using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CharacterHoverAudio : MonoBehaviour
{
    public string hoverSound = "CharacterHover";

    private void OnMouseEnter()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(hoverSound);
        }
    }
}