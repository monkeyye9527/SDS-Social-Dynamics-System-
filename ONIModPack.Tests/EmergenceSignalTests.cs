using Xunit;
using ONIModPack.Content.SocialDynamics.Emergence;

namespace ONIModPack.Tests
{
    /// <summary>
    /// 回归测试（U59 实测根因）：NativeSignals 缺数据时必须是 0（无信号 = 无压力输入），
    /// 绝不能退化为 0.5——0.5 会被 PressureModel 积分成强负面输入，
    /// 导致压力轴开局灌满、合法性 30 秒内崩到 0。
    /// </summary>
    public class EmergenceSignalTests
    {
        [Fact]
        public void NativeSignals_DefaultsToZero()
        {
            var s = new NativeSignals();
            Assert.Equal(0f, s.BeliefFracture);
            Assert.Equal(0f, s.AversionRefusal);
            Assert.Equal(0f, s.EmotionInstability);
            Assert.Equal(0f, s.OpinionDiscontent);
            Assert.Equal(0f, s.WorkMoraleDip);
        }

        [Fact]
        public void LaborTensionInput_IsZero_WhenNoSignals()
        {
            // PressureModel.ComputeAxisInput 是私有的，这里直接验证公式输入组合：
            // 全部信号为 0 时 LaborTension 输入 = 0，不会推动压力轴。
            var s = new NativeSignals();
            float laborInput = s.AversionRefusal * 0.5f + s.WorkMoraleDip * 0.3f + s.OpinionDiscontent * 0.2f;
            Assert.Equal(0f, laborInput);
        }

        [Fact]
        public void LaborTensionInput_WouldBeStrong_IfSignalsWereHalf()
        {
            // 反证：若退化为 0.5，输入 = 0.5（每 tick 积分），轴必然灌满——本测试固化该认知。
            float halfInput = 0.5f * 0.5f + 0.5f * 0.3f + 0.5f * 0.2f;
            Assert.Equal(0.5f, halfInput);
            Assert.True(halfInput > 0.3f, "0.5 中性值在压力轴积分中是不可接受的强输入");
        }
    }
}
