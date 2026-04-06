using UnityEngine;

public class ItemCollectible : MonoBehaviour,IInteractable
{
    public string InteractionPromptName => "Collect Item";

    public void Interact()
    {
        ScoreManager.instance.AddScore(10);
        Destroy(gameObject);
    }

}
