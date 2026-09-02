using System;
using ONIModPack.Core;
using ONIModPack.Core.Services;
using ONIModPack.Core.Simulation;
using Xunit;

namespace ONIModPack.Tests
{
    /// <summary>
    /// 统一调度器测试（替代已删除的 SimulationScheduler / ISimulationSystem 遗留测试）。
    /// 评审点十：UnifiedScheduler 是全项目唯一权威调度器。
    /// </summary>
    public class SchedulerTests
    {
        private static UnifiedScheduler CreateRunningScheduler()
        {
            var scheduler = new UnifiedScheduler();
            scheduler.Initialize();
            scheduler.OnSimulationRunning();
            return scheduler;
        }

        [Fact]
        public void RegisterAndExecuteTimedTask()
        {
            var scheduler = CreateRunningScheduler();
            int executeCount = 0;

            scheduler.RegisterSystem("TestTask", dt => executeCount++, SimulationPhase.Behavior, 200, 1f);

            Assert.Equal(1, scheduler.RegisteredCount);

            scheduler.Update(0.5f);
            Assert.Equal(0, executeCount);

            scheduler.Update(0.6f); // 累积 1.1s ≥ 1s → 触发 1 次
            Assert.Equal(1, executeCount);

            scheduler.Update(1.0f); // 累积 1.0s → 触发 1 次
            Assert.Equal(2, executeCount);

            scheduler.Shutdown();
        }

        [Fact]
        public void AccumulatorSupportsMultipleTriggersPerFrame()
        {
            var scheduler = CreateRunningScheduler();
            int executeCount = 0;

            scheduler.RegisterSystem("MultiTriggerTask", dt => executeCount++, SimulationPhase.Behavior, 200, 0.5f);

            scheduler.Update(2.0f); // 0.5s 间隔 → 4 次
            Assert.Equal(4, executeCount);

            scheduler.Shutdown();
        }

        [Fact]
        public void RegisterSameSystemIdSkipsRegistration()
        {
            var scheduler = CreateRunningScheduler();
            int executeCount = 0;

            scheduler.RegisterSystem("DuplicateTask", dt => executeCount++, SimulationPhase.Behavior, 200, 1f);
            scheduler.RegisterSystem("DuplicateTask", dt => executeCount++, SimulationPhase.Behavior, 200, 1f);

            Assert.Equal(1, scheduler.RegisteredCount);

            scheduler.Update(1.0f);
            Assert.Equal(1, executeCount);

            scheduler.Shutdown();
        }

        [Fact]
        public void UnregisterSystemRemovesFromScheduler()
        {
            var scheduler = CreateRunningScheduler();
            int executeCount = 0;

            scheduler.RegisterSystem("RemovableTask", dt => executeCount++, SimulationPhase.Behavior, 200, 0.1f);
            Assert.Equal(1, scheduler.RegisteredCount);

            scheduler.UnregisterSystem("RemovableTask");
            Assert.Equal(0, scheduler.RegisteredCount);

            scheduler.Update(0.2f);
            Assert.Equal(0, executeCount);

            scheduler.Shutdown();
        }

        [Fact]
        public void CycleUpdateExecutesCycleSystems()
        {
            var scheduler = CreateRunningScheduler();
            int cycleExecuteCount = 0;

            scheduler.RegisterSystemWithCycle(
                "CycleTest", dt => { }, () => cycleExecuteCount++,
                SimulationPhase.Social, 200, UpdateFrequency.Cycle);

            scheduler.CycleUpdate();
            Assert.Equal(1, cycleExecuteCount);

            scheduler.CycleUpdate();
            Assert.Equal(2, cycleExecuteCount);

            scheduler.Shutdown();
        }

        [Fact]
        public void DisabledSystemIsSkipped()
        {
            var scheduler = CreateRunningScheduler();
            int executeCount = 0;

            scheduler.RegisterSystem(
                "DisabledTask", dt => executeCount++, SimulationPhase.Behavior, 200, 1f,
                isEnabledFunc: () => false);

            scheduler.Update(1.5f);
            Assert.Equal(0, executeCount);

            scheduler.Shutdown();
        }
    }
}
