using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameMenu : MonoBehaviour
{
    private bool _isPaused = false;
    private Rect _menuRect;

    void Start()
    {
        _menuRect = new Rect(Screen.width / 2 - 100, Screen.height / 2 - 100, 200, 200);
    }

    void Update()
    {
        // Toggle Menu with ESC
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;

        if (_isPaused)
        {
            Time.timeScale = 0f; // Pause Game
            Cursor.lockState = CursorLockMode.None; // Unlock Cursor
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1f; // Resume Game
            Cursor.lockState = CursorLockMode.Locked; // Lock Cursor
            Cursor.visible = false;
        }
    }

    void OnGUI()
    {
        if (_isPaused)
        {
            // Draw semi-transparent background
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            // Draw Menu
            GUILayout.BeginArea(_menuRect);
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("<b>PAUSED</b>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 });
            GUILayout.Space(20);

            if (GUILayout.Button("Resume", GUILayout.Height(30)))
            {
                TogglePause();
            }
            
            GUILayout.Space(10);

            if (GUILayout.Button("Restart", GUILayout.Height(30)))
            {
                Time.timeScale = 1f; // Reset time before reloading
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Quit Game", GUILayout.Height(30)))
            {
                #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                #else
                    Application.Quit();
                #endif
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
