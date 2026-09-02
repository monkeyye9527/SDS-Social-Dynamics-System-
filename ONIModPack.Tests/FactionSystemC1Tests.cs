using Xunit;
using ONIModPack.Content.SocialDynamics.Politics;

namespace ONIModPack.Tests
{
    /// <summary>
    /// C1 缺陷回归测试：ApplyModifiers 曾以 popularity * (1 + modifier) 乘性缩放，
    /// 每次轮询（10s）复合增长导致民望指数级冲向 1.0 / 0.0。
    /// 修复：加性、时间比例（modifier × dt × PopularityChangeMultiplier），纯函数可测。
    /// </summary>
    public class FactionSystemC1Tests
    {
        [Fact]
        public void ZeroModifier_ReturnsPopularityUnchanged()
            => Assert.Equal(0.5f, FactionSystem.ApplyPopularityModifier(0.5f, 0f, 10f, 0.1f), 5);

        [Fact]
        public void PositiveModifier_AdditiveNotCompound()
            => Assert.Equal(0.6f, FactionSystem.ApplyPopularityModifier(0.5f, 0.1f, 10f, 0.1f), 5);

        [Fact]
        public void NegativeModifier_LinearDecrease()
            => Assert.Equal(0.4f, FactionSystem.ApplyPopularityModifier(0.5f, -0.1f, 10f, 0.1f), 5);

        [Fact]
        public void ClampsToUpperBound()
            => Assert.Equal(1f, FactionSystem.ApplyPopularityModifier(0.9f, 0.15f, 10f, 0.1f), 5);

        [Fact]
        public void ClampsToLowerBound()
            => Assert.Equal(0f, FactionSystem.ApplyPopularityModifier(0.1f, -0.15f, 10f, 0.1f), 5);

        [Fact]
        public void ZeroDeltaTime_NoChange()
            => Assert.Equal(0.5f, FactionSystem.ApplyPopularityModifier(0.5f, 0.2f, 0f, 0.1f), 5);

        [Fact]
        public void SplitDeltaTime_TimeInvariant_NoCompounding()
        {
            // 拆成 2 个 5s 应用 == 1 个 10s 应用：证明加性，而非每 tick 复合
            float oneStep = FactionSystem.ApplyPopularityModifier(0.5f, 0.1f, 10f, 0.1f);
            float first = FactionSystem.ApplyPopularityModifier(0.5f, 0.1f, 5f, 0.1f);
            float second = FactionSystem.ApplyPopularityModifier(first, 0.1f, 5f, 0.1f);
            Assert.Equal(oneStep, second, 5);
        }
    }
}