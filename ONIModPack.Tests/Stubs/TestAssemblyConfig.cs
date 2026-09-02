using Xunit;

// 缘由：ServiceRegistry / EventBus 等组件的静态全局状态在多个 xUnit 测试类之间共享。
// xUnit 默认按测试类并行执行，多个类同时读写同一份静态状态会产生交错竞态，
// 导致偶发失败（如 ReliefConfigProblemOverrideTests Expected 0.25 / Actual 0.5）。
// 单类隔离运行时稳定通过（1/1 PASS），全量并行时偶发复现，故禁用并行化以消除竞态。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
