﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Collections.Concurrent;
using static ConstModule.ConstDefine;

namespace ProjectWaterMelon.Network
{
    // Session(UserToken)개별 소유
    public sealed class CTcpSocket : CSocketBase
    {
        private bool mIsDisposed = false;
        // 메시지 Recv 처리 
        private CMessageResolver mMessageReceiver = new CMessageResolver();
        
        // 메시지 Send 처리
        private int mAlreadySendBytes;
        private int mHaveToSendBytes;

        // tcp 소켓의 경우 소켓 송, 수신 버퍼 사용 (클라 -> 송신 버퍼 -> 전송 -> 수신 버퍼 -> 서버)
        private ConcurrentQueue<CPacket> mSendPacketQ = new ConcurrentQueue<CPacket>();
        private ConcurrentQueue<CPacket> mRecvPacketQ = new ConcurrentQueue<CPacket>();

        public System.Net.IPEndPoint mRemoteEP { get; private set;}

        public CTcpSocket()
        {
        }

        // Accept 이후 비동기 소켓 통신을 위해 사용되는 소켓 세팅 
        public void SetSocket(in Socket socket)
        {
            base.mRawSocket = socket;
            SetSocketOpt();
        }

        public void SetSocketOpt()
        {
            // nagle 알고리즘은 리얼타임 어플리케이션에서는 성능이 안좋음(반응속도가 느려지기 때문에)
            // nagle 알고리즘을 사용하지 않는다. (네트워크 트래픽 부하 감소 보단 성능선택)
            base.mRawSocket.NoDelay = true;
            base.mRawSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            base.mRawSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
        }

        public void SetRemoteIPEndPoint(in System.Net.IPEndPoint IPEndPoint)
        {
            mRemoteEP = IPEndPoint;
        }
  
        protected override sealed void Dispose(bool isDisposed) 
        {
            if (mIsDisposed)
                return;

            if (isDisposed)
            {
                // dispose managed resource
                mMessageReceiver = null;
            }
            // dispose unmanaged resource

            mIsDisposed = true;

            Dispose(isDisposed);
        }

        public void PushPacketToSendQ(CPacket packet)
        {
            mSendPacketQ.Enqueue(packet);
        }

        public bool TryPopPacketForSendQ(out CPacket packet)
        {
            return mSendPacketQ.TryDequeue(out packet);
        }

        public bool TryPeekPacketForSendQ(out CPacket packet)
        {
            return mSendPacketQ.TryPeek(out packet);
        }

        public void AsyncSend(CPacket packet)
        {
            if (mRawSocket == null)
            {
                CLog4Net.LogError($"Exception in CTcpSocket.AsyncSend - Socket NULL Error!!!");
                return;
            }

            if (IsAbleToSend() == false)
            {
                CLog4Net.LogError($"Exception in CTcpSocket.AsyncSend - Socket state isn't to send packet!!!");
                return;
            }

            mSendPacketQ.Enqueue(packet);
            StartSend();
        }

        public void StartSend()
        {
            CPacket packet = new CPacket();
            if (TryPeekPacketForSendQ(out packet))
            {
                if (mAlreadySendBytes == 0)
                {
                    // 해당 패킷을 처음 보내는 경우
                    mHaveToSendBytes = packet.m_size;
                    // 송신 버퍼 세팅
                    mSendArgs.SetBuffer(new byte[MAX_BUFFER_SIZE], mAlreadySendBytes, mHaveToSendBytes);
                    // 패킷 버퍼에 담긴 내용을 비동기 소켓 전달 객체인 SocketAsyncEventArgs 버퍼(송신버퍼)에 담는다
                    Array.Copy(packet.m_buffer, mAlreadySendBytes, mSendArgs.Buffer, mSendArgs.Offset, packet.m_size);
                }
                else
                {
                    // 해당 패킷을 한번에 보내지 못한 경우
                    mHaveToSendBytes = packet.m_size - mAlreadySendBytes;
                    // 송신 버퍼 세팅
                    mSendArgs.SetBuffer(mAlreadySendBytes, mHaveToSendBytes);
                    // 패킷 버퍼에 담긴 내용을 비동기 소켓 전달 객체인 SocketAsyncEventArgs 버퍼(송신버퍼)에 담는다
                    Array.Copy(packet.m_buffer, mAlreadySendBytes + 1, mSendArgs.Buffer, mSendArgs.Offset, mHaveToSendBytes);
                }

                bool lPending = mRawSocket.SendAsync(mSendArgs);
                if (!lPending)
                {
                    OnSendHandler(this, mSendArgs);
                }
            }
        }

