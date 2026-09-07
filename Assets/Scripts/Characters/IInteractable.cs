namespace TheShedding.Characters
{
    public interface IInteractable
    {
        bool CanInteract(BaseCharacterController interactor);
        void Interact(BaseCharacterController interactor);
        string GetInteractPrompt(BaseCharacterController interactor);
    }
}
