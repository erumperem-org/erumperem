namespace InteractionSystem.Concrete
{
    /// <summary>
    /// Não adiciona nenhum comportamento próprio hoje - existe como tipo
    /// nomeado para clareza e como ponto de extensão futuro (ex:
    /// sobrescrever CanInteractWith para checar facção/reputação). Toda a
    /// integração externa é feita via onInteractionStarted (herdado, ex:
    /// chamar "StartDialogue(dialogueId)" no seu sistema de diálogo). A
    /// reativação NÃO é automática: quem sinalizar o fim da interação (ex:
    /// um evento "OnDialogueEnded" do seu sistema de diálogo) deve chamar
    /// SetAvailable(true) neste componente - arraste esse evento externo
    /// pra cá no Inspector, com o valor "true" fixo no parâmetro.
    /// </summary>
    public class NpcInteractable : InteractableBase
    {
    }
}
