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

    private void Awake()
    {
        roofRenderers = roof.GetComponentsInChildren<SpriteRenderer>(true);
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
    }
}
