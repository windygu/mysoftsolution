﻿using System;

namespace MySoft.RESTful
{
    /// <summary>
    /// 发布的REST方法
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class PublishMethodAttribute : Attribute
    {
        /// <summary>
        /// 方法名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 方法描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 调用方式
        /// </summary>
        public HttpMethod Method { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 用户认证的参数名，如UserParameter = "username" 或 “userid"
        /// </summary>
        public string UserParameter { get; set; }

        /// <summary>
        /// 实例化PublishMethod
        /// </summary>
        public PublishMethodAttribute()
        {
            this.Enabled = true;
            this.Method = HttpMethod.GET;
        }

        /// <summary>
        /// 实例化PublishMethod
        /// </summary>
        /// <param name="name"></param>
        public PublishMethodAttribute(string name)
            : this()
        {
            this.Name = name;
        }
    }
}
