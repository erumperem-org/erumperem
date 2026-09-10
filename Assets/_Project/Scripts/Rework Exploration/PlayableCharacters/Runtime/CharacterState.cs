/// <summary>
/// Papel atual de um PlayableCharacters. Sempre existe exatamente um
/// personagem InGame e um Companion no roster; todo o resto é Resting.
/// </summary>
public enum CharacterState
{
    InGame,
    Companion,
    Resting
}
