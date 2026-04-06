using UnityEngine;

public class LightInteractable : MonoBehaviour,IInteractable
{
    public string InteractionPromptName => "Turn On / Off Light";

    [SerializeField] Light LightObj;

    bool BisLightOn = false;

    public void Interact()
    {
        BisLightOn = !BisLightOn;
        if(!BisLightOn)
        {
            LightObj.enabled = false;
        }
        else
        {
            LightObj.enabled = true;
        }
    }

}
