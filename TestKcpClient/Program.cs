using System;
using System.IO;
using System.Text;
using System.Threading;
using KcpProject;
using Google.Protobuf;
using Protocol;

namespace TestKcpClient
{
    class Program
    {
        static UDPSession session;
        static bool isConnected = false;
        static int reqId = 1;
        static byte[] recvBuffer = new byte[8192];
        static MemoryStream streamBuffer = new MemoryStream();
        static float lastHeartbeatTime = 0;
        static float lastSyncTime = 0;

        const int HEADER_LENGTH = 4;
        const int MSG_TYPE_HANDSHAKE = 1;
        const int MSG_TYPE_HANDSHAKE_ACK = 2;
        const int MSG_TYPE_HEARTBEAT = 3;
        const int MSG_TYPE_DATA = 4;
        const int MSG_TYPE_KICK = 5;

        static void Main(string[] args)
        {
            Console.WriteLine("Starting TestKcpClient...");

            Connect();

            while (true)
            {
                Update();
                Thread.Sleep(10); // Sleep 10ms to simulate ~100fps
            }
        }

        static void Connect()
        {
            try
            {
                session = new UDPSession();
                session.Connect("127.0.0.1", 3250);
                
                // Match NanoKcpClient settings
                session.mKCP.NoDelay(1, 10, 2, 1);
                session.mKCP.WndSize(128, 128);
                session.mKCP.SetStreamMode(true);
                session.AckNoDelay = true;

                Console.WriteLine("Connecting to 127.0.0.1:3250...");
                SendHandshake();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Connect Error: {e.Message}");
            }
        }

        static void Update()
        {
            if (session != null)
            {
                // Update KCP
                session.Update();

                // Recv
                while (true)
                {
                    // UDPSession.Recv already handles socket receive
                    // We just need to pull data from KCP
                    // But wait, UDPSession.Recv implementation:
                    // It receives from socket into mRecvBuffer
                    // Then inputs to KCP
                    // Then calls KCP.Recv to get data into 'buffer'
                    // So we need to call session.Recv repeatedly until it returns <= 0
                    
                    // However, UDPSession.Recv implementation in my file:
                    // It does socket receive ONCE per call if available > 0
                    // So we should call it.
                    
                    int n = session.Recv(recvBuffer, 0, recvBuffer.Length);
                    if (n > 0)
                    {
                        // Console.WriteLine($"Received {n} bytes from KCP layer");
                        long oldPos = streamBuffer.Position;
                        streamBuffer.Seek(0, SeekOrigin.End);
                        streamBuffer.Write(recvBuffer, 0, n);
                        streamBuffer.Position = oldPos;
                    }
                    else if (n < 0 && n != -1)
                    {
                         // -1 means no data, others are errors
                         // -2 peeksize > length
                    }
                    
                    if (n <= 0) break;
                }

                ProcessStream();

                // Heartbeat
                float time = (float)(DateTime.Now.Ticks / 10000000.0);
                if (isConnected && time - lastHeartbeatTime > 5f)
                {
                    lastHeartbeatTime = time;
                    SendRaw(MSG_TYPE_HEARTBEAT, new byte[0]);
                    Console.WriteLine("Sent Heartbeat");
                }

                // Simulate Move (if connected and joined)
                if (isConnected && time - lastSyncTime > 1f) // Every 1s
                {
                    lastSyncTime = time;
                    // We need to join room first. 
                    // For simplicity, let's just try to join room if we haven't joined.
                    // But here we just assume we joined after handshake for test?
                    // No, we need to send join request.
                }
            }
        }

        static void ProcessStream()
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
                catch (Exception e) { Console.WriteLine($"Packet Error: {e}"); }
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

        static void HandlePacket(int type, byte[] body)
        {
            switch (type)
            {
                case MSG_TYPE_HANDSHAKE:
                    Console.WriteLine("Handshake received.");
                    SendHandshakeAck();
                    isConnected = true;
                    
                    // Auto Join Room after Handshake
                    Console.WriteLine("Sending Join Request...");
                    SendRequest("room.join", new JoinRequest { Name = "ConsoleClient" });
                    break;

                case MSG_TYPE_DATA:
                    HandleDataPacket(body);
                    break;
                    
                case MSG_TYPE_HEARTBEAT:
                    // Console.WriteLine("Heartbeat received");
                    break;
            }
        }

        static void HandleDataPacket(byte[] body)
        {
            // Try parse Join Response
            try {
                var joinRes = JoinResponse.Parser.ParseFrom(body);
                if (!string.IsNullOrEmpty(joinRes.RoomId) || joinRes.Code != 0) {
                     Console.WriteLine($"Join Response: Code={joinRes.Code} RoomID={joinRes.RoomId} Msg={joinRes.Message}");
                     if (joinRes.Code == 200)
                     {
                         // Start Sending Move
                         Console.WriteLine("Joined Room! Starting Move Loop...");
                         new Thread(MoveLoop).Start();
                     }
                     return;
                }
            } catch {}
            
            // Try parse Move Push (from other players)
            try {
                var movePush = PlayerMovePush.Parser.ParseFrom(body);
                if (!string.IsNullOrEmpty(movePush.Id)) {
                    Console.WriteLine($"Player Move Push: {movePush.Id} -> ({movePush.Position.X}, {movePush.Position.Y}, {movePush.Position.Z})");
                    return;
                }
            } catch {}
        }

        static void MoveLoop()
        {
            float x = 0;
            while (true)
            {
                x += 0.5f;
                var moveMsg = new MoveRequest
                {
                    Position = new Protocol.Vector3 { X = x, Y = 0, Z = 0 },
                    Rotation = new Protocol.Quaternion { X = 0, Y = 0, Z = 0, W = 1 }
                };
                
                SendNotify("room.move", moveMsg);
                Console.WriteLine($"Sent Move: {x}");
                
                Thread.Sleep(500);
            }
        }

        static void SendHandshake()
        {
            string json = "{\"sys\":{\"type\":\"unity\",\"version\":\"1.0.0\"},\"user\":{}}";
            SendRaw(MSG_TYPE_HANDSHAKE, Encoding.UTF8.GetBytes(json));
        }

        static void SendHandshakeAck()
        {
            SendRaw(MSG_TYPE_HANDSHAKE_ACK, new byte[0]);
        }

        static void SendRequest(string route, IMessage message)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x00); // Request
                WriteVarInt(ms, reqId++);
                byte[] routeBytes = Encoding.UTF8.GetBytes(route);
                ms.WriteByte((byte)routeBytes.Length);
                ms.Write(routeBytes, 0, routeBytes.Length);
                message.WriteTo(ms);
                SendRaw(MSG_TYPE_DATA, ms.ToArray());
            }
        }
        
        static void SendNotify(string route, IMessage message)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x02); // Notify
                byte[] routeBytes = Encoding.UTF8.GetBytes(route);
                ms.WriteByte((byte)routeBytes.Length);
                ms.Write(routeBytes, 0, routeBytes.Length);
                message.WriteTo(ms);
                SendRaw(MSG_TYPE_DATA, ms.ToArray());
            }
        }

        static void WriteVarInt(Stream stream, int value)
        {
            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0) b |= 0x80;
                stream.WriteByte(b);
            } while (value != 0);
        }

        static void SendRaw(int type, byte[] body)
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
            
            // Note: UDPSession.Send expects raw KCP packet or user data?
            // Wait, UDPSession.Send calls KCP.Send.
            // So we are sending KCP payload.
            // The Packet Structure (Header + Body) is the Payload for KCP.
            
            session.Send(packet, 0, packet.Length);
        }
    }
}
