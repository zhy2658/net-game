using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using KcpProject;
using Google.Protobuf;
using Protocol;

public class NanoKcpClient : MonoBehaviour
{
    public string host = GameConstants.DefaultHost;
    public int port = GameConstants.DefaultPort;

    [Header("Reconnection")]
    public bool autoReconnect = true;
    public float reconnectBaseDelay = 1f;
    public float reconnectMaxDelay = 30f;
    public int maxReconnectAttempts = 10;

    private UDPSession session;
    private byte[] recvBuffer = new byte[8192];
    private MemoryStream streamBuffer = new MemoryStream();
    private bool isConnected;
    private int reqId = 1;
    private float _lastHeartbeatTime;

    private int _reconnectAttempts;
    private float _nextReconnectTime;
    private bool _wasConnected;

    public bool IsConnected => isConnected;

    const int HEADER_LENGTH = 4;
    const int MSG_TYPE_HANDSHAKE = 1;
    const int MSG_TYPE_HANDSHAKE_ACK = 2;
    const int MSG_TYPE_HEARTBEAT = 3;
    const int MSG_TYPE_DATA = 4;
    const int MSG_TYPE_KICK = 5;

    public Action OnConnected;
    public Action OnDisconnected;

    private Dictionary<string, Action<byte[]>> _routeHandlers = new();
    private Dictionary<int, Action<byte[]>> _responseCallbacks = new();

    public void RegisterHandler(string route, Action<byte[]> handler) => _routeHandlers[route] = handler;
    public void UnregisterHandler(string route) => _routeHandlers.Remove(route);

    void Start() => Connect();

    public void JoinRoom(string roomId = "", string playerName = "UnityPlayer")
    {
        if (!isConnected) return;
        SendRequest("room.join", new JoinRequest { RoomId = roomId, Name = playerName });
    }

    public void Connect()
    {
        if (session != null && isConnected) return;

        try
        {
            Disconnect();
            session = new UDPSession();
            session.Connect(host, port);
            session.mKCP.NoDelay(1, 10, 2, 1);
            session.mKCP.WndSize(128, 128);
            session.mKCP.SetStreamMode(true);
            session.AckNoDelay = true;
            Debug.Log($"[KCP] Connecting to {host}:{port}");
            SendHandshake();
        }
        catch (Exception e)
        {
            Debug.LogError($"[KCP] Connect failed: {e.Message}");
            ScheduleReconnect();
        }
    }

    public void Disconnect()
    {
        if (session != null)
        {
            try { session.Close(); } catch { }
            session = null;
        }

        if (isConnected)
        {
            isConnected = false;
            OnDisconnected?.Invoke();
        }
        streamBuffer.SetLength(0);
    }

    void Update()
    {
        if (session != null)
        {
            try { session.Update(); }
            catch { HandleConnectionLost(); return; }

            if (isConnected && Time.time - _lastHeartbeatTime > 5f)
            {
                _lastHeartbeatTime = Time.time;
                SendRaw(MSG_TYPE_HEARTBEAT, new byte[0]);
            }

            try
            {
                while (true)
                {
                    int n = session.Recv(recvBuffer, 0, recvBuffer.Length);
                    if (n <= 0) break;

                    long oldPos = streamBuffer.Position;
                    streamBuffer.Seek(0, SeekOrigin.End);
                    streamBuffer.Write(recvBuffer, 0, n);
                    streamBuffer.Position = oldPos;
                }
                ProcessStream();
            }
            catch (Exception e)
            {
                Debug.LogError($"[KCP] Recv error: {e.Message}");
                HandleConnectionLost();
            }
        }
        else if (autoReconnect && _wasConnected && Time.time >= _nextReconnectTime && _reconnectAttempts < maxReconnectAttempts)
        {
            _reconnectAttempts++;
            Debug.Log($"[KCP] Reconnecting ({_reconnectAttempts}/{maxReconnectAttempts})...");
            Connect();
        }
    }

    private void HandleConnectionLost()
    {
        if (isConnected)
        {
            Debug.LogWarning("[KCP] Connection lost");
            _wasConnected = true;
        }
        Disconnect();
        ScheduleReconnect();
    }

    private void ScheduleReconnect()
    {
        if (!autoReconnect || _reconnectAttempts >= maxReconnectAttempts) return;
        float delay = Mathf.Min(reconnectBaseDelay * Mathf.Pow(2, _reconnectAttempts), reconnectMaxDelay);
        _nextReconnectTime = Time.time + delay;
    }

