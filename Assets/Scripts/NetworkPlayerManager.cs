using System;
using System.Collections.Generic;
using UnityEngine;
using Protocol;
using Google.Protobuf;

public class NetworkPlayerManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public NanoKcpClient kcpClient;
    public string myPlayerId = "";
    public string roomId = "lobby";
    public string playerName = "UnityPlayer";
    public bool autoJoinOnConnect = true;

    private Dictionary<string, RemotePlayerController> _remotePlayers = new Dictionary<string, RemotePlayerController>();
    private bool _hasJoined = false;

    void Start()
    {
        InitializeNetwork();
    }

    void InitializeNetwork()
    {
        if (kcpClient == null) kcpClient = FindFirstObjectByType<NanoKcpClient>();

        if (kcpClient != null)
        {
            kcpClient.OnConnected += OnConnected;
            if (kcpClient.IsConnected) OnConnected();

            kcpClient.RegisterHandler("OnSelfJoin", OnSelfJoin);
            kcpClient.RegisterHandler("OnPlayerMove", OnPlayerMove);
            kcpClient.RegisterHandler("OnPlayerJoin", OnPlayerJoin);
            kcpClient.RegisterHandler("OnPlayerLeave", OnPlayerLeave);
            kcpClient.RegisterHandler("OnPlayerEnterAOI", OnPlayerEnterAOI);
            kcpClient.RegisterHandler("OnPlayerLeaveAOI", OnPlayerLeaveAOI);

            Debug.Log($"[NET] NetworkPlayerManager initialized. Handlers registered.");
        }
        else
        {
            Debug.LogError("[NET] NanoKcpClient not found!");
        }
    }

    void OnDestroy()
    {
        if (kcpClient != null)
        {
            kcpClient.OnConnected -= OnConnected;
            kcpClient.UnregisterHandler("OnSelfJoin");
            kcpClient.UnregisterHandler("OnPlayerMove");
            kcpClient.UnregisterHandler("OnPlayerJoin");
            kcpClient.UnregisterHandler("OnPlayerLeave");
            kcpClient.UnregisterHandler("OnPlayerEnterAOI");
            kcpClient.UnregisterHandler("OnPlayerLeaveAOI");
        }
    }

    private void OnConnected()
    {
        Debug.Log("[NET] KCP Connected.");
        if (autoJoinOnConnect && !_hasJoined)
        {
            Debug.Log($"[NET] Auto-joining room: {roomId}");
            kcpClient.JoinRoom(roomId, playerName);
        }
    }

    private void OnSelfJoin(byte[] data)
    {
        try
        {
            var msg = PlayerState.Parser.ParseFrom(data);
            myPlayerId = msg.Id;
            _hasJoined = true;
            Debug.Log($"[NET] Joined! My Player ID: {myPlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NET] Error parsing OnSelfJoin: {e}");
        }
    }

    private void OnPlayerMove(byte[] data)
    {
        try
        {
            var msg = PlayerMovePush.Parser.ParseFrom(data);

            if (msg.Id == myPlayerId) return;

            if (_remotePlayers.TryGetValue(msg.Id, out var remote))
            {
                remote.SetTarget(msg.Position, msg.Rotation);
            }
            else
            {
                Debug.Log($"[NET] Move from unknown player {msg.Id}, spawning...");
                SpawnPlayer(msg.Id, msg.Position, msg.Rotation);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NET] Error parsing OnPlayerMove: {e}");
        }
    }

    private void OnPlayerJoin(byte[] data)
    {
        try
        {
            var msg = PlayerJoinPush.Parser.ParseFrom(data);
            Debug.Log($"[NET] Player joined: {msg.Id} ({msg.Name})");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NET] Error parsing OnPlayerJoin: {e}");
        }
    }

    private void OnPlayerEnterAOI(byte[] data)
    {
        try
        {
            var msg = PlayerState.Parser.ParseFrom(data);
            Debug.Log($"[NET] Player entered AOI: {msg.Id} at ({msg.Position?.X}, {msg.Position?.Y}, {msg.Position?.Z})");

            if (msg.Id == myPlayerId) return;

            if (!_remotePlayers.ContainsKey(msg.Id))
            {
                SpawnPlayer(msg.Id, msg.Position, msg.Rotation);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NET] Error parsing OnPlayerEnterAOI: {e}");
        }
    }

    private void OnPlayerLeave(byte[] data)
    {
        try
        {
            var msg = PlayerLeavePush.Parser.ParseFrom(data);
            Debug.Log($"[NET] Player left: {msg.Id}");
            RemovePlayer(msg.Id);
        }
        catch (Exception e)
        {
            Debug.LogError($"[NET] Error parsing OnPlayerLeave: {e}");
        }
    }

    private void OnPlayerLeaveAOI(byte[] data)
    {
        try
        {
            var msg = PlayerLeavePush.Parser.ParseFrom(data);
            Debug.Log($"[NET] Player left AOI: {msg.Id}");
            RemovePlayer(msg.Id);
        }
        catch (Exception e)
        {
            Debug.LogError($"[NET] Error parsing OnPlayerLeaveAOI: {e}");
        }
    }

    private void SpawnPlayer(string id, Protocol.Vector3 pos, Protocol.Quaternion rot)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[NET] playerPrefab not assigned in NetworkPlayerManager!");
            return;
        }

        if (_remotePlayers.ContainsKey(id)) return;

        UnityEngine.Vector3 spawnPos = new UnityEngine.Vector3(
            pos != null ? pos.X : 0,
            pos != null ? pos.Y : 0,
            pos != null ? pos.Z : 0);
        UnityEngine.Quaternion spawnRot = rot != null
            ? new UnityEngine.Quaternion(rot.X, rot.Y, rot.Z, rot.W)
            : UnityEngine.Quaternion.identity;

        GameObject go = Instantiate(playerPrefab, spawnPos, spawnRot);
        go.name = $"RemotePlayer_{id}";

        var tpc = go.GetComponent<SimpleThirdPersonController>();
        if (tpc != null) Destroy(tpc);

        var audioListener = go.GetComponentInChildren<AudioListener>();
        if (audioListener != null) Destroy(audioListener);

        var cam = go.GetComponentInChildren<Camera>();
        if (cam != null) Destroy(cam.gameObject);

        var remote = go.GetComponent<RemotePlayerController>();
        if (remote == null) remote = go.AddComponent<RemotePlayerController>();
        remote.SetTarget(pos, rot);

        _remotePlayers.Add(id, remote);
        Debug.Log($"[NET] Spawned remote player: {id} at {spawnPos}");
    }

    private void RemovePlayer(string id)
    {
        if (_remotePlayers.TryGetValue(id, out var remote))
        {
            if (remote != null && remote.gameObject != null)
                Destroy(remote.gameObject);
            _remotePlayers.Remove(id);
            Debug.Log($"[NET] Removed remote player: {id}");
        }
    }
}
