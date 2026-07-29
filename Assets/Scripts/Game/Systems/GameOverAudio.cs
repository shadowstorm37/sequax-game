using UnityEngine;

namespace Game.Systems
{
    /// <summary>Plays the death/game-over sting once as soon as the GameOver scene loads.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class GameOverAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip deathSound;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        private void Start()
        {
            AudioSource audioSource = GetComponent<AudioSource>();
            if (deathSound != null)
            {
                audioSource.PlayOneShot(deathSound, volume);
            }
        }
    }
}