    private void ProcessStream()
    {
        long length = streamBuffer.Length;
        while (length - streamBuffer.Position >= HEADER_LENGTH)
        {
            long packetStart = streamBuffer.Position;
            byte[] header = new byte[4];
            streamBuffer.Read(header, 0, 4);
            int type = header[0];
            int bodyLen = (header[1] << 16) | (header[2] << 8) | header[3];

            if (length - packetStart < HEADER_LENGTH + bodyLen)
            {
                streamBuffer.Position = packetStart;
                break;
            }

            byte[] body = new byte[bodyLen];
            streamBuffer.Read(body, 0, bodyLen);

            try { HandlePacket(type, body); }
            catch (Exception e) { Debug.LogError($"[KCP] Packet error: {e.Message}"); }
        }

        if (streamBuffer.Position >= streamBuffer.Length)
            streamBuffer.SetLength(0);
        else if (streamBuffer.Position > 4096)
        {
            byte[] rest = new byte[streamBuffer.Length - streamBuffer.Position];
            streamBuffer.Read(rest, 0, rest.Length);
            streamBuffer = new MemoryStream();
            streamBuffer.Write(rest, 0, rest.Length);
            streamBuffer.Position = 0;
        }
    }

    private void HandlePacket(int type, byte[] body)
    {
        switch (type)
        {
            case MSG_TYPE_HANDSHAKE:
                SendHandshakeAck();
                isConnected = true;
                _wasConnected = true;
                _reconnectAttempts = 0;
                Debug.Log("[KCP] Connected");
                OnConnected?.Invoke();
                break;
            case MSG_TYPE_HEARTBEAT:
                break;
            case MSG_TYPE_DATA:
                HandleDataPacket(body);
                break;
            case MSG_TYPE_KICK:
                Debug.LogWarning("[KCP] Kicked by server");
                autoReconnect = false;
                Disconnect();
                break;
        }
    }

    private void HandleDataPacket(byte[] body)
    {
        if (body.Length < 1) return;
        int msgType = (body[0] >> 1) & 0x07;

        if (msgType == 3) { HandlePushMessage(body); return; }
        if (msgType == 2) { HandleResponseMessage(body); return; }
    }

    private void HandlePushMessage(byte[] body)
    {
        int offset = 1;
        if (offset >= body.Length) return;
        int routeLen = body[offset++];
        if (offset + routeLen > body.Length) return;

        string route = Encoding.UTF8.GetString(body, offset, routeLen);
        offset += routeLen;

        byte[] msgBody = new byte[body.Length - offset];
        Array.Copy(body, offset, msgBody, 0, msgBody.Length);

        if (_routeHandlers.TryGetValue(route, out var handler))
        {
            try { handler?.Invoke(msgBody); }
            catch (Exception e) { Debug.LogError($"[KCP] Handler error ({route}): {e.Message}"); }
        }
    }

    private void HandleResponseMessage(byte[] body)
    {
        int offset = 1;
        int id = ReadVarInt(body, ref offset);
        if (offset >= body.Length) return;

        byte[] respBody = new byte[body.Length - offset];
        Array.Copy(body, offset, respBody, 0, respBody.Length);

        if (_responseCallbacks.TryGetValue(id, out var callback))
        {
            _responseCallbacks.Remove(id);
            try { callback?.Invoke(respBody); }
            catch (Exception e) { Debug.LogError($"[KCP] Response callback error (id={id}): {e.Message}"); }
        }
    }

    private int ReadVarInt(byte[] data, ref int offset)
    {
        int result = 0, shift = 0;
        while (offset < data.Length)
        {
            byte b = data[offset++];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        return result;
    }

    private void SendHandshake()
    {
        string json = "{\"sys\":{\"type\":\"unity\",\"version\":\"1.0.0\"},\"user\":{}}";
        SendRaw(MSG_TYPE_HANDSHAKE, Encoding.UTF8.GetBytes(json));
    }

    private void SendHandshakeAck() => SendRaw(MSG_TYPE_HANDSHAKE_ACK, new byte[0]);

    public void SendRequest(string route, IMessage message, Action<byte[]> callback = null)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x00);

        int currentReqId = reqId++;
        WriteVarInt(ms, currentReqId);

        byte[] routeBytes = Encoding.UTF8.GetBytes(route);
        ms.WriteByte((byte)routeBytes.Length);
        ms.Write(routeBytes, 0, routeBytes.Length);

        message.WriteTo(ms);

        if (callback != null)
            _responseCallbacks[currentReqId] = callback;

        SendRaw(MSG_TYPE_DATA, ms.ToArray());
    }

    public void SendNotify(string route, IMessage message)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x02);

        byte[] routeBytes = Encoding.UTF8.GetBytes(route);
        ms.WriteByte((byte)routeBytes.Length);
        ms.Write(routeBytes, 0, routeBytes.Length);

        message.WriteTo(ms);
        SendRaw(MSG_TYPE_DATA, ms.ToArray());
    }

    private void WriteVarInt(Stream stream, int value)
    {
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0) b |= 0x80;
            stream.WriteByte(b);
        } while (value != 0);
    }

    private void SendRaw(int type, byte[] body)
    {
        if (session == null) return;
        int len = body.Length;
        byte[] packet = new byte[4 + len];
        packet[0] = (byte)type;
        packet[1] = (byte)((len >> 16) & 0xFF);
        packet[2] = (byte)((len >> 8) & 0xFF);
        packet[3] = (byte)(len & 0xFF);
        Array.Copy(body, 0, packet, 4, len);
        session.Send(packet, 0, packet.Length);
    }

    void OnDestroy()
    {
        autoReconnect = false;
        Disconnect();
    }
}
