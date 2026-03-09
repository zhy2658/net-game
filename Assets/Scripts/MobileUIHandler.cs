using UnityEngine;

public class MobileUIHandler : MonoBehaviour
{
    void Awake()
    {
        CheckPlatform();
    }

    void CheckPlatform()
    {
        // Debug Log to help troubleshoot
        Debug.Log($"[MobileUIHandler] Checking Platform... Application.isMobilePlatform: {Application.isMobilePlatform}, Platform: {Application.platform}");

        bool showMobileControls = false;

        if (Application.isMobilePlatform)
        {
            showMobileControls = true;
        }

        // Uncomment to test in Editor
        // #if UNITY_EDITOR
        // showMobileControls = true;
        // #endif

        if (showMobileControls)
        {
            Debug.Log("[MobileUIHandler] Enabling Mobile Controls.");
            gameObject.SetActive(true);
        }
        else
        {
            Debug.Log("[MobileUIHandler] Disabling Mobile Controls (PC/Console detected).");
            gameObject.SetActive(false);
        }
    }
}
