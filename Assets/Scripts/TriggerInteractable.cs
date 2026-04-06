using UnityEngine;
using UnityEngine.Events;

public class TriggerInteractable : MonoBehaviour,IInteractable
{
    public string InteractionPromptName => "Trigger / Untrigger Object";

    public UnityEvent EventToTrigger , EventToUntrigger;

    bool IsInteracted = false;

    public void Interact()
    {
        IsInteracted = !IsInteracted;
        if(!IsInteracted)
        {
            EventToTrigger.Invoke();
        }
        else
        {
            EventToUntrigger.Invoke();
        }
      
    }

}
