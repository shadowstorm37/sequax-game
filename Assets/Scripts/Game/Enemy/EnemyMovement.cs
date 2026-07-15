using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    [SerializeField] private float _baseSpeed = 3f;
    [SerializeField] private float _maxSpeed = 15f;
    [SerializeField] private float _speedExponent = 1.2f; 
    [SerializeField] private float _rotationSpeed;
    
    [Header("Player Tracking / Aggro")]
    [SerializeField] private string _playerTag = "Player";
    [SerializeField] private float _timeToAttack = 10f; // seconds of continuous tracking before attacking
    [SerializeField] private float _minPlayerDistance = 3f; // Enemy won't get closer than this to the player in tracking mode
    
    [Header("Hiding / Cover")]
    [SerializeField] private float _coverSearchRadius = 6f; // Max distance from the player to look for cover
    [SerializeField] private float _hideOffset = 0.5f;        // Extra buffer distance behind the cover object
    [SerializeField] private float _hideStoppingDistance = 0.2f; // Accuracy of stopping behind cover
    [SerializeField] private float _minCoverTime = 2f;       // Minimum time to hold cover before switching
    [SerializeField] private float _maxCoverTime = 3f;      // Maximum time to hold cover before switching

    [Header("Obstacle Avoidance")]
    [SerializeField] private float _obstacleCheckCircleRadius;
    [SerializeField] private float _obstacleCheckDistance;
    [SerializeField] private LayerMask _obstacleLayerMask;
    [SerializeField] private float _repulsionForce = 0.5f;
    [SerializeField] private float _avoidanceSmoothing = 8f; // Higher = faster steering, Lower = smoother/wider turns

    [Header("Investigation")]
    [SerializeField] private float _trackingAreaRadius = 1.5f;
    [SerializeField] private float _attackModeDuration = 7f;

    private float _currentDynamicSpeed;
    [SerializeField] private float _trackingTimer;   // how long we've been continuously tracking the player
    [SerializeField] private float _attackTimer;     // countdown while actively attacking
    private bool _inAttackMode;
    private bool _isAtCover; 
    private float _baseRotationSpeed;
    private Rigidbody2D _rigidbody;
    private SoundAwareness _playerAwarenessController;
    
    private Vector2 _desiredDirection; // Where the enemy *wants* to go
    private Vector2 _targetDirection;  // The smoothly blended direction the enemy actually moves
    private RaycastHit2D[] _obstacleCollisions;
    
    // Direct reference to the player's transform for homing in during Attack Mode
    private Transform _playerTransform; 

    // Track the current active cover collider and the last cover we switched from
    private Collider2D _activeCover;
    private Collider2D _previousCover;
    private float _coverSwitchTimer;

    private void Awake()
    {
        _inAttackMode = false;
        _isAtCover = false;
        _trackingTimer = 0f;
        _attackTimer = 0f;
        _rigidbody = GetComponent<Rigidbody2D>();
        _playerAwarenessController = GetComponent<SoundAwareness>();
        _obstacleCollisions = new RaycastHit2D[10];
        _baseRotationSpeed = _rotationSpeed;

        // Locate the player object using your configured tag
        GameObject playerObj = GameObject.FindWithTag(_playerTag);
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
        }
    }

    private void FixedUpdate()
    {
        // Check directly if SoundAwareness is actively hearing a sound that intersected ranges
        bool hearingSound = _playerAwarenessController.IsHearingSound;
        Vector2 targetPos = _playerAwarenessController.TargetSoundLocation;
        float targetLoudness = _playerAwarenessController.TargetSoundLoudness;
        
        // Ensure we only identify a player if we are actively hearing a sound
        string soundTag = hearingSound ? _playerAwarenessController.LastSoundSourceTag : "";
        bool isPlayer = soundTag == _playerTag;

        Vector2 movementTargetPosition = targetPos;
        bool wasAtCover = _isAtCover;
        _isAtCover = false; // Reset state to recalculate below

        // 1. Check if the incoming sound is originating from within our current cover obstacle
        bool soundIsWithinCurrentCover = false;
        if (hearingSound && _activeCover != null)
        {
            float coverRadius = _activeCover.bounds.extents.magnitude;
            
            // Check if the sound point overlaps the physical collider or falls within its radius
            if (_activeCover.OverlapPoint(targetPos) || Vector2.Distance(targetPos, _activeCover.bounds.center) <= coverRadius)
            {
                soundIsWithinCurrentCover = true;
            }
        }

        // 2. State Resolution
        if (_inAttackMode)
        {
            _activeCover = null; // Break cover during active chase
            _previousCover = null;
            HandleAttackingMode();
            if (_playerTransform != null) movementTargetPosition = _playerTransform.position;
        }
        else if (soundIsWithinCurrentCover)
        {
            // The sound is within our cover footprint: Freeze movement, but maintain cover orientation
            _desiredDirection = Vector2.zero;
            _isAtCover = wasAtCover; // Hold our previous "at cover" state so we keep facing the player

            // Tick down the cover switch timer even if we are holding our ground due to sound overlapping
            UpdateCoverTimer(targetPos);
        }
        else if (hearingSound) // If SoundAwareness registers a sound, we decide whether to hide or track
        {
            if (isPlayer)
            {
                // Try to find a valid obstacle to hide behind relative to the player (excluding the previous cover)
                if (TryFindCoverSpot(targetPos, out Vector2 hidePosition, _previousCover))
                {
                    movementTargetPosition = hidePosition;
                    HandleMoveToPosition(hidePosition, _hideStoppingDistance);

                    // If we are within stopping distance of our cover position, we are officially "in cover"
                    if (Vector2.Distance(transform.position, hidePosition) <= _hideStoppingDistance)
                    {
                        if (!wasAtCover)
                        {
                            // Reset the timer when we first successfully arrive at a new cover spot
                            ResetCoverTimer();
                        }
                        _isAtCover = true;
                    }
                }
                else if (_previousCover != null && TryFindCoverSpot(targetPos, out hidePosition, null))
                {
                    // Fallback: If no *other* cover exists, allow using the previous cover rather than running into the open
                    movementTargetPosition = hidePosition;
                    HandleMoveToPosition(hidePosition, _hideStoppingDistance);

                    if (Vector2.Distance(transform.position, hidePosition) <= _hideStoppingDistance)
                    {
                        if (!wasAtCover)
                        {
                            ResetCoverTimer();
                        }
                        _isAtCover = true;
                    }
                }
                else
                {
                    // Fallback to tracking player directly if no cover is available
                    _activeCover = null;
                    _previousCover = null;
                    movementTargetPosition = targetPos;
                    HandleTrackMode(targetPos, targetLoudness, isPlayer);
                }

                // If we are actively at our cover destination, manage our switching countdown
                if (_isAtCover)
                {
                    UpdateCoverTimer(targetPos);
                }

                _trackingTimer += Time.fixedDeltaTime;
                if (_trackingTimer >= _timeToAttack)
                {
                    EnterAttackMode();
                }
            }
            else
            {
                // Non-player sound: Track normally, no hiding behavior and no minimum distance restriction
                _activeCover = null;
                _previousCover = null;
                movementTargetPosition = targetPos;
                HandleTrackMode(targetPos, targetLoudness, false);
                _trackingTimer = Mathf.Max(0f, _trackingTimer - Time.fixedDeltaTime / 50f);
            }
        }
        else
        {
            // Not hearing anything: Stop moving and decay the tracking timer
            _desiredDirection = Vector2.zero;
            _trackingTimer = Mathf.Max(0f, _trackingTimer - Time.fixedDeltaTime / 50f);
            _previousCover = null; // Clear history when player is lost
        }

        if (!_inAttackMode) CalculateExponentialSpeed(movementTargetPosition);
        
        // Process obstacle avoidance and smoothly update _targetDirection
        ResolveObstaclesAndAvoidJitter();
        
        RotateTowardsTarget();
        SetVelocity();
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
            // Store our current cover as the "previous" one to avoid immediately backtracking to it
            _previousCover = _activeCover;
            _activeCover = null; 
            _isAtCover = false;
            
            // Re-evaluate cover search immediately using the updated blacklist
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
        
        // Find all colliders on the obstacle layer within the cover radius of the player
        Collider2D[] potentialCovers = Physics2D.OverlapCircleAll(playerPosition, _coverSearchRadius, _obstacleLayerMask);
        
        Collider2D bestCover = null;
        float closestDistanceToEnemy = float.MaxValue;

        foreach (Collider2D col in potentialCovers)
        {
            // Ignore self, triggers, disabled colliders, or the blacklisted cover we are trying to switch away from
            if (col.gameObject == gameObject || col.isTrigger || !col.enabled) continue;
            if (ignoreCover != null && col == ignoreCover) continue;

            Vector2 obstacleCenter = col.bounds.center;
            float distanceToPlayer = Vector2.Distance(playerPosition, obstacleCenter);
            
            // Get the bounding radius of this collider to ensure the ENTIRE object fits inside the range
            float obstacleBoundingRadius = col.bounds.extents.magnitude;

            // Enforce rule: The obstacle must be FULLY within the range from the player
            if (distanceToPlayer + obstacleBoundingRadius > _coverSearchRadius)
            {
                continue; // Too far away or partially leaks out of the range limit
            }

            // Find the closest valid cover to the enemy's current position to minimize transit time
            float distanceToEnemy = Vector2.Distance(transform.position, obstacleCenter);
            if (distanceToEnemy < closestDistanceToEnemy)
            {
                closestDistanceToEnemy = distanceToEnemy;
                bestCover = col;
            }
        }

        // Assign the active cover reference
        _activeCover = bestCover;

        // If we successfully identified a valid cover object, calculate the coordinates behind it
        if (_activeCover != null)
        {
            Vector2 coverCenter = _activeCover.bounds.center;
            Vector2 directionFromPlayerToCover = (coverCenter - playerPosition).normalized;
            
            // Determine the thickness of the obstacle along the hiding line
            float obstacleRadius = _activeCover.bounds.extents.magnitude;
            
            // Position coordinates: Center + direction offset * (radius + safety offset)
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

        // Set direction immediately
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

    private void OnDrawGizmosSelected()
    {
        if (_playerTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_playerTransform.position, _coverSearchRadius);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_playerTransform.position, _minPlayerDistance);
        }
    }
}