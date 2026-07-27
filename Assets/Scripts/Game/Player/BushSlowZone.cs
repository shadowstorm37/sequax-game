using UnityEngine;

/// <summary>
/// Marks a bush trigger volume that slows the player while they overlap it.
/// The actual movement math lives in <see cref="PlayerScript"/> (mirroring the
/// creek slowdown), so this component just tags the object as a slow zone and
/// carries a tunable multiplier for reference/inspector tuning.
/// </summary>
public class BushSlowZone : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Fraction of normal speed the player moves at while inside this bush. 0.5 = 50%.")]
    private float speedMultiplier = 0.5f;

    public float SpeedMultiplier => speedMultiplier;
}
