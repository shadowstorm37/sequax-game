using UnityEngine;

public class SoundDebugListener : MonoBehaviour
{
    private void OnEnable()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.OnSoundEmitted += HandleSound;
        }
        else
        {
            Debug.LogWarning("SoundDebugListener: no SoundManager.Instance yet in OnEnable. " +
                              "Make sure SoundManager's Awake() runs before this (check Script Execution Order if needed).");
        }
    }

    private void OnDisable()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.OnSoundEmitted -= HandleSound;
        }
    }

    private void HandleSound(SoundEvent evt)
    {
        Debug.Log($"[Sound] type={evt.type} pos={evt.position} loudness={evt.loudness} " +
                  $"source={(evt.source != null ? evt.source.name : "none")} t={evt.timestamp:F2}");
    }
}