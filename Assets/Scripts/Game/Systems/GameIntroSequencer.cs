using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameIntroSequencer : MonoBehaviour
{
    [Header("Player Tracking")]
    [SerializeField] private MonoBehaviour playerMovementScript;
    [SerializeField] private Transform playerTransform;

    [Header("UI Elements")]
    [SerializeField] private Image blackFader;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private GameObject skipPrompt;

    [Header("Gameplay UI Setup")]
    [Tooltip("The parent GameObject containing your normal health bars, inventory HUD, etc.")]
    [SerializeField] private GameObject standardGameplayUI;

    [Header("Cutscene Audio")]
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioClip loudLoudSound;

    [Header("Scene Transition Markers")]
    [SerializeField] private Transform offscreenWalkTarget;
    [SerializeField] private Transform interiorSpawnPoint;
    [SerializeField] private float walkSpeed = 3f;

    private bool canAdvanceDialogue = false;
    private bool playerPressedAdvance = false;

    private PlayerDirectionalSprite playerDirectionalSprite;
    private PlayerScript playerScriptComponent;

    private void Start()
    {
        // Block player movement control immediately
        if (playerMovementScript != null) playerMovementScript.enabled = false;

        if (playerTransform != null)
        {
            playerDirectionalSprite = playerTransform.GetComponent<PlayerDirectionalSprite>();
            playerScriptComponent = playerTransform.GetComponent<PlayerScript>();
        }

        // Hide both the cutscene dialogue overlay AND your regular HUD elements at boot
        dialoguePanel.SetActive(false);
        skipPrompt.SetActive(false);

        if (standardGameplayUI != null)
        {
            standardGameplayUI.SetActive(false);
        }

        StartCoroutine(ExecuteIntroCutscene());
    }

    private void Update()
    {
        if (canAdvanceDialogue && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E)))
        {
            playerPressedAdvance = true;
        }
    }

    private IEnumerator ExecuteIntroCutscene()
    {
        // --- PHASE 1: THE WAKE UP LOUD SOUNDS ---
        blackFader.color = new Color(0, 0, 0, 1f);
        yield return new WaitForSeconds(1.5f);

        if (sfxAudioSource != null && loudLoudSound != null)
        {
            sfxAudioSource.PlayOneShot(loudLoudSound);
        }
        yield return new WaitForSeconds(1.0f);

        yield return StartCoroutine(FadeScreen(1f, 0f));

        // --- PHASE 2: INITIAL THOUGHT BUBBLES ---
        yield return StartCoroutine(DisplayThought("What was that...?"));
        yield return StartCoroutine(DisplayThought("That sounded like it was coming from the Ranger station..."));
        yield return StartCoroutine(DisplayThought("I should go check it out..."));

        // --- PHASE 3: AUTOMATIC WALK OFFSCREEN ---
        if (offscreenWalkTarget != null && playerTransform != null)
        {
            while (Vector2.Distance(playerTransform.position, offscreenWalkTarget.position) > 0.1f)
            {
                // 1. Force the flashlight vision cone to look LEFT by manually writing to the backing field
                SetPlayerFacingDirectionField(Vector2.left);

                // 2. Force the visual art system to render the LEFT sprite asset
                OverrideSpriteDirection(Vector2.left);

                playerTransform.position = Vector2.MoveTowards(
                    playerTransform.position,
                    offscreenWalkTarget.position,
                    walkSpeed * Time.deltaTime
                );
                yield return null;
            }
        }

        // --- PHASE 4: CUT TO INTERIOR ---
        yield return StartCoroutine(FadeScreen(0f, 1f));

        if (playerTransform != null && interiorSpawnPoint != null)
        {
            playerTransform.position = interiorSpawnPoint.position;

            // Force physical rotation update on the base transform container to look DOWN (0, -1)
            Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Quaternion targetRotation = Quaternion.LookRotation(playerTransform.forward, Vector2.down);
                rb.MoveRotation(targetRotation);
            }
        }

        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(FadeScreen(1f, 0f));

        // --- PHASE 5: DISCOVERY DIALOGUE ---
        // Force both the flashlight and sprite system to look DOWN at the body
        SetPlayerFacingDirectionField(Vector2.down);
        OverrideSpriteDirection(Vector2.down);

        yield return StartCoroutine(DisplayThought("Oh God... the park ranger is dead?!"));
        yield return StartCoroutine(DisplayThought("What... what kind of animal did this to him?"));
        yield return StartCoroutine(DisplayThought("I need to get out of here...wait...where are my car keys? They were in my pocket!"));
        yield return StartCoroutine(DisplayThought("I should probably read that manual on the pedestal for guidance"));
        yield return StartCoroutine(DisplayThought("I'm going to have to go out there and find them..."));

        // --- PHASE 6: RELEASE TO GAMEPLAY ---
        dialoguePanel.SetActive(false);

        if (standardGameplayUI != null)
        {
            standardGameplayUI.SetActive(true);
        }

        if (playerMovementScript != null) playerMovementScript.enabled = true;

        Destroy(gameObject);
    }

    /// <summary>
    /// Uses reflection to update the auto-implemented property backing field for FacingDirection
    /// while PlayerScript is disabled during the cutscene.
    /// </summary>
    private void SetPlayerFacingDirectionField(Vector2 direction)
    {
        if (playerScriptComponent == null) return;

        try
        {
            // Auto-implemented properties like 'public Vector2 FacingDirection { get; private set; }'
            // generate a hidden backing field named '<FacingDirection>k__BackingField'
            System.Reflection.FieldInfo backingField = typeof(PlayerScript).GetField("<FacingDirection>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (backingField != null)
            {
                backingField.SetValue(playerScriptComponent, direction);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to force flash light direction: {e.Message}");
        }
    }

    /// <summary>
    /// Forces the sprite rendering component to select a specific directional visual layout.
    /// </summary>
    private void OverrideSpriteDirection(Vector2 direction)
    {
        if (playerDirectionalSprite == null) return;

        System.Reflection.MethodInfo pickSpriteMethod = typeof(PlayerDirectionalSprite).GetMethod("PickSprite", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        System.Reflection.FieldInfo spriteRendererField = typeof(PlayerDirectionalSprite).GetField("spriteRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        System.Reflection.FieldInfo shadowRendererField = typeof(PlayerDirectionalSprite).GetField("shadowRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (pickSpriteMethod != null && spriteRendererField != null)
        {
            Sprite targetSprite = (Sprite)pickSpriteMethod.Invoke(playerDirectionalSprite, new object[] { direction });
            SpriteRenderer sr = (SpriteRenderer)spriteRendererField.GetValue(playerDirectionalSprite);
            SpriteRenderer shadowSr = shadowRendererField != null ? (SpriteRenderer)shadowRendererField.GetValue(playerDirectionalSprite) : null;

            if (targetSprite != null && sr != null)
            {
                sr.sprite = targetSprite;
                if (shadowSr != null) shadowSr.sprite = targetSprite;
            }
        }
    }

    private IEnumerator DisplayThought(string line)
    {
        dialoguePanel.SetActive(true);
        dialogueText.text = line;

        yield return new WaitForSeconds(0.2f);

        canAdvanceDialogue = true;
        skipPrompt.SetActive(true);

        while (!playerPressedAdvance)
        {
            yield return null;
        }

        playerPressedAdvance = false;
        canAdvanceDialogue = false;
        skipPrompt.SetActive(false);
    }

    private IEnumerator FadeScreen(float startAlpha, float endAlpha)
    {
        float duration = 0.8f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            blackFader.color = new Color(0, 0, 0, currentAlpha);
            yield return null;
        }
        blackFader.color = new Color(0, 0, 0, endAlpha);
    }
}
