﻿using System;

namespace MySoft.IoC
{
    /// <summary>
    /// 订阅选项（默认全部启用）
    /// </summary>
    [Serializable]
    public class SubscribeOptions
    {
        /// <summary>
        /// 超时时间，用于监控超时服务调用
        /// </summary>
        public double CallTimeout { get; set; }

        /// <summary>
        /// 调用记数超时行数
        /// </summary>
        public int CallRowCount { get; set; }

        /// <summary>
        /// 推送调用超时
        /// </summary>
        public bool PushCallTimeout { get; set; }

        /// <summary>
        /// 推送调用错误
        /// </summary>
        public bool PushCallError { get; set; }

        /// <summary>
        /// 推送服务状态信息
        /// </summary>
        public bool PushServerStatus { get; set; }

        /// <summary>
        /// 定时推送状态定时：单位（秒）
        /// </summary>
        public int ServerStatusTimer { get; set; }

        /// <summary>
        /// 推送客户端连接信息
        /// </summary>
        public bool PushClientConnect { get; set; }

        /// <summary>
        /// 实例化SubscribeOptions
        /// </summary>
        public SubscribeOptions()
        {
            this.CallTimeout = 5;           //调用超时为5秒
            this.CallRowCount = 1000;        //调用超时行数1000行
            this.PushCallTimeout = true;    //推送超时
            this.PushCallError = true;      //推送异常
            this.ServerStatusTimer = 5;     //默认间隔为5秒
            this.PushServerStatus = true;
            this.PushClientConnect = true;
        }
    }
}
