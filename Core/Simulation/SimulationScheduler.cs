using System;
using System.Collections.Generic;
using ONIModPack.Core.Services;

namespace ONIModPack.Core
{
    public class ScheduledTask
    {
        public string TaskId { get; }
        public ISimulationSystem System { get; }
        public UpdateFrequency Frequency { get; }
        public float Interval { get; }
        public System.Action UpdateAction { get; }
        public float LastExecutionTime { get; set; }
        public float Accumulator { get; set; }
        public int FailureCount { get; set; }
        public bool IsDisabled { get; set; }
        public const int MaxFailures = 3;

        public ScheduledTask(string taskId, ISimulationSystem system, UpdateFrequency frequency)
        {
            TaskId = taskId;
            System = system;
            Frequency = frequency;
            Interval = GetInterval(frequency);
            
            if (frequency == UpdateFrequency.Cycle)
            {
                UpdateAction = system.CycleUpdate;
            }
            else
            {
                UpdateAction = () => system.Update(Interval);
            }
        }

        public ScheduledTask(string taskId, System.Action updateAction, float interval)
        {
            TaskId = taskId;
            UpdateAction = updateAction;
            Interval = interval;
            Frequency = UpdateFrequency.EventTriggered;
        }

        private static float GetInterval(UpdateFrequency frequency)
        {
            switch (frequency)
            {
                case UpdateFrequency.EveryFrame: return 0f;
                case UpdateFrequency.Second1: return 1f;
                case UpdateFrequency.Second10: return 10f;
                case UpdateFrequency.Second30: return 30f;
                case UpdateFrequency.Minute1: return 60f;
                case UpdateFrequency.Cycle: return 60f;
                default: return 1f;
            }
        }
    }

    public class SimulationScheduler : ISimulationScheduler
    {
        private readonly List<ScheduledTask> _cycleTasks = new List<ScheduledTask>();
        private readonly List<ScheduledTask> _timedTasks = new List<ScheduledTask>();
        private readonly List<ScheduledTask> _everyFrameTasks = new List<ScheduledTask>();
        private readonly List<ScheduledTask> _second1Tasks = new List<ScheduledTask>();
        private readonly List<ScheduledTask> _second10Tasks = new List<ScheduledTask>();
        private readonly List<ScheduledTask> _second30Tasks = new List<ScheduledTask>();
        private readonly List<ScheduledTask> _minute1Tasks = new List<ScheduledTask>();
        
        private bool _isInitialized;
        
        public int TaskCount => _timedTasks.Count + _cycleTasks.Count + _everyFrameTasks.Count + 
                               _second1Tasks.Count + _second10Tasks.Count + _second30Tasks.Count + _minute1Tasks.Count;
        public int EveryFrameCount => _everyFrameTasks.Count;
        public int TimedTaskCount => _timedTasks.Count;
        public bool IsInitialized => _isInitialized;

        public void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            ModLogger.Info("[Scheduler] SimulationScheduler initialized");
        }

        public void Register(ISimulationSystem system, UpdateFrequency frequency)
        {
            var taskId = system.SystemName;
            
            if (ContainsTask(taskId))
            {
                ModLogger.Warning($"[Scheduler] System {taskId} already registered, skipping");
                return;
            }
            
            var task = new ScheduledTask(taskId, system, frequency);
            
            switch (frequency)
            {
                case UpdateFrequency.Cycle:
                    _cycleTasks.Add(task);
                    break;
                case UpdateFrequency.EveryFrame:
                    _everyFrameTasks.Add(task);
                    break;
                case UpdateFrequency.Second1:
                    _second1Tasks.Add(task);
                    break;
                case UpdateFrequency.Second10:
                    _second10Tasks.Add(task);
                    break;
                case UpdateFrequency.Second30:
                    _second30Tasks.Add(task);
                    break;
                case UpdateFrequency.Minute1:
                    _minute1Tasks.Add(task);
                    break;
                default:
                    _timedTasks.Add(task);
                    break;
            }
            ModLogger.Debug($"[Scheduler] Registered system: {taskId} ({frequency})");
        }

        public void Register(string taskId, System.Action updateAction, float interval)
        {
            if (ContainsTask(taskId))
            {
                ModLogger.Warning($"[Scheduler] Task {taskId} already registered, skipping");
                return;
            }
            
            var task = new ScheduledTask(taskId, updateAction, interval);
            _timedTasks.Add(task);
            ModLogger.Debug($"[Scheduler] Registered task: {taskId} ({interval}s)");
        }
        
