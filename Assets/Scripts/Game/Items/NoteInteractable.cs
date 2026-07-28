using UnityEngine;
using TMPro;

namespace Game.Items
{
    [RequireComponent(typeof(Collider2D))]
    public class NoteInteractable : MonoBehaviour
    {
        [Header("Note Metadata")]
        [SerializeField] private string noteName = "Note";

        [TextArea(8, 12)]
        [Tooltip("Use the <page> tag to split text across multiple pages. (e.g., 'Page 1 text... <page> Page 2 text...')")]
        [SerializeField] private string noteContent;

        [Header("World UI Shared Elements")]
        [SerializeField] private TextMeshProUGUI interactionPromptText;

        [Header("Note Reading UI Overlay")]
        [SerializeField] private GameObject noteDisplayPanel;
        [SerializeField] private TextMeshProUGUI noteBodyTextDisplay;

        [Header("Optional Page Number Indicator")]
        [Tooltip("Optional TextMeshPro field to show 'Page 1 / 3'. Leave empty if not wanted.")]
        [SerializeField] private TextMeshProUGUI pageNumberTextDisplay;

        private bool isPlayerInRange = false;
        private bool isReading = false;
        private int currentPage = 1;
        private int totalPages = 1;

        private void Start()
        {
            if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
            if (noteDisplayPanel != null) noteDisplayPanel.SetActive(false);
        }

        private void Update()
        {
            if (!isReading)
            {
                // Open note if player is in range and presses E
                if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
                {
                    OpenNoteUI();
                }
            }
            else
            {
                // Close note strictly with Escape or 'E'
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
                {
                    CloseNoteUI();
                    return;
                }

                // Handle Page Navigation
                HandlePageInput();
            }
        }

        private void OpenNoteUI()
        {
            isReading = true;
            currentPage = 1; // Always start on the first page

            if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);

            if (noteDisplayPanel != null && noteBodyTextDisplay != null)
            {
                noteBodyTextDisplay.text = noteContent;
                noteDisplayPanel.SetActive(true);

                // Force TMPro to process the text immediately so we can read the page count accurately
                noteBodyTextDisplay.ForceMeshUpdate();
                totalPages = noteBodyTextDisplay.textInfo.pageCount;

                UpdatePageDisplay();
            }
        }

        private void HandlePageInput()
        {
            int previousPage = currentPage;

            // 1. Right Arrow or Scroll Down -> Next Page
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetAxis("Mouse ScrollWheel") < 0f)
            {
                if (currentPage < totalPages)
                {
                    currentPage++;
                }
            }

            // 2. Left Arrow or Scroll Up -> Previous Page
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetAxis("Mouse ScrollWheel") > 0f)
            {
                if (currentPage > 1)
                {
                    currentPage--;
                }
            }

            // Only update the display visual if the page actually changed
            if (currentPage != previousPage)
            {
                UpdatePageDisplay();
            }
        }

        private void UpdatePageDisplay()
        {
            if (noteBodyTextDisplay != null)
            {
                noteBodyTextDisplay.pageToDisplay = currentPage;
            }

            if (pageNumberTextDisplay != null)
            {
                pageNumberTextDisplay.text = $"Page {currentPage} / {totalPages}";
            }
        }

        private void CloseNoteUI()
        {
            isReading = false;

            if (noteDisplayPanel != null)
            {
                noteDisplayPanel.SetActive(false);
            }

            if (isPlayerInRange && interactionPromptText != null)
            {
                interactionPromptText.gameObject.SetActive(true);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            isPlayerInRange = true;

            if (interactionPromptText != null && !isReading)
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
            if (isReading) CloseNoteUI();
        }
    }
}
