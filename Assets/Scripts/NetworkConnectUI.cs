using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class NetworkConnectUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    void Awake()
    {
        // Auto-assign buttons if not set (looks for child buttons by name)
        if (hostButton == null) hostButton = transform.Find("HostButton")?.GetComponent<Button>();
        if (clientButton == null) clientButton = transform.Find("ClientButton")?.GetComponent<Button>();
    }

    void Start()
    {
        // Force unlock cursor so we can click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (hostButton != null)
        {
            hostButton.onClick.AddListener(() => {
                NetworkManager.Singleton.StartHost();
                HideUI();
            });
        }

        if (clientButton != null)
        {
            clientButton.onClick.AddListener(() => {
                NetworkManager.Singleton.StartClient();
                HideUI();
            });
        }
        
        // Ensure NanoClient exists
        if (FindObjectOfType<NanoClient>() == null)
        {
            GameObject nanoObj = new GameObject("NanoClient");
            if (System.Type.GetType("NanoClient") != null)
            {
                 nanoObj.AddComponent(System.Type.GetType("NanoClient"));
            }
        }
    }

    void HideUI()
    {
        // Only hide the buttons, not the whole UI object if it contains other things
        if (hostButton != null) hostButton.gameObject.SetActive(false);
        if (clientButton != null) clientButton.gameObject.SetActive(false);
    }
}
