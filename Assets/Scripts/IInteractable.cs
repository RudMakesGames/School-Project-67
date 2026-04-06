using UnityEngine;

public interface IInteractable
{
    public string InteractionPromptName { get; set; }

    public void Interact();
}
