using UnityEngine;

public class CharacterStatsView : MonoBehaviour
{
    public PlayableCharactersManager manager;
    public TMPro.TextMeshPro life, speed, defense, critical;
    void Awake()
    {
        manager.OnMainChanged += UpdateView;
    }

    void UpdateView(IPlayableCharacter character)
    {
        if(character is PlayableCharacter)
        {
            
        }
    }
}
