using UnityEngine;

namespace Game.Items
{
    /// <summary>
    /// One of these per throwable item, each with its own AudioSource + clip -
    /// same pairing PlayerScript uses for footstep audio, just one instance per item
    /// instead of a single shared source. Put each as a child object (e.g. under the
    /// Player) so you can assign a different AudioSource/clip per item in the Inspector.
    /// </summary>
    public class ThrowableItemAudio : MonoBehaviour
    {
        [SerializeField] private ItemId itemId;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        public ItemId ItemId => itemId;

        public void Play()
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }
    }
}
