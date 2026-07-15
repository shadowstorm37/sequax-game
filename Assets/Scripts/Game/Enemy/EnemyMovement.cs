using UnityEngine;
using UnityEngine.SceneManagement; 

public class EnemyMovement : MonoBehaviour
{
    [SerializeField] private float _baseSpeed = 3f;
    [SerializeField] private float _maxSpeed = 15f;
    [SerializeField] private float _speedExponent = 1.2f; 
    [SerializeField] private float _rotationSpeed;
    
    [Header("Player Tracking / Aggro")]
    [SerializeField] private string _playerTag = "Player";
    [SerializeField] private float _timeToAttack = 10f; 
    [SerializeField] private float _minPlayerDistance = 3f; 
    [SerializeField] private float _instantAttackDistance = 2f; 
    [SerializeField] private float _passiveTrackingRange = 5f;  
    
    // NEW: Reference to the vision cone script to check visibility
    [Header("Vision Evasion")]
    [SerializeField] private VisionConeMask _playerVision; // Replace VisionConeMask with your actual script name if different
    [SerializeField] private float _evasionSpeed = 8f;     // How fast they scramble out of the light

    [Header("Hiding / Cover")]
    [SerializeField] private float _coverSearchRadius = 6f; 
    [SerializeField] private float _hideOffset = 0.5f;        
    [SerializeField] private float _hideStoppingDistance = 0.2f; 
    [SerializeField] private float _minCoverTime = 2f;       
    [SerializeField] private float _maxCoverTime = 3f;      

    [Header("Obstacle Avoidance")]
    [SerializeField] private float _obstacleCheckCircleRadius;
    [SerializeField] private float _obstacleCheckDistance;
    [SerializeField] private LayerMask _obstacleLayerMask;
    [SerializeField] private float _repulsionForce = 0.5f;
    [SerializeField] private float _avoidanceSmoothing = 8f; 

    [Header("Investigation")]
    [SerializeField] private float _trackingAreaRadius = 1.5f;
    [SerializeField] private float _attackModeDuration = 7f;

    private float _currentDynamicSpeed;
    [SerializeField] private float _trackingTimer;   
    [SerializeField] private float _attackTimer;     
    private bool _inAttackMode;
    private bool _isAtCover; 
    private bool _isEvadingVision; // NEW: Track if we are actively dodging light
    private float _baseRotationSpeed;
    private Rigidbody2D _rigidbody;
    private SoundAwareness _playerAwarenessController;
    
    private Vector2 _desiredDirection; 
    private Vector2 _targetDirection;  
    private RaycastHit2D[] _obstacleCollisions;
    
    private Transform _playerTransform; 
    private Collider2D _activeCover;
    private Collider2D _previousCover;
    private float _coverSwitchTimer;

