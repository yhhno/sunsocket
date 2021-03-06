﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using SunSocket.Core.Interface;
using SunRpc.Client;
using System.Diagnostics;
using Rpc.Interface.Server;
using System.IO;

namespace Rpc.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var loger = new Loger();
            var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8088);
            var config = new ClientConfig();
            config.Server = endPoint;
            config.Loger = loger;
            config.BinPath = AppDomain.CurrentDomain.BaseDirectory;
            // SingleTest(config);
            proxyTest(config);
            //MessageTest(config);
        }
        public static void MessageTest(ClientConfig config)
        {
            ProxyFactory fac = new ProxyFactory(config);
            Connect conn = fac.GetConnect();
            ISCaculator obj = conn.GetInstance<ISCaculator>("Caculator");
            while (true)
            {
                string c = Console.ReadLine();
                if (!string.IsNullOrEmpty(c))
                {
                    obj.BroadCast("给大家来个广播");
                }
            }
        }
        public static void proxyTest(ClientConfig config)
        {
            ProxyFactory fac = new ProxyFactory(config);
            Connect conn = fac.GetConnect();
            ISCaculator caculator = conn.GetInstance<ISCaculator>();
            while (true)
            {
                var c = Console.ReadLine();
                int count = 10000;
                if (!string.IsNullOrEmpty(c))
                    count = Convert.ToInt32(c);
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var t1 = Task.Run(() =>
                 {
                     for (int i = 0; i < count; i++)
                     {
                         var r = caculator.Add(1, -100);
                     }
                 });
                var t2 = Task.Run(() =>
                 {
                     for (int i = 0; i < count; i++)
                     {
                         var r = caculator.Add(1, -100);
                     }
                 });
                var t3 = Task.Run(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        var r = caculator.Add(1, -100);
                    }
                });
                var t4 = Task.Run(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        var r = caculator.Add(1, -100);
                    }
                });
                Task.WaitAll(t1, t2, t3, t4);
                sw.Stop();
                //Console.WriteLine("RPC对服务器完成{0}次单向调用，运行时间：{1} 秒{2}毫秒", count * 4, sw.Elapsed.Seconds, sw.Elapsed.Milliseconds);
                Console.WriteLine("RPC对服务器完成{0}次递归调用(服务器方法内部再调用一次客户端的方法)，运行时间：{1} 秒{2}毫秒", count * 4, sw.Elapsed.Seconds, sw.Elapsed.Milliseconds);
            }
        }
        public static void SingleTest(ClientConfig config)
        {
            var client = new Connect(config);
            client.Connect();
            Console.WriteLine("连接成功");
            while (true)
            {
                Console.ReadLine();
                int count = 100;
                Stopwatch sw = new Stopwatch();
                //List<Task<object>> list = new List<Task<object>>();
                sw.Start();
                //for (int i = 0; i < count; i++)
                //{
                //    var t = client.Invoke<List<string>>("Caculator", "GetList");
                //    list.Add(t);
                //}
                Parallel.For(0, count, i =>
                {
                    var t = client.Invoke<List<string>>("Caculator", "GetList");
                });
                //Task.WaitAll(list.ToArray());
                sw.Stop();
                Console.WriteLine("RPC完成{0}次调用，运行时间：{1} 秒{2}毫秒", count, sw.Elapsed.Seconds, sw.Elapsed.Milliseconds);
            }
        }
    }
    public class Loger : ILoger
    {
        public void Debug(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Debug(string message)
        {
            throw new NotImplementedException();
        }

        public void Error(Exception e)
        {
            throw new NotImplementedException();
        }

        public void Error(string message)
        {
            Console.WriteLine(message);
        }

        public void Fatal(Exception e)
        {
            Console.WriteLine(e.Message);
        }

        public void Fatal(string message)
        {
            throw new NotImplementedException();
        }

        public void Info(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Info(string message)
        {
            throw new NotImplementedException();
        }

        public void Log(string message)
        {
            throw new NotImplementedException();
        }

        public void Trace(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Trace(string message)
        {
            throw new NotImplementedException();
        }

        public void Warning(Exception e)
        {
            throw new NotImplementedException();
        }

        public void Warning(string message)
        {
            throw new NotImplementedException();
        }
    }
}
