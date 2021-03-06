﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Collections.Concurrent;
using SunSocket.Core.Interface;
using SunSocket.Server.Interface;
using SunSocket.Server.Config;
using ProtoBuf;
using SunSocket.Server;
using SunRpc.Core.Ioc;
using SunRpc.Core;

namespace SunRpc.Server
{
    public class CallStatus
    {
        public ITcpSession Session { get; set; }
        public RpcCallData Data { get; set; }
    }
    public class RpcServer : TcpServer
    {
        RpcContainer<IServerController> rpcContainer;
        ProxyFactory RpcFactory;
        RpcServerConfig rpcConfig;
        public RpcServer(RpcServerConfig config, ILoger loger) : base(config, loger)
        {
            rpcConfig = config;
            rpcContainer = new RpcContainer<IServerController>();
            RpcFactory = new ProxyFactory(config);
        }
        public override void Start()
        {
            base.Start();
            rpcContainer.Load(rpcConfig.BinPath);
        }
        public override void OnReceived(ITcpSession session, IDynamicBuffer dataBuffer)
        {
            int cmd = dataBuffer.Buffer[0];
            MemoryStream ms = new MemoryStream(dataBuffer.Buffer, 1, dataBuffer.DataSize - 1);
            switch (cmd)
            {
                case 1:
                    {
                        RpcCallData data = Serializer.Deserialize<RpcCallData>(ms);
                        ThreadPool.QueueUserWorkItem(CallFunc, new CallStatus() { Session = session, Data = data });
                    }
                    break;
                case 2:
                    {
                        var data = Serializer.Deserialize<RpcReturnData>(ms);
                        RpcFactory.GetInvoke(session.SessionId).ReturnData(data);
                    }
                    break;
                default:
                    {
                        var data = Serializer.Deserialize<RpcErrorInfo>(ms);
                        RpcFactory.GetInvoke(session.SessionId).ReturnError(data);
                    }
                    break;
            }
            ms.Dispose();
        }
        public void CallFunc(object status)
        {
            CallStatus callData = status as CallStatus;
            CallProcess(callData.Session, callData.Data);
        }
        protected void CallProcess(ITcpSession session, RpcCallData data)
        {
            IServerController controller = rpcContainer.GetController(session.SessionId,data.Controller.ToLower());
            if (controller.Session==null)
            {
                controller.Session =session;
            }
            try
            {
                string key = (data.Controller + ":" + data.Action).ToLower();
                var method = rpcContainer.GetMethod(key);
                object[] args = null;
                if (data.Arguments != null && data.Arguments.Count > 0)
                {
                    args = new object[data.Arguments.Count];
                    var types = GetParaTypeList(key);
                    for (int i = 0; i < data.Arguments.Count; i++)
                    {
                        var arg = data.Arguments[i];
                        MemoryStream stream = new MemoryStream(arg, 0, arg.Length);
                        var obj = Serializer.NonGeneric.Deserialize(types[i], stream);
                        args[i] = obj;
                        stream.Dispose();
                    }
                }
                object value = method.Invoke(controller, args);
                RpcReturnData result = new RpcReturnData() { Id = data.Id };
                var ms = new MemoryStream();
                Serializer.Serialize(ms, value);
                byte[] bytes = new byte[ms.Position];
                Buffer.BlockCopy(ms.GetBuffer(), 0, bytes, 0, bytes.Length);
                result.Value = bytes;
                ms.Position = 0;
                ms.WriteByte(2);
                Serializer.Serialize(ms, result);
                byte[] rBytes = new byte[ms.Position];
                Buffer.BlockCopy(ms.GetBuffer(), 0, rBytes, 0, rBytes.Length);
                session.SendAsync(rBytes);
                ms.Dispose();
            }
            catch (Exception e)
            {
                RpcErrorInfo error = new RpcErrorInfo() { Id = data.Id, Message = e.Message };
                var ms = new MemoryStream();
                ms.WriteByte(0);
                Serializer.Serialize(ms, error);
                byte[] rBytes = new byte[ms.Position];
                Buffer.BlockCopy(ms.GetBuffer(), 0, rBytes, 0, rBytes.Length);
                session.SendAsync(rBytes);
                ms.Dispose();
            }
        }
        public ConcurrentDictionary<string, List<Type>> methodParasDict = new ConcurrentDictionary<string, List<Type>>();
        public List<Type> GetParaTypeList(string key)
        {
            List<Type> result;
            if (!methodParasDict.TryGetValue(key, out result))
            {
                result = rpcContainer.GetMethod(key).GetParameters().Select(p => p.ParameterType).ToList();
                methodParasDict.TryAdd(key, result);
            }
            return result;
        }
        public override void OnConnected(ITcpSession session)
        {
            if (!RpcFactory.invokeDict.ContainsKey(session.SessionId))
            {
                var invoke = new RpcInvoke(session, rpcConfig.RemoteInvokeTimeout);
                RpcFactory.invokeDict.TryAdd(session.SessionId, invoke);
            }
            session.SessionData.Set("proxyfactory",RpcFactory);
            rpcContainer.CreateScope(session.SessionId);
        }
        public override void OnDisConnect(ITcpSession session)
        {
            RpcFactory.GetInvoke(session.SessionId).DisConnect();
            rpcContainer.DestroyScope(session.SessionId);
        }
    }
}