    private void Awake()
    {
        _inAttackMode = false;
        _isAtCover = false;
        _isEvadingVision = false;
        _trackingTimer = 0f;
        _attackTimer = 0f;
        _rigidbody = GetComponent<Rigidbody2D>();
        _playerAwarenessController = GetComponent<SoundAwareness>();
        _obstacleCollisions = new RaycastHit2D[10];
        _baseRotationSpeed = _rotationSpeed;

        GameObject playerObj = GameObject.FindWithTag(_playerTag);
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
            
            // NEW: Auto-grab the vision script if you forget to assign it in the Inspector
            if (_playerVision == null)
                _playerVision = playerObj.GetComponentInChildren<VisionConeMask>(); 
        }
    }

    private void FixedUpdate()
    {
        // --- Instant Proximity Aggro Check ---
        float distanceToPlayer = float.MaxValue;
        bool isPlayerWithinPassiveRange = false;
        bool isVisibleToPlayer = false; // NEW

        if (_playerTransform != null)
        {
            distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
            
            // 1. Check for instant physical contact panic
            if (!_inAttackMode && distanceToPlayer <= _instantAttackDistance)
            {
                EnterAttackMode();
            }

            // 2. Check if player is within silent passive tracking distance
            isPlayerWithinPassiveRange = distanceToPlayer <= _passiveTrackingRange;
            
            // NEW: 3. Check if enemy is currently inside the vision cone
            if (_playerVision != null)
            {
                isVisibleToPlayer = _playerVision.IsPointVisible(transform.position);
            }
        }

        bool hearingSound = _playerAwarenessController.IsHearingSound;
        Vector2 targetPos = _playerAwarenessController.TargetSoundLocation;
        float targetLoudness = _playerAwarenessController.TargetSoundLoudness;
        
        string soundTag = hearingSound ? _playerAwarenessController.LastSoundSourceTag : "";
        bool isHearingPlayerSound = soundTag == _playerTag;

        Vector2 movementTargetPosition = targetPos;
        bool wasAtCover = _isAtCover;
        _isAtCover = false; 
        _isEvadingVision = false; // Reset evasion state every frame

        bool soundIsWithinCurrentCover = false;
        if (hearingSound && _activeCover != null)
        {
            float coverRadius = _activeCover.bounds.extents.magnitude;
            if (_activeCover.OverlapPoint(targetPos) || Vector2.Distance(targetPos, _activeCover.bounds.center) <= coverRadius)
            {
                soundIsWithinCurrentCover = true;
            }
        }

        // --- Movement & State Resolution ---
        if (_inAttackMode)
        {
            _activeCover = null; 
            _previousCover = null;
            HandleAttackingMode();
            if (_playerTransform != null) movementTargetPosition = _playerTransform.position;
        }
        else if (isVisibleToPlayer) // NEW: Evasion State (Highest priority outside of Attack Mode)
        {
            _isEvadingVision = true;
            _activeCover = null;     // Break cover immediately
            _previousCover = null;
            HandleEvasionMode();     // Steer perpendicular to the vision cone
        }
        else if (soundIsWithinCurrentCover)
        {
            _desiredDirection = Vector2.zero;
            _isAtCover = wasAtCover; 
            UpdateCoverTimer(targetPos);
        }
        else if (hearingSound) 
        {
            if (isHearingPlayerSound)
            {
                if (TryFindCoverSpot(targetPos, out Vector2 hidePosition, _previousCover))
                {
                    movementTargetPosition = hidePosition;
                    HandleMoveToPosition(hidePosition, _hideStoppingDistance);

                    if (Vector2.Distance(transform.position, hidePosition) <= _hideStoppingDistance)
                    {
                        if (!wasAtCover) ResetCoverTimer();
                        _isAtCover = true;
                    }
                }
                else if (_previousCover != null && TryFindCoverSpot(targetPos, out hidePosition, null))
                {
                    movementTargetPosition = hidePosition;
                    HandleMoveToPosition(hidePosition, _hideStoppingDistance);

                    if (Vector2.Distance(transform.position, hidePosition) <= _hideStoppingDistance)
                    {
                        if (!wasAtCover) ResetCoverTimer();
                        _isAtCover = true;
                    }
                }
                else
                {
                    _activeCover = null;
                    _previousCover = null;
                    movementTargetPosition = targetPos;
                    HandleTrackMode(targetPos, targetLoudness, isHearingPlayerSound);
                }

                if (_isAtCover) UpdateCoverTimer(targetPos);
            }
            else
            {
                _activeCover = null;
                _previousCover = null;
                movementTargetPosition = targetPos;
                HandleTrackMode(targetPos, targetLoudness, false);
            }
        }
        else
        {
            _desiredDirection = Vector2.zero;
            _previousCover = null; 
        }

        // --- Unified Tracking Timer Resolution ---
        // NEW: Being in the vision cone also increases the tracking timer (they aggro faster when looking right at them)
        bool shouldTrackPlayer = isHearingPlayerSound || isPlayerWithinPassiveRange || isVisibleToPlayer;

        if (!_inAttackMode && shouldTrackPlayer)
        {
            // If they are actively visible to the player, double the aggro rate
            float aggroMultiplier = isVisibleToPlayer ? 2f : 1f;
            _trackingTimer += Time.fixedDeltaTime * aggroMultiplier;
            
            if (_trackingTimer >= _timeToAttack)
            {
                EnterAttackMode();
            }
        }
        else if (!_inAttackMode)
        {
            _trackingTimer = Mathf.Max(0f, _trackingTimer - Time.fixedDeltaTime / 20f);
        }

        // --- Physics & Translation Execution ---
        
        // NEW: Do not use exponential targeting speed if we are running away (evading)
        if (!_inAttackMode && !_isEvadingVision) CalculateExponentialSpeed(movementTargetPosition);
        
        ResolveObstaclesAndAvoidJitter();
        RotateTowardsTarget();
        SetVelocity();
    }

    // NEW: Method to calculate fleeing trajectory
    private void HandleEvasionMode()
    {
        if (_playerTransform == null) return;

        // Force a specific fast speed while dodging the light
        _currentDynamicSpeed = _evasionSpeed;

        // Assuming your player's forward vision direction matches transform.up in 2D. 
        // (Change this to _playerTransform.right if your player sprite faces along the X axis)
        Vector2 playerLookDirection = _playerTransform.up; 
        Vector2 vectorToEnemy = ((Vector2)transform.position - (Vector2)_playerTransform.position).normalized;

        // Calculate the two vectors perpendicular to the player's line of sight
        Vector2 perpRight = new Vector2(-playerLookDirection.y, playerLookDirection.x);
        Vector2 perpLeft = new Vector2(playerLookDirection.y, -playerLookDirection.x);

        // Dot product checks which side of the cone center-line the enemy is currently on.
        // We pick the perpendicular direction that moves them *further* in that direction (fastest way out).
        if (Vector2.Dot(perpRight, vectorToEnemy) > 0)
        {
            _desiredDirection = perpRight;
        }
        else
        {
            _desiredDirection = perpLeft;
        }
    }

    private void ResetCoverTimer()
    {
        _coverSwitchTimer = Random.Range(_minCoverTime, _maxCoverTime);
    }

    private void UpdateCoverTimer(Vector2 playerPosition)
    {
        _coverSwitchTimer -= Time.fixedDeltaTime;
        if (_coverSwitchTimer <= 0f)
        {
            _previousCover = _activeCover;
            _activeCover = null; 
            _isAtCover = false;
            
            TryFindCoverSpot(playerPosition, out _, _previousCover);
            ResetCoverTimer();
        }
    }

    private void HandleTrackMode(Vector2 targetPos, float loudness, bool isPlayer)
    {
        float stoppingDistance = isPlayer ? _minPlayerDistance : _trackingAreaRadius;
        HandleMoveToPosition(targetPos, stoppingDistance);
    }

    private void HandleMoveToPosition(Vector2 position, float stoppingDistance)
    {
        Vector2 toTarget = position - (Vector2)transform.position;
        if (toTarget.magnitude <= stoppingDistance)
        {
            _desiredDirection = Vector2.zero;
        }
        else
        {
            _desiredDirection = toTarget.normalized;
        }
    }

    private bool TryFindCoverSpot(Vector2 playerPosition, out Vector2 hidePosition, Collider2D ignoreCover = null)
    {
        hidePosition = Vector2.zero;
        Collider2D[] potentialCovers = Physics2D.OverlapCircleAll(playerPosition, _coverSearchRadius, _obstacleLayerMask);
        
        Collider2D bestCover = null;
        float closestDistanceToEnemy = float.MaxValue;
        int obstaclesLayer = LayerMask.NameToLayer("Obstacles");

        foreach (Collider2D col in potentialCovers)
        {
            if (col == null || col.gameObject == gameObject || col.isTrigger || !col.enabled) continue;
            if (ignoreCover != null && col == ignoreCover) continue;
            if (col.gameObject.layer != obstaclesLayer) continue;

            if (!(col is PolygonCollider2D || col is BoxCollider2D || col is CircleCollider2D || col is CapsuleCollider2D || col is CompositeCollider2D))
            {
                continue;
            }

            Vector2 obstacleCenter = col.bounds.center;
            float distanceToPlayer = Vector2.Distance(playerPosition, obstacleCenter);
            float obstacleBoundingRadius = col.bounds.extents.magnitude;

            if (distanceToPlayer + obstacleBoundingRadius > _coverSearchRadius)
            {
                continue; 
            }

            float distanceToEnemy = Vector2.Distance(transform.position, obstacleCenter);
            if (distanceToEnemy < closestDistanceToEnemy)
            {
                closestDistanceToEnemy = distanceToEnemy;
                bestCover = col;
            }
        }

        _activeCover = bestCover;

        if (_activeCover != null)
        {
            Vector2 coverCenter = _activeCover.bounds.center;
            Vector2 directionFromPlayerToCover = (coverCenter - playerPosition).normalized;
            float obstacleRadius = _activeCover.bounds.extents.magnitude;
            
            hidePosition = coverCenter + (directionFromPlayerToCover * (obstacleRadius + _hideOffset));
            return true;
        }

        return false;
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
        _activeCover = null;
        _previousCover = null;
        _rotationSpeed = _baseRotationSpeed * 2f; 
        _currentDynamicSpeed = _maxSpeed * 3f;
        _inAttackMode = true;
        _attackTimer = _attackModeDuration;
        _trackingTimer = 0f;

        SetAttackDirection();
    }

    private void HandleAttackingMode()
    {
        SetAttackDirection();
        
        _attackTimer -= Time.fixedDeltaTime;
        if (_attackTimer <= 0f)
        {
            Debug.Log("NO LONGER IN ATTACK MODE");
            _attackTimer = 0f;
            _inAttackMode = false;
            _rotationSpeed = _baseRotationSpeed; 
        }
    }

    private void SetAttackDirection()
    {
        if (_playerTransform != null)
        {
            _desiredDirection = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        }
        else
        {
            _desiredDirection = _playerAwarenessController.DirectionToSound;
        }
    }

    private void ResolveObstaclesAndAvoidJitter()
    {
        if (_desiredDirection == Vector2.zero)
        {
            _targetDirection = Vector2.MoveTowards(_targetDirection, Vector2.zero, _avoidanceSmoothing * Time.fixedDeltaTime);
            return;
        }

        Vector2 calculatedAvoidanceDir = _desiredDirection;
        var contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(_obstacleLayerMask);
        contactFilter.useTriggers = false;

        int numberOfCollisions = Physics2D.CircleCast(transform.position, _obstacleCheckCircleRadius,
                                                     _desiredDirection, contactFilter, _obstacleCollisions, _obstacleCheckDistance);
        
        for (int index = 0; index < numberOfCollisions; index++)
        {
            var hit = _obstacleCollisions[index];
            
            if (hit.collider == null || hit.collider.gameObject == gameObject) continue;
            if (hit.collider.isTrigger || !hit.collider.enabled) continue;
            
            Vector2 normal = hit.normal;
            Vector2 tangentRight = new Vector2(-normal.y, normal.x);
            Vector2 tangentLeft = new Vector2(normal.y, -normal.x);
            
            Vector2 bestTangent = Vector2.Dot(tangentRight, _desiredDirection) > 0 ? tangentRight : tangentLeft;
            calculatedAvoidanceDir = (bestTangent + (normal * _repulsionForce)).normalized;
            break;
        }

        _targetDirection = Vector2.Lerp(_targetDirection, calculatedAvoidanceDir, _avoidanceSmoothing * Time.fixedDeltaTime);
    }

    private void RotateTowardsTarget()
    {
        Vector2 lookDirection = _targetDirection;

        bool tooCloseToPlayerTracking = !_inAttackMode && 
                                       _playerAwarenessController.IsHearingSound && 
                                       (_playerAwarenessController.LastSoundSourceTag == _playerTag) &&
                                       Vector2.Distance(transform.position, _playerAwarenessController.TargetSoundLocation) <= _minPlayerDistance;

        if (_isAtCover || tooCloseToPlayerTracking)
        {
            if (_playerTransform != null)
            {
                lookDirection = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
            }
            else
            {
                lookDirection = _playerAwarenessController.DirectionToSound;
            }
        }

        if (lookDirection == Vector2.zero) return;
        
        Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, lookDirection);
        _rigidbody.SetRotation(Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime));
    }

    private void SetVelocity()
    {
        if (_targetDirection == Vector2.zero || _targetDirection.sqrMagnitude < 0.01f)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
        }
        else
        {
            _rigidbody.linearVelocity = _targetDirection * _currentDynamicSpeed;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckPlayerTouch(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckPlayerTouch(other.gameObject);
    }

    private void CheckPlayerTouch(GameObject targetObject)
    {
        if (targetObject.CompareTag(_playerTag))
        {
            SceneManager.LoadScene("GameOver");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_playerTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_playerTransform.position, _coverSearchRadius);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_playerTransform.position, _minPlayerDistance);
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _instantAttackDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _passiveTrackingRange);
    }
}