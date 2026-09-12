/// <summary>
/// Troca direta: quem era Em Jogo vira Companheiro (passando a seguir quem
/// era Companheiro antes, agora Em Jogo) e vice-versa. Ninguém vai para
/// Resting nesta operação.
/// </summary>
public class SwapInGameAndCompanionOperation : ICharacterSwitchOperation
{
    public void Execute(PlayableCharacterController controller)
    {
        var previousInGame = controller.InGameCharacter;
        var previousCompanion = controller.CompanionCharacter;

        if (previousInGame == null || previousCompanion == null)
        {
            return;
        }

        controller.AssignInGame(previousCompanion);
        controller.AssignCompanion(previousInGame);
    }
}
