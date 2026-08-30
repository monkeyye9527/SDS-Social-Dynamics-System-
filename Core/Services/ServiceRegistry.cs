using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ONIModPack.Core.Services
{
    public enum ServiceState
    {
        Constructed,
        Initialized,
        Starting,
        Running,
        Stopping,
        Stopped,
        Shutdown,
        Disposed
    }

    public interface IServiceDescriptor
    {
        Type ServiceType { get; }
        Type ImplementationType { get; }
        IService Instance { get; }
        ServiceState State { get; }
        string Name { get; }
        bool IsSingleton { get; }
    }

    public class ServiceDescriptor : IServiceDescriptor
    {
        public Type ServiceType { get; }
        public Type ImplementationType { get; }
        public IService Instance { get; internal set; }
        public ServiceState State { get; internal set; } = ServiceState.Constructed;
        public string Name { get; }
        public bool IsSingleton { get; }

        public ServiceDescriptor(Type serviceType, Type implementationType, IService instance, string name, bool isSingleton)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Instance = instance;
            Name = name;
            IsSingleton = isSingleton;
        }
    }

    public static class ServiceRegistry
    {
        private static readonly ConcurrentDictionary<Type, ServiceDescriptor> _servicesByType = new ConcurrentDictionary<Type, ServiceDescriptor>();
        private static readonly ConcurrentDictionary<string, ServiceDescriptor> _servicesByName = new ConcurrentDictionary<string, ServiceDescriptor>();
        private static readonly object _writeLock = new object();
        private static bool _isShuttingDown;

        public static bool IsShuttingDown => _isShuttingDown;

        public static void Register<T>(T service) where T : IService
        {
            RegisterInternal(typeof(T), service, typeof(T).Name, true);
        }

        public static void Register<TInterface, TImpl>(TImpl service) where TImpl : class, TInterface, IService
        {
            RegisterInternal(typeof(TInterface), service, typeof(TInterface).Name, true);
            RegisterInternal(typeof(TImpl), service, typeof(TImpl).Name, true);
        }

        public static void Register<T>(T service, string name) where T : IService
        {
            RegisterInternal(typeof(T), service, name, true);
        }

        public static void RegisterInterface<TInterface, TImpl>(TImpl service) where TImpl : class, TInterface
        {
            RegisterInterfaceInternal(typeof(TInterface), service, typeof(TInterface).Name, true);
            if (service is IService serviceImpl)
            {
                RegisterInternal(typeof(TImpl), serviceImpl, typeof(TImpl).Name, true);
            }
        }

        private static void RegisterInternal(Type serviceType, IService service, string name, bool isSingleton)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var descriptor = new ServiceDescriptor(serviceType, service.GetType(), service, name, isSingleton);

            if (_servicesByType.TryAdd(serviceType, descriptor))
            {
                _servicesByName.TryAdd(name, descriptor);

                if (!service.IsInitialized)
                {
                    service.Initialize();
                }

                descriptor.State = ServiceState.Initialized;
                ModLogger.Debug($"[ServiceRegistry] Registered service: {serviceType.Name} ({name})");
            }
            else
            {
                ModLogger.Warning($"[ServiceRegistry] Service {serviceType.Name} already registered");
            }
        }

        private static void RegisterInterfaceInternal(Type serviceType, object service, string name, bool isSingleton)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var descriptor = new ServiceDescriptor(serviceType, service.GetType(), service as IService, name, isSingleton);

            if (_servicesByType.TryAdd(serviceType, descriptor))
            {
                _servicesByName.TryAdd(name, descriptor);

                if (service is IService serviceImpl && !serviceImpl.IsInitialized)
                {
                    serviceImpl.Initialize();
                }

                descriptor.State = ServiceState.Initialized;
                ModLogger.Debug($"[ServiceRegistry] Registered interface: {serviceType.Name} ({name})");
            }
            else
            {
                ModLogger.Warning($"[ServiceRegistry] Interface {serviceType.Name} already registered");
            }
        }

        public static T Get<T>() where T : IService
        {
            if (_servicesByType.TryGetValue(typeof(T), out var descriptor))
            {
                return (T)descriptor.Instance;
            }

            ModLogger.Error($"[ServiceRegistry] Service {typeof(T).Name} not found");
            return default;
        }

        public static T Get<T>(string name) where T : class, IService
        {
            if (_servicesByName.TryGetValue(name, out var descriptor))
            {
                return descriptor.Instance as T;
            }

            ModLogger.Error($"[ServiceRegistry] Service '{name}' not found");
            return default;
        }

        public static bool TryGet<T>(out T service) where T : IService
        {
            if (_servicesByType.TryGetValue(typeof(T), out var descriptor))
            {
                service = (T)descriptor.Instance;
                return true;
            }

            service = default;
            return false;
        }

        public static bool TryGet<T>(string name, out T service) where T : class, IService
        {
            if (_servicesByName.TryGetValue(name, out var descriptor))
            {
                service = descriptor.Instance as T;
                return service != null;
            }

            service = default;
            return false;
        }

        public static T GetInterface<T>()
        {
            if (_servicesByType.TryGetValue(typeof(T), out var descriptor))
            {
                return (T)descriptor.Instance;
            }

            ModLogger.Error($"[ServiceRegistry] Interface {typeof(T).Name} not found");
            return default;
        }

        public static bool TryGetInterface<T>(out T service)
        {
            if (_servicesByType.TryGetValue(typeof(T), out var descriptor))
            {
                service = (T)descriptor.Instance;
                return true;
            }

            service = default;
            return false;
        }

        public static bool IsRegistered<T>() where T : IService
        {
            return _servicesByType.ContainsKey(typeof(T));
        }

        public static bool IsRegistered(string name)
        {
            return _servicesByName.ContainsKey(name);
        }

        public static void Unregister<T>() where T : IService
        {
            if (_servicesByType.TryRemove(typeof(T), out var descriptor))
            {
                _servicesByName.TryRemove(descriptor.Name, out _);
                descriptor.Instance.Shutdown();
                descriptor.State = ServiceState.Shutdown;
                ModLogger.Debug($"[ServiceRegistry] Unregistered service: {typeof(T).Name}");
            }
        }

        public static void Unregister(string name)
        {
            if (_servicesByName.TryRemove(name, out var descriptor))
            {
                _servicesByType.TryRemove(descriptor.ServiceType, out _);
                descriptor.Instance.Shutdown();
                descriptor.State = ServiceState.Shutdown;
                ModLogger.Debug($"[ServiceRegistry] Unregistered service: {name}");
            }
        }

        public static void StartAll()
        {
            lock (_writeLock)
            {
                foreach (var descriptor in _servicesByType.Values)
                {
                    if (descriptor.State == ServiceState.Initialized && descriptor.Instance is IServiceLifecycle lifecycle)
                    {
                        try
                        {
                            lifecycle.Start();
                            descriptor.State = ServiceState.Running;
                            ModLogger.Debug($"[ServiceRegistry] Started service: {descriptor.Name}");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Error($"[ServiceRegistry] Failed to start {descriptor.Name}: {ex.Message}");
                        }
                    }
                }
            }
        }

        public static void StopAll()
        {
            lock (_writeLock)
            {
                foreach (var descriptor in _servicesByType.Values)
                {
                    if (descriptor.State == ServiceState.Running && descriptor.Instance is IServiceLifecycle lifecycle)
                    {
                        try
                        {
                            lifecycle.Stop();
                            descriptor.State = ServiceState.Stopped;
                            ModLogger.Debug($"[ServiceRegistry] Stopped service: {descriptor.Name}");
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Error($"[ServiceRegistry] Failed to stop {descriptor.Name}: {ex.Message}");
                        }
                    }
                }
            }
        }

        public static void ShutdownAll()
        {
            lock (_writeLock)
            {
                _isShuttingDown = true;

                foreach (var descriptor in _servicesByType.Values)
                {
                    try
                    {
                        descriptor.Instance.Shutdown();
                        descriptor.State = ServiceState.Shutdown;
                        ModLogger.Debug($"[ServiceRegistry] Shutdown service: {descriptor.Name}");
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error($"[ServiceRegistry] Failed to shutdown {descriptor.Name}: {ex.Message}");
                    }
                }

                _servicesByType.Clear();
                _servicesByName.Clear();
                _isShuttingDown = false;
                ModLogger.Info("[ServiceRegistry] All services shutdown");
            }
        }

        public static void Clear()
        {
            ShutdownAll();
        }

        public static IReadOnlyCollection<IServiceDescriptor> GetAllServices()
        {
            return (IReadOnlyCollection<IServiceDescriptor>)_servicesByType.Values;
        }

        public static ServiceState GetServiceState<T>() where T : IService
        {
            if (_servicesByType.TryGetValue(typeof(T), out var descriptor))
            {
                return descriptor.State;
            }

            return ServiceState.Disposed;
        }

        public static ServiceState GetServiceState(string name)
        {
            if (_servicesByName.TryGetValue(name, out var descriptor))
            {
                return descriptor.State;
            }

            return ServiceState.Disposed;
        }
    }
}