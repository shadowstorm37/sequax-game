using UnityEngine;
using UnityEngine.SceneManagement; // Required for switching scenes

public class CarActivation : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("The exact name of the scene you want to load when the car is activated.")]
    [SerializeField] private string sceneToLoad;

    [Header("UI Settings (Optional)")]
    [Tooltip("An optional UI element (like a 'Press E' canvas) that turns on when the player is in range.")]
    [SerializeField] private GameObject interactionPrompt;

    private bool isPlayerInRange = false;

    private void Start()
    {
        // Ensure the interaction prompt is hidden at the start of the game
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
    }

    private void Update()
    {
        // If the player is in the trigger zone and presses the 'E' key
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
        {
            ActivateCar();
        }
    }

    private void ActivateCar()
    {
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.Log("Activating car... Loading scene: " + sceneToLoad);
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

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(true);
            }
        }
    }

    // Detect when the player walks out of the interaction radius
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;

            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }
    }
}
