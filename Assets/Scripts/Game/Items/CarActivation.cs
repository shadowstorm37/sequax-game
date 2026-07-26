using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Items;

public class CarActivation : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string sceneToLoad;

    [Header("References")]
    [SerializeField] private Inventory playerInventory;
    [Tooltip("Assign the Car Requirements UI component script here.")]
    [SerializeField] private CarRequirementsUI requirementsUI;

    [Header("UI Settings (Optional)")]
    [Tooltip("The 'Press E to Escape' prompt graphic.")]
    [SerializeField] private GameObject interactionPrompt;

    private bool isPlayerInRange = false;

    private void Start()
    {
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }

    private void Update()
    {
        // Only allow extraction if the player is in range, presses E, and actually has the gear
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E) && playerInventory != null && playerInventory.CanEscape)
        {
            ExecuteEscape();
        }
    }

    private void ExecuteEscape()
    {
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.Log("Escape successful! Activating car... Loading scene: " + sceneToLoad);
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogError("Car Activation Failed: No scene name has been entered in the inspector slot!");
        }
    }

    // Detect when the player enters the interaction radius
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;

            if (playerInventory != null)
            {
                // ONLY show the "Press E" prompt if the player actually has the keys OR all 3 tools
                if (interactionPrompt != null)
                {
                    interactionPrompt.SetActive(playerInventory.CanEscape);
                }

                // Always show the requirements item checklist overlay so they know what they need/have
                if (requirementsUI != null)
                {
                    requirementsUI.ShowRequirements(playerInventory);
                }
            }
        }
    }

    // Detect when the player walks out of the interaction radius
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;

            // Safe to turn off both UI elements when they step away
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
            if (requirementsUI != null) requirementsUI.HideRequirements();
        }
    }
}
