using UnityEngine;
using UnityEngine.UI;
using Game.Items;
using System.Linq;

public class CarRequirementsUI : MonoBehaviour
{
    [Header("UI Panels")]
    [Tooltip("The main parent Canvas/Panel GameObject that holds the entire popup UI layout.")]
    [SerializeField] private GameObject popupPanel;

    [Header("Item Requirement Images")]
    [SerializeField] private Image carKeysImage;
    [SerializeField] private Image screwdriverImage;
    [SerializeField] private Image electricalTapeImage;
    [SerializeField] private Image wireCuttersImage;

    [Header("Visual Tuning")]
    [Tooltip("The opacity alpha level applied to items the player is missing (e.g., 0.3f for 30% opacity).")]
    [Range(0f, 1f)]
    [SerializeField] private float missingItemAlpha = 0.3f;

    private Inventory playerInventory;
    private bool isPlayerOverlapping = false;

    private void Start()
    {
        // Hide the requirements popup entirely when the game boots up
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    private void Update()
    {
        // Only evaluate inventory status if the player is actually looking at or standing near the vehicle
        if (isPlayerOverlapping && playerInventory != null)
        {
            RefreshRequirementVisuals();
        }
    }

    /// <summary>Called by the CarActivation trigger zones to present the visual list.</summary>
    public void ShowRequirements(Inventory inventory)
    {
        playerInventory = inventory;
        isPlayerOverlapping = true;

        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

        RefreshRequirementVisuals();
    }

    /// <summary>Called by the CarActivation trigger zones when the player moves away.</summary>
    public void HideRequirements()
    {
        isPlayerOverlapping = false;
        playerInventory = null;

        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    /// <summary>Queries the player's key item hashsets and adjusts image transparency accordingly.</summary>
    private void RefreshRequirementVisuals()
    {
        if (playerInventory == null) return;

        // Check individual key items using the exact collection structure from Inventory.cs
        SetItemState(carKeysImage, playerInventory.HasCarKeys);

        // Check the individual sub-items manually against the KeyItems collection
        bool hasScrewdriver = playerInventory.KeyItems.Contains(ItemId.Screwdriver);
        bool hasTape = playerInventory.KeyItems.Contains(ItemId.ElectricalTape);
        bool hasCutters = playerInventory.KeyItems.Contains(ItemId.WireCutters);

        SetItemState(screwdriverImage, hasScrewdriver);
        SetItemState(electricalTapeImage, hasTape);
        SetItemState(wireCuttersImage, hasCutters);
    }

    private void SetItemState(Image targetImage, bool hasItem)
    {
        if (targetImage == null) return;

        Color targetColor = targetImage.color;

        // If they have the item, make it completely solid (100% alpha). Otherwise, drop its alpha down.
        targetColor.a = hasItem ? 1f : missingItemAlpha;

        // Optional Bonus: Slightly tint missing items darker grey to maximize the visual read
        targetColor.r = hasItem ? 1f : 0.5f;
        targetColor.g = hasItem ? 1f : 0.5f;
        targetColor.b = hasItem ? 1f : 0.5f;

        targetImage.color = targetColor;
    }
}
