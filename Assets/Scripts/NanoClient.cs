using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Game.Protocol; // Generated Protobuf Code
using ProtoBuf;      // protobuf-net

// Simple Nano Protocol Implementation for Unity
public class NanoClient : MonoBehaviour
{
    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    
    [SerializeField] private string serverUrl = "ws://localhost:3250/nano";
    public string connectionStatus = "Disconnected";
    
    // Latency
    public int latency = 0;
    private long _pingTimestamp;

    // Nano Protocol Constants
    const int PKG_HANDSHAKE = 1;
    const int PKG_HANDSHAKE_ACK = 2;
    const int PKG_HEARTBEAT = 3;
    const int PKG_DATA = 4;

    private int _reqId = 1;

    void Update()
    {
        // Force cursor visible for testing UI
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    async void Start()
    {
        connectionStatus = "Connecting...";
        await Connect();
    }
    
    void OnGUI()
    {
        GUI.color = Color.black;
        GUI.Label(new Rect(10, 10, 400, 20), $"Nano Status: {connectionStatus} | Ping: {latency}ms");
        
        if (GUI.Button(new Rect(10, 40, 100, 30), "Join"))
        {
            SendRequest("Room.Join", new JoinRequest { Name = "UnityPlayer" });
        }
        
        if (GUI.Button(new Rect(120, 40, 100, 30), "Say Hello"))
        {
            SendRequest("Room.Message", new ChatMessage { senderId = 1, Content = "Hello from Unity!" });
        }
    }

    public async Task Connect()
    {
        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        try
        {
            await _ws.ConnectAsync(new Uri(serverUrl), _cts.Token);
            connectionStatus = "Connected (Sending Handshake)";
            
            // 1. Send Handshake
            string handshake = "{\"sys\":{\"type\":\"js-websocket\",\"version\":\"0.0.1\",\"rsa\":{}},\"user\":{}}";
            SendPackage(PKG_HANDSHAKE, Encoding.UTF8.GetBytes(handshake));
            
            _ = ReceiveLoop();
            _ = HeartbeatLoop();
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection failed: {e.Message}");
            connectionStatus = $"Failed: {e.Message}";
        }
    }
    
    // --- Protocol Packing ---
    
    private void SendPackage(int type, byte[] body)
    {
        if (_ws.State != WebSocketState.Open) return;
        
        // Header: Type(1) + Length(3)
        int len = body.Length;
        byte[] header = new byte[4];
        header[0] = (byte)(type & 0xff);
        header[1] = (byte)((len >> 16) & 0xff);
        header[2] = (byte)((len >> 8) & 0xff);
        header[3] = (byte)(len & 0xff);
        
        byte[] packet = new byte[header.Length + len];
        Array.Copy(header, 0, packet, 0, header.Length);
        Array.Copy(body, 0, packet, header.Length, len);
        
        _ws.SendAsync(new ArraySegment<byte>(packet), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    private void SendRequest<T>(string route, T msg)
    {
        // Message Protocol: Flag(1) + ReqID(Varint) + Route(Len+Str) + Body(Protobuf)
        // Flag: 0 (Request) | (RouteCompressed ? 1 : 0) -> For simplicity we use uncompressed route (0)
        // Actually, Nano default message type for request is 0x00 (Request) or 0x02 (Notify)
        // Let's use Request (0x00) which expects a response
        
        using (var ms = new MemoryStream())
        {
            // 1. Flag (0x00 = Request)
            ms.WriteByte(0x00);
            
            // 2. ReqID (Varint)
            WriteVarInt(ms, _reqId++);
            
            // 3. Route (Length + String)
            byte[] routeBytes = Encoding.UTF8.GetBytes(route);
            ms.WriteByte((byte)routeBytes.Length);
            ms.Write(routeBytes, 0, routeBytes.Length);
            
            // 4. Body (Protobuf)
            Serializer.Serialize(ms, msg);
            
            SendPackage(PKG_DATA, ms.ToArray());
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

    private async Task HeartbeatLoop()
    {
        while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
        {
            await Task.Delay(2000); // 2s interval for better latency check
            _pingTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            SendPackage(PKG_HEARTBEAT, new byte[0]);
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096];
        while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
        {
            try
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                
                // Parse Package
                // In production, we need to handle fragmentation (TCP stream). 
                // WebSocket frames usually contain full messages but not guaranteed.
                // Assuming simple frame = packet for this demo.
                
                int type = buffer[0];
                // Length is at buffer[1..3]
                
                if (type == PKG_HANDSHAKE)
                {
                     Debug.Log("Handshake Response Received. Sending Ack.");
                     SendPackage(PKG_HANDSHAKE_ACK, new byte[0]);
                     connectionStatus = "Connected (Ready)";
                }
                else if (type == PKG_HEARTBEAT)
                {
                    // Calculate Latency
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (_pingTimestamp > 0)
                    {
                        latency = (int)(now - _pingTimestamp);
                    }
                }
                else if (type == PKG_DATA)
                {
                    // Parse Message
                    // Head: Flag(1) + ReqID(Varint)? + Route?
                    // This part is complex because response format differs from push format.
                    // Response: Flag | ReqID | Body
                    // Push: Flag | Route | Body
                    
                    Debug.Log($"Received Data Packet (Type {type})");
                    // We need a proper parser here to extract Protobuf body.
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Receive error: {e.Message}");
                break;
            }
        }
    }
    
    private async void OnDestroy()
    {
        _cts?.Cancel();
        if (_ws != null && _ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
    }
}
