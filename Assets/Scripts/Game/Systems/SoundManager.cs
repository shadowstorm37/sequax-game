using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global sound event bus. 
/// Anything that makes noise calls EmitSound().
/// Anything that needs to hear like the Monster AI and debug tools 
/// subscribes to OnSoundEmitted.
///
/// This class does NOT decide who reacts to a sound - it just broadcasts it.
/// The listener will decide whether it reacts.
/// </summary>
public class SoundManager : MonoBehaviour
{
    private static SoundManager instance;

    // Falls back to a scene search if something's OnEnable/Awake runs before ours does,
    // so callers don't depend on script execution order.
    public static SoundManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SoundManager>();
            }
            return instance;
        }
        private set => instance = value;
    }

    /// <summary>Fired every time a sound is emitted, after environment modifiers are applied.</summary>
    public event Action<SoundEvent> OnSoundEmitted;

    [Header("Debug")]
    [Tooltip("Draw a circle in the Scene view for every emitted sound.")]
    [SerializeField] private bool debugDrawSounds = true;
    [SerializeField] private float debugCircleDuration = 1.5f;
    [SerializeField] private Color debugCircleColor = new Color(1f, 0.4f, 0.1f, 0.8f);

    // Kept around briefly so Gizmos can redraw them between physics/render frames.
    private readonly List<(SoundEvent evt, float expireAt)> recentDebugEvents = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // If SoundManager needs to persist across scene loads later (e.g. tutorial -> main level),
        // uncomment: DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Emit a sound into the world.
    /// </summary>
    /// <param name="position">World position the sound originates from.</param>
    /// <param name="loudness">Base hearing radius before environment modifiers.</param>
    /// <param name="type">Sound category.</param>
    /// <param name="source">Optional originating GameObject (usually the player or an item).</param>
    public void EmitSound(Vector2 position, float loudness, SoundType type, GameObject source = null)
    {
        var evt = new SoundEvent(position, loudness, type, source);

        // Environment modifiers hook: once cabins/indoor zones exist, apply
        // muffling/amplification here before broadcasting. For example:
        //   evt.loudness = EnvironmentZone.ModifyLoudness(evt);
        // Left as a straight pass-through for now.
        evt.loudness = evt.baseLoudness;

        OnSoundEmitted?.Invoke(evt);

        if (debugDrawSounds)
        {
            recentDebugEvents.Add((evt, Time.time + debugCircleDuration));
        }
    }

    private void OnDrawGizmos()
    {
        if (!debugDrawSounds || recentDebugEvents.Count == 0) return;

        Gizmos.color = debugCircleColor;
        for (int i = recentDebugEvents.Count - 1; i >= 0; i--)
        {
            var (evt, expireAt) = recentDebugEvents[i];
            if (Time.time > expireAt)
            {
                recentDebugEvents.RemoveAt(i);
                continue;
            }
            DrawCircle(evt.position, evt.loudness);
        }
    }

    private static void DrawCircle(Vector2 center, float radius, int segments = 24)
    {
        if (radius <= 0f) return;
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
}