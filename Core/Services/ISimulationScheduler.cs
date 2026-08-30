using System;

namespace ONIModPack.Core.Services
{
    public interface ISimulationScheduler : IService
    {
        int TaskCount { get; }
        int EveryFrameCount { get; }
        int TimedTaskCount { get; }

        void Register(ISimulationSystem system, UpdateFrequency frequency);
        void Register(string taskId, System.Action updateAction, float interval);
        void Unregister(string taskId);
        void Update(float deltaTime);
        void CycleUpdate();
        void Clear();
    }

    public interface ISimulationSystem
    {
        string SystemName { get; }
        void Update(float deltaTime);
        void CycleUpdate();
        bool IsEnabled { get; }
    }

    public enum UpdateFrequency
    {
        EveryFrame,
        Second1,
        Second10,
        Second30,
        Minute1,
        Cycle,
        EventTriggered
    }
}
