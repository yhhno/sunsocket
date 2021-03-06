﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using SunSocket.Core.Buffer;
using SunSocket.Core.Protocol;
using SunSocket.Client.Interface;
using SunSocket.Core.Interface;

namespace SunSocket.Client.Protocol
{
    public class TcpClientPacketProtocol : ITcpClientPacketProtocol
    {
        object closeLock = new object();
        bool NetByteOrder = false;
        static int intByteLength = sizeof(int);
        private object clearLock = new object();
        //缓冲器池
        private static FixedBufferPool BufferPool;
        private int alreadyReceivePacketLength, needReceivePacketLenght;
        private IFixedBuffer InterimPacketBuffer;
        //数据接收缓冲器队列
        private Queue<IFixedBuffer> ReceiveBuffers;
        //数据发送缓冲器
        public IFixedBuffer SendBuffer { get; set; }
        private IDynamicBuffer ReceiveDataBuffer { get; set; }
        public ITcpClientSession Session
        {
            get;
            set;
        }

        private SendData NoComplateCmd = null;//未完全发送指令
        bool isSend = false;//发送状态
        private ConcurrentQueue<SendData> sendDataQueue = new ConcurrentQueue<SendData>();//指令发送队列
        public TcpClientPacketProtocol(int bufferSize, int fixedBufferPoolSize)
        {
            if (BufferPool == null)
            {
                lock(closeLock)
                {
                    if(BufferPool==null)
                        BufferPool = new FixedBufferPool(fixedBufferPoolSize, bufferSize);
                }
            }
            ReceiveBuffers = new Queue<IFixedBuffer>();
            SendBuffer = new FixedBuffer(bufferSize);
            ReceiveDataBuffer = new DynamicBuffer(bufferSize);
        }
        public bool ProcessReceiveBuffer(byte[] receiveBuffer, int offset, int count)
        {
            while (count > 0)
            {
                if (needReceivePacketLenght > 0 && alreadyReceivePacketLength + count >= needReceivePacketLenght)//说明包已获取完成
                {
                    if (InterimPacketBuffer != null)
                    {
                        ReceiveBuffers.Enqueue(InterimPacketBuffer);
                        InterimPacketBuffer = null;
                    }
                    if (ReceiveBuffers.Count > 0)
                    {
                        int getLenght = 0;//已取出数据

                        var cacheBuffer = ReceiveBuffers.Dequeue();
                        getLenght = cacheBuffer.DataSize - intByteLength;
                        ReceiveDataBuffer.WriteBuffer(cacheBuffer.Buffer, intByteLength, getLenght);
                        BufferPool.Push(cacheBuffer);
                        while (ReceiveBuffers.Count > 0)
                        {
                            var popBuffer = ReceiveBuffers.Dequeue();
                            ReceiveDataBuffer.WriteBuffer(popBuffer.Buffer, 0, popBuffer.DataSize);
                            getLenght += popBuffer.DataSize;
                            BufferPool.Push(popBuffer);
                        }
                        var needLenght = needReceivePacketLenght - getLenght - intByteLength;
                        ReceiveDataBuffer.WriteBuffer(receiveBuffer, offset, needLenght);
                        offset += needLenght;
                        count -= needLenght;
                        //触发获取指令事件
                        ReceiveData();
                        //清理合包数据
                        needReceivePacketLenght = 0; alreadyReceivePacketLength = 0;
                    }
                }
                else
                {
                    if (InterimPacketBuffer != null && alreadyReceivePacketLength == 0)
                    {
                        var diff = intByteLength - InterimPacketBuffer.DataSize;
                        InterimPacketBuffer.WriteBuffer(receiveBuffer, offset, diff);
                        offset += diff;
                        count -= diff;
                        var packetLength = BitConverter.ToInt32(InterimPacketBuffer.Buffer, 0);
                        if (NetByteOrder)
                            packetLength = IPAddress.NetworkToHostOrder(packetLength); //把网络字节顺序转为本地字节顺序
                        if (count >= packetLength)
                        {
                            BufferPool.Push(InterimPacketBuffer);
                            InterimPacketBuffer = null;
                            ReceiveDataBuffer.WriteBuffer(receiveBuffer, offset, packetLength);
                            ReceiveData();
                            offset += packetLength;
                            count -= packetLength;
                        }
                        else
                        {
                            needReceivePacketLenght = packetLength + intByteLength;
                            alreadyReceivePacketLength = intByteLength;
                        }
                    }
                }
                if (needReceivePacketLenght > 0)
                {
                    while (count > 0)//遍历把数据放入缓冲器中
                    {
                        if (InterimPacketBuffer == null)
                        {
                            InterimPacketBuffer = BufferPool.Pop();
                        }
                        var surpos = InterimPacketBuffer.Buffer.Length - InterimPacketBuffer.DataSize;//中间buffer剩余空间
                        if (count > surpos)
                        {
                            InterimPacketBuffer.WriteBuffer(receiveBuffer, offset, surpos);
                            ReceiveBuffers.Enqueue(InterimPacketBuffer);
                            InterimPacketBuffer = null;
                            alreadyReceivePacketLength += surpos;//记录已接收的数据
                            offset += surpos;
                            count -= surpos;
                        }
                        else
                        {
                            InterimPacketBuffer.WriteBuffer(receiveBuffer, offset, count);
                            alreadyReceivePacketLength += count;//记录已接收的数据
                            count = 0;
                        }
                    }
                }
                else
                {
                    if (count > intByteLength)
                    {
                        //按照长度分包
                        int packetLength = BitConverter.ToInt32(receiveBuffer, offset); //获取包长度
                        if (NetByteOrder)
                            packetLength = IPAddress.NetworkToHostOrder(packetLength); //把网络字节顺序转为本地字节顺序
                        if ((count - intByteLength) >= packetLength) //如果数据包达到长度则马上进行解析
                        {
                            ReceiveDataBuffer.WriteBuffer(receiveBuffer, offset + intByteLength, packetLength);
                            //触发获取指令事件
                            ReceiveData();
                            int processLenght = packetLength + intByteLength;
                            offset += processLenght;
                            count -= processLenght;
                        }
                        else
                        {
                            needReceivePacketLenght = packetLength + intByteLength;//记录当前包总共需要多少的数据
                        }
                    }
                    else
                    {
                        if (InterimPacketBuffer == null)
                        {
                            InterimPacketBuffer = BufferPool.Pop();
                        }
                        InterimPacketBuffer.WriteBuffer(receiveBuffer, offset, count);
                        count = 0;
                    }
                }
            }
            return count == 0;
        }
        public void ReceiveData()
        {
            Session.OnReceived(Session, ReceiveDataBuffer);
            ReceiveDataBuffer.Clear();//清空数据接收器缓存
        }
        object lockObj = new object();
        public void SendAsync(SendData data)
        {
            sendDataQueue.Enqueue(data);
            if (!isSend)
            {
                lock (lockObj)
                {
                    if (!isSend)
                    {
                        isSend = true;
                        if (Session.ConnectSocket != null)
                        {
                            SendProcess();
                        }
                        else
                        {
                            Session.DisConnect();
                        }
                    }
                }
            }
        }

