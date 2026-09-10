/// <summary>
/// O personagem indicado (que estava Resting) vira Companheiro. Quem era
/// Companheiro antes vai para Resting. O Em Jogo não muda.
/// </summary>
public class PromoteToCompanionOperation : ICharacterSwitchOperation
{
    private readonly string characterId;

    public PromoteToCompanionOperation(string characterId)
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

        var previousCompanion = controller.CompanionCharacter;

        if (previousCompanion != null && previousCompanion != target)
        {
            controller.AssignResting(previousCompanion);
        }

        controller.AssignCompanion(target);
    }
}
