using UnityEngine;
using UnityEngine.UI;

public enum MovementState { Idle, Crouch, Walk, Sprint }

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerScript : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 6f;

    [Header("Rotation")]
    [Tooltip("Degrees per second the player turns to face FacingDirection.")]
    [SerializeField] private float rotationSpeed = 720f;

    [Header("Directional Movement Penalty")]
    [Tooltip("Speed multiplier when moving in the direction you're facing (into your own vision cone).")]
    [SerializeField, Range(0f, 1f)] private float forwardSpeedMultiplier = 1f;
    [Tooltip("Speed multiplier when moving directly away from the direction you're facing (backpedaling, blind to what's behind you). Strafing lands between this and the forward multiplier.")]
    [SerializeField, Range(0f, 1f)] private float backwardSpeedMultiplier = 0.6f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrainPerSecond = 25f;
    [SerializeField] private float staminaRegenPerSecond = 15f;
    [Tooltip("Can't START sprinting below this, so you can't tap-sprint with near-zero stamina. Depleting to 0 WHILE sprinting still cuts you off immediately.")]
    [SerializeField] private float minStaminaToStartSprint = 10f;

    [Header("Footstep Sound - Distance Between Steps")]
    [SerializeField] private float crouchStepDistance = 1.2f;
    [SerializeField] private float walkStepDistance = 1.5f;
    [SerializeField] private float sprintStepDistance = 1.8f;

    [Header("Footstep Sound - Hearing Radius")]
    [SerializeField] private float crouchLoudness = 1.5f;
    [SerializeField] private float walkLoudness = 5f;
    [SerializeField] private float sprintLoudness = 10f;

    [Header("Footstep Audio")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip grassFootstep;
    [SerializeField] private AudioClip pavementFootstep;
    [SerializeField] private AudioClip waterFootstep;
    [SerializeField, Range(0f, 1f)] private float crouchFootstepVolume = 0.15f;
    [SerializeField, Range(0f, 1f)] private float walkFootstepVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] private float sprintFootstepVolume = 0.35f;
    [SerializeField, Range(0.5f, 3f)] private float grassVolumeMultiplier = 1f;
    [SerializeField, Range(0.5f, 3f)] private float pavementVolumeMultiplier = 1.5f;
    [SerializeField, Range(0.5f, 3f)] private float waterVolumeMultiplier = 1f;
    [SerializeField, Range(0.5f, 1.5f)] private float crouchPitch = 0.9f;
    [SerializeField, Range(0.5f, 1.5f)] private float walkPitch = 1f;
    [SerializeField, Range(0.5f, 1.5f)] private float sprintPitch = 1.1f;

    [Header("Creek Slowdown")]
    [SerializeField, Range(0f, 1f)] private float creekSpeedMultiplier = 0.6f;

    [Header("Bush Slowdown")]
    [SerializeField, Range(0f, 1f)] private float bushSpeedMultiplier = 0.5f;

    public MovementState CurrentState { get; private set; } = MovementState.Idle;
    public Image staminaBar;
    public float Stamina { get; private set; }
    public float StaminaNormalized => Stamina / maxStamina;
    public Vector2 FacingDirection { get; private set; } = Vector2.up;

    private Rigidbody2D rb;
    private Camera mainCamera;
    private Vector2 moveInput;
    private float distanceSinceLastStep;
    private bool wantsToSprint;
    private bool wantsToCrouch;
    private int creekZones;
    private int pavementZones;
    private int bushZones;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // top-down game, no gravity
        mainCamera = Camera.main;
        Stamina = maxStamina;
    }

    private void Update()
    {
        // Read input every frame for responsiveness; movement itself is applied in FixedUpdate.
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
        moveInput = moveInput.normalized;

        UpdateFacingFromMouse();

        wantsToSprint = Input.GetKey(KeyCode.LeftShift);
        wantsToCrouch = Input.GetKey(KeyCode.LeftControl);
    }

    private void UpdateFacingFromMouse()
    {
        if (mainCamera == null) return;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);

        Vector2 toMouse = (Vector2)mouseWorldPos - (Vector2)transform.position;
        if (toMouse.sqrMagnitude > 0.0001f)
        {
            FacingDirection = toMouse.normalized;
        }
    }

    private void FixedUpdate()
    {
        UpdateMovementState();
        ApplyMovement();
        RotateTowardsFacing();
        UpdateStamina();
        HandleFootsteps();
    }

    private void UpdateMovementState()
    {
        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        if (!isMoving)
        {
            CurrentState = MovementState.Idle;
            return;
        }

        // Sprint takes priority over crouch if both are held, but only while stamina allows it.
        // NOTE: crouch-sprinting isn't a thing here - decide if that's intentional for your design.
        if (wantsToSprint && Stamina > 0f && (CurrentState == MovementState.Sprint || Stamina > minStaminaToStartSprint))
        {
            CurrentState = MovementState.Sprint;
        }
        else if (wantsToCrouch)
        {
            CurrentState = MovementState.Crouch;
        }
        else
        {
            CurrentState = MovementState.Walk;
        }
    }

    private void ApplyMovement()
    {
        float speed = CurrentState switch
        {
            MovementState.Crouch => crouchSpeed,
            MovementState.Sprint => sprintSpeed,
            MovementState.Walk => walkSpeed,
            _ => 0f
        };

        // Environmental slowdowns don't stack multiplicatively; the strongest
        // (lowest) active multiplier wins, so a creek+bush overlap can't crawl
        // the player to a near-stop.
        float environmentalMultiplier = 1f;
        if (creekZones > 0)
            environmentalMultiplier = Mathf.Min(environmentalMultiplier, creekSpeedMultiplier);
        if (bushZones > 0)
            environmentalMultiplier = Mathf.Min(environmentalMultiplier, bushSpeedMultiplier);

        rb.linearVelocity = moveInput * speed * GetDirectionalSpeedMultiplier() * environmentalMultiplier;
    }

    // Full speed moving into your own facing/vision direction, reduced speed moving
    // away from it (backpedaling into what you can't see), with strafing landing
    // smoothly in between based on how aligned moveInput is with FacingDirection.
    private float GetDirectionalSpeedMultiplier()
    {
        if (moveInput.sqrMagnitude < 0.0001f) return 1f;

        float dot = Vector2.Dot(moveInput, FacingDirection);
        float t = (dot + 1f) * 0.5f; // 0 = directly backward, 1 = directly forward
        return Mathf.Lerp(backwardSpeedMultiplier, forwardSpeedMultiplier, t);
    }

    private void RotateTowardsFacing()
    {
        Quaternion targetRotation = Quaternion.LookRotation(transform.forward, FacingDirection);
        Quaternion rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(rotation);
    }

    private void UpdateStamina()
    {
        if (CurrentState == MovementState.Sprint)
        {
            Stamina = Mathf.Max(0f, Stamina - staminaDrainPerSecond * Time.fixedDeltaTime);
        }
        else
        {
            Stamina = Mathf.Min(maxStamina, Stamina + staminaRegenPerSecond * Time.fixedDeltaTime);
        }
        staminaBar.fillAmount = Stamina / maxStamina;
    }

    private void HandleFootsteps()
    {
        if (CurrentState == MovementState.Idle) return;

        distanceSinceLastStep += rb.linearVelocity.magnitude * Time.fixedDeltaTime;

        float stepDistance = CurrentState switch
        {
            MovementState.Crouch => crouchStepDistance,
            MovementState.Sprint => sprintStepDistance,
            _ => walkStepDistance
        };

        if (distanceSinceLastStep >= stepDistance)
        {
            distanceSinceLastStep = 0f;
            EmitFootstepSound();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Water"))
        {
            creekZones++;
        }
        else if (other.TryGetComponent(out PavementFootstep _))
        {
            pavementZones++;
        }
        else if (other.TryGetComponent(out BushSlowZone _))
        {
            bushZones++;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Water"))
        {
            creekZones = Mathf.Max(0, creekZones - 1);
        }
        else if (other.TryGetComponent(out PavementFootstep _))
        {
            pavementZones = Mathf.Max(0, pavementZones - 1);
        }
        else if (other.TryGetComponent(out BushSlowZone _))
        {
            bushZones = Mathf.Max(0, bushZones - 1);
        }
    }

    private void OnDisable()
    {
        creekZones = 0;
        pavementZones = 0;
        bushZones = 0;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Monster"))
        {
            Debug.Log("Monster gotchu ! TODO: add game over sequence here");
        }
    }

    private void EmitFootstepSound()
    {
        float volume = CurrentState switch
        {
            MovementState.Crouch => crouchFootstepVolume,
            MovementState.Sprint => sprintFootstepVolume,
            _ => walkFootstepVolume
        };

        AudioClip clip = grassFootstep;
        float surfaceMultiplier = grassVolumeMultiplier;
        if (creekZones > 0 && waterFootstep != null)
        {
            clip = waterFootstep;
            surfaceMultiplier = waterVolumeMultiplier;
        }
        else if (pavementZones > 0 && pavementFootstep != null)
        {
            clip = pavementFootstep;
            surfaceMultiplier = pavementVolumeMultiplier;
        }

        if (footstepAudioSource != null && clip != null)
        {
            footstepAudioSource.pitch = CurrentState switch
            {
                MovementState.Crouch => crouchPitch,
                MovementState.Sprint => sprintPitch,
                _ => walkPitch
            };
            footstepAudioSource.PlayOneShot(clip, volume * surfaceMultiplier);
        }
        if (SoundManager.Instance == null) return;

        float loudness = CurrentState switch
        {
            MovementState.Crouch => crouchLoudness,
            MovementState.Sprint => sprintLoudness,
            _ => walkLoudness
        };

        SoundType type = CurrentState == MovementState.Sprint ? SoundType.Sprint : SoundType.Footstep;

        SoundManager.Instance.EmitSound(rb.position, loudness, type, gameObject);
    }
}