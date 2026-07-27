using UnityEngine;

/// <summary>
/// Crossfades a looping outdoor ambience track in and out based on whether the player is inside
/// any building, using RoofVisibilityTrigger's aggregated indoor/outdoor state - no separate
/// trigger colliders needed here.
/// </summary>
public class AmbientSoundController : MonoBehaviour
{
    [SerializeField] private AudioSource outdoorAmbienceSource;
    [SerializeField] private AudioClip outdoorAmbienceClip;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 0.5f;
    [Tooltip("Volume change per second while crossfading in/out.")]
    [SerializeField] private float fadeSpeed = 0.3f;

    private float targetVolume;

    private void Start()
    {
        if (outdoorAmbienceSource == null) return;

        outdoorAmbienceSource.clip = outdoorAmbienceClip;
        outdoorAmbienceSource.loop = true;
        outdoorAmbienceSource.volume = 0f;

        targetVolume = RoofVisibilityTrigger.IsPlayerIndoors ? 0f : maxVolume;

        if (outdoorAmbienceClip != null) outdoorAmbienceSource.Play();
    }

    private void OnEnable()
    {
        RoofVisibilityTrigger.OnPlayerIndoorsChanged += HandleIndoorsChanged;
    }

    private void OnDisable()
    {
        RoofVisibilityTrigger.OnPlayerIndoorsChanged -= HandleIndoorsChanged;
    }

    private void HandleIndoorsChanged(bool isIndoors)
    {
        targetVolume = isIndoors ? 0f : maxVolume;
    }

    private void Update()
    {
        if (outdoorAmbienceSource == null) return;

        outdoorAmbienceSource.volume = Mathf.MoveTowards(outdoorAmbienceSource.volume, targetVolume, fadeSpeed * Time.deltaTime);
    }
}