        private bool ContainsTask(string taskId)
        {
            foreach (var task in _timedTasks)
                if (task.TaskId == taskId) return true;
            foreach (var task in _cycleTasks)
                if (task.TaskId == taskId) return true;
            foreach (var task in _everyFrameTasks)
                if (task.TaskId == taskId) return true;
            foreach (var task in _second1Tasks)
                if (task.TaskId == taskId) return true;
            foreach (var task in _second10Tasks)
                if (task.TaskId == taskId) return true;
            foreach (var task in _second30Tasks)
                if (task.TaskId == taskId) return true;
            foreach (var task in _minute1Tasks)
                if (task.TaskId == taskId) return true;
            return false;
        }

        public void Unregister(string taskId)
        {
            _timedTasks.RemoveAll(t => t.TaskId == taskId);
            _cycleTasks.RemoveAll(t => t.TaskId == taskId);
            _everyFrameTasks.RemoveAll(t => t.TaskId == taskId);
            _second1Tasks.RemoveAll(t => t.TaskId == taskId);
            _second10Tasks.RemoveAll(t => t.TaskId == taskId);
            _second30Tasks.RemoveAll(t => t.TaskId == taskId);
            _minute1Tasks.RemoveAll(t => t.TaskId == taskId);
            ModLogger.Debug($"[Scheduler] Unregistered task: {taskId}");
        }
        
        public void Update(float deltaTime)
        {
            foreach (var task in _everyFrameTasks)
            {
                if (ShouldExecute(task))
                {
                    ExecuteTaskWithDelta(task, deltaTime);
                }
            }

            foreach (var task in _timedTasks)
            {
                if (ShouldExecute(task))
                {
                    task.Accumulator += deltaTime;
                    while (task.Accumulator >= task.Interval)
                    {
                        task.Accumulator -= task.Interval;
                        ExecuteTask(task);
                        if (task.IsDisabled) break;
                    }
                }
            }

            ExecuteFrequencyTasks(_second1Tasks, 1f, deltaTime);
            ExecuteFrequencyTasks(_second10Tasks, 10f, deltaTime);
            ExecuteFrequencyTasks(_second30Tasks, 30f, deltaTime);
            ExecuteFrequencyTasks(_minute1Tasks, 60f, deltaTime);
        }
        
        private void ExecuteFrequencyTasks(List<ScheduledTask> tasks, float interval, float deltaTime)
        {
            foreach (var task in tasks)
            {
                if (ShouldExecute(task))
                {
                    task.Accumulator += deltaTime;
                    while (task.Accumulator >= interval)
                    {
                        task.Accumulator -= interval;
                        ExecuteTaskWithDelta(task, interval);
                        if (task.IsDisabled) break;
                    }
                }
            }
        }

        private bool ShouldExecute(ScheduledTask task)
        {
            return !task.IsDisabled && (task.System == null || task.System.IsEnabled);
        }

        public void CycleUpdate()
        {
            foreach (var task in _cycleTasks)
            {
                if (ShouldExecute(task))
                {
                    ExecuteTask(task);
                }
            }
        }

        private void ExecuteTask(ScheduledTask task)
        {
            try
            {
                task.UpdateAction();
                task.FailureCount = 0;
            }
            catch (Exception ex)
            {
                task.FailureCount++;
                ModLogger.Error($"[Scheduler] Error executing task {task.TaskId}: {ex}");
                
                if (task.FailureCount >= ScheduledTask.MaxFailures)
                {
                    task.IsDisabled = true;
                    ModLogger.Warning($"[Scheduler] Task {task.TaskId} disabled after {ScheduledTask.MaxFailures} failures");
                }
            }
        }

        private void ExecuteTaskWithDelta(ScheduledTask task, float deltaTime)
        {
            try
            {
                if (task.System != null)
                {
                    task.System.Update(deltaTime);
                }
                else
                {
                    task.UpdateAction();
                }
                task.FailureCount = 0;
            }
            catch (Exception ex)
            {
                task.FailureCount++;
                ModLogger.Error($"[Scheduler] Error executing task {task.TaskId}: {ex}");
                
                if (task.FailureCount >= ScheduledTask.MaxFailures)
                {
                    task.IsDisabled = true;
                    ModLogger.Warning($"[Scheduler] Task {task.TaskId} disabled after {ScheduledTask.MaxFailures} failures");
                }
            }
        }

        public void Clear()
        {
            _timedTasks.Clear();
            _cycleTasks.Clear();
            _everyFrameTasks.Clear();
            _second1Tasks.Clear();
            _second10Tasks.Clear();
            _second30Tasks.Clear();
            _minute1Tasks.Clear();
        }

        public void Shutdown()
        {
            Clear();
            _isInitialized = false;
        }
    }
}
