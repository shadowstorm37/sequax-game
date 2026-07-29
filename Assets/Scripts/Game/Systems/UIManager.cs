using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuUI; // PauseMenu Image kept here

    [Header("Note System Integration")]
    [Tooltip("Drag the same UI overlay panel used for reading notes here.")]
    [SerializeField] private GameObject noteDisplayPanel; // Reference to block pause while reading

    [Header("Scene Configuration")]
    [Tooltip("The exact name of your main gameplay forest scene.")]
    [SerializeField] private string gameplaySceneName = "MainForest";

    private bool isPaused = false;

    void Start()
    {
        isPaused = false;
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;

        // Forces the Resume button speaker to bypass the pause state ---
        AudioSource resumeAudio = GetComponent<AudioSource>();
        if (resumeAudio == null && pauseMenuUI != null)
        {
            resumeAudio = pauseMenuUI.GetComponentInChildren<AudioSource>();
        }

        if (resumeAudio != null)
        {
            resumeAudio.ignoreListenerPause = true; // Tells Unity to play this audio even if game time is 0
        }

        // UNLOCK MOUSE: Unlock for Menus, Game Over, or Victory scenes so players can click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        // Block pausing if we are on the Main Menu, WinsGame screen, or GameOver screen
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene != "WinsGame" && currentScene != "GameOver" && SceneManager.GetActiveScene().buildIndex != 0)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Check the static variable from NoteInteractable
                if (Game.Items.NoteInteractable.IsReadingNote)
                {
                    return; // Skip pausing entirely on this frame
                }

                TogglePause();
            }
        }
    }

    // Handles show/hide pause menu
    private void TogglePause()
    {
        isPaused = !isPaused;

        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(isPaused);
        }

        Time.timeScale = isPaused ? 0f : 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Loads the first level (Scene index 1) when Start Game is pressed
    public void OnStartGamePress()
    {
        Time.timeScale = 1f;

        // NEW: Wipe the intro completed flag here so a fresh run from the main menu ALWAYS plays the intro!
        PlayerPrefs.DeleteKey("IntroCompleted");
        PlayerPrefs.Save();

        SceneManager.LoadScene(1); // Loads the scene at index 1 in Build Settings
    }

    // Restart the game from WITHIN the gameplay scene via pause menu
    public void OnRestartPress()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // NEW: Call this from your "Try Again" button on the separate Game Over screen
    public void OnTryAgainPress()
    {
        Time.timeScale = 1f;

        // We intentionally do NOT delete "IntroCompleted" here.
        // Your GameIntroSequencer will see the key and instantly skip the intro cutscene!
        SceneManager.LoadScene(gameplaySceneName);
    }

    // When return to game button is pressed, it will resume play
    public void OnResumeGamePress()
    {
        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null)
        {
            audio.Play();
        }

        TogglePause();
    }

    // Returns the player to the Main Menu screen from anywhere
    public void OnMainMenuPress()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // Exits the game when the player clicks on Exit Game button
    public void OnExitGamePress()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; 
#else
        Application.Quit();
#endif
    }
}
