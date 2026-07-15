using UnityEngine;

public class RoofVisibilityTrigger : MonoBehaviour
{
    [SerializeField] private GameObject roof;

    private int playerTriggerCount;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerTriggerCount++;
        roof.SetActive(false);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerTriggerCount--;

        if (playerTriggerCount <= 0)
        {
            playerTriggerCount = 0;
            roof.SetActive(true);
        }
    }
}