        public void SendProcess()
        {
            SendBuffer.Clear(); //清除已发送的包
            int surplus = SendBuffer.Buffer.Length;
            while (sendDataQueue.Count > 0)
            {
                if (NoComplateCmd != null)
                {
                    int noComplateLength = NoComplateCmd.Data.Length - NoComplateCmd.Offset;
                    if (noComplateLength <= surplus)
                    {
                        SendBuffer.WriteBuffer(NoComplateCmd.Data, NoComplateCmd.Offset, noComplateLength);
                        surplus -= noComplateLength;
                        NoComplateCmd = null;
                    }
                    else
                    {
                        SendBuffer.WriteBuffer(NoComplateCmd.Data, NoComplateCmd.Offset,surplus);
                        NoComplateCmd.Offset += surplus;
                        surplus = 0;
                        break;//跳出当前循环
                    }
                }
                if (surplus >= intByteLength)
                {
                    SendData data;
                    if (sendDataQueue.TryDequeue(out data))
                    {
                        var PacketAllLength = data.Data.Length + intByteLength;
                        if (PacketAllLength <= surplus)
                        {
                            SendBuffer.WriteInt(data.Data.Length, false); //写入总大小
                            SendBuffer.WriteBuffer(data.Data); //写入命令内容
                            surplus -= PacketAllLength;
                        }
                        else
                        {
                            SendBuffer.WriteInt(data.Data.Length, false); //写入总大小
                            surplus -= intByteLength;
                            if (surplus > 0)
                            {
                                SendBuffer.WriteBuffer(data.Data, data.Offset, surplus); //写入命令内容
                                data.Offset = surplus;
                            }
                            NoComplateCmd = data;//把未全部发送指令缓存
                            break;
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            if (surplus < SendBuffer.Buffer.Length)
            {
                Session.SendEventArgs.SetBuffer(SendBuffer.Buffer, 0, SendBuffer.DataSize);
                if (Session.ConnectSocket != null)
                {
                    bool willRaiseEvent = Session.ConnectSocket.SendAsync(Session.SendEventArgs);
                    if (!willRaiseEvent)
                    {
                        Session.SendComplate();
                    }
                }
                else
                {
                    isSend = false;
                }
            }
            else
            {
                isSend = false;
            }
        }
        //断开连接
        private void DisConnect()
        {
            if (Session.ConnectSocket != null)
            {
                lock (closeLock)
                {
                    if (Session.ConnectSocket != null)
                        Session.DisConnect();
                }
            }
        }
        public void Clear()
        {
            SendBuffer.Clear();
            lock (clearLock)
            {
                isSend = false;
                if (sendDataQueue.Count > 0)
                {
                    SendData cmd;
                    while (sendDataQueue.TryDequeue(out cmd))
                    {
                    }
                }
                if (InterimPacketBuffer != null)
                {
                    BufferPool.Push(InterimPacketBuffer);
                    InterimPacketBuffer = null;
                }
                while (ReceiveBuffers.Count > 0)
                {
                    var packetBuffer = ReceiveBuffers.Dequeue();
                    BufferPool.Push(packetBuffer);
                }
            }
            NoComplateCmd = null;
            alreadyReceivePacketLength = 0;
            needReceivePacketLenght = 0;
        }
    }
}
