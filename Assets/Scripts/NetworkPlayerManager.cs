using System;
using System.Collections.Generic;
using UnityEngine;
using Protocol;

public class NetworkPlayerManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public NanoKcpClient kcpClient;
    public string myPlayerId = "";
    public string roomId = "lobby";
    public string playerName = "UnityPlayer";
    public bool autoJoinOnConnect = true;

    private Dictionary<string, RemotePlayerController> _remotePlayers = new();
    private bool _hasJoined;
    private RuntimeAnimatorController _animCtrl;

    void Start()
    {
        var localPlayer = FindFirstObjectByType<SimpleThirdPersonController>();
        if (localPlayer != null)
        {
            var localAnim = localPlayer.GetComponent<Animator>();
            if (localAnim != null)
                _animCtrl = localAnim.runtimeAnimatorController;
        }

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
        if (autoJoinOnConnect && !_hasJoined)
            kcpClient.JoinRoom(roomId, playerName);
    }

    private void OnSelfJoin(byte[] data)
    {
        try
        {
            var msg = PlayerState.Parser.ParseFrom(data);
            myPlayerId = msg.Id;
            _hasJoined = true;
            Debug.Log($"[NET] Joined as player {myPlayerId}");
        }
        catch (Exception e) { Debug.LogError($"[NET] OnSelfJoin error: {e.Message}"); }
    }

    private void OnPlayerMove(byte[] data)
    {
        try
        {
            var msg = PlayerMovePush.Parser.ParseFrom(data);
            if (msg.Id == myPlayerId) return;

            if (_remotePlayers.TryGetValue(msg.Id, out var remote))
                remote.SetTarget(msg.Position, msg.Rotation, msg.Speed, msg.IsGrounded);
            else
                SpawnPlayer(msg.Id, msg.Position, msg.Rotation);
        }
        catch (Exception e) { Debug.LogError($"[NET] OnPlayerMove error: {e.Message}"); }
    }

    private void OnPlayerJoin(byte[] data)
    {
        try
        {
            var msg = PlayerJoinPush.Parser.ParseFrom(data);
            Debug.Log($"[NET] Player joined: {msg.Id} ({msg.Name})");
        }
        catch (Exception e) { Debug.LogError($"[NET] OnPlayerJoin error: {e.Message}"); }
    }

    private void OnPlayerEnterAOI(byte[] data)
    {
        try
        {
            var msg = PlayerState.Parser.ParseFrom(data);
            if (msg.Id == myPlayerId) return;
            if (!_remotePlayers.ContainsKey(msg.Id))
                SpawnPlayer(msg.Id, msg.Position, msg.Rotation);
        }
        catch (Exception e) { Debug.LogError($"[NET] OnPlayerEnterAOI error: {e.Message}"); }
    }

    private void OnPlayerLeave(byte[] data)
    {
        try
        {
            var msg = PlayerLeavePush.Parser.ParseFrom(data);
            RemovePlayer(msg.Id);
        }
        catch (Exception e) { Debug.LogError($"[NET] OnPlayerLeave error: {e.Message}"); }
    }

    private void OnPlayerLeaveAOI(byte[] data)
    {
        try
        {
            var msg = PlayerLeavePush.Parser.ParseFrom(data);
            RemovePlayer(msg.Id);
        }
        catch (Exception e) { Debug.LogError($"[NET] OnPlayerLeaveAOI error: {e.Message}"); }
    }

    private void SpawnPlayer(string id, Protocol.Vector3 pos, Protocol.Quaternion rot)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[NET] playerPrefab not assigned!");
            return;
        }
        if (_remotePlayers.ContainsKey(id)) return;

        var spawnPos = new UnityEngine.Vector3(pos?.X ?? 0, pos?.Y ?? 0, pos?.Z ?? 0);
        var spawnRot = rot != null
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

        // Force-assign AnimatorController (prefab variant override is unreliable)
        var animator = go.GetComponent<Animator>();
        if (animator != null && _animCtrl != null)
            animator.runtimeAnimatorController = _animCtrl;

        var remote = go.GetComponent<RemotePlayerController>();
        if (remote == null) remote = go.AddComponent<RemotePlayerController>();
        remote.SetTarget(pos, rot);

        _remotePlayers.Add(id, remote);
        Debug.Log($"[NET] Spawned remote player: {id}");
    }

    private void RemovePlayer(string id)
    {
        if (_remotePlayers.TryGetValue(id, out var remote))
        {
            if (remote != null && remote.gameObject != null)
                Destroy(remote.gameObject);
            _remotePlayers.Remove(id);
        }
    }
}
