﻿using MySoft.Cache;
using MySoft.IoC.Messages;
using MySoft.IoC.Services.Tasks;
using MySoft.Threading;
using System;
using System.Collections;
using System.Threading;

namespace MySoft.IoC.Services
{
    /// <summary>
    /// 异步调用器
    /// </summary>
    internal class AsyncCaller : IDisposable
    {
        private IService service;
        private ServiceCacheType type;
        private IAsyncResult ar;

        /// <summary>
        /// 实例化AsyncCaller
        /// </summary>
        /// <param name="service"></param>
        /// <param name="type"></param>
        public AsyncCaller(IService service, ServiceCacheType type)
        {
            this.service = service;
            this.type = type;
            this.ar = new AsyncResult<ResponseMessage>(null, null);
        }

        /// <summary>
        /// 异步调用服务
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reqMsg"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public ResponseMessage AsyncRun(OperationContext context, RequestMessage reqMsg, TimeSpan timeout)
        {
            //异步请求
            ManagedThreadPool.QueueUserWorkItem(WaitCallback, new ArrayList { context, reqMsg });

            //超时返回
            if (!ar.AsyncWaitHandle.WaitOne(timeout, false))
            {
                throw new TimeoutException(string.Format("The current request timeout {0} ms!", timeout.TotalMilliseconds));
            }

            //异步响应
            return (ar as AsyncResult<ResponseMessage>).EndInvoke();
        }

        /// <summary>
        /// 异步调用服务
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reqMsg"></param>
        /// <returns></returns>
        public ResponseMessage SyncRun(OperationContext context, RequestMessage reqMsg)
        {
            try
            {
                if (NeedCacheResult(reqMsg))
                    return GetResponseFromCache(context, reqMsg);
                else
                    return GetResponseFromService(context, reqMsg);
            }
            catch (Exception ex)
            {
                //获取异常响应信息
                return IoCHelper.GetResponse(reqMsg, ex);
            }
        }

        #region 私有方法

        /// <summary>
        /// 异步响应
        /// </summary>
        /// <param name="state"></param>
        private void WaitCallback(object state)
        {
            var handler = ar as AsyncResult<ResponseMessage>;

            try
            {
                var arr = state as ArrayList;
                var _context = arr[0] as OperationContext;
                var _reqMsg = arr[1] as RequestMessage;

                var resMsg = SyncRun(_context, _reqMsg);

                handler.SetAsCompleted(resMsg, false);
            }
            catch (Exception ex)
            {
                handler.SetAsCompleted(ex, false);
            }
        }

        /// <summary>
        /// 获取响应从本地缓存
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reqMsg"></param>
        /// <returns></returns>
        private ResponseMessage GetResponseFromService(OperationContext context, RequestMessage reqMsg)
        {
            //设置上下文
            OperationContext.Current = context;

            try
            {
                //响应结果，清理资源
                return service.CallService(reqMsg);
            }
            catch (ThreadAbortException ex)
            {
                //取消中止线程
                Thread.ResetAbort();

                throw new ThreadStateException("The current request thread is aborted!", ex);
            }
            finally
            {
                OperationContext.Current = null;
            }
        }

        /// <summary>
        /// 从内存获取数据项
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reqMsg"></param>
        /// <returns></returns>
        private ResponseMessage GetResponseFromCache(OperationContext context, RequestMessage reqMsg)
        {
            //获取cacheKey
            var cacheKey = GetCacheKey(reqMsg, context.Caller);

            //转换成对应的缓存类型
            var cacheType = type == ServiceCacheType.Memory ? LocalCacheType.Memory : LocalCacheType.File;

            //获取内存缓存
            return CacheHelper<ResponseMessage>.Get(cacheType, cacheKey, TimeSpan.FromSeconds(reqMsg.CacheTime), state =>
            {
                var arr = state as ArrayList;
                var _context = arr[0] as OperationContext;
                var _reqMsg = arr[1] as RequestMessage;

                //同步请求响应数据
                var resMsg = GetResponseFromService(_context, _reqMsg);

                if (CheckResponse(resMsg))
                {
                    resMsg = new ResponseBuffer
                    {
                        ServiceName = resMsg.ServiceName,
                        MethodName = resMsg.MethodName,
                        Parameters = resMsg.Parameters,
                        ElapsedTime = resMsg.ElapsedTime,
                        Error = resMsg.Error,
                        Buffer = IoCHelper.SerializeObject(resMsg.Value)
                    };
                }

                return resMsg;

            }, new ArrayList { context, reqMsg }, p => p is ResponseBuffer);
        }

        /// <summary>
        /// 判断是否需要缓存
        /// </summary>
        /// <param name="reqMsg"></param>
        /// <returns></returns>
        private bool NeedCacheResult(RequestMessage reqMsg)
        {
            if (type == ServiceCacheType.None) return false;

            return reqMsg.EnableCache && reqMsg.CacheTime > 0;
        }

        /// <summary>
        /// 检测响应是否有效
        /// </summary>
        /// <param name="resMsg"></param>
        /// <returns></returns>
        private bool CheckResponse(ResponseMessage resMsg)
        {
            if (resMsg == null) return false;
            if (resMsg is ResponseBuffer) return false;

            //如果符合条件，则缓存 
            return !resMsg.IsError && resMsg.Count > 0;
        }

        /// <summary>
        /// 获取cacheKey
        /// </summary>
        /// <param name="reqMsg"></param>
        /// <param name="appCaller"></param>
        /// <returns></returns>
        private string GetCacheKey(RequestMessage reqMsg, AppCaller appCaller)
        {
            var cacheKey = appCaller.Parameters.ToLower();
            cacheKey = cacheKey.Replace("\r\n", "").Replace("\t", "").Replace(" ", "");

            //对Key进行组装
            var methodName = appCaller.MethodName.Substring(appCaller.MethodName.IndexOf(' ') + 1);

            if (reqMsg.InvokeMethod)
                return string.Join("_$$_", new[] { "invoke", appCaller.ServiceName, methodName, cacheKey });
            else
                return string.Join("_$$_", new[] { appCaller.ServiceName, methodName, cacheKey });
        }

        #endregion

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            this.ar = null;
        }
    }
}