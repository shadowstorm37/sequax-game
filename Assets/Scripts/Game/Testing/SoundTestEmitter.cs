using UnityEngine;

/// <summary>
/// TEST HARNESS - not part of the real game. Delete or disable once the
/// player controller and monster AI exist and can emit/hear sounds for real.
///
/// Click anywhere in the Scene/Game view (with this object's camera set)
/// to emit a test sound at that world position. Press 1-4 to change loudness
/// so you can see different-sized debug circles.
/// </summary>
public class SoundTestEmitter : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SoundType testType = SoundType.Footstep;

    private float currentLoudness = 5f;

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) currentLoudness = 3f;   // e.g. footstep
        if (Input.GetKeyDown(KeyCode.Alpha2)) currentLoudness = 8f;   // e.g. thrown rock
        if (Input.GetKeyDown(KeyCode.Alpha3)) currentLoudness = 15f;  // e.g. glass break
        if (Input.GetKeyDown(KeyCode.Alpha4)) currentLoudness = 25f;  // e.g. phone / radio

        if (Input.GetMouseButtonDown(0))
        {
            if (targetCamera == null)
            {
                Debug.LogWarning("SoundTestEmitter: no camera assigned/found.");
                return;
            }

            Vector3 mouseWorld = targetCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 emitPos = new Vector2(mouseWorld.x, mouseWorld.y);

            if (SoundManager.Instance == null)
            {
                Debug.LogError("SoundTestEmitter: no SoundManager in scene.");
                return;
            }

            SoundManager.Instance.EmitSound(emitPos, currentLoudness, testType, gameObject);
        }
    }
}