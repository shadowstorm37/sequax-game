using System.Collections.Generic;
using UnityEngine;

public class SoundAwareness : MonoBehaviour
{
    public bool PlayerTooClose { get; private set; }
    public Vector2 DirectionToSound { get; private set; } 

    [Header("Debug Adjustments")]
    [Tooltip("Draw a circle in the Scene view for this object's hearing threshold.")]
    [SerializeField] private bool _debugDrawHearingRange = true;
    [SerializeField] private Color _debugCircleColor = new Color(0.2f, 0.6f, 1f, 0.6f);

    [Header("Current Target Sound Data")]
    public bool IsHearingSound;
    public Vector2 TargetSoundLocation;
    public SoundType TargetSoundType;
    public float TargetSoundLoudness; 
    public string LastSoundSourceTag; 

    [Header("Acoustic Bounds")]
    [SerializeField] private float _playerTooCloseDistance = 2f; 
    [SerializeField] private float _hearingRange = 15f;          

    private Transform _player;
    private readonly List<HeardSoundNode> _perceivedSoundsStack = new();

    private class HeardSoundNode
    {
        public SoundEvent Data;
        public float CreatedTime;
    }

    private void Awake() 
    {
        var playerScript = FindFirstObjectByType<PlayerScript>();
        if (playerScript != null)
        {
            _player = playerScript.transform;
        }
    }

    private void Update()
    {
        CleanExpiredSounds(decayDuration: 2.0f);

        // 1. Proximity Aggro Override (The absolute loudest sound/threat)
        if (_player != null)
        {
            Vector2 enemyToPlayerVector = (Vector2)_player.position - (Vector2)transform.position;
            PlayerTooClose = enemyToPlayerVector.magnitude <= _playerTooCloseDistance;

            if (PlayerTooClose)
            {
                DirectionToSound = enemyToPlayerVector.normalized;
                IsHearingSound = true;
                TargetSoundLocation = _player.position;
                TargetSoundLoudness = 999f; 
                LastSoundSourceTag = _player.tag; // Instantly target the player's tag
                return; 
            }
        }

        // 2. Process the Loudest Sound on Stack
        if (_perceivedSoundsStack.Count > 0)
        {
            // Because the stack is sorted descending, index 0 is always the loudest active sound
            HeardSoundNode dominantSound = _perceivedSoundsStack[0];
            
            IsHearingSound = true;
            TargetSoundLocation = dominantSound.Data.position;
            TargetSoundType = dominantSound.Data.type;
            TargetSoundLoudness = dominantSound.Data.loudness;

            // FIX: Keep the source tag synchronized with the loudest sound being targeted
            if (dominantSound.Data.source != null)
            {
                LastSoundSourceTag = dominantSound.Data.source.tag;
            }
            else
            {
                LastSoundSourceTag = "Untagged";
            }

            Vector2 monsterToSound = TargetSoundLocation - (Vector2)transform.position;
            DirectionToSound = monsterToSound.normalized;
        }
        else
        {
            IsHearingSound = false;
            TargetSoundLoudness = 0f;
            DirectionToSound = Vector2.zero;
            LastSoundSourceTag = "Untagged";
        }
    }

    private void HandleSoundEmitted(SoundEvent soundData)
    {
        float distance = Vector2.Distance(transform.position, soundData.position);

        if (distance <= (soundData.loudness + _hearingRange))
        {
            ReactToSound(soundData);
        }
    }

    private void ReactToSound(SoundEvent soundData)
    {
        HeardSoundNode newSound = new HeardSoundNode
        {
            Data = soundData,
            CreatedTime = Time.time
        };

        _perceivedSoundsStack.Add(newSound);
        
        // Sorts descending: loudest sound (highest loudness value) ends up at index 0
        _perceivedSoundsStack.Sort((a, b) => b.Data.loudness.CompareTo(a.Data.loudness));
    }

    private void CleanExpiredSounds(float decayDuration)
    {
        bool stackChanged = false;

        for (int i = _perceivedSoundsStack.Count - 1; i >= 0; i--)
        {
            if (Time.time - _perceivedSoundsStack[i].CreatedTime > decayDuration)
            {
                _perceivedSoundsStack.RemoveAt(i);
                stackChanged = true;
            }
        }

        if (stackChanged && _perceivedSoundsStack.Count > 1)
        {
            _perceivedSoundsStack.Sort((a, b) => b.Data.loudness.CompareTo(a.Data.loudness));
        }
    }

    // GIZMO RENDER PROCEDURES
    private void OnDrawGizmos()
    {
        if (!_debugDrawHearingRange || _hearingRange <= 0f) return;

        Gizmos.color = _debugCircleColor;
        DrawHearingCircle(transform.position, _hearingRange);
    }

    private static void DrawHearingCircle(Vector2 center, float radius, int segments = 24)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector2(radius, 0f);
        
        for (int i = 1; i <= segments; i++)
        {
            float rad = Mathf.Deg2Rad * (angleStep * i);
            Vector3 nextPoint = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }

    private void OnEnable()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.OnSoundEmitted += HandleSoundEmitted;
        }
    }

    private void OnDisable()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.OnSoundEmitted -= HandleSoundEmitted;
        }
    }
}