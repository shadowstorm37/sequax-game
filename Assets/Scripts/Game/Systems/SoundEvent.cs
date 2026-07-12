using UnityEngine;

/// <summary>
/// Categorizes sound sources so listeners can weigh them
/// differently and so distraction-fatigue can track how many times a sound is
/// heard.
/// </summary>
public enum SoundType
{
    Footstep,
    Sprint,
    ThrowImpact,
    GlassBreak,
    Radio,
    Phone,
    PlasticBag,
    Door,
    Keys,
    Custom
}

/// <summary>
/// A single emitted sound. Immutable snapshot passed to listeners.
/// Loudness is treated as a world-space hearing radius, not decibels -
/// simplest thing that works and easy to eyeball-tune in the Inspector.
/// </summary>
[System.Serializable]
public struct SoundEvent
{
    public Vector2 position;
    public float loudness;      // effective hearing radius, in world units, AFTER environment modifiers
    public float baseLoudness;  // radius BEFORE modifiers, kept for debugging/tuning
    public SoundType type;
    public float timestamp;
    public GameObject source;   // may be null for ambient/scripted sounds

    public SoundEvent(Vector2 position, float loudness, SoundType type, GameObject source = null)
    {
        this.position = position;
        this.baseLoudness = loudness;
        this.loudness = loudness; // SoundManager may overwrite this after applying modifiers
        this.type = type;
        this.timestamp = Time.time;
        this.source = source;
    }
}