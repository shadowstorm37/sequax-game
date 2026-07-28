using UnityEngine;
using TMPro;

namespace Game.Items
{
    [RequireComponent(typeof(Collider2D))]
    public class NoteInteractable : MonoBehaviour
    {
        [Tooltip("The clean title of the note (e.g., 'Ranger's Log' or 'Scrawled Note').")]
        [SerializeField] private string noteName = "Note";

        [TextArea(8, 12)]
        [Tooltip("The actual body text that shows up on screen when they read it.")]
        [SerializeField] private string noteContent;

        [Tooltip("Drag the same UI TextMeshPro prompt element here.")]
        [SerializeField] private TextMeshProUGUI interactionPromptText;

        // Reference to a separate note UI panel
        // [SerializeField] private GameObject noteDisplayUI; 

        private bool isPlayerInRange = false;

        private void Start()
        {
            if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
            {
                ReadNote();
            }
        }

        private void ReadNote()
        {
            Debug.Log($"Reading: {noteName}\nContent: {noteContent}");
            // Open the note UI overlay canvas here and inject 'noteContent'
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            isPlayerInRange = true;

            if (interactionPromptText != null)
            {
                interactionPromptText.text = $"Press 'E' to read {noteName}";
                interactionPromptText.gameObject.SetActive(true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            isPlayerInRange = false;
            if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
        }
    }
}