        // Send Handler after operating async send
        public void OnSendHandler(object sender, SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                CSession lUserToken = e.UserToken as CSession;
                if (lUserToken != null)
                {
                    if (e.BytesTransferred == lUserToken.mTcpSocket.mHaveToSendBytes)
                    {
                        CPacket packet = new CPacket();
                        if (!TryPopPacketForSendQ(out packet))
                            CLog4Net.LogError($"Exception in CTcpSocket.OnSendHandler - Packet send queue pop error!!!");        
                    }
                    else
                    {
                        // 패킷을 정상적으로 모두 보내지 못했을 때
                        lUserToken.mTcpSocket.mAlreadySendBytes = e.BytesTransferred;
                        StartSend();
                    }
                }
            }
            else
            {
                //패킷 메시지 정상 전송 실패, 후처리 작업 진행 
                CLog4Net.LogError($"Exception in CTcpSocket.OnSendHandler - Packet send error!!!(BytesTransferred = {e.BytesTransferred}, SocketError = {e.SocketError}, SendQueueSize = {mSendPacketQ.Count}");             
                StartSend();
            }
        }


        /*
        public void AsyncSend()
        {
            var lSentMsgCount = 0;

            if (mSocket == null)
            {
                CLog4Net.LogError($"Exception in CTcpSocket.AsyncSend - Socket NULL Error!!!");
                return;
            }

            if (IsAbleToSend() == false)
            {
                CLog4Net.LogError($"Exception in CTcpSocket.AsyncSend - Socket state isn't to send packet!!!");
                return;
            }

            while (lSentMsgCount < mSendPacketQ.Count)
            {
                CPacket packet = new CPacket();
                if (!TryPeekPacketForSendQ(ref packet))
                    break;

                mSendArgs.SetBuffer(mSendArgs.Offset, packet.m_size);

                Array.Copy(packet.m_buffer, 0, mSendArgs.Buffer, mSendArgs.Offset, packet.m_size);

                mSentPacketQ.Enqueue(packet);
                bool lPending = mSocket.SendAsync(mSendArgs);
                if (!lPending)
                {
                    OnSendHandler(this, mSendArgs);
                }
                ++lSentMsgCount;
            }         
        }
        */

        public void AsyncRecv()
        {

        }

        // Recv Handler after operating async recv
        public void OnReceiveHandler(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                CLog4Net.LogDebugSysLog($"4.CTcpSocket.OnReceiveHandler", $"OnRecive Call Success - {e.BytesTransferred}, {e.Offset}");
                if (e.BytesTransferred > 0)
                {
                    mMessageReceiver.OnReceive(e.Buffer, e.Offset, e.BytesTransferred, (Packet) => { mMessageReceiver.ProcessPacket(Packet); });
                    var lPending = mRawSocket.ReceiveAsync(e);
                    if (!lPending)
                    {
                        OnReceiveHandler(this, mRecvArgs);
                    }
                }
                else
                {
                    // 서버에서 받은 패킷 데이터가 없는 경우(수신된 패킷 데이터 사이즈 = 0)
                    CLog4Net.LogError($"Exception in CTcpSocket.OnReceiveHandler - Packet receive bytesTransferred error!!!(BytesTransferred = {e.BytesTransferred})");
                    return;
                }
            }
            else
            {
                // 소켓 통신 에러 - 리시브 에러 
                CLog4Net.LogError($"Exception in CTcpSocket.OnReceiveHandler - Data receive error!!!(BytesTransferred = {e.BytesTransferred}, SocketError = {e.SocketError})");
                Disconnect();
                return;
            }
        }

        // TODO
        // 나중에 필요없으면 삭제 
        public override void Disconnect()
        {
            base.Disconnect();
        }
    }
}