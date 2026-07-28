using System.Collections.Generic;
using Game.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemyMovement : MonoBehaviour
{
    [SerializeField] private float _baseSpeed = 3f;
    [SerializeField] private float _maxSpeed = 15f;
    [SerializeField] private float _speedExponent = 1.2f;

    [Header("Player Tracking / Aggro")]
    [SerializeField] private string _playerTag = "Player";
    [SerializeField] private float _timeToAttack = 10f; 
    [SerializeField] private float _minPlayerDistance = 6f;
    [SerializeField] private float _instantAttackDistance = 2f;
    [SerializeField] private float _passiveTrackingRange = 8f;
    
    [Header("Vision Evasion")]
    [SerializeField] private VisionConeMask _playerVision;
    [SerializeField] private float _evasionSpeed = 8f;
    [SerializeField] private float _maxVisionEvadeDistance = 15f; // NEW: Enemy ignores vision cone beyond this distance

    [Header("Evasion Follow-up")]
    [Tooltip("Chance (0-1), once line of sight is broken, that the monster fully disengages instead of repositioning to flank.")]
    [SerializeField, Range(0f, 1f)] private float _disengageChance = 0.4f;
    [SerializeField] private float _disengageDurationMin = 2f;
    [SerializeField] private float _disengageDurationMax = 4f;
    [Tooltip("How far ahead of the player's facing direction the flank point is placed.")]
    [SerializeField] private float _repositionForwardDistance = 7f;
    [SerializeField] private float _repositionLateralOffset = 4f;
    [Tooltip("Safety cutoff so a flank attempt can't wander forever if the point is unreachable.")]
    [SerializeField] private float _repositionTimeout = 5f;

    [Header("Charge (Bold Approach)")]
    [Tooltip("Random cooldown range between charge attempts.")]
    [SerializeField] private float _chargeCooldownMin = 10f;
    [SerializeField] private float _chargeCooldownMax = 20f;
    [SerializeField] private float _chargeSpeed = 11f;
    [SerializeField] private float _chargeDuration = 3f;
    [SerializeField] private float _chargeMinDistance = 4f;
    [SerializeField] private float _chargeMaxDistance = 14f;
    [Tooltip("How long he holds still and screams before actually bolting - a telegraph so a charge from off-screen/behind is still fair.")]
    [SerializeField] private float _chargeWindupDuration = 0.6f;
    [Tooltip("Seconds of sustained tracking pressure needed to count as 'angry' at the start of a run: a charge that gets spotted no longer aborts (commits to attack instead), and contact from behind becomes lethal. Deliberately independent of Time To Attack so the two can be tuned separately.")]
    [SerializeField] private float _angryTrackingSecondsBaseline = 80f;

    [Header("Lost Tracking Patrol")]
    [Tooltip("How long he lingers and searches the last-known area before fully giving up.")]
    [SerializeField] private float _patrolDurationMin = 4f;
    [SerializeField] private float _patrolDurationMax = 8f;
    [Tooltip("How far from the last-known point each wander waypoint can land.")]
    [SerializeField] private float _patrolRadius = 4f;
    [SerializeField] private float _patrolWaypointIntervalMin = 1.5f;
    [SerializeField] private float _patrolWaypointIntervalMax = 3f;
    [SerializeField] private float _patrolStoppingDistance = 0.3f;

    [Header("Fallback (Catch-Up)")]
    [Tooltip("Once patrolling gives up without re-finding the player, he stops hiding and beelines back toward their current position instead of idling forever - prevents indefinitely outrunning him in a straight line.")]
    [SerializeField] private float _fallbackSpeed = 10f;
    [Tooltip("How far from the player's live position he aims to land - kept outside easy immediate contact so a cross-map catch-up isn't a cheap ambush.")]
    [SerializeField] private float _fallbackLandingDistance = 9f;
    [SerializeField] private float _fallbackStoppingDistance = 1f;
    [Tooltip("Safety cutoff so a fallback run can't chase forever if the player keeps moving unpredictably.")]
    [SerializeField] private float _fallbackTimeout = 15f;
    [Tooltip("Distant cue played the instant Fallback triggers - losing him for too long shouldn't mean an unfair silent return.")]
    [SerializeField] private AudioClip _fallbackClip;

    [Header("Escalation")]
    [Tooltip("Seconds of effective elapsed time - accelerated by key items collected - to go from baseline to fully escalated.")]
    [SerializeField] private float _escalationRampSeconds = 300f;
    [Tooltip("Extra rate added to the escalation clock per key item collected. 0.5 means each item speeds it up by 50%.")]
    [SerializeField] private float _escalationPerItemBonus = 0.5f;
    [Tooltip("Charge cooldown range once fully escalated - keeps a floor above zero so charges are frequent late-game, never nonstop.")]
    [SerializeField] private float _escalatedChargeCooldownMin = 4f;
    [SerializeField] private float _escalatedChargeCooldownMax = 8f;
    [Tooltip("Seconds of sustained tracking pressure needed to count as 'angry' once fully escalated - kept above zero so it's never instant, just faster to reach. You still need to be able to make it back to the car.")]
    [SerializeField] private float _angryTrackingSecondsEscalated = 25f;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [Tooltip("Played the instant a charge begins, before the windup ends and he actually moves.")]
    [SerializeField] private AudioClip _chargeScreamClip;
    [Tooltip("Played once, the instant he crosses the angry threshold - the player's cue that blind-spot contact is now lethal.")]
    [SerializeField] private AudioClip _angryClip;
    [Tooltip("Played the instant attack mode is entered, from any trigger.")]
    [SerializeField] private AudioClip _attackWarningClip;
    [SerializeField, Range(0f, 1f)] private float _audioVolume = 1f;

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

    [Header("Distraction Fatigue")]
    [Tooltip("How many times a distraction sound type (per item) can be used at full strength before he starts tuning it out.")]
    [SerializeField] private int _fatigueThreshold = 2;
    [Tooltip("Additional uses after the threshold before the response bottoms out at its floor below.")]
    [SerializeField] private int _fatigueDecaySteps = 4;
    [Tooltip("Rocks never fully stop working - once fatigued they still make him flinch and hesitate for a beat instead of a real investigation.")]
    [SerializeField, Range(0f, 1f)] private float _rockFatigueFloor = 0.15f;
    [Tooltip("Non-rock distractions fatigue all the way to zero - completely ignored, no reaction at all, once worn out.")]
    [SerializeField, Range(0f, 1f)] private float _otherFatigueFloor = 0f;

    private enum EvasionPhase { None, BreakingLineOfSight, Reposition, Disengaged }

    private float _currentDynamicSpeed;
    [SerializeField] private float _trackingTimer;
    [SerializeField] private float _attackTimer;
    private bool _inAttackMode;
    private bool _isAtCover;
    private Rigidbody2D _rigidbody;

    private EvasionPhase _evasionPhase;
    private Vector2 _evasionDestination;
    private float _disengageTimer;
    private float _repositionTimer;

    private bool _isCharging;
    private bool _inChargeWindup;
    private float _chargeWindupTimer;
    private float _chargeTimer;
    private float _chargeCooldownTimer;
    private bool _wasAngry;
    private bool _lastVisibleToPlayer;

    private bool _isPatrolling;
    private float _patrolTimer;
    private float _patrolWaypointTimer;
    private Vector2 _patrolDestination;
    private Vector2 _lastKnownPlayerPosition;
    private bool _hasLastKnownPlayerPosition;

    private bool _isFallingBack;
    private float _fallbackTimer;
    private Vector2 _fallbackApproachOffset;
    private Vector2 _fallbackDestination;

    private float _elapsedRunTime;
    private float _escalationProgress;
    private Inventory _playerInventory;

    private SoundAwareness _playerAwarenessController;
    private readonly Dictionary<SoundType, int> _distractionUseCounts = new();

    private Vector2 _desiredDirection;
    private Vector2 _targetDirection;
    private RaycastHit2D[] _obstacleCollisions;

    private Transform _playerTransform;
    private Collider2D _activeCover;
    private Collider2D _previousCover;
    private float _coverSwitchTimer;

    // "Really angry": once the tracking meter crosses this many seconds of sustained pressure, a
    // spotted charge commits to attack instead of fleeing, and contact from behind (outside the
    // player's vision) turns lethal. Kept as an absolute value rather than a fraction of Time To
    // Attack so the two can be tuned independently - Time To Attack can be set arbitrarily high
    // without dragging the anger threshold up with it.
    private float CurrentAngryTrackingSeconds => Mathf.Lerp(_angryTrackingSecondsBaseline, _angryTrackingSecondsEscalated, _escalationProgress);
    private bool IsAngry => _trackingTimer >= CurrentAngryTrackingSeconds;

    private float CurrentChargeCooldownMin => Mathf.Lerp(_chargeCooldownMin, _escalatedChargeCooldownMin, _escalationProgress);
    private float CurrentChargeCooldownMax => Mathf.Lerp(_chargeCooldownMax, _escalatedChargeCooldownMax, _escalationProgress);

    private void Awake()
    {
        _inAttackMode = false;
        _isAtCover = false;
        _trackingTimer = 0f;
        _attackTimer = 0f;
        _elapsedRunTime = 0f;
        _escalationProgress = 0f;
        _chargeCooldownTimer = Random.Range(_chargeCooldownMin, _chargeCooldownMax);
        _rigidbody = GetComponent<Rigidbody2D>();
        _playerAwarenessController = GetComponent<SoundAwareness>();
        _obstacleCollisions = new RaycastHit2D[10];

        if (_playerAwarenessController != null)
        {
            _playerAwarenessController.OnSoundHeard += HandleDistractionSoundHeard;
        }

        GameObject playerObj = GameObject.FindWithTag(_playerTag);
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
            _playerInventory = playerObj.GetComponent<Inventory>();

            if (_playerVision == null)
                _playerVision = playerObj.GetComponentInChildren<VisionConeMask>(); 
        }
    }

    private void OnDestroy()
    {
        if (_playerAwarenessController != null)
        {
            _playerAwarenessController.OnSoundHeard -= HandleDistractionSoundHeard;
        }
    }

    private static bool IsDistractionSound(SoundType type) =>
        type == SoundType.ThrowImpact || type == SoundType.GlassBreak || type == SoundType.Phone || type == SoundType.Radio;

    private float GetFatigueFloor(SoundType type) => type == SoundType.ThrowImpact ? _rockFatigueFloor : _otherFatigueFloor;

    // 1.0 while under the fatigue threshold (fresh/rarely-used sound), then lerps down to the
    // type's floor over _fatigueDecaySteps further uses. Rocks have a floor above zero so they
    // never fully stop registering; everything else can decay all the way to being ignored.
    private float GetInvestigateStrength(SoundType type)
    {
        int count = _distractionUseCounts.TryGetValue(type, out int c) ? c : 0;
        if (count <= _fatigueThreshold) return 1f;

        float floor = GetFatigueFloor(type);
        float t = _fatigueDecaySteps > 0 ? Mathf.Clamp01((float)(count - _fatigueThreshold) / _fatigueDecaySteps) : 1f;
        return Mathf.Lerp(1f, floor, t);
    }

    private void HandleDistractionSoundHeard(SoundEvent soundEvent)
    {
        if (!IsDistractionSound(soundEvent.type)) return;

        _distractionUseCounts.TryGetValue(soundEvent.type, out int count);
        _distractionUseCounts[soundEvent.type] = count + 1;
    }

    // A distraction that's completely worn out still makes him flinch toward the noise for a beat
    // instead of committing to a full investigation - reads as "heard that, not fooled again"
    // rather than a full ignore. Only reachable for types with a floor above zero (rocks).
    private void HandleHesitation()
    {
        _currentDynamicSpeed = 0f;
        _desiredDirection = Vector2.zero;
    }

    private void FixedUpdate()
    {
        // Freeze all aggro/attack timers and movement while the in-game intro still has control locked
        if (!GameState.PlayerHasControl) return;

        UpdateEscalation();

        // --- Instant Proximity Aggro Check ---
        float distanceToPlayer = float.MaxValue;
        bool isPlayerWithinPassiveRange = false;
        bool isVisibleToPlayer = false; 

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
            
            // 3. UPDATED: Check if enemy is inside the vision cone AND close enough to care
            if (_playerVision != null && distanceToPlayer <= _maxVisionEvadeDistance)
            {
                isVisibleToPlayer = _playerVision.IsPointVisible(transform.position);
            }
        }

        _lastVisibleToPlayer = isVisibleToPlayer;

        bool hearingSound = _playerAwarenessController.IsHearingSound;
        Vector2 targetPos = _playerAwarenessController.TargetSoundLocation;
        float targetLoudness = _playerAwarenessController.TargetSoundLoudness;
        
        string soundTag = hearingSound ? _playerAwarenessController.LastSoundSourceTag : "";
        bool isHearingPlayerSound = soundTag == _playerTag;

        // --- Distraction Fatigue ---
        // Thrown items share the player's tag (they're emitted with the player as source), so the
        // only way to tell a worn-out distraction apart from a real footstep is the sound TYPE.
        SoundType dominantSoundType = _playerAwarenessController.TargetSoundType;
        bool isDistractionSound = hearingSound && IsDistractionSound(dominantSoundType);
        float distractionStrength = isDistractionSound ? GetInvestigateStrength(dominantSoundType) : 1f;
        // Fresh/rarely-used distraction: worth breaking cover for, so it's allowed to override the
        // vision-cone-avoidance instinct below - otherwise the player throws a rock, the monster
        // dodges into a bush to avoid being seen, and there's no way to tell the distraction landed.
        bool distractionAtFullStrength = isDistractionSound && distractionStrength >= 1f;
        bool distractionBottomedOut = isDistractionSound && distractionStrength <= GetFatigueFloor(dominantSoundType) + 0.001f;
        bool distractionFullyIgnored = distractionBottomedOut && GetFatigueFloor(dominantSoundType) <= 0f;
        bool distractionHesitateOnly = distractionBottomedOut && !distractionFullyIgnored;

        bool isHearingPlayerSoundEffective = isHearingPlayerSound && !distractionBottomedOut;
        bool effectiveHearingSound = hearingSound && !distractionFullyIgnored;

        if (isHearingPlayerSoundEffective)
        {
            _lastKnownPlayerPosition = targetPos;
            _hasLastKnownPlayerPosition = true;
        }

        Vector2 movementTargetPosition = targetPos;
        bool wasAtCover = _isAtCover;
        _isAtCover = false;

        bool soundIsWithinCurrentCover = false;
        if (hearingSound && _activeCover != null)
        {
            float coverRadius = _activeCover.bounds.extents.magnitude;
            if (_activeCover.OverlapPoint(targetPos) || Vector2.Distance(targetPos, _activeCover.bounds.center) <= coverRadius)
            {
                soundIsWithinCurrentCover = true;
            }
        }

        bool knowsPlayerLocation = isHearingPlayerSoundEffective || isPlayerWithinPassiveRange;

        // --- Charge Trigger ---
        // Only winds up while calmly tracking (hidden, not already evading/charging) so a charge
        // always starts as a surprise rather than interrupting a moment the player already reacted to.
        if (!_inAttackMode && !_isCharging && _evasionPhase == EvasionPhase.None && !isVisibleToPlayer)
        {
            _chargeCooldownTimer -= Time.fixedDeltaTime;
            if (_chargeCooldownTimer <= 0f && knowsPlayerLocation &&
                distanceToPlayer >= _chargeMinDistance && distanceToPlayer <= _chargeMaxDistance)
            {
                StartCharge();
            }
        }

        // --- Movement & State Resolution ---
        if (_inAttackMode)
        {
            _isPatrolling = false;
            _isFallingBack = false;
            _activeCover = null;
            _previousCover = null;
            HandleAttackingMode();
            if (_playerTransform != null) movementTargetPosition = _playerTransform.position;
        }
        else if (_isCharging)
        {
            _isPatrolling = false;
            _isFallingBack = false;
            Vector2 chargeTarget = _playerTransform != null && (isVisibleToPlayer || isPlayerWithinPassiveRange || isHearingPlayerSound)
                ? (Vector2)_playerTransform.position
                : targetPos;
            HandleCharge(chargeTarget, isVisibleToPlayer);
            movementTargetPosition = chargeTarget;
        }
        else
        {
            if (isVisibleToPlayer && _evasionPhase == EvasionPhase.None && !distractionAtFullStrength)
            {
                BeginEvasion();
            }

            if (_evasionPhase != EvasionPhase.None)
            {
                _isPatrolling = false;
                _isFallingBack = false;
                HandleEvasionStateMachine(isVisibleToPlayer);
                movementTargetPosition = _evasionDestination;
            }
            else if (distractionHesitateOnly)
            {
                _isPatrolling = false;
                _isFallingBack = false;
                HandleHesitation();
                movementTargetPosition = transform.position;
            }
            else if (soundIsWithinCurrentCover)
            {
                _isPatrolling = false;
                _isFallingBack = false;
                _desiredDirection = Vector2.zero;
                _isAtCover = wasAtCover;
                UpdateCoverTimer(targetPos);
            }
            else if (effectiveHearingSound)
            {
                _isPatrolling = false;
                _isFallingBack = false;
                if (isHearingPlayerSoundEffective)
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
            else if (_isFallingBack)
            {
                _previousCover = null;
                HandleFallback();
                movementTargetPosition = _fallbackDestination;
            }
            else
            {
                _previousCover = null;
                HandlePatrol();
                movementTargetPosition = _patrolDestination;
            }
        }

        // --- Unified Tracking Timer Resolution ---
        bool shouldTrackPlayer = isHearingPlayerSoundEffective || isPlayerWithinPassiveRange || isVisibleToPlayer;

        if (!_inAttackMode && shouldTrackPlayer)
        {
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

        bool isAngryNow = IsAngry;
        if (isAngryNow && !_wasAngry)
        {
            PlaySound(_angryClip);
        }
        _wasAngry = isAngryNow;

        // --- Physics & Translation Execution ---
        bool speedHandledElsewhere = _inAttackMode || _isCharging || _evasionPhase != EvasionPhase.None || _isPatrolling || _isFallingBack || distractionHesitateOnly;
        if (!speedHandledElsewhere) CalculateExponentialSpeed(movementTargetPosition);

        ResolveObstaclesAndAvoidJitter();
        KeepUpright();
        SetVelocity();
    }

    // Elapsed run time is the baseline driver; each key item collected speeds up the effective
    // clock, so escalation still happens on a slow/exploring run but accelerates as the player
    // gets closer to escaping. Progress is clamped to 1, so nothing scales past its floor value -
    // the run is always survivable, just tighter over time.
    private void UpdateEscalation()
    {
        int itemsCollected = _playerInventory != null ? _playerInventory.KeyItems.Count : 0;
        _elapsedRunTime += Time.fixedDeltaTime * (1f + itemsCollected * _escalationPerItemBonus);
        _escalationProgress = _escalationRampSeconds > 0f ? Mathf.Clamp01(_elapsedRunTime / _escalationRampSeconds) : 1f;
    }

    private void StartCharge()
    {
        _isCharging = true;
        _inChargeWindup = true;
        _chargeWindupTimer = _chargeWindupDuration;
        _chargeTimer = _chargeDuration;
        PlaySound(_chargeScreamClip);
    }

    private void EndCharge()
    {
        _isCharging = false;
        _inChargeWindup = false;
        _chargeCooldownTimer = Random.Range(CurrentChargeCooldownMin, CurrentChargeCooldownMax);
    }

    private void HandleCharge(Vector2 chargeTarget, bool isVisibleToPlayer)
    {
        if (_inChargeWindup)
        {
            // Holds still to scream - EnemyDirectionalSprite's idle behavior already turns him to
            // stare at the player while stationary, so this reads as roaring at them before bolting.
            _currentDynamicSpeed = 0f;
            _desiredDirection = Vector2.zero;
            _chargeWindupTimer -= Time.fixedDeltaTime;

            if (isVisibleToPlayer)
            {
                ResolveChargeInterruption();
                return;
            }

            if (_chargeWindupTimer <= 0f)
            {
                _inChargeWindup = false;
            }
            return;
        }

        _currentDynamicSpeed = _chargeSpeed;
        HandleMoveToPosition(chargeTarget, _instantAttackDistance);
        _chargeTimer -= Time.fixedDeltaTime;

        if (isVisibleToPlayer)
        {
            ResolveChargeInterruption();
            return;
        }

        if (_chargeTimer <= 0f)
        {
            EndCharge();
        }
    }

    // Being spotted mid-charge (windup or sprint) either scares him off or, if he's already angry
    // enough, commits straight into a real attack instead of backing down.
    private void ResolveChargeInterruption()
    {
        bool isAngry = IsAngry;
        EndCharge();

        if (isAngry)
        {
            EnterAttackMode();
        }
        else
        {
            BeginEvasion();
        }
    }

    // Once the sound trail goes cold, he doesn't just freeze - he lingers and wanders randomly
    // around the last place he heard the player for a short while before fully giving up. Getting
    // heard or seen again cancels this immediately (the movement resolution above clears
    // _isPatrolling in every other branch), so it's purely filler between real signals.
    private void HandlePatrol()
    {
        if (!_hasLastKnownPlayerPosition)
        {
            _isPatrolling = false;
            _desiredDirection = Vector2.zero;
            return;
        }

        if (!_isPatrolling)
        {
            _isPatrolling = true;
            _patrolTimer = Random.Range(_patrolDurationMin, _patrolDurationMax);
            PickNewPatrolWaypoint();
        }

        _currentDynamicSpeed = _baseSpeed;
        _patrolTimer -= Time.fixedDeltaTime;
        _patrolWaypointTimer -= Time.fixedDeltaTime;

        if (_patrolWaypointTimer <= 0f || Vector2.Distance(transform.position, _patrolDestination) <= _patrolStoppingDistance)
        {
            PickNewPatrolWaypoint();
        }

        HandleMoveToPosition(_patrolDestination, _patrolStoppingDistance);

        if (_patrolTimer <= 0f)
        {
            _isPatrolling = false;
            _hasLastKnownPlayerPosition = false;
            StartFallback();
        }
    }

    private void PickNewPatrolWaypoint()
    {
        Vector2 offset = Random.insideUnitCircle * _patrolRadius;
        _patrolDestination = _lastKnownPlayerPosition + offset;
        _patrolWaypointTimer = Random.Range(_patrolWaypointIntervalMin, _patrolWaypointIntervalMax);
    }

    // Prevents indefinitely outrunning him: once the trail's gone cold long enough that patrolling
    // gave up, he stops searching and beelines back toward the player's live position instead of
    // idling forever. Resets the tracking meter so he comes back at a fair, fresh baseline rather
    // than already primed to attack the instant he arrives.
    private void StartFallback()
    {
        if (_playerTransform == null) return;

        _isFallingBack = true;
        _fallbackTimer = _fallbackTimeout;
        _fallbackApproachOffset = Random.insideUnitCircle.normalized * _fallbackLandingDistance;
        _trackingTimer = 0f;
        _wasAngry = false;
        PlaySound(_fallbackClip);
    }

    private void HandleFallback()
    {
        if (_playerTransform == null)
        {
            _isFallingBack = false;
            _desiredDirection = Vector2.zero;
            return;
        }

        _currentDynamicSpeed = _fallbackSpeed;
        _fallbackTimer -= Time.fixedDeltaTime;

        _fallbackDestination = (Vector2)_playerTransform.position + _fallbackApproachOffset;
        HandleMoveToPosition(_fallbackDestination, _fallbackStoppingDistance);

        bool arrived = Vector2.Distance(transform.position, _fallbackDestination) <= _fallbackStoppingDistance;
        if (arrived || _fallbackTimer <= 0f)
        {
            _isFallingBack = false;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip, _audioVolume);
        }
    }

    private void BeginEvasion()
    {
        _evasionPhase = EvasionPhase.BreakingLineOfSight;
        _activeCover = null;
    }

    private void EndEvasion()
    {
        _evasionPhase = EvasionPhase.None;
        _activeCover = null;
    }

    // Runs the post-spotted flow: duck out of sight first, then either go quiet for a beat or
    // swing around to a point ahead of the player, so the monster reads as reacting to being seen
    // rather than just sidestepping back into view a moment later.
    private void HandleEvasionStateMachine(bool isVisibleToPlayer)
    {
        switch (_evasionPhase)
        {
            case EvasionPhase.BreakingLineOfSight:
                _currentDynamicSpeed = _evasionSpeed;

                bool foundCover = _playerTransform != null &&
                    (TryFindCoverSpot(_playerTransform.position, out _evasionDestination, _previousCover) ||
                     (_previousCover != null && TryFindCoverSpot(_playerTransform.position, out _evasionDestination, null)));

                if (!foundCover)
                {
                    // No cover nearby - fall back to just putting distance behind it.
                    Vector2 away = _playerTransform != null
                        ? ((Vector2)transform.position - (Vector2)_playerTransform.position).normalized
                        : -_desiredDirection;
                    _evasionDestination = (Vector2)transform.position + away * 5f;
                }

                HandleMoveToPosition(_evasionDestination, _hideStoppingDistance);

                if (!isVisibleToPlayer)
                {
                    _previousCover = _activeCover;
                    TransitionToNextEvasionPhase();
                }
                break;

            case EvasionPhase.Reposition:
                _currentDynamicSpeed = _evasionSpeed;
                _repositionTimer -= Time.fixedDeltaTime;
                HandleMoveToPosition(_evasionDestination, _hideStoppingDistance);

                if (isVisibleToPlayer)
                {
                    _evasionPhase = EvasionPhase.BreakingLineOfSight;
                }
                else if (Vector2.Distance(transform.position, _evasionDestination) <= _hideStoppingDistance ||
                         _repositionTimer <= 0f)
                {
                    EndEvasion();
                }
                break;

            case EvasionPhase.Disengaged:
                _currentDynamicSpeed = _baseSpeed;
                _disengageTimer -= Time.fixedDeltaTime;

                if (_playerTransform != null)
                {
                    float distanceNow = Vector2.Distance(transform.position, _playerTransform.position);
                    Vector2 away = ((Vector2)transform.position - (Vector2)_playerTransform.position).normalized;
                    _evasionDestination = (Vector2)transform.position + away;
                    _desiredDirection = distanceNow < _passiveTrackingRange * 2f ? away : Vector2.zero;
                }

                if (isVisibleToPlayer)
                {
                    _evasionPhase = EvasionPhase.BreakingLineOfSight;
                }
                else if (_disengageTimer <= 0f)
                {
                    EndEvasion();
                }
                break;
        }
    }

    private void TransitionToNextEvasionPhase()
    {
        if (Random.value < _disengageChance)
        {
            _evasionPhase = EvasionPhase.Disengaged;
            _disengageTimer = Random.Range(_disengageDurationMin, _disengageDurationMax);
        }
        else
        {
            _evasionPhase = EvasionPhase.Reposition;
            _evasionDestination = ComputeFlankPosition();
            _repositionTimer = _repositionTimeout;
        }
    }

    // Aims for a point ahead of the player's current facing so the monster approaches from a new
    // angle instead of trailing behind - "coming from the front" rather than following like a puppy.
    private Vector2 ComputeFlankPosition()
    {
        if (_playerTransform == null) return transform.position;

        Vector2 playerPos = _playerTransform.position;
        Vector2 forward = _playerTransform.up;
        Vector2 lateral = new Vector2(-forward.y, forward.x) * (Random.value < 0.5f ? 1f : -1f);

        return playerPos + forward * _repositionForwardDistance + lateral * _repositionLateralOffset;
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
        PlaySound(_attackWarningClip);
        _activeCover = null;
        _previousCover = null;
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

    // The body deliberately never turns. Facing is drawn, not rotated: EnemyDirectionalSprite
    // swaps to the pre-drawn art for the direction of travel, and that art is already angled for
    // the top-down camera, so any real rotation would tilt it. Nothing else reads this rotation
    // (the collider is a circle, and evasion reads the *player's* transform.up), so the body just
    // stays upright and physics is kept from spinning it on collisions.
    private void KeepUpright()
    {
        _rigidbody.SetRotation(0f);
        _rigidbody.angularVelocity = 0f;
    }

    private void SetVelocity()
    {
        // FIX: Increased deadzone slightly to stop movement jitter when reaching a target
        if (_targetDirection == Vector2.zero || _targetDirection.sqrMagnitude < 0.05f)
        {
            _rigidbody.linearVelocity = Vector2.zero;
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
        if (!targetObject.CompareTag(_playerTag)) return;

        // A calm, unseen bump from behind isn't a fair kill - the player had no way to know it was
        // coming. Contact only kills if he's already committed to attacking, angry enough that
        // blind-spot contact is expected to be lethal (telegraphed by the angry sound), or the
        // player could actually see him at the moment of contact.
        bool canKill = _inAttackMode || IsAngry || _lastVisibleToPlayer;
        if (canKill)
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

            // NEW: Draw the vision evasion cutoff radius around the player
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(_playerTransform.position, _maxVisionEvadeDistance);
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _instantAttackDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _passiveTrackingRange);
    }
}