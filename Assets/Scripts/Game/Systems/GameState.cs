/// <summary>
/// Global flag for whether the player has been handed control of gameplay yet.
/// Set to true by GameIntroSequencer once the in-game intro finishes; systems that
/// shouldn't act while the intro is still playing (e.g. the monster's aggro timers)
/// should check this before ticking.
/// </summary>
public static class GameState
{
    // Defaults true so scenes without a GameIntroSequencer (e.g. testing a level directly) work normally.
    public static bool PlayerHasControl = true;
}
