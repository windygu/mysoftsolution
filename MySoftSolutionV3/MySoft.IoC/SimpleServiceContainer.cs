using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using Castle.Core;
using Castle.Core.Internal;
using Castle.Facilities.Startable;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using MySoft.Cache;
using MySoft.IoC.Messages;
using MySoft.IoC.Services;
using MySoft.Logger;
using System.Linq;

namespace MySoft.IoC
{
    /// <summary>
    /// The simple service container.
    /// </summary>
    public sealed class SimpleServiceContainer : IServiceContainer
    {
        #region Private Members

        private IWindsorContainer container;
        private ICacheDependent cache;

        private void Init(CastleFactoryType type, IDictionary serviceKeyTypes)
        {
            //�������Զ��ģʽ����������ý�
            if (type == CastleFactoryType.Remote || ConfigurationManager.GetSection("castle") == null)
                container = new WindsorContainer();
            else
                container = new WindsorContainer(new ServiceInterpreter());

            //����������ע��
            container.AddFacility("startable", new StartableFacility());

            if (serviceKeyTypes != null && serviceKeyTypes.Count > 0)
            {
                RegisterComponents(serviceKeyTypes);
            }

            this.DiscoverServices();
        }

        private void DiscoverServices()
        {
            foreach (Type type in GetInterfaces<ServiceContractAttribute>())
            {
                object instance = null;
                try
                {
                    instance = this[type];
                }
                catch
                {
                }

                //�ж�ʵ���Ƿ�ӽӿڷ���
                if (instance != null && type.IsAssignableFrom(instance.GetType()))
                {
                    IService service = new DynamicService(this, type, instance);
                    if (instance is IStartable)
                    {
                        RegisterComponent("Startable_" + service.ServiceName, type, instance.GetType());
                    }
                    RegisterComponent("Service_" + service.ServiceName, service);
                }
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleServiceContainer"/> class.
        /// </summary>
        /// <param name="config"></param>
        public SimpleServiceContainer(CastleFactoryType type)
        {
            Init(type, null);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleServiceContainer"/> class.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="serviceKeyTypes">The service key types.</param>
        public SimpleServiceContainer(CastleFactoryType type, IDictionary serviceKeyTypes)
        {
            Init(type, serviceKeyTypes);
        }

        #endregion

        #region IServiceContainer Members

        /// <summary>
        /// ��������
        /// </summary>
        public ICacheDependent Cache
        {
            get
            {
                return this.cache;
            }
            set
            {
                this.cache = value;
            }
        }

        /// <summary>
        /// Gets the kernel.
        /// </summary>
        /// <value>The kernel.</value>
        public IKernel Kernel
        {
            get { return container.Kernel; }
        }

        /// <summary>
        /// Registers the component.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="classType">Type of the service.</param>
        /// <param name="serviceType">Type of the service.</param>
        public void RegisterComponent(string key, Type classType, Type serviceType)
        {
            container.Register(Component.For(classType).Named(key).ImplementedBy(serviceType).LifeStyle.Transient);
        }

        /// <summary>
        /// Registers the component.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="instance">Type of the class.</param>
        public void RegisterComponent(string key, object instance)
        {
            container.Register(Component.For(instance.GetType()).Named(key).Instance(instance).LifeStyle.Transient);
        }

        /// <summary>
        /// Registers the components.
        /// </summary>
        /// <param name="serviceKeyTypes">The service key types.</param>
        public void RegisterComponents(IDictionary serviceKeyTypes)
        {
            System.Collections.IDictionaryEnumerator en = serviceKeyTypes.GetEnumerator();
            while (en.MoveNext())
            {
                if (en.Value != null)
                {
                    IService service = new DynamicService(this, (Type)en.Key, en.Value);
                    if (en.Value is IStartable)
                    {
                        RegisterComponent("Startable_" + service.ServiceName, (Type)en.Key, en.Value.GetType());
                    }
                    RegisterComponent("Service_" + service.ServiceName, service);
                }
            }
        }

        /// <summary>
        /// Releases the specified obj.
        /// </summary>
        /// <param name="obj">The obj.</param>
        public void Release(object obj)
        {
            container.Release(obj);
        }

        /// <summary>
        /// Gets the <see cref="System.Object"/> with the specified key.
        /// </summary>
        /// <value></value>
        public object this[string key]
        {
            get { return container.Resolve<object>(key); }
        }

        /// <summary>
        /// Gets the <see cref="System.Object"/> with the specified service type.
        /// </summary>
        /// <value></value>
        public object this[Type serviceType]
        {
            get { return container.Resolve(serviceType); }
        }

        /// <summary>
        /// Calls the service.
        /// </summary>
        /// <param name="reqMsg"></param>
        /// <param name="logTimeout"></param>
        /// <returns></returns>
        public ResponseMessage CallService(RequestMessage reqMsg, double logTimeout)
        {
            IService service = container.ResolveAll<IService>()
                .SingleOrDefault(model => model.ServiceName == reqMsg.ServiceName);

            if (service == null)
            {
                string title = string.Format("The server not find matching service ({0}).", reqMsg.ServiceName);
                throw new WarningException(title)
                {
                    ApplicationName = reqMsg.AppName,
                    ExceptionHeader = string.Format("Application��{0}��occurs error. ==> Comes from {1}({2}).", reqMsg.AppName, reqMsg.HostName, reqMsg.IPAddress)
                };
            }

            return service.CallService(reqMsg, logTimeout);
        }

        /// <summary>
        /// ��ȡԼ���Ľӿ�
        /// </summary>
        /// <returns></returns>
        public Type[] GetInterfaces<ContractType>()
        {
            List<Type> typelist = new List<Type>();
            GraphNode[] nodes = this.Kernel.GraphNodes;
            nodes.Cast<ComponentModel>().ForEach(model =>
            {
                bool markedWithServiceContract = false;
                var attr = CoreHelper.GetTypeAttribute<ContractType>(model.Service);
                if (attr != null)
                {
                    markedWithServiceContract = true;
                }

                if (markedWithServiceContract)
                {
                    typelist.Add(model.Service);
                }
            });

            return typelist.ToArray();
        }

        #endregion

        /// <summary>
        /// ��������
        /// </summary>
        public string ServiceName
        {
            get { return typeof(SimpleServiceContainer).FullName; }
        }

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            container.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region ILogable Members

        /// <summary>
        /// OnLog event.
        /// </summary>
        public event LogEventHandler OnLog;

        #endregion

        #region IErrorLogable Members

        /// <summary>
        /// OnError event.
        /// </summary>
        public event ErrorLogEventHandler OnError;

        #endregion

        #region IServiceContainer ��Ա

        /// <summary>
        /// �����־
        /// </summary>
        /// <param name="log"></param>
        public void WriteLog(string log, LogType type)
        {
            try
            {
                if (OnLog != null) OnLog(log, type);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// �������
        /// </summary>
        /// <param name="exception"></param>
        public void WriteError(Exception exception)
        {
            try
            {
                if (OnError != null) OnError(exception);
            }
            catch (Exception)
            {
            }
        }

        #endregion
    }
}