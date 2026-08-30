using System;

namespace ONIModPack.Core.Modules
{
    public abstract class ModuleBase : IModule
    {
        private ModuleState _currentState = ModuleState.Constructed;
        
        protected string _moduleId;
        
        public virtual string ModuleId { get => _moduleId; protected set => _moduleId = value; }
        
        public ModuleState CurrentState => _currentState;
        
        public bool IsInitialized => _currentState >= ModuleState.Initialized && _currentState < ModuleState.Shutdown;
        
        public bool IsRunning => _currentState == ModuleState.Running;
        
        public bool IsFailed => _currentState == ModuleState.Failed;
        
        public bool CanTransitionTo(ModuleState targetState)
        {
            return _currentState switch
            {
                ModuleState.Constructed => targetState == ModuleState.Registered,
                ModuleState.Registered => targetState == ModuleState.Initialized || targetState == ModuleState.Shutdown,
                ModuleState.Initialized => targetState == ModuleState.Starting || targetState == ModuleState.Shutdown,
                ModuleState.Starting => targetState == ModuleState.Running || targetState == ModuleState.Failed || targetState == ModuleState.Stopping,
                ModuleState.Running => targetState == ModuleState.Stopping || targetState == ModuleState.Failed,
                ModuleState.Stopping => targetState == ModuleState.Stopped,
                ModuleState.Stopped => targetState == ModuleState.Starting || targetState == ModuleState.Shutdown,
                ModuleState.Failed => targetState == ModuleState.Shutdown,
                ModuleState.Shutdown => targetState == ModuleState.Disposed,
                ModuleState.Disposed => false,
                _ => false
            };
        }
        
        protected bool TransitionTo(ModuleState targetState)
        {
            if (!CanTransitionTo(targetState))
                return false;
            
            _currentState = targetState;
            return true;
        }
        
        public void SetState(ModuleState targetState)
        {
            _currentState = targetState;
        }
        
        public virtual void Initialize()
        {
            TransitionTo(ModuleState.Initialized);
        }
        
        public virtual void Start()
        {
            TransitionTo(ModuleState.Starting);
            TransitionTo(ModuleState.Running);
        }
        
        public virtual void Shutdown()
        {
            if (_currentState == ModuleState.Running)
                TransitionTo(ModuleState.Stopping);
            
            TransitionTo(ModuleState.Stopped);
            TransitionTo(ModuleState.Shutdown);
        }
        
        public void MarkAsFailed()
        {
            _currentState = ModuleState.Failed;
        }
    }
}
