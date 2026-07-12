using UnityEngine;

/// <summary>
/// Covers the entire camera view in solid black, except where a SpriteMask
/// (see VisionConeMask.cs) cuts a hole in it. Attach this to the Main Camera.
///
/// Uses a procedurally generated 1x1 white texture tinted black, scaled to
/// always fill the orthographic camera's view - no art asset needed.
/// </summary>
[RequireComponent(typeof(Camera))]
public class DarknessOverlay : MonoBehaviour
{
    [SerializeField] private Color darknessColor = Color.black;
    [Tooltip("Must render above your gameplay sprites. Increase if the darkness isn't covering something it should.")]
    [SerializeField] private int sortingOrder = 1000;

    private Camera cam;
    private SpriteRenderer overlayRenderer;
    private Transform overlayTransform;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        BuildOverlay();
    }

    private void BuildOverlay()
    {
        var overlayObject = new GameObject("DarknessOverlay_Generated");
        overlayTransform = overlayObject.transform;
        overlayTransform.SetParent(transform); // child of the camera, so it always travels with it

        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        overlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
        overlayRenderer.sprite = sprite;
        overlayRenderer.color = darknessColor;
        overlayRenderer.sortingOrder = sortingOrder;

        // The key line: this sprite draws everywhere EXCEPT where a SpriteMask overlaps it.
        overlayRenderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
    }

    private void LateUpdate()
    {
        if (!cam.orthographic || overlayTransform == null) return;

        float height = cam.orthographicSize * 2f;
        float width = height * cam.aspect;

        // Local position (0,0) relative to the camera, pushed out along the camera's
        // forward axis so it's in front of the near clip plane but behind everything else.
        overlayTransform.localPosition = new Vector3(0f, 0f, Mathf.Abs(cam.nearClipPlane) + 1f);
        overlayTransform.localScale = new Vector3(width, height, 1f);
    }
}