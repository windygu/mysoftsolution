﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MySoft.Cache;
using MySoft.IoC.Messages;

namespace MySoft.IoC.Services
{
    /// <summary>
    /// 异步调用器
    /// </summary>
    internal class AsyncCaller
    {
        /// <summary>
        /// 用于存储并发队列信息
        /// </summary>
        private Hashtable hashtable = new Hashtable();

        private IService service;
        private ICacheStrategy cache;
        private TimeSpan timeout;
        private bool enabledCache;
        private bool fromServer;
        private ThreadManager manager;
        private AsyncMethodCaller caller;
        private Random random;

        /// <summary>
        /// 实例化AsyncCaller
        /// </summary>
        /// <param name="service"></param>
        /// <param name="timeout"></param>
        /// <param name="fromServer"></param>
        public AsyncCaller(IService service, TimeSpan timeout, bool fromServer)
        {
            this.service = service;
            this.timeout = timeout;
            this.enabledCache = false;
            this.fromServer = fromServer;
            this.random = new Random();
            this.caller = new AsyncMethodCaller(GetInvokeResponse);
        }

        /// <summary>
        /// 实例化AsyncCaller
        /// </summary>
        /// <param name="service"></param>
        /// <param name="timeout"></param>
        /// <param name="cache"></param>
        /// <param name="fromServer"></param>
        public AsyncCaller(IService service, TimeSpan timeout, ICacheStrategy cache, bool fromServer)
            : this(service, timeout, fromServer)
        {
            this.cache = cache;
            this.enabledCache = true;
            this.manager = new ThreadManager(service, caller, cache);
        }

        /// <summary>
        /// 异步调用服务
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reqMsg"></param>
        /// <returns></returns>
        public ResponseMessage Run(OperationContext context, RequestMessage reqMsg)
        {
            //获取CallerKey
            var callKey = GetCallerKey(reqMsg, context.Caller);

            if (enabledCache)
            {
                //定义一个响应值
                ResponseMessage resMsg = null;

                //从缓存中获取数据
                if (GetResponseFromCache(callKey, reqMsg, out resMsg))
                {
                    //刷新数据
                    manager.RefreshWorker(callKey);

                    return resMsg;
                }
            }
            //异步响应
            using (var waitResult = new WaitResult(reqMsg))
            {
                //开始异步请求
                var callerItem = new ThreadItem
                {
                    CallKey = callKey,
                    Context = context,
                    Request = reqMsg
                };

                //获取异步结果
                var ar = GetAsyncResult(callerItem, waitResult);

                //等待超时
                if (!waitResult.WaitOne(timeout))
                {
                    try
                    {
                        //关闭句柄
                        CloseHandle(ar, callerItem);

                        //获取超时响应
                        return GetTimeoutResponse(reqMsg);
                    }
                    finally
                    {
                        lock (hashtable.SyncRoot)
                        {
                            //超时时也移除
                            if (hashtable.ContainsKey(callKey))
                            {
                                hashtable.Remove(callKey);
                            }
                        }
                    }
                }

                //返回响应结果
                return waitResult.Message;
            }
        }

        /// <summary>
        /// 结束句柄
        /// </summary>
        /// <param name="ar"></param>
        /// <param name="callerItem"></param>
        private void CloseHandle(IAsyncResult ar, ThreadItem callerItem)
        {
            //清理资源
            if (ar != null)
            {
                try
                {
                    caller.EndInvoke(ar);
                    ar.AsyncWaitHandle.Close();
                }
                catch (Exception ex)
                {
                }
            }

            //结束线程
            if (callerItem.Thread != null)
            {
                try
                {
                    callerItem.Thread.Abort();
                }
                catch (Exception ex)
                {
                }
            }
        }

        /// <summary>
        /// 获取异常结果
        /// </summary>
        /// <param name="callerItem"></param>
        /// <param name="waitResult"></param>
        /// <returns></returns>
        private IAsyncResult GetAsyncResult(CallerItem callerItem, WaitResult waitResult)
        {
            IAsyncResult ar = null;

            lock (hashtable.SyncRoot)
            {
                if (!hashtable.ContainsKey(callerItem.CallKey))
                {
                    //将waitResult加入队列中
                    var waitQueue = new Queue<WaitResult>();
                    waitQueue.Enqueue(waitResult);

                    hashtable[callerItem.CallKey] = waitQueue;

                    //启动线程调用
                    ar = caller.BeginInvoke(callerItem, AsyncCallback, callerItem);
                }
                else
                {
                    //加入队列中
                    var waitQueue = hashtable[callerItem.CallKey] as Queue<WaitResult>;
                    waitQueue.Enqueue(waitResult);
                }
            }

            return ar;
        }

