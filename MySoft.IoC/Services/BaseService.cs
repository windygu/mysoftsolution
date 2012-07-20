using System;
using System.Diagnostics;
using System.Threading;
using MySoft.IoC.Messages;

namespace MySoft.IoC.Services
{
    /// <summary>
    /// The base class of services.
    /// </summary>
    [Serializable]
    public abstract class BaseService : IService
    {
        /// <summary>
        ///  The service logger
        /// </summary>
        private IServiceContainer logger;
        private Type serviceType;

        /// <summary>
        /// Gets the name of the service.
        /// </summary>
        /// <value>The name of the service.</value>
        public string ServiceName
        {
            get { return serviceType.FullName; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseService"/> class.
        /// </summary>
        /// <param name="serviceName">Name of the service.</param>
        public BaseService(IServiceContainer logger, Type serviceType)
        {
            this.logger = logger;
            this.serviceType = serviceType;
        }

        /// <summary>
        /// Runs the specified MSG.
        /// </summary>
        /// <param name="reqMsg">The MSG.</param>
        /// <returns>The msg.</returns>
        protected abstract ResponseMessage Run(RequestMessage reqMsg);

        /// <summary>
        /// Dispose
        /// </summary>
        public abstract void Dispose();

        #region IService Members

        /// <summary>
        /// Calls the service.
        /// </summary>
        /// <param name="reqMsg">The MSG.</param>
        /// <returns>The msg.</returns>
        public ResponseMessage CallService(RequestMessage reqMsg)
        {
            ResponseMessage resMsg = null;

            //开始计时
            var watch = Stopwatch.StartNew();

            try
            {
                resMsg = Run(reqMsg);

                watch.Stop();

                //计算超时
                resMsg.ElapsedTime = watch.ElapsedMilliseconds;

                if (resMsg.IsError)
                {
                    string body = string.Format("Remote client【{0}】call service ({1},{2}) error.\r\nParameters => {3}\r\nMessage => {4}",
                        reqMsg.Message, reqMsg.ServiceName, reqMsg.MethodName, reqMsg.Parameters.ToString(), resMsg.Message);

                    //获取异常
                    var error = IoCHelper.GetException(OperationContext.Current, reqMsg, body, resMsg.Error);

                    logger.WriteError(error);

                    //返回结果数据
                    if (reqMsg.InvokeMethod)
                    {
                        resMsg.Value = new InvokeData
                        {
                            Value = SerializationManager.SerializeJson(resMsg.Value),
                            Count = resMsg.Count,
                            ElapsedTime = resMsg.ElapsedTime,
                            OutParameters = resMsg.Parameters.ToString()
                        };

                        //清除参数集合
                        resMsg.Parameters.Clear();
                    }
                }
            }
            catch (ThreadAbortException ex)
            {
                //Reset thread abort.
                Thread.ResetAbort();

                string body = string.Format("Remote client【{0}】call service ({1},{2}) error.\r\nParameters => {3}\r\nMessage => {4}",
                  reqMsg.Message, reqMsg.ServiceName, reqMsg.MethodName, reqMsg.Parameters.ToString(),
                    string.Format("Service call timeout {0} ms, the request was aborted initiative. ", watch.ElapsedMilliseconds));

                //获取异常
                var error = IoCHelper.GetException(OperationContext.Current, reqMsg, new ThreadException(body, ex));

                logger.WriteError(error);
            }

            return resMsg;
        }

        #endregion
    }
}
