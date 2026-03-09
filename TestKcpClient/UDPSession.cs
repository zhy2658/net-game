using System;
using System.Net;
using System.Net.Sockets;

namespace KcpProject
{
    public class UDPSession
    {
        private Socket mSocket = null;
        public KCP mKCP = null;

        private ByteBuffer mRecvBuffer = ByteBuffer.Allocate(1024 * 32);
        private UInt32 mNextUpdateTime = 0;

        public bool IsConnected { get { return mSocket != null && mSocket.Connected; } }
        public bool WriteDelay { get; set; }
        public bool AckNoDelay { get; set; }

        public IPEndPoint RemoteAddress { get; private set; }
        public IPEndPoint LocalAddress { get; private set; }

        public void Connect(string host, int port)
        {
            IPAddress ipAddress;
            // Try Parse IP directly first
            if (!IPAddress.TryParse(host, out ipAddress))
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(host);
                if (hostEntry.AddressList.Length == 0)
                {
                    throw new Exception("Unable to resolve host: " + host);
                }
                // Prefer IPv4
                foreach (var addr in hostEntry.AddressList) {
                    if (addr.AddressFamily == AddressFamily.InterNetwork) {
                        ipAddress = addr;
                        break;
                    }
                }
                if (ipAddress == null) ipAddress = hostEntry.AddressList[0];
            }

            Console.WriteLine($"[UDPSession] Resolved IP: {ipAddress} Family: {ipAddress.AddressFamily}");

            mSocket = new Socket(ipAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            mSocket.Connect(new IPEndPoint(ipAddress, port));
            
            RemoteAddress = (IPEndPoint)mSocket.RemoteEndPoint;
            LocalAddress = (IPEndPoint)mSocket.LocalEndPoint;
            
            mKCP = new KCP((uint)(new Random().Next(1, Int32.MaxValue)), rawSend);
            Console.WriteLine($"[UDPSession] Created KCP with Conv: {mKCP.GetHashCode()} (Note: GetHashCode may not be conv, need property)");
            // normal:  0, 40, 2, 1
            // fast:    0, 30, 2, 1
            // fast2:   1, 20, 2, 1
            // fast3:   1, 10, 2, 1
            mKCP.NoDelay(0, 30, 2, 1);
            mKCP.SetStreamMode(true);
            
            mRecvBuffer.Clear();
        }

        public void Close()
        {
            if (mSocket != null) {
                mSocket.Close();
                mSocket = null;
                mKCP = null;
            }
        }

        private void rawSend(byte[] data, int size)
        {
            if (mSocket != null) {
                mSocket.Send(data, size, SocketFlags.None);
            }
        }

        public void Update()
        {
            if (mSocket == null) return;

            if (0 == mNextUpdateTime || mKCP.CurrentMS >= mNextUpdateTime)
            {
                mKCP.Update();
                mNextUpdateTime = mKCP.Check();
            }
        }

        public int Recv(byte[] buffer, int index, int length)
        {
            if (mSocket == null) return -1;

            // recv from socket
            if (mSocket.Available > 0) {
                int rn = mSocket.Receive(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, mRecvBuffer.WritableBytes, SocketFlags.None);
                
                if (rn <= 0) {
                    return rn;
                }
                mRecvBuffer.WriterIndex += rn;

                // Debug KCP Input
                Console.WriteLine($"[UDPSession] Socket Recv: {rn} bytes");
                // Print first 24 bytes (KCP Header)
                string hex = BitConverter.ToString(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, Math.Min(rn, 24));
                Console.WriteLine($"[UDPSession] Header: {hex}");

                var inputN = mKCP.Input(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, mRecvBuffer.ReadableBytes, true, AckNoDelay);
                if (inputN < 0) {
                    Console.WriteLine($"[UDPSession] KCP Input failed: {inputN}. Conv mismatch?");
                    mRecvBuffer.Clear();
                    return inputN;
                }

                mRecvBuffer.DiscardReadBytes();
            }

            // recv from kcp
            int size = mKCP.PeekSize();
            if (size < 0) return -1;

            return mKCP.Recv(buffer, index, length);
        }

        public void Send(byte[] buffer, int index, int length)
        {
            if (mSocket == null) return;
            mKCP.Send(buffer, index, length);
            if (!WriteDelay)
            {
                mKCP.Flush(false);
            }
        }
    }
}