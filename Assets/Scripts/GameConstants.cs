using UnityEngine;

public static class GameConstants
{
    public static readonly Vector3 SafeSpawnPos = new Vector3(82f, 15f, -50f);

    public const float MoveSpeed = 5f;
    public const float RunSpeed = 8f;
    public const float SyncInterval = 0.05f; // 20 Hz

    public const string DefaultRoom = "lobby";
    public const string DefaultPlayerName = "UnityPlayer";
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 3250;
}
