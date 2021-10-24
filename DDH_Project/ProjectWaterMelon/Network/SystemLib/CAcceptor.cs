﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// --- custom --- //
using ProjectWaterMelon.Network.Session;
using ProjectWaterMelon.Log;
using static ProjectWaterMelon.ConstDefine;
using static ProjectWaterMelon.GSocketState;
// -------------- //

namespace ProjectWaterMelon.Network.SystemLib
{
    class CAcceptor
    {
        private Socket mListenSocket;                           // 변동 가능성 존재
        private AutoResetEvent mFlowControlEvt = new AutoResetEvent(true);  // AutoResetEvent 를 false로 지정시 해당 이벤트가 호출된 스레드는 다음 AutoResetEvent.Set을 만나기 전까지 대기상태
                                                                            // 비동기 -> 동기처럼 진행하게끔 
        //private SocketAsyncEventArgs mAcceptArgs;
        private CSocketAsyncEventArgsPool mAcceptAsyncPool;
        private System.Net.IPEndPoint mIPEndPoint;              // 변동 가능성 존재

        private CBufferManager mBufferMng = new CBufferManager(MAX_CONNECTION, MAX_BUFFER_SIZE);
       
        public delegate void OnSocketAsyncEventArgsInput(object sender, SocketAsyncEventArgs args);
        public delegate void OnNewClientHandler(Socket Socket, object UserToken);
  

        public CAcceptor(in Socket Socket, in System.Net.IPEndPoint IPEndPoint, eSockEvtType type = eSockEvtType.CONCURRENT)
        {
            // listen socket -> accept socket
            mListenSocket = Socket;

            // 이후 소켓 재연결에 사용할 endpoint
            mIPEndPoint = IPEndPoint;

            //Set Accept SocketAsyncEventArgsPool
            mAcceptAsyncPool = new CSocketAsyncEventArgsPool(MAX_ACCEPT_OPT, type);
            GenerateAAsyncEvtItemMax(OnAcceptHandler);
        }

        // Accept SocketAsyncEventArgs 세팅 시 고정적인 것만 먼저 생성해둔채로 풀링한다. 변동적인건 추후 세팅
        private SocketAsyncEventArgs GenerateAAsyncEvtItem(OnSocketAsyncEventArgsInput handler)
        {
            SocketAsyncEventArgs lAccpetArgs = new SocketAsyncEventArgs();
            lAccpetArgs.Completed += new EventHandler<SocketAsyncEventArgs>(handler);

            CSession lUserToken = new CSession();
            lAccpetArgs.UserToken = lUserToken;

            return lAccpetArgs;
        }

        // Accept의 경우 buffer에 데이터를 담아 보내는게 없으므로 buffer 세팅(mBufferMng.SetBuffer())은 안한다
        // buffer 세팅진행 후, 데이터를 안 담아 보내면 callback이 일어나지 않는다
        private void GenerateAAsyncEvtItemMax(OnSocketAsyncEventArgsInput handler)
        {
            for (var i = 0; i < MAX_ACCEPT_OPT; ++i)
            {            
                mAcceptAsyncPool.Push(GenerateAAsyncEvtItem(handler));
            }
        }

