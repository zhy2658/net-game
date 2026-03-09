using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

        private DateTime startDt = DateTime.Now;
        const int logmask = KCP.IKCP_LOG_IN_ACK | KCP.IKCP_LOG_OUT_ACK | KCP.IKCP_LOG_IN_DATA | KCP.IKCP_LOG_OUT_DATA;

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

            UnityEngine.Debug.Log($"[UDPSession] Resolved IP: {ipAddress} Family: {ipAddress.AddressFamily}");

            mSocket = new Socket(ipAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            mSocket.Connect(new IPEndPoint(ipAddress, port));
            
            RemoteAddress = (IPEndPoint)mSocket.RemoteEndPoint;
            LocalAddress = (IPEndPoint)mSocket.LocalEndPoint;
            
            mKCP = new KCP((uint)(new Random().Next(1, Int32.MaxValue)), rawSend);
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
                mRecvBuffer.Clear();
            }
        }

        private void rawSend(byte[] data, int length)
        {
            if (mSocket != null) {
                mSocket.Send(data, length, SocketFlags.None);
            }
        }

        public int Send(byte[] data, int index, int length)
        {
            if (mSocket == null)
                return -1;

            var waitsnd = mKCP.WaitSnd;
            if (waitsnd < mKCP.SndWnd && waitsnd < mKCP.RmtWnd) {

                var sendBytes = 0;
                do {
                    var n = Math.Min((int)mKCP.Mss, length - sendBytes);
                    mKCP.Send(data, index + sendBytes, n);
                    sendBytes += n;
                } while (sendBytes < length);

                waitsnd = mKCP.WaitSnd;
                if (waitsnd >= mKCP.SndWnd || waitsnd >= mKCP.RmtWnd || !WriteDelay) {
                    mKCP.Flush(false);
                }

                return length;
            }

            return 0;
        }

        public int Recv(byte[] data, int index, int length)
        {
            // 上次剩下的部分
            if (mRecvBuffer.ReadableBytes > 0) {
                var recvBytes = Math.Min(mRecvBuffer.ReadableBytes, length);
                Buffer.BlockCopy(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, data, index, recvBytes);
                mRecvBuffer.ReaderIndex += recvBytes;
                // 读完重置读写指针
                if (mRecvBuffer.ReaderIndex == mRecvBuffer.WriterIndex) {
                    mRecvBuffer.Clear();
                }
                return recvBytes;
            }

            if (mSocket == null)
                return -1;

            if (!mSocket.Poll(0, SelectMode.SelectRead)) {
                return 0;
            }

            var rn = 0;
            try {
                rn = mSocket.Receive(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, mRecvBuffer.WritableBytes, SocketFlags.None);
            } catch(Exception ex) {
                Console.WriteLine(ex);
                rn = -1;
            }

            if (rn <= 0) {
                return rn;
            }
            mRecvBuffer.WriterIndex += rn;

            // Debug KCP Input
            // UnityEngine.Debug.Log($"[UDPSession] Socket Recv: {rn} bytes");

            var inputN = mKCP.Input(mRecvBuffer.RawBuffer, mRecvBuffer.ReaderIndex, mRecvBuffer.ReadableBytes, true, AckNoDelay);
            if (inputN < 0) {
                Console.WriteLine($"[UDPSession] KCP Input failed: {inputN}. Conv mismatch?");
                mRecvBuffer.Clear();
                return inputN;
            }
            mRecvBuffer.Clear();

            // 读完所有完整的消息
            for (;;) {
                var size = mKCP.PeekSize();
                if (size < 0) break;

                mRecvBuffer.EnsureWritableBytes(size);

                var n = mKCP.Recv(mRecvBuffer.RawBuffer, mRecvBuffer.WriterIndex, size);
                if (n > 0) mRecvBuffer.WriterIndex += n;
            }

            // 有数据待接收
            if (mRecvBuffer.ReadableBytes > 0) {
                return Recv(data, index, length);
            }

            return 0;
        }

        public void Update()
        {
            if (mSocket == null)
                return;

            if (0 == mNextUpdateTime || mKCP.CurrentMS >= mNextUpdateTime)
            {
                mKCP.Update();
                mNextUpdateTime = mKCP.Check();
            }
        }

        public void Log(string str)
        {
            DateTime now = DateTime.Now;
            int t = (int)(now - startDt).TotalMilliseconds;
            Console.WriteLine($"[{t.ToString().PadLeft(10, ' ')}] {str}");
        }
    }
}
