using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using kcp2k;
using Protocol;
using Google.Protobuf;
using System.IO;

public class KcpGoClient : MonoBehaviour
{
    // Configuration
    public string host = "127.0.0.1";
    public ushort port = 3250;
    
    // KCP Config matches Go server: NoDelay(1, 10, 2, 1), Wnd(128, 128), AckNoDelay
    public bool noDelay = true;
    public uint interval = 10;
    public int fastResend = 2;
    public bool noCongestionWindow = true; // nc=1
    public uint sendWindow = 128;
    public uint receiveWindow = 128;

    // State
    private Socket socket;
    private EndPoint remoteEndPoint;
    private Kcp kcp;
    private bool connected;
    private byte[] rawReceiveBuffer = new byte[4096];
    private byte[] kcpReceiveBuffer = new byte[4096];
    private MemoryStream streamBuffer = new MemoryStream();

    // Pitaya/Pomelo State
    private uint reqId = 1;
    private bool handshakeDone = false;
    private float lastMoveTime;
    
    // Packet Constants
    const int HEADER_LENGTH = 4;
    const int MSG_TYPE_HANDSHAKE = 1;
    const int MSG_TYPE_HANDSHAKE_ACK = 2;
    const int MSG_TYPE_HEARTBEAT = 3;
    const int MSG_TYPE_DATA = 4;
    const int MSG_TYPE_KICK = 5;

    void Start()
    {
        Connect();
    }

    public void Connect()
    {
        try
        {
            // 1. Resolve IP
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            if (addresses.Length == 0)
            {
                Debug.LogError("Host not found");
                return;
            }
            remoteEndPoint = new IPEndPoint(addresses[0], port);

            // 2. Create Socket
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Blocking = false;
            socket.Connect(remoteEndPoint);

            // 3. Initialize KCP
            // Generate a random conversation ID (conv) like kcp-go does
            uint conv = (uint)UnityEngine.Random.Range(1, int.MaxValue);
            
            kcp = new Kcp(conv, (data, size) => {
                if (socket != null && connected)
                {
                    try
                    {
                        // Send data to server
                        // We need to copy because 'data' buffer is reused by KCP
                        byte[] buffer = new byte[size];
                        Buffer.BlockCopy(data, 0, buffer, 0, size);
                        socket.Send(buffer);
                    }
                    catch (SocketException ex)
                    {
                        Debug.LogWarning($"Socket Send Error: {ex.Message}");
                    }
                }
            });

            // Apply Config
            kcp.SetNoDelay(noDelay ? 1u : 0u, interval, fastResend, noCongestionWindow);
            kcp.SetWindowSize(sendWindow, receiveWindow);
            kcp.SetMtu(1400); // Standard MTU, kcp-go default is usually 1400

            connected = true;
            Debug.Log($"KCP Client Started. Conv={conv}");

            // 4. Send Handshake (Application Layer)
            // We don't need a KCP-level handshake because we are using raw KCP.
            // The first packet we send will establish the session on the server.
            SendHandshake();
        }
        catch (Exception e)
        {
            Debug.LogError($"Connect failed: {e.Message}");
        }
    }

    void Update()
    {
        if (!connected || kcp == null) return;

        // 1. Receive from Socket -> Input to KCP
        try
        {
            while (socket.Available > 0)
            {
                int received = socket.Receive(rawReceiveBuffer);
                if (received > 0)
                {
                    // Feed to KCP
                    kcp.Input(rawReceiveBuffer, 0, received);
                }
            }
        }
        catch (SocketException) { /* Ignore would block */ }

        // 2. Update KCP Logic
        uint current = (uint)(Time.time * 1000);
        kcp.Update(current);

        // 3. Read from KCP -> Application Logic
        int size;
        while ((size = kcp.PeekSize()) > 0)
        {
            if (size > kcpReceiveBuffer.Length)
                kcpReceiveBuffer = new byte[size];

            int read = kcp.Receive(kcpReceiveBuffer, size);
            if (read > 0)
            {
                // Write to stream buffer
                long oldPos = streamBuffer.Position;
                streamBuffer.Seek(0, SeekOrigin.End);
                streamBuffer.Write(kcpReceiveBuffer, 0, read);
                streamBuffer.Position = oldPos;
            }
        }
        
        // Process Stream Buffer
        ProcessStream();

        // 4. Game Logic Loop
        if (handshakeDone)
        {
            if (Time.time - lastMoveTime > 1.0f)
            {
                lastMoveTime = Time.time;
                SendMove();
            }
        }
    }

