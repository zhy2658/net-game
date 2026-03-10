using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameMenu : MonoBehaviour
{
    private bool _isPaused;
    private Rect _menuRect;

    void Start()
    {
        _menuRect = new Rect(Screen.width / 2 - 100, Screen.height / 2 - 100, 200, 200);
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        Time.timeScale = _isPaused ? 0f : 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        if (!_isPaused) return;

        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

        GUILayout.BeginArea(_menuRect);
        GUILayout.BeginVertical("box");

        GUILayout.Label("<b>PAUSED</b>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 });
        GUILayout.Space(20);

        if (GUILayout.Button("Resume", GUILayout.Height(30)))
            TogglePause();

        GUILayout.Space(10);
        if (GUILayout.Button("Restart", GUILayout.Height(30)))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Quit", GUILayout.Height(30)))
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
