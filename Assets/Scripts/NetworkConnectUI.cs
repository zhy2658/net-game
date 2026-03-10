using UnityEngine;
using UnityEngine.UI;

public class NetworkConnectUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    [Header("KCP Connection")]
    [SerializeField] private string kcpHost = "127.0.0.1";
    [SerializeField] private int kcpPort = 3250;
    [SerializeField] private string roomId = "lobby";

    private NanoKcpClient _kcpClient;

    void Awake()
    {
        if (hostButton == null) hostButton = transform.Find("HostButton")?.GetComponent<Button>();
        if (clientButton == null) clientButton = transform.Find("ClientButton")?.GetComponent<Button>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _kcpClient = FindFirstObjectByType<NanoKcpClient>();
        if (_kcpClient == null)
        {
            GameObject nanoObj = new GameObject("NanoKcpClient");
            _kcpClient = nanoObj.AddComponent<NanoKcpClient>();
        }

        if (hostButton != null)
        {
            hostButton.onClick.AddListener(ConnectKcp);
        }

        if (clientButton != null)
        {
            clientButton.onClick.AddListener(ConnectKcp);
        }
    }

    private void ConnectKcp()
    {
        if (_kcpClient == null) return;

        _kcpClient.host = kcpHost;
        _kcpClient.port = kcpPort;
        _kcpClient.Connect();
        HideUI();
    }

    void HideUI()
    {
        if (hostButton != null) hostButton.gameObject.SetActive(false);
        if (clientButton != null) clientButton.gameObject.SetActive(false);
    }
}