    void ProcessStream()
    {
        long length = streamBuffer.Length;
        while (length - streamBuffer.Position >= HEADER_LENGTH)
        {
            long packetStart = streamBuffer.Position;
            byte[] header = new byte[HEADER_LENGTH];
            streamBuffer.Read(header, 0, HEADER_LENGTH);
            
            int type = header[0];
            int bodyLen = ((header[1] << 16) | (header[2] << 8) | header[3]);

            if (length - packetStart < HEADER_LENGTH + bodyLen)
            {
                streamBuffer.Position = packetStart;
                break;
            }

            byte[] body = new byte[bodyLen];
            streamBuffer.Read(body, 0, bodyLen);

            try {
                HandlePacket(type, body);
            } catch (Exception e) {
                Debug.LogError($"Packet Handle Error: {e}");
            }
        }

        // Trim Buffer
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

    void HandlePacket(int type, byte[] body)
    {
        switch (type)
        {
            case MSG_TYPE_HANDSHAKE:
                Debug.Log("Handshake Response Received");
                SendHandshakeAck();
                handshakeDone = true;
                
                // Start Game Sequence
                CreateRoom();
                break;
            case MSG_TYPE_DATA:
                HandleDataMessage(body);
                break;
            case MSG_TYPE_HEARTBEAT:
                // Auto reply?
                break;
        }
    }

    void ProcessPacket(byte[] buffer, int size)
    {
        // Deprecated by ProcessStream
    }

    void HandleDataMessage(byte[] data)
    {
        // Pitaya Message: [Flag(1)]...
        if (data.Length == 0) return;
        
        byte flag = data[0];
        int msgType = (flag >> 1) & 0x07; // Request(0), Notify(1), Response(2), Push(3)

        int offset = 1;
        uint id = 0;

        if (msgType == 0 || msgType == 2) // Request or Response has ID
        {
            // Parse VarInt ID
            int shift = 0;
            while (offset < data.Length)
            {
                byte b = data[offset++];
                id |= (uint)((b & 0x7F) << shift);
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
        }

        // Route? (Only for Push/Notify/Request, not Response usually)
        // Implementation simplified for Response/Push
        
        byte[] payload = new byte[data.Length - offset];
        Buffer.BlockCopy(data, offset, payload, 0, payload.Length);

        if (msgType == 2) // Response
        {
            Debug.Log($"Received Response ID={id}");
            // Decode based on what we expect... simplified logic
            try
            {
                var listResp = ListRoomsResponse.Parser.ParseFrom(payload);
                if (listResp.Rooms.Count > 0)
                {
                    Debug.Log($"ListRooms: Found {listResp.Rooms.Count} rooms");
                    foreach (var r in listResp.Rooms) Debug.Log($" - {r.Name}");
                    
                    // Join the first room
                    JoinRoom(listResp.Rooms[0].Id);
                    return;
                }
            }
            catch {}

            try
            {
                var createResp = CreateRoomResponse.Parser.ParseFrom(payload);
                if (!string.IsNullOrEmpty(createResp.Id))
                {
                    Debug.Log($"Created Room: {createResp.Name} ({createResp.Id})");
                    // List rooms after creation
                    ListRooms();
                    return;
                }
            }
            catch {}
            
            try
            {
                var joinResp = JoinResponse.Parser.ParseFrom(payload);
                if (joinResp.Code != 0 || !string.IsNullOrEmpty(joinResp.RoomId))
                {
                    Debug.Log($"Joined Room: {joinResp.RoomId}");
                    return;
                }
            }
            catch {}
        }
        else if (msgType == 3) // Push
        {
            // Push messages have Route. We need to parse it if we want to be correct.
            // [Flag][RouteLen][Route][Body] (Compressed route not supported here yet)
            // But wait, if Flag indicates Route Compression...
            // Let's assume non-compressed route for now.
            // Re-parsing header correctly:
            
            // Reset offset to 1
            offset = 1; 
            // Push has NO ID.
            
            // Route
            int routeLen = data[offset++];
            string route = Encoding.UTF8.GetString(data, offset, routeLen);
            offset += routeLen;
            
            payload = new byte[data.Length - offset];
            Buffer.BlockCopy(data, offset, payload, 0, payload.Length);
            
            Debug.Log($"Received Push: {route}");
            
            if (route == "OnPlayerMove")
            {
                var move = PlayerMovePush.Parser.ParseFrom(payload);
                Debug.Log($"Player {move.Id} moved to {move.Position.X}, {move.Position.Z}");
            }
            else if (route == "OnPlayerJoin")
            {
                var join = PlayerJoinPush.Parser.ParseFrom(payload);
                Debug.Log($"Player Joined: {join.Name}");
            }
        }
    }

    // --- Senders ---

    void SendHandshake()
    {
        string json = "{\"sys\":{\"type\":\"unity\",\"version\":\"1.0.0\"},\"user\":{}}";
        byte[] body = Encoding.UTF8.GetBytes(json);
        SendPacket(MSG_TYPE_HANDSHAKE, body);
    }

    void SendHandshakeAck()
    {
        SendPacket(MSG_TYPE_HANDSHAKE_ACK, new byte[0]);
    }

    void CreateRoom()
    {
        var req = new CreateRoomRequest { Name = "UnityRoom", MaxPlayers = 10 };
        SendRequest("room.create", req);
    }

    void ListRooms()
    {
        SendRequest("room.list", new ListRoomsRequest());
    }

    void JoinRoom(string roomId)
    {
        SendRequest("room.join", new JoinRequest { RoomId = roomId, Name = "UnityUser" });
    }

    void SendMove()
    {
        var req = new MoveRequest {
            Position = new Protocol.Vector3 { X = UnityEngine.Random.Range(0, 10), Y = 0, Z = UnityEngine.Random.Range(0, 10) },
            Rotation = new Protocol.Quaternion { W = 1 }
        };
        SendNotify("room.move", req);
    }

    // --- Low Level Sending ---

    void SendRequest(string route, IMessage msg)
    {
        // [Flag(0=Req)][ReqID][RouteLen][Route][Body]
        using (var ms = new MemoryStream())
        {
            ms.WriteByte(0x00); // Request
            WriteVarInt(ms, reqId++);
            
            byte[] routeBytes = Encoding.UTF8.GetBytes(route);
            ms.WriteByte((byte)routeBytes.Length);
            ms.Write(routeBytes, 0, routeBytes.Length);
            
            msg.WriteTo(ms);
            SendPacket(MSG_TYPE_DATA, ms.ToArray());
        }
    }

    void SendNotify(string route, IMessage msg)
    {
        // [Flag(2=Notify)][RouteLen][Route][Body]
        using (var ms = new MemoryStream())
        {
            ms.WriteByte(0x02); // Notify
            
            byte[] routeBytes = Encoding.UTF8.GetBytes(route);
            ms.WriteByte((byte)routeBytes.Length);
            ms.Write(routeBytes, 0, routeBytes.Length);
            
            msg.WriteTo(ms);
            SendPacket(MSG_TYPE_DATA, ms.ToArray());
        }
    }

    void SendPacket(int type, byte[] body)
    {
        // [Type][Len][Body]
        int len = body.Length;
        byte[] header = new byte[4];
        header[0] = (byte)type;
        header[1] = (byte)((len >> 16) & 0xFF);
        header[2] = (byte)((len >> 8) & 0xFF);
        header[3] = (byte)(len & 0xFF);

        // Send to KCP
        kcp.Send(header, 0, 4);
        if (len > 0) kcp.Send(body, 0, len);
    }

    void WriteVarInt(Stream stream, uint value)
    {
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0) b |= 0x80;
            stream.WriteByte(b);
        } while (value != 0);
    }

    void OnDestroy()
    {
        if (socket != null)
        {
            socket.Close();
            socket = null;
        }
        connected = false;
    }
}
