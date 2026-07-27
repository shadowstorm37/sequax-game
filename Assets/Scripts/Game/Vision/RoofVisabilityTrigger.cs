using System;
using UnityEngine;

public class RoofVisibilityTrigger : MonoBehaviour
{
    [SerializeField] private GameObject roof;
    [Tooltip("How fast the roof fades in/out, in units per second (1 = fully fade in 1 second).")]
    [SerializeField] private float fadeSpeed = 4f;

    private int playerTriggerCount;
    private SpriteRenderer[] roofRenderers;
    private float targetAlpha = 1f;
    private float currentAlpha = 1f;

    // Aggregated across every RoofVisibilityTrigger in the scene, so anything that only cares
    // about "is the player inside any building" (ambient audio, indoor sound muffling, etc.)
    // doesn't need its own trigger colliders - it just reads/subscribes here.
    private static int globalIndoorTriggerCount;
    public static bool IsPlayerIndoors => globalIndoorTriggerCount > 0;
    public static event Action<bool> OnPlayerIndoorsChanged;

    private void Awake()
    {
        roofRenderers = roof.GetComponentsInChildren<SpriteRenderer>(true);

        // Runs before any trigger events can fire this scene, so resetting here (once per
        // instance, order doesn't matter) clears any stale count left over from a scene reload.
        globalIndoorTriggerCount = 0;
    }

    private void LateUpdate()
    {
        if (Mathf.Approximately(currentAlpha, targetAlpha))
            return;

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        for (int i = 0; i < roofRenderers.Length; i++)
        {
            SpriteRenderer renderer = roofRenderers[i];
            Color color = renderer.color;
            color.a = currentAlpha;
            renderer.color = color;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerTriggerCount++;
        targetAlpha = 0f;

        bool wasIndoors = globalIndoorTriggerCount > 0;
        globalIndoorTriggerCount++;
        if (!wasIndoors) OnPlayerIndoorsChanged?.Invoke(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerTriggerCount--;

        if (playerTriggerCount <= 0)
        {
            playerTriggerCount = 0;
            targetAlpha = 1f;
        }

        globalIndoorTriggerCount = Mathf.Max(0, globalIndoorTriggerCount - 1);
        if (globalIndoorTriggerCount == 0) OnPlayerIndoorsChanged?.Invoke(false);
    }
}
