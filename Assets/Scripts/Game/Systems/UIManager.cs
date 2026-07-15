using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{

    [SerializeField] private GameObject pauseMenuUI; // PauseMenu Image kept here

    private bool isPaused = false;

    void Start()
    {
        isPaused = false;
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;

        // CHECK IF MAIN MENU: If buildIndex is 0, unlock the mouse so players can click buttons
        if (SceneManager.GetActiveScene().buildIndex == 0)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void Update()
    {
        // Only allow pausing if we are NOT on the main menu
        if (SceneManager.GetActiveScene().buildIndex != 0 && Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
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
        SceneManager.LoadScene(1); // Loads the scene at index 1 in Build Settings
    }

    // Restart the game when the player clicks on Restart Game button
    public void OnRestartPress()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // When return to game button is pressed, it will resume play
    public void OnResumeGamePress()
    {
        TogglePause();
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
