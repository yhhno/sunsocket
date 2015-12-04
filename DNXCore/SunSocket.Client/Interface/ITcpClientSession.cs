﻿using System;
using System.Net.Sockets;
using SunSocket.Core.Protocol;
using SunSocket.Core.Interface;


namespace SunSocket.Client.Interface
{
    public interface ITcpClientSession :IDisposable
    {
        string SessionId { get; set; }
        /// <summary>
        /// 所在池
        /// </summary>
        IMonitorPool<string, ITcpClientSession> Pool { get; set; }
        /// <summary>
        /// 连接时间
        /// </summary>
        DateTime? ConnectDateTime { get; set; }
        /// <summary>
        /// 最后活动时间
        /// </summary>
        DateTime? ActiveDateTime { get; set; }
        /// <summary>
        /// 连接套接字
        /// </summary>
        Socket ConnectSocket { get; set; }
        /// <summary>
        /// 包协议解析器
        /// </summary>
        ITcpClientPacketProtocol PacketProtocol { get; set; }
        /// <summary>
        /// 接收数据
        /// </summary>
        SocketAsyncEventArgs ReceiveEventArgs { get; set; }
        /// <summary>
        /// 发送数据
        /// </summary>
        SocketAsyncEventArgs SendEventArgs { get; set; }
        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="cmd"></param>
        void SendAsync(SendData cmd);
        /// <summary>
        /// 开始接收数据
        /// </summary>
        void StartReceiveAsync();
        /// <summary>
        /// 发送完成通知
        /// </summary>
        void SendComplate();
        /// <summary>
        /// 连接
        /// </summary>
        void Connect();
        /// <summary>
        /// 断开连接
        /// </summary>
        void DisConnect();
        /// <summary>
        /// 清理但不断开连接
        /// </summary>
        void Clear();
        /// <summary>
        /// 连接成功事件
        /// </summary>
        event EventHandler<ITcpClientSession> OnConnected;
        /// <summary>
        /// 断开连接事件
        /// </summary>
        event EventHandler<ITcpClientSession> OnDisConnect;
        /// <summary>
        /// 收到指令事件
        /// </summary>
        event EventHandler<IDynamicBuffer> OnReceived;
    }
}