using System;
using System.IO;
using System.Text;
using UnityEngine;
using KcpProject; 
using Google.Protobuf; 
using Protocol; 

public class NanoKcpClient : MonoBehaviour
{
    public string host = "127.0.0.1";
    public int port = 3250;

    private UDPSession session;
    private byte[] recvBuffer = new byte[8192]; 
    private MemoryStream streamBuffer = new MemoryStream(); 
    private bool isConnected = false;
    private int reqId = 1;
    private float _lastHeartbeatTime;

    public bool IsConnected => isConnected;

    const int HEADER_LENGTH = 4;
    const int MSG_TYPE_HANDSHAKE = 1;
    const int MSG_TYPE_HANDSHAKE_ACK = 2;
    const int MSG_TYPE_HEARTBEAT = 3;
    const int MSG_TYPE_DATA = 4;
    const int MSG_TYPE_KICK = 5;

    public Action<JoinResponse> OnJoinResponse;
    public Action<ChatMessage> OnChatMessage;
    public Action OnConnected;

    void Start()
    {
        Connect();
    }

    void OnGUI()
    {
        GUI.color = Color.white;
        string status = isConnected ? "Connected (KCP)" : "Disconnected";
        GUI.Label(new Rect(10, 10, 300, 20), $"Status: {status}");

        if (isConnected)
        {
            if (GUI.Button(new Rect(10, 40, 120, 30), "Join Room"))
            {
                SendRequest("room.join", new JoinRequest { Name = "UnityKcpUser" });
            }

            if (GUI.Button(new Rect(140, 40, 120, 30), "Send Chat"))
            {
                SendRequest("room.message", new ChatMessage { SenderId = "User1", Content = "Hello via KCP!" });
            }
        }
        else
        {
            if (GUI.Button(new Rect(10, 40, 100, 30), "Connect"))
            {
                Connect();
            }
        }
    }

    public void Connect()
    {
        if (session != null && isConnected) return;

        try
        {
            session = new UDPSession();
            // Pitaya KCP Settings
            session.Connect(host, port);
            session.mKCP.NoDelay(1, 10, 2, 1);
            session.mKCP.WndSize(128, 128);
            session.mKCP.SetStreamMode(true); // Enable Stream Mode!
            session.AckNoDelay = true; // Enable AckNoDelay

            Debug.Log($"Connecting to {host}:{port}...");
            SendHandshake();
        }
        catch (Exception e)
        {
            Debug.LogError($"Connect Error: {e.Message}");
        }
    }

    void Update()
    {
        if (session != null)
        {
            session.Update();
            // Heartbeat Logic (Only when connected)
            if (isConnected && Time.time - _lastHeartbeatTime > 5f)
            {
                _lastHeartbeatTime = Time.time;
                SendRaw(MSG_TYPE_HEARTBEAT, new byte[0]);
            }

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
            int bodyLen = ((header[1] << 16) | (header[2] << 8) | header[3]);

            if (length - packetStart < HEADER_LENGTH + bodyLen)
            {
                streamBuffer.Position = packetStart;
                break;
            }

            byte[] body = new byte[bodyLen];
            streamBuffer.Read(body, 0, bodyLen);

            try { HandlePacket(type, body); }
            catch (Exception e) { Debug.LogError($"Packet Error: {e}"); }
        }

        if (streamBuffer.Position >= streamBuffer.Length)
        {
            streamBuffer.SetLength(0);
        }
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
                Debug.Log("Handshake received.");
                SendHandshakeAck();
                isConnected = true;
                OnConnected?.Invoke();
                break;
            case MSG_TYPE_HEARTBEAT:
                // Auto reply heartbeat if needed, or just ignore
                break;
            case MSG_TYPE_DATA:
                HandleDataPacket(body);
                break;
        }
    }

    private void HandleDataPacket(byte[] body)
    {
        // Pitaya Data Packet: [Flag(1) | ReqID(VarInt) | Body] (For Response)
        // Or [Flag(1) | Route(Str) | Body] (For Push)
        // This is complex, for now we try to parse directly if it fails we might need to skip header
        // Assuming simple response for now:
        
        // Skip header heuristic
        int offset = 0;
        if (body.Length > 0)
        {
             // Try to skip Flag and ID
             // This is a simplification. Real implementation needs a proper Pitaya Message Decoder.
             // For this demo, we just try to parse the body directly or with offset.
        }

        try {
            var joinRes = JoinResponse.Parser.ParseFrom(body);
            if (!string.IsNullOrEmpty(joinRes.RoomId) || joinRes.Code != 0) {
                 Debug.Log($"Join Response: {joinRes}");
                 OnJoinResponse?.Invoke(joinRes);
                 return;
            }
        } catch {}

        try {
             var chatMsg = ChatMessage.Parser.ParseFrom(body);
             if (!string.IsNullOrEmpty(chatMsg.Content)) {
                 Debug.Log($"Chat Msg: {chatMsg.Content}");
                 OnChatMessage?.Invoke(chatMsg);
                 return;
             }
        } catch {}
    }

    private void SendHandshake()
    {
        string json = "{\"sys\":{\"type\":\"unity\",\"version\":\"1.0.0\"},\"user\":{}}";
        SendRaw(MSG_TYPE_HANDSHAKE, Encoding.UTF8.GetBytes(json));
    }

    private void SendHandshakeAck()
    {
        SendRaw(MSG_TYPE_HANDSHAKE_ACK, new byte[0]);
    }

    public void SendRequest(string route, IMessage message)
    {
        // Pitaya Message Format: [Flag(1) | ReqID(VarInt) | RouteLen(1) | Route(Str) | Body]
        using (var ms = new MemoryStream())
        {
            // 1. Flag (0x00 = Request, 0x02 = Notify)
            ms.WriteByte(0x00); // Request

            // 2. ReqID
            WriteVarInt(ms, reqId++);

            // 3. Route
            byte[] routeBytes = Encoding.UTF8.GetBytes(route);
            ms.WriteByte((byte)routeBytes.Length);
            ms.Write(routeBytes, 0, routeBytes.Length);

            // 4. Body
            message.WriteTo(ms);

            SendRaw(MSG_TYPE_DATA, ms.ToArray());
        }
    }

    public void SendNotify(string route, IMessage message)
    {
        // Pitaya Notify Format: [Flag(1) | RouteLen(1) | Route(Str) | Body]
        using (var ms = new MemoryStream())
        {
            // 1. Flag (0x02 = Notify)
            ms.WriteByte(0x02); 

            // 2. Route
            byte[] routeBytes = Encoding.UTF8.GetBytes(route);
            ms.WriteByte((byte)routeBytes.Length);
            ms.Write(routeBytes, 0, routeBytes.Length);

            // 3. Body
            message.WriteTo(ms);

            SendRaw(MSG_TYPE_DATA, ms.ToArray());
        }
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
        byte[] header = new byte[4];
        header[0] = (byte)type;
        header[1] = (byte)((len >> 16) & 0xFF);
        header[2] = (byte)((len >> 8) & 0xFF);
        header[3] = (byte)(len & 0xFF);
        byte[] packet = new byte[4 + len];
        Array.Copy(header, 0, packet, 0, 4);
        Array.Copy(body, 0, packet, 4, len);
        session.Send(packet, 0, packet.Length);
    }

    void OnDestroy()
    {
        if (session != null) session.Close();
    }
}