        /// <summary>
        /// 运行请求
        /// </summary>
        /// <param name="ar"></param>
        private void AsyncCallback(IAsyncResult ar)
        {
            var callerItem = ar.AsyncState as CallerItem;

            try
            {
                //获取响应信息
                var resMsg = caller.EndInvoke(ar);
                ar.AsyncWaitHandle.Close();

                if (resMsg != null)
                {
                    if (enabledCache)
                    {
                        //设置响应信息到缓存
                        SetResponseToCache(callerItem, resMsg);
                    }

                    //设置响应信息
                    SetInvokeResponse(callerItem.CallKey, resMsg);
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 设置响应信息
        /// </summary>
        /// <param name="callKey"></param>
        /// <param name="resMsg"></param>
        private void SetInvokeResponse(string callKey, ResponseMessage resMsg)
        {
            lock (hashtable.SyncRoot)
            {
                if (hashtable.ContainsKey(callKey))
                {
                    //获取队列
                    var waitQueue = hashtable[callKey] as Queue<WaitResult>;

                    try
                    {
                        while (waitQueue.Count > 0)
                        {
                            try
                            {
                                //响应队列中的请求
                                var waitItem = waitQueue.Dequeue();

                                waitItem.SetResponse(resMsg);
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                    }
                    finally
                    {
                        //移除队列
                        hashtable.Remove(callKey);
                    }
                }
            }
        }

        /// <summary>
        /// 获取CallerKey
        /// </summary>
        /// <param name="reqMsg"></param>
        /// <param name="caller"></param>
        /// <returns></returns>
        private string GetCallerKey(RequestMessage reqMsg, AppCaller caller)
        {
            //对Key进行组装
            return string.Format("{0}_Caller_{1}${2}${3}", (reqMsg.InvokeMethod ? "Invoke" : "Direct"),
                                caller.ServiceName, caller.MethodName, caller.Parameters)
                    .Replace(" ", "").Replace("\r\n", "").Replace("\t", "").ToLower();
        }

        /// <summary>
        /// 获取超时响应信息
        /// </summary>
        /// <param name="reqMsg"></param>
        /// <returns></returns>
        private ResponseMessage GetTimeoutResponse(RequestMessage reqMsg)
        {
            //获取异常响应信息
            var title = string.Format("Async call service ({0},{1}) timeout ({2}) ms.",
                        reqMsg.ServiceName, reqMsg.MethodName, (int)timeout.TotalMilliseconds);

            var resMsg = IoCHelper.GetResponse(reqMsg, new System.TimeoutException(title));

            //设置耗时时间
            resMsg.ElapsedTime = (long)timeout.TotalMilliseconds;

            return resMsg;
        }

        /// <summary>
        /// 调用方法
        /// </summary>
        /// <param name="caller"></param>
        /// <returns></returns>
        private ResponseMessage GetInvokeResponse(CallerItem caller)
        {
            //定义响应的消息
            ResponseMessage resMsg = null;

            if (caller is ThreadItem)
            {
                //设置线程
                (caller as ThreadItem).Thread = Thread.CurrentThread;
            }

            try
            {
                OperationContext.Current = caller.Context;

                //响应结果，清理资源
                resMsg = service.CallService(caller.Request);
            }
            catch (ThreadInterruptedException ex) { }
            catch (ThreadAbortException ex)
            {
                //恢复线程
                Thread.ResetAbort();
            }
            catch (Exception ex)
            {
                //获取异常响应信息
                resMsg = IoCHelper.GetResponse(caller.Request, ex);
            }
            finally
            {
                OperationContext.Current = null;
            }

            return resMsg;
        }

        /// <summary>
        /// 设置响应信息到缓存
        /// </summary>
        /// <param name="callerItem"></param>
        /// <param name="resMsg"></param>
        private void SetResponseToCache(CallerItem callerItem, ResponseMessage resMsg)
        {
            var callKey = callerItem.CallKey;
            var reqMsg = callerItem.Request;
            var context = callerItem.Context;

            if (reqMsg.CacheTime <= 0) return;

            //如果符合条件，则自动缓存 【自动缓存功能】
            if (resMsg != null && resMsg.Value != null && !resMsg.IsError && resMsg.Count > 0)
            {
                //克隆一个新的对象
                var newMsg = NewResponseMessage(reqMsg, resMsg);

                cache.InsertCache(callKey, newMsg, reqMsg.CacheTime * 10);

                //Add worker item
                var worker = new WorkerItem
                {
                    CallKey = callKey,
                    Context = context,
                    Request = reqMsg,
                    SlidingTime = reqMsg.CacheTime,
                    UpdateTime = DateTime.Now.AddSeconds(reqMsg.CacheTime),
                    IsRunning = false
                };

                manager.AddWorker(callKey, worker);
            }
        }

        /// <summary>
        /// 从缓存中获取数据
        /// </summary>
        /// <param name="callKey"></param>
        /// <param name="reqMsg"></param>
        /// <param name="resMsg"></param>
        /// <returns></returns>
        private bool GetResponseFromCache(string callKey, RequestMessage reqMsg, out ResponseMessage resMsg)
        {
            //从缓存中获取数据
            resMsg = cache.GetCache<ResponseMessage>(callKey);

            if (resMsg != null)
            {
                //克隆一个新的对象
                resMsg = NewResponseMessage(reqMsg, resMsg);

                return true;
            }

            return false;
        }

        /// <summary>
        /// 产生一个新的对象
        /// </summary>
        /// <param name="reqMsg"></param>
        /// <param name="resMsg"></param>
        /// <returns></returns>
        private ResponseMessage NewResponseMessage(RequestMessage reqMsg, ResponseMessage resMsg)
        {
            var newMsg = new ResponseMessage
            {
                TransactionId = reqMsg.TransactionId,
                ReturnType = resMsg.ReturnType,
                ServiceName = resMsg.ServiceName,
                MethodName = resMsg.MethodName,
                Parameters = resMsg.Parameters,
                Error = resMsg.Error,
                Value = resMsg.Value,
                ElapsedTime = 0
            };

            //如果是服务端，直接返回对象
            if (!fromServer && !reqMsg.InvokeMethod)
            {
                var watch = Stopwatch.StartNew();

                try
                {
                    newMsg.Value = CoreHelper.CloneObject(newMsg.Value);

                    watch.Stop();

                    //设置耗时时间
                    newMsg.ElapsedTime = watch.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    //TODO
                }
                finally
                {
                    if (watch.IsRunning)
                    {
                        watch.Stop();
                    }
                }
            }

            return newMsg;
        }
    }
}