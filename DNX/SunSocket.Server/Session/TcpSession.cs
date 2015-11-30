﻿using System;
using SunSocket.Core.Session;
using System.Net.Sockets;
using SunSocket.Core;
using SunSocket.Core.Protocol;
using SunSocket.Server.Interface;
using SunSocket.Core.Interface;

namespace SunSocket.Server.Session
{
    public class TcpSession : ITcpSession
    {
        ILoger loger;
        object closeLock = new object();
        public TcpSession(ILoger loger)
        {
            this.loger = loger;
            SessionId = Guid.NewGuid().ToString();//生成唯一sesionId
            SessionData = new DataContainer();
            ReceiveEventArgs = new SocketAsyncEventArgs();
            SendEventArgs = new SocketAsyncEventArgs();
            SendEventArgs.Completed += SendComplate;//数据发送完成事件
            ReceiveEventArgs.Completed += ReceiveComplate;
        }
        
        public string SessionId{get; set;}
        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime? ConnectDateTime { get; set; }
        /// <summary>
        /// 上次活动时间
        /// </summary>
        public DateTime ActiveDateTime { get; set; }
        Socket connectSocket;
        /// <summary>
        /// 连接套接字
        /// </summary>
        public Socket ConnectSocket
        {
            get { return connectSocket; }
            set
            {
                connectSocket = value;
                if (connectSocket == null) //清理缓存
                {
                    PacketProtocol.Clear();
                }
                ReceiveEventArgs.AcceptSocket = connectSocket;
                SendEventArgs.AcceptSocket = connectSocket;
            }
        }
        /// <summary>
        /// 接受数据
        /// </summary>
        public SocketAsyncEventArgs ReceiveEventArgs{get;set;}
        /// <summary>
        /// 发送数据
        /// </summary>
        public SocketAsyncEventArgs SendEventArgs{get;set;}
        ITcpPacketProtocol packetProtocol;
        //包接收发送处理器
        public ITcpPacketProtocol PacketProtocol
        {
            get {
                return packetProtocol;
            }
            set {
                packetProtocol = value;
                packetProtocol.Session = this;
            }
        }

        public DataContainer SessionData
        {
            get;set;
        }

        public ITcpSessionPool<string, ITcpSession> Pool
        {
            get;
            set;
        }

        /// <summary>
        /// 发送指令
        /// </summary>
        /// <param name="cmd"></param>
        public void SendAsync(SendData cmd)
        {
            PacketProtocol.SendAsync(cmd);
        }

        public void StartReceiveAsync()
        {
            try
            {
                bool willRaiseEvent = ConnectSocket.ReceiveAsync(ReceiveEventArgs); //投递接收请求
                if (!willRaiseEvent)
                {
                    ReceiveComplate(null, ReceiveEventArgs);
                }
            }
            catch (Exception e)
            {
                loger.Fatal(e);
            }
        }
        private void ReceiveComplate(object sender, SocketAsyncEventArgs receiveEventArgs)
        {
            if (receiveEventArgs.BytesTransferred > 0 && receiveEventArgs.SocketError == SocketError.Success)
            {
                ActiveDateTime = DateTime.Now;
                if (!PacketProtocol.ProcessReceiveBuffer(receiveEventArgs.Buffer, receiveEventArgs.Offset, receiveEventArgs.BytesTransferred))
                { //如果处理数据返回失败，则断开连接
                    DisConnect();
                }
                StartReceiveAsync();//再次等待接收数据
            }
            else
            {
                DisConnect();
            }
        }
        public void SendComplate()
        {
            SendComplate(null, SendEventArgs);
        }
        private void SendComplate(object sender, SocketAsyncEventArgs sendEventArgs)
        {
            ActiveDateTime = DateTime.Now;//发送数据视为活跃
            if (sendEventArgs.SocketError == SocketError.Success)
            {
                if (ConnectSocket != null)
                {
                    PacketProtocol.SendProcess();//继续发送
                }
            }
            else
            {
                lock (closeLock)
                {
                    DisConnect();
                }
            }
        }
        //断开连接
        public void DisConnect()
        {
            if (ConnectDateTime != null)
            {
                lock (closeLock)
                {
                    if (ConnectDateTime != null)
                    {
                        if (Pool != null)
                        {
                            _DisConnect();
                            Clear();
                            Pool.Push(this);
                        }
                        else
                        {
                            Dispose();
                        }
                    }
                }
            }
        }
        private void _DisConnect()
        {
            ConnectDateTime = null;
            if (OnDisConnect != null)
            {
                OnDisConnect(null, this);
            }
            if (ConnectSocket != null)
            {
                try
                {
                    ConnectSocket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception e)
                {
                    //日志记录
                    loger.Fatal(string.Format("CloseClientSocket Disconnect client {0} error, message: {1}", ConnectSocket, e.Message));
                }
                ConnectSocket.Dispose();
                ConnectSocket = null;
            }
        }
        //清理session
        public void Clear()
        {
            //释放引用，并清理缓存，包括释放协议对象等资源
            PacketProtocol.Clear();
            SessionData.Clear();//清理session数据
        }

        public void Dispose()
        {
            _DisConnect();
            Clear();
            ReceiveEventArgs.Dispose();
            SendEventArgs.Dispose();
        }

        //断开连接事件
        public event EventHandler<ITcpSession> OnDisConnect;
        /// <summary>
        /// 数据包提取完成事件
        /// </summary>
        public event EventHandler<IDynamicBuffer> OnReceived { add { PacketProtocol.OnReceived += value; }remove { PacketProtocol.OnReceived -= value; } }
    }
}