using System.Collections.Generic;

namespace ONIModPack.Core.Services
{
    public interface IService
    {
        void Initialize();
        void Shutdown();
        bool IsInitialized { get; }
    }

    public interface IServiceLifecycle : IService
    {
        string ServiceName { get; }
        ServiceState CurrentState { get; }
        void Start();
        void Stop();
        bool IsRunning { get; }
    }

    public abstract class ServiceBase : IServiceLifecycle
    {
        public abstract string ServiceName { get; }
        public ServiceState CurrentState { get; private set; } = ServiceState.Constructed;
        public bool IsInitialized => CurrentState >= ServiceState.Initialized;
        public bool IsRunning => CurrentState == ServiceState.Running;

        public virtual void Initialize()
        {
            if (CurrentState != ServiceState.Constructed)
            {
                Logger.Warning($"[Service] {ServiceName} cannot Initialize from state {CurrentState}");
                return;
            }
            CurrentState = ServiceState.Initialized;
            Logger.Debug($"[Service] {ServiceName} Initialized");
        }

        public virtual void Start()
        {
            if (CurrentState != ServiceState.Initialized)
            {
                Logger.Warning($"[Service] {ServiceName} cannot Start from state {CurrentState}");
                return;
            }
            CurrentState = ServiceState.Starting;
            OnStart();
            CurrentState = ServiceState.Running;
            Logger.Debug($"[Service] {ServiceName} Started");
        }

        public virtual void Stop()
        {
            if (CurrentState != ServiceState.Running)
            {
                Logger.Warning($"[Service] {ServiceName} cannot Stop from state {CurrentState}");
                return;
            }
            CurrentState = ServiceState.Stopping;
            OnStop();
            CurrentState = ServiceState.Stopped;
            Logger.Debug($"[Service] {ServiceName} Stopped");
        }

        public virtual void Shutdown()
        {
            if (CurrentState >= ServiceState.Shutdown)
            {
                return;
            }

            if (CurrentState == ServiceState.Running)
            {
                Stop();
            }

            CurrentState = ServiceState.Shutdown;
            OnShutdown();
            Logger.Debug($"[Service] {ServiceName} Shutdown");
        }

        public void Dispose()
        {
            Shutdown();
            OnDispose();
            CurrentState = ServiceState.Disposed;
            Logger.Debug($"[Service] {ServiceName} Disposed");
        }

        protected virtual void OnStart() { }
        protected virtual void OnStop() { }
        protected virtual void OnShutdown() { }
        protected virtual void OnDispose() { }
    }
}