        /*
         * 정의: 별도의 accpet 스레드에서 비동기 accept 진행 
         */
        public void Start()
        {
            while (true)
            {
                try
                {
                    // AcceptAsync를 통해 비동기로 클라접속을 받은 뒤 처리될 때까지 start 함수호출 스레드 대기상태로 변경
                    mFlowControlEvt.WaitOne();

                    CLog4Net.LogDebugSysLog($"1.Acceptor.Start", $"Accept Start");
                    Console.WriteLine($"Thread[OnConnectHandler] ID => {Thread.CurrentThread.ManagedThreadId} ---- { Thread.CurrentThread.ThreadState}");

                    var lEvtArgs = mAcceptAsyncPool.Count > 0 ? mAcceptAsyncPool.Pop() : GenerateAAsyncEvtItem(OnAcceptHandler); 
                    if (lEvtArgs != null)
                    {
                        var lPending = mListenSocket.AcceptAsync(lEvtArgs);
                        if (!lPending)
                        {
                            // 비동기 함수 호출이 즉시완료 되지 않은경우 pending = false. 이 경우 비동기 함수 호출을 직접 진행 
                            OnAcceptHandler(this, lEvtArgs);
                            CLog4Net.LogDebugSysLog($"1.Acceptor.Start", $"Call OnAcceptHandler(No Async Call)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    CLog4Net.LogError($"Exception in CAcceptor.Start!!! - {ex.Message} - {ex.StackTrace}");
                    continue;
                }
            }
        }

        /*
         * 정의: accept 실패 시 해당 소켓과 연결된 대상 종료 및 리소스 초기화 진행
         * accept 에 사용된 SocketAsyncEventArgs 객체는 다른 곳에서 쓸 수 있도록 반환한다
         * accept - connect에 성공한 대상의 경우에만 session 생성
         * accpet는 connect와 다르게 사용한 SocketAsyncEventArgs 객체풀에 push  
         */
        private void onBadAcceptHandler(ref SocketAsyncEventArgs e)
        {
            // 1.accept 단계에서 SocketError 발생 대상들은 소켓만 Close 한다
            e.AcceptSocket?.Close();
           
            // 2.대상 속성 초기화
            e.AcceptSocket = null;
            e.UserToken = null;
            e.RemoteEndPoint = null;

            // 3.풀링 객체에 반납 
            mAcceptAsyncPool.Push(e);
        }

        private void ResetAndSetCSession(in Socket socket, ref SocketAsyncEventArgs recvArgs, ref SocketAsyncEventArgs sendArgs)
        {
            recvArgs.UserToken = null;
            sendArgs.UserToken = null;

            CSession lUserToken = new CSession();
            lUserToken.mTcpSocket.SetSocket(socket);
            lUserToken.mTcpSocket.SetRemoteAndLocalEP(socket.RemoteEndPoint, socket.LocalEndPoint);
            lUserToken.mTcpSocket.SetSocketConnected(true);
            recvArgs.UserToken = lUserToken;
            sendArgs.UserToken = lUserToken;
            
            lUserToken.mTcpSocket.SetEventArgs(recvArgs, sendArgs);
        }


        /*
         * 정의: accept 비동기 호출 완료 시 호출되는 콜백함수
         * 성공 시 recv 진행, 실패 시 onBadAcceptHandler 호출 
         */
        // Accept - Connect의 경우 buffer에 별도의 내용을 보내주지 않기때문에 bytetransferred = 0
        private void OnAcceptHandler(Object sender, SocketAsyncEventArgs e)
        {
            Console.WriteLine($"Thread[OnConnectHandler] ID => {Thread.CurrentThread.ManagedThreadId} ---- { Thread.CurrentThread.ThreadState}");
            if (e.SocketError == SocketError.Success)
            {
                CLog4Net.LogDebugSysLog($"2.CAcceptor.OnAcceptHandler", $"Call OnAccpetHandler(Accpet is success)");

                var recvArgs = CSocketAsyncEventManager.GetRecvSendSocketAsyncEventArgsPools(eSocketType.RECV);
                if (recvArgs == null) recvArgs = CSocketAsyncEventManager.GetRecvSendSocketAsyncEventArgs(eSocketType.RECV);

                var sendArgs = CSocketAsyncEventManager.GetRecvSendSocketAsyncEventArgsPools(eSocketType.SEND);
                if (sendArgs == null) sendArgs = CSocketAsyncEventManager.GetRecvSendSocketAsyncEventArgs(eSocketType.SEND);

                // 여기서 pop을 통해 받아온 recv/sendargs 값의 UserToken의 socket 은 listen socket으로 되어있다
                // 반면, GetRecvSendSocketAsyncEventArgs 을 통해 받아온 UserToken의 socket은 accpet Socket으로 되어있다
                if (recvArgs != null && sendArgs != null)
                {
                    ResetAndSetCSession(e.AcceptSocket, ref recvArgs, ref sendArgs);
                    CLog4Net.LogDebugSysLog($"3.CAcceptor.OnAcceptHandler", $"Receive Start");

                    var lUserToken = recvArgs.UserToken as CSession;
                    CSessionManager.Add(ref lUserToken);

                    bool lPending = lUserToken.mTcpSocket.mRawSocket.ReceiveAsync(recvArgs);
                    if (!lPending)
                    {
                        CLog4Net.LogDebugSysLog($"3.CAcceptor.OnAcceptHandler", $"Call OnReceiveHandler(No Async Call)");
                        lUserToken.mTcpSocket.OnReceiveHandler(this, recvArgs);
                    }

                    lUserToken.NotifyConnected();
                }
                else
                {
                    onBadAcceptHandler(ref e);
                    if (recvArgs == null)
                    {
                        CLog4Net.LogError($"Error in CAcceptor.OnAcceptHandler!!! - Recv socketAsyncEventArgs is NULL");
                    }
                    else if (sendArgs == null)
                    {
                        CLog4Net.LogError($"Error in CAcceptor.OnAcceptHandler!!! - Send socketAsyncEventArgs is NULL");
                    }
                    else
                    {
                        CLog4Net.LogError($"Error in CAcceptor.OnAcceptHandler!!! - Recv && Send socketAsyncEventArgs is NULL");
                    }
                }
            }
            else
            {
                onBadAcceptHandler(ref e);
                CLog4Net.LogError($"Error in CAcceptor.OnAcceptHandler!!! - {e.SocketError}");
            }
            mFlowControlEvt.Set();
        }
    }
}


/*private void OnAcceptHandler(object sender, SocketAsyncEventArgs e)
{
    if (e.SocketError == SocketError.Success && e.UserToken is CSession lUserToken)
    {
        CLog4Net.LogDebugSysLog($"2.CAcceptor.OnAcceptHandler", $"Call OnAccpetHandler(Accpet is success)");

        // 소켓정보 세팅
        lUserToken.mTcpSocket.SetSocket(e.AcceptSocket);
        lUserToken.mTcpSocket.SetRemoteAndLocalEP(e.AcceptSocket.RemoteEndPoint, e.AcceptSocket.LocalEndPoint);
        lUserToken.mTcpSocket.SetSocketConnected(true);

        // 세션 세팅 
        CSessionManager.Add(ref lUserToken);

        bool AsyncResult = lUserToken.mTcpSocket.mRawSocket.ReceiveAsync(e);
        if (!AsyncResult)
        {
            CLog4Net.LogDebugSysLog($"3.CAcceptor.OnAcceptHandler", $"Call OnReceiveHandler(No Async Call)");
            lUserToken.mTcpSocket.OnReceiveHandler(this, e);
        }

        lUserToken.NotifyConnected();
    }
    else
    {
        onBadAcceptHandler(ref e);
        CLog4Net.LogError($"Error in CAcceptor.OnAcceptHandler!!! - {e.SocketError}");
    }
    mFlowControlEvt.Set();
}
*/

/*public void Start()
{
    try
    {    
        while (true)
        {
            // AcceptAsync를 통해 비동기로 클라접속을 받은 뒤 처리될 때까지 start 함수호출 스레드 대기상태로 변경
            mFlowControlEvt.WaitOne();
            CLog4Net.LogDebugSysLog($"1.Acceptor.Start", $"Accept Start");
            Console.WriteLine($"Thread[OnConnectHandler] ID => {Thread.CurrentThread.ManagedThreadId} ---- { Thread.CurrentThread.ThreadState}");

            mAcceptArgs = mAcceptAsyncPool.Count > 0 ? mAcceptAsyncPool.Pop() : GetAndSetSocketAsyncEventArgs(OnAcceptHandler);
            if (mAcceptArgs != null)
            {
                //1. 풀링하거나 생성한 Accept SocketAsyncEventArgs 객체 세팅
                mAcceptArgs.RemoteEndPoint = mIPEndPoint;

                //2. Accept SocketAsyncEventArgs 객체속성인 UserToken 세팅
                var lUserToken = mAcceptArgs.UserToken as CSession;
                lUserToken.mTcpSocket.SetSocket(mListenSocket);
                lUserToken.mTcpSocket.SetRemoteIPEndPoint(mIPEndPoint);
                //mAcceptArgs.UserToken = lUserToken;

                var lPending = lUserToken.mTcpSocket.mRawSocket.AcceptAsync(mAcceptArgs);
                if (!lPending)
                {
                    // 비동기 함수 호출이 즉시완료 되지 않은경우 pending = false. 이 경우 비동기 함수 호출을 직접 진행 
                    OnAcceptHandler(this, mAcceptArgs);
                    CLog4Net.LogDebugSysLog($"1.Acceptor.Start", $"Call OnAcceptHandler(No Async Call)");
                }
            }  
       }
    }
    catch (Exception ex)
    {
        CLog4Net.LogError($"Exception in CAcceptor.Start!!! - {ex.Message}, {ex.StackTrace}");
    }
}
*/