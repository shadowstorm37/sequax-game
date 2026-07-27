using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class IntroCutsceneManager : MonoBehaviour
{
    [Header("Scene Flow")]
    [Tooltip("The exact name of your main gameplay scene to load after the intro.")]
    [SerializeField] private string gameplaySceneName;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI narrativeText;
    [SerializeField] private CanvasGroup UIAlphaGroup; // Used to cleanly fade text in/out

    [Header("Narrative Settings")]
    [TextArea(3, 5)]
    [SerializeField] private string[] storyBeats;
    [SerializeField] private float typingSpeed = 0.04f;
    [SerializeField] private float delayBetweenBeats = 2.5f;

    private int currentBeatIndex = 0;
    private bool isTextFullyDisplayed = false;
    private string currentFullText = "";
    private Coroutine typingCoroutine;

    private void Start()
    {
        if (storyBeats == null || storyBeats.Length == 0)
        {
            Debug.LogError("Intro Cutscene error: No story beats assigned in the inspector!");
            AdvanceToGameplay();
            return;
        }

        // Start the story sequence
        StartCoroutine(PlayIntroSequence());
    }

    private void Update()
    {
        // Allow the player to skip or advance text by pressing Space or Left Click
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            HandlePlayerInput();
        }
    }

    private IEnumerator PlayIntroSequence()
    {
        while (currentBeatIndex < storyBeats.Length)
        {
            isTextFullyDisplayed = false;
            currentFullText = storyBeats[currentBeatIndex];

            // Fade UI in gently
            yield return StartCoroutine(FadeUI(0f, 1f, 0.5f));

            // Type out the text character by character
            typingCoroutine = StartCoroutine(TypeText(currentFullText));
            yield return typingCoroutine;

            isTextFullyDisplayed = true;

            // Wait for the player to read it before automatically moving to the next beat
            yield return new WaitForSeconds(delayBetweenBeats);

            // Fade UI out gently before switching lines
            yield return StartCoroutine(FadeUI(1f, 0f, 0.5f));

            currentBeatIndex++;
        }

        // All story beats finished, load the game!
        AdvanceToGameplay();
    }

    private IEnumerator TypeText(string textToType)
    {
        narrativeText.text = "";
        foreach (char letter in textToType.ToCharArray())
        {
            narrativeText.text += letter;
            // Optional trigger a tiny audio click sound effect
            yield return new WaitForSeconds(typingSpeed);
        }
    }

    private void HandlePlayerInput()
    {
        if (!isTextFullyDisplayed)
        {
            // If text is still typing, stop the typewriter effect and immediately show the full sentence
            StopCoroutine(typingCoroutine);
            narrativeText.text = currentFullText;
            isTextFullyDisplayed = true;
        }
        else
        {
            // If the sentence was already fully showing, skip the remaining wait delay and jump to the next line
            StopAllCoroutines();
            StartCoroutine(SkipToNextBeat());
        }
    }

    private IEnumerator SkipToNextBeat()
    {
        yield return StartCoroutine(FadeUI(UIAlphaGroup.alpha, 0f, 0.2f));
        currentBeatIndex++;

        if (currentBeatIndex < storyBeats.Length)
        {
            StartCoroutine(PlayIntroSequence());
        }
        else
        {
            AdvanceToGameplay();
        }
    }

    private IEnumerator FadeUI(float startAlpha, float endAlpha, float duration)
    {
        if (UIAlphaGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            UIAlphaGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }
        UIAlphaGroup.alpha = endAlpha;
    }

    private void AdvanceToGameplay()
    {
        if (!string.IsNullOrEmpty(gameplaySceneName))
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
        else
        {
            Debug.LogError("Intro Scene Error: Gameplay Scene Name is empty!");
        }
    }
}
