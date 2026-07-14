using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class UIManager : MonoBehaviour {

    [SerializeField] private GameObject pauseMenuUI; // Drag the PauseMenu Image here

    private bool isPaused = false;

    // Might have to move this to GameManager later along with Update()
    void Start()
    {
        // Forces the pause menu to be hidden at the start of the game
        isPaused = false;
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    // Handles show/hide pause menu
    private void TogglePause() {
        isPaused = !isPaused;

        // Only toggles the pause menu; GameplayUI remains untouched
        if (pauseMenuUI != null) {
            pauseMenuUI.SetActive(isPaused);
        }

        Time.timeScale = isPaused ? 0f : 1f;
        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isPaused;
    }

    // Restart the game when the player clicks on Restart Game button
    public void OnRestartPress() { 
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // When return to game button is pressed, it will resume play
    public void OnResumeGamePress() { 
        TogglePause();
    }

    // Exits the game when the player clicks on Exit Game button
    public void OnExitGamePress() {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; 
        #else
            Application.Quit();
        #endif
    }
}
