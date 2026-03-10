using UnityEngine;
using UnityEngine.UI;

public class NetworkConnectUI : MonoBehaviour
{
    [SerializeField] private Button connectButton;

    [Header("KCP Connection")]
    [SerializeField] private string kcpHost = GameConstants.DefaultHost;
    [SerializeField] private int kcpPort = GameConstants.DefaultPort;

    private NanoKcpClient _kcpClient;

    void Awake()
    {
        if (connectButton == null)
            connectButton = GetComponentInChildren<Button>();
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

        if (connectButton != null)
            connectButton.onClick.AddListener(ConnectKcp);
    }

    private void ConnectKcp()
    {
        if (_kcpClient == null) return;

        _kcpClient.host = kcpHost;
        _kcpClient.port = kcpPort;
        _kcpClient.Connect();

        if (connectButton != null)
            connectButton.gameObject.SetActive(false);
    }
}
