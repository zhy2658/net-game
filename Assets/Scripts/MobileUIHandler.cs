using UnityEngine;

public class MobileUIHandler : MonoBehaviour
{
    void Awake()
    {
        gameObject.SetActive(Application.isMobilePlatform);
    }
}
