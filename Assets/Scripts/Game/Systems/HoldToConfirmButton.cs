using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class HoldToConfirmButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // This dropdown lets you choose what the button does in the Unity Inspector
    public enum ButtonAction { RestartGame, ExitGame }

    [Header("Action Settings")]
    [SerializeField] private ButtonAction buttonAction = ButtonAction.RestartGame;
    [SerializeField] private float holdDuration = 2.5f; // Time in seconds required to hold

    [Header("Audio Setup")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip holdSound; // Your 0.9s sound clip

    [Header("UI Visuals (Optional)")]
    [SerializeField] private Image fillImage;

    private float currentHoldTime = 0f;
    private bool isPointerDown = false;

    void Update()
    {
        if (isPointerDown)
        {
            currentHoldTime += Time.unscaledDeltaTime; // Works while paused

            if (fillImage != null)
            {
                fillImage.fillAmount = currentHoldTime / holdDuration;
            }

            if (currentHoldTime >= holdDuration)
            {
                TriggerAction();
            }
        }
        else
        {
            if (currentHoldTime > 0f)
            {
                currentHoldTime = 0f;
                if (fillImage != null) fillImage.fillAmount = 0f;
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;

        if (audioSource != null && holdSound != null)
        {
            audioSource.clip = holdSound;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        StopHoldSound();
    }

    private void TriggerAction()
    {
        isPointerDown = false;
        currentHoldTime = 0f;
        StopHoldSound();
        Time.timeScale = 1f;

        // Execute the code based on what you selected in the Inspector dropdown
        if (buttonAction == ButtonAction.RestartGame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else if (buttonAction == ButtonAction.ExitGame)
        {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false; 
#else
            Application.Quit();
#endif
        }
    }

    private void StopHoldSound()
    {
        if (audioSource != null && audioSource.clip == holdSound)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }
    }
}
