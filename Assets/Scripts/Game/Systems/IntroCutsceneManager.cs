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
    [SerializeField] private CanvasGroup textAlphaGroup; // Controls text fade
    [SerializeField] private Image backgroundImage;      // Controls the background rendering
    [SerializeField] private CanvasGroup bgAlphaGroup;    // Optional: Controls smooth background cross-fades

    [Header("Narrative Settings")]
    [TextArea(3, 5)]
    [SerializeField] private string[] storyBeats;

    [Tooltip("Assign a background sprite for each story beat. The list size MUST match the Story Beats size!")]
    [SerializeField] private Sprite[] backgroundSprites;

    [SerializeField] private float typingSpeed = 0.04f;
    [SerializeField] private float delayBetweenBeats = 3.5f; // Raised slightly so they can take in the art!

    private int currentBeatIndex = 0;
    private bool isTextFullyDisplayed = false;
    private string currentFullText = "";
    private Coroutine typingCoroutine;

    private void Start()
    {
        // Checks to ensure arrays match perfectly
        if (storyBeats == null || backgroundSprites == null || storyBeats.Length != backgroundSprites.Length)
        {
            Debug.LogError("Intro Cutscene Error: The number of Story Beats and Background Sprites must match exactly!");
            AdvanceToGameplay();
            return;
        }

        StartCoroutine(PlayIntroSequence());
    }

    private void Update()
    {
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

            // Swap the background graphic *while* it's hidden or fading in
            if (backgroundImage != null && backgroundSprites[currentBeatIndex] != null)
            {
                backgroundImage.sprite = backgroundSprites[currentBeatIndex];
            }

            // Fade both background and text in smoothly
            StartCoroutine(FadeCanvas(bgAlphaGroup, bgAlphaGroup.alpha, 1f, 0.75f));
            yield return StartCoroutine(FadeCanvas(textAlphaGroup, 0f, 1f, 0.5f));

            // Type out the text character by character
            typingCoroutine = StartCoroutine(TypeText(currentFullText));
            yield return typingCoroutine;

            isTextFullyDisplayed = true;

            // Wait for player reading time
            yield return new WaitForSeconds(delayBetweenBeats);

            // Fade out text (and optionally background if transitioning to a drastically different scene)
            yield return StartCoroutine(FadeCanvas(textAlphaGroup, 1f, 0f, 0.5f));

            currentBeatIndex++;
        }

        // Final fade out of everything before entering the nightmarish reality
        yield return StartCoroutine(FadeCanvas(bgAlphaGroup, 1f, 0f, 1.0f));
        AdvanceToGameplay();
    }

    private IEnumerator TypeText(string textToType)
    {
        narrativeText.text = "";
        foreach (char letter in textToType.ToCharArray())
        {
            narrativeText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }
    }

    private void HandlePlayerInput()
    {
        if (!isTextFullyDisplayed)
        {
            StopCoroutine(typingCoroutine);
            narrativeText.text = currentFullText;
            isTextFullyDisplayed = true;
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(SkipToNextBeat());
        }
    }

    private IEnumerator SkipToNextBeat()
    {
        // Fast fade text out to keep pacing snappy on manual skips
        yield return StartCoroutine(FadeCanvas(textAlphaGroup, textAlphaGroup.alpha, 0f, 0.2f));
        currentBeatIndex++;

        if (currentBeatIndex < storyBeats.Length)
        {
            StartCoroutine(PlayIntroSequence());
        }
        else
        {
            yield return StartCoroutine(FadeCanvas(bgAlphaGroup, bgAlphaGroup.alpha, 0f, 0.5f));
            AdvanceToGameplay();
        }
    }

    private IEnumerator FadeCanvas(CanvasGroup group, float startAlpha, float endAlpha, float duration)
    {
        if (group == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }
        group.alpha = endAlpha;
    }

    private void AdvanceToGameplay()
    {
        if (!string.IsNullOrEmpty(gameplaySceneName))
        {
            SceneManager.LoadScene(gameplaySceneName);
        }
    }
}
