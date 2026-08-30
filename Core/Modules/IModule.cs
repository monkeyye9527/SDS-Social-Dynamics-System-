using System;

namespace ONIModPack.Core.Modules
{
    public enum ModuleState
    {
        Constructed,
        Registered,
        Initialized,
        Starting,
        Running,
        Stopping,
        Stopped,
        Failed,
        Shutdown,
        Disposed
    }

    public interface IModuleState
    {
        ModuleState CurrentState { get; }
        bool IsInitialized { get; }
        bool IsRunning { get; }
        bool IsFailed { get; }
        bool CanTransitionTo(ModuleState targetState);
    }

    public interface IModule : IModuleState
    {
        string ModuleId { get; }
        
        void Initialize();
        
        void Start();
        
        void Shutdown();
    }
    
    public interface IModule<TConfig> : IModule where TConfig : class
    {
        TConfig Config { get; }
        
        void Configure(TConfig config);
    }
}