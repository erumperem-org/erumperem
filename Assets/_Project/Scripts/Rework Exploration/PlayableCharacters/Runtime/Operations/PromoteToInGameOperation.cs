/// <summary>
/// O personagem indicado (que estava Resting) vira Em Jogo. Quem era Em
/// Jogo antes vai para Resting. O Companheiro atual continua o mesmo, mas
/// passa a seguir o novo Em Jogo.
/// </summary>
public class PromoteToInGameOperation : ICharacterSwitchOperation
{
    private readonly string characterId;

    public PromoteToInGameOperation(string characterId)
    {
        this.characterId = characterId;
    }

    public void Execute(PlayableCharacterController controller)
    {
        var target = controller.GetCharacter(characterId);

        if (target == null)
        {
            return;
        }

        var previousInGame = controller.InGameCharacter;

        if (previousInGame != null && previousInGame != target)
        {
            controller.AssignResting(previousInGame);
        }

        controller.AssignInGame(target);
        controller.RefreshCompanionFollowTarget();
    }
}
