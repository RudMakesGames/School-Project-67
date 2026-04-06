using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private MonoBehaviour interactableObject; 
    private IInteractable interactableInterface;

    [SerializeField] Collider _coll;
    public bool isInteractable;

    [SerializeField]
    GameObject InteractionBox;
    [SerializeField]
    TextMeshProUGUI InteractPrompt; 

    private void Awake()
    {
        interactableInterface = interactableObject as IInteractable;

        if (interactableInterface == null)
        {
            Debug.LogError("Assigned object does not implement IInteractable!");
        }
    }
    private void Update()
    {
        Interact();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            isInteractable = true;
            InteractionBox.SetActive(true);
            InteractPrompt.text = interactableInterface.InteractionPromptName;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            isInteractable = false;
            InteractionBox.SetActive(false);
            InteractPrompt.text = "";
        }
    }

  public void Interact()
    {
        if(Input.GetKeyDown(KeyCode.E) && isInteractable)
        {
            interactableInterface.Interact();
            InteractionBox.SetActive(false);
            InteractPrompt.text = "";
        }
    }
}
