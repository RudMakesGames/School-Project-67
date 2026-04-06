using UnityEngine;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    bool BisOpen;

    [SerializeField] 
    string OpenAnim,CloseAnim;

    [SerializeField]
    Animator anim;
    public string InteractionPromptName => "Open / Close Door";

    public void Interact()
    {
        BisOpen = !BisOpen;
        if(BisOpen)
        {
            anim.Play(CloseAnim);
        }
        else
        {
            anim.Play(OpenAnim);
        }
    }

    
}
