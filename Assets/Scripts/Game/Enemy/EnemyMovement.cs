using UnityEngine;
public class EnemyMovement : MonoBehaviour
{
    [SerializeField] private float _baseSpeed = 3f;
    [SerializeField] private float _maxSpeed = 15f;
    [SerializeField] private float _speedExponent = 1.2f; 
    [SerializeField] private float _rotationSpeed;
    [Header("Player Detection")]
    [SerializeField] private float _playerDetectionRange = 5.0f;
    [SerializeField] private string _playerTag = "Player";
    [SerializeField] private float _timeToAttack = 10f; // seconds of continuous tracking before attacking
    [Header("Obstacle Avoidance")]
    [SerializeField] private float _obstacleCheckCircleRadius;
    [SerializeField] private float _obstacleCheckDistance;
    [SerializeField] private LayerMask _obstacleLayerMask;
    [SerializeField] private float _repulsionForce = 0.5f;
    [Header("Investigation")]
    [SerializeField] private float _trackingAreaRadius = 1.5f;
    [SerializeField] private float _attackModeDuration = 7f;

    private float _currentDynamicSpeed;
    [SerializeField] private float _trackingTimer;   // how long we've been continuously tracking the player
    [SerializeField] private float _attackTimer;     // countdown while actively attacking
    private bool _inAttackMode;
    private float _baseRotationSpeed;
    private Rigidbody2D _rigidbody;
    private SoundAwareness _playerAwarenessController;
    private Vector2 _targetDirection;
    private RaycastHit2D[] _obstacleCollisions;

    private void Awake()
    {
        _inAttackMode = false;
        _trackingTimer = 0f;
        _attackTimer = 0f;
        _rigidbody = GetComponent<Rigidbody2D>();
        _playerAwarenessController = GetComponent<SoundAwareness>();
        _obstacleCollisions = new RaycastHit2D[10];
        _baseRotationSpeed = _rotationSpeed;
    }

    private void FixedUpdate()
    {
    bool hearingSound = _playerAwarenessController.IsHearingSound;
    Vector2 targetPos = _playerAwarenessController.TargetSoundLocation;
    float targetLoudness = _playerAwarenessController.TargetSoundLoudness;
    string soundTag = _playerAwarenessController.LastSoundSourceTag;

    bool isPlayer = soundTag == _playerTag;
    float distToSound = Vector2.Distance(transform.position, targetPos);
    bool isWithinRange = distToSound <= _playerDetectionRange;
    if (_inAttackMode)
    {
        HandleAttackingMode();
    }
    else if (isPlayer && isWithinRange)
    {
        HandleTrackMode(targetPos, targetLoudness);

        if (_trackingTimer >= _timeToAttack)
        {
            EnterAttackMode();
        }
    }
    else
    {
        // Not tracking the player right now — buildup resets
        _trackingTimer = 0f;
        _targetDirection = Vector2.zero;
    }

    if (!_inAttackMode) CalculateExponentialSpeed(targetPos);
    HandleObstacles();
    RotateTowardsTarget();
    SetVelocity();
    }

    private void HandleTrackMode(Vector2 targetPos, float loudness)
    {
        Vector2 toTarget = targetPos - (Vector2)transform.position;
        if (toTarget.magnitude <= _trackingAreaRadius)
        {
            _targetDirection = Vector2.zero;
        }
        else
        {
            _targetDirection = toTarget.normalized;
        }

        // Accumulate real elapsed time, not a magic constant
        _trackingTimer += Time.fixedDeltaTime;
        Debug.Log(_trackingTimer);
    }

    private void CalculateExponentialSpeed(Vector2 targetPos)
    {
        if (_targetDirection == Vector2.zero)
        {
            _currentDynamicSpeed = _baseSpeed;
            return;
        }
        float distance = Vector2.Distance(transform.position, targetPos);
        float calculatedSpeed = _baseSpeed + Mathf.Pow(distance, _speedExponent);
        _currentDynamicSpeed = Mathf.Clamp(calculatedSpeed, _baseSpeed, _maxSpeed);
    }

    private void EnterAttackMode()
    {
        Debug.Log("IN ATTACK MODE");
        _targetDirection = _playerAwarenessController.DirectionToSound;
        _rotationSpeed = _baseRotationSpeed * 1.3f; // set relative to base, not compounding
        _currentDynamicSpeed = _maxSpeed * 3f;
        _inAttackMode = true;
        _attackTimer = _attackModeDuration;
        _trackingTimer = 0f;
    }

    private void HandleAttackingMode()
    {
        _targetDirection = _playerAwarenessController.DirectionToSound;
        _attackTimer -= Time.fixedDeltaTime;
        if (_attackTimer <= 0f)
        {
            Debug.Log("NO LONGER IN ATTACK MODE");
            _attackTimer = 0f;
            _inAttackMode = false;
            _rotationSpeed = _baseRotationSpeed; // restore, not divide
        }
    }

    private void HandleObstacles()
    {
        if (_targetDirection == Vector2.zero) return;
        var contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(_obstacleLayerMask);

        int numberOfCollisions = Physics2D.CircleCast(transform.position, _obstacleCheckCircleRadius,
                                 _targetDirection, contactFilter, _obstacleCollisions, _obstacleCheckDistance);
        for (int index = 0; index < numberOfCollisions; index++)
        {
            var hit = _obstacleCollisions[index];
            if (hit.collider.gameObject == gameObject) continue;
            Vector2 normal = hit.normal;
            Vector2 tangentRight = new Vector2(-normal.y, normal.x);
            Vector2 tangentLeft = new Vector2(normal.y, -normal.x);
            Vector2 bestTangent = Vector2.Dot(tangentRight, _targetDirection) > 0 ? tangentRight : tangentLeft;
            _targetDirection = (bestTangent + (normal * _repulsionForce)).normalized;
            break;
        }
    }

    private void RotateTowardsTarget()
    {
        if (_targetDirection == Vector2.zero) return;
        Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, _targetDirection);
        _rigidbody.SetRotation(Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime));
    }

    private void SetVelocity()
    {
        if (_targetDirection == Vector2.zero)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
        }
        else
        {
            _rigidbody.linearVelocity = _targetDirection * _currentDynamicSpeed;
        }
    }
}