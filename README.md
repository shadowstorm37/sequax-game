# Sequax

A top-down 2D pixel survival horror game built in Unity. The player is stranded in a dark forest after their campsite is destroyed, hunted by a monster (the Sequax) that tracks almost entirely by sound. No combat - only listening, distraction, and escape.

Full design details live in the Game Design Document (Group 1).

## Team

| Name | Role |
|---|---|
| Dmytro Moshkovskyi | AI / Project Lead |
| Justin Harper | Artist / Programmer |
| Dev Patel | Level Design |
| James Barnett | Programmer / Monster AI |
| Julio Maldonado | UI / Programmer |

## Current Status

Core systems in progress. Monster AI not yet implemented.

### Built and working
- **Sound system** (`Scripts/Game/Systems/`) - global event bus (`SoundManager`) that any object can emit sounds into (`SoundEvent`, `SoundType`). Anything can subscribe and react (monster AI will hook in here once built).
- **Player movement** (`Scripts/Game/Player/`) - Walk / Crouch / Sprint with a stamina bar. Each movement state emits real footstep sounds at different loudness and frequency through the sound system.
- **Camera follow** - smoothed top-down camera tracking.
- **Vision cone / darkness** (`Scripts/Game/Vision/`) - Darkwood-style limited visibility. See **Vision & Visibility** below for current behavior, which has evolved slightly from the original GDD spec.
- **Item pickup** (`Scripts/Game/Items/`) - `ItemData` (ScriptableObject) defines item types; `WorldItem` marks pickupable world objects; `PlayerInventory` handles range-based pickup (E to interact) into a 6-slot inventory.

## Vision & Visibility (design update)

Original GDD spec: area outside the vision cone is solid black.

**Current direction:** the area outside the player's vision cone/circle is **greyscale**, not solid black - the player can make out shapes and terrain at low fidelity, but color is desaturated. Additionally, **items and the monster are not rendered at all outside the vision cone's light** - they only become visible once inside it. This adds a layer of tension distinct from the terrain (you can see *where* you are, but not *what's* out there or what's about to be) and gives the Sequax a genuine "was that there a second ago?" moment when it steps into view.

This is a deliberate evolution from the GDD, not a bug - flagging here so the whole team (especially Dev on level art and Dmytro on monster visibility logic) designs with this in mind rather than the original full-black spec.

**Implementation note:** the current `DarknessOverlay`/`VisionCone` scripts implement the original full-black version. Greyscale-outside-cone will need a shader-based approach (likely a grayscale post-process or a second render layer) rather than the current solid-color `SpriteMask` overlay - not yet implemented.

## Controls (current build)

| Action | Key |
|---|---|
| Move | WASD |
| Sprint | Left Shift |
| Crouch | Left Ctrl |
| Interact / Pick up | E |

## Project Structure

```
Assets/
  Scenes/
  Scripts/
    Game/
      Player/     - PlayerController, camera follow
      Systems/    - SoundEvent, SoundManager
      Vision/     - VisionCone, DarknessOverlay
      Items/      - ItemData, WorldItem, PlayerInventory
      Enemy/      - PlayerAwareness (monster AI, in progress)
      Testing/    - throwaway debug/test scripts, not part of the real game
      Editor/     - editor-only tools (e.g. Find Missing Scripts)
  Sprites/
```

## Setup

1. Clone the repo.
2. Open with Unity 6000.x via Unity Hub.
3. Open `Assets/Scenes/Game.unity` (or whichever scene you're testing in).
4. Project uses the Built-in Render Pipeline, 2D editor defaults.
