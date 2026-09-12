/// <summary>
/// Uma operação de troca de papel entre personagens. Centraliza a lógica de
/// cada tipo de troca numa classe isolada, para facilitar adicionar novas
/// operações sem tocar em PlayableCharacterController.
/// </summary>
public interface ICharacterSwitchOperation
{
    void Execute(PlayableCharacterController controller);
}
