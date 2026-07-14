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

    public MovementState CurrentState { get; private set; } = MovementState.Idle;
    public Image staminaBar;
    public float Stamina { get; private set; }
    public float StaminaNormalized => Stamina / maxStamina;
    public Vector2 FacingDirection { get; private set; } = Vector2.up;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private float distanceSinceLastStep;
    private bool wantsToSprint;
    private bool wantsToCrouch;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // top-down game, no gravity
        Stamina = maxStamina;
    }

    private void Update()
    {
        // Read input every frame for responsiveness; movement itself is applied in FixedUpdate.
        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
        moveInput = moveInput.normalized;
        if (moveInput.sqrMagnitude > 0.01f)
        {
            FacingDirection = moveInput;
        }
        
        wantsToSprint = Input.GetKey(KeyCode.LeftShift);
        wantsToCrouch = Input.GetKey(KeyCode.LeftControl);
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

        rb.linearVelocity = moveInput * speed;
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Monster"))
        {
            Debug.Log("Monster gotchu ! TODO: add game over sequence here");
        }
    }

    private void EmitFootstepSound()
    {
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