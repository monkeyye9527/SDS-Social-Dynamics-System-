using System;
using Xunit;
using ONIModPack.Core;
using ONIModPack.Core.Services;
using ONIModPack.Content.Legislation.Events;
using ONIModPack.Content.SocialDynamics;
using ONIModPack.Content.SocialDynamics.Politics;

namespace ONIModPack.Tests
{
    /// <summary>
    /// C2 缺陷回归测试：换届（任期重置）信号原先绑定在 OnRulingFactionChanged（仅政权交替触发）。
    /// 连任（获胜者 == 现任）时无任何信号 → 否决权不重置、竞选纲领不结算/不开新任期。
    /// 修复：新增 OnTermEnded 任期边界事件（每次选举结束无条件触发 + 强设执政触发），
    /// 否决权与纲领改订阅该事件。本测试用桩模拟"选举结束（含连任）"驱动服务响应。
    /// </summary>
    public class ElectionTermResetC2Tests
    {
        [Fact]
        public void TermEnded_ResetsVetoQuota_EvenOnReelection()
        {
            var fake = new FakeElectionSystem { CurrentRulingFaction = FactionType.Engineering };
            var ruling = new RulingFactionService(fake);
            ruling.Initialize();
            try
            {
                Assert.True(ruling.TryConsumeVeto(out _)); // 用尽本届否决权
                Assert.Equal(0, ruling.VetoesRemaining);

                // 连任：选举结束但执政派系未变（无 OnRulingFactionChanged）
                fake.RaiseTermEnded(FactionType.Engineering);

                Assert.Equal(1, ruling.VetoesRemaining); // 否决权已重置
                Assert.True(ruling.TryConsumeVeto(out _));
            }
            finally
            {
                ruling.Shutdown();
            }
        }

        [Fact]
        public void TermEnded_SettlesAndStartsNewCampaignTerm_EvenOnReelection()
        {
            EventBus.Clear();
            ServiceRegistry.Clear();
            var fake = new FakeElectionSystem { CurrentRulingFaction = FactionType.Engineering };
            var ruling = new RulingFactionService(fake);
            ServiceRegistry.Register<IRulingFactionService>(ruling);
            var platform = new CampaignPlatformService(fake);
            platform.Initialize();
            try
            {
                // 1 部偏好类别（Labor）法案生效 → Engineering 纲领进度 1
                EventBus.Publish(new LawEnactedEvent { LawId = "l1", Category = LawCategory.Labor });
                Assert.Equal(1, platform.CurrentProgress[FactionType.Engineering]);

                // 连任：获胜者 == 现任，上任届兑现度必须被结算、新任期进度清零
                fake.RaiseTermEnded(FactionType.Engineering);

                Assert.Equal(0.5f, platform.GetLastFulfillment(FactionType.Engineering), 3);
                Assert.Equal(0, platform.CurrentProgress[FactionType.Engineering]);
            }
            finally
            {
                ServiceRegistry.Clear();
                platform.Shutdown();
                ruling.Shutdown();
            }
        }

        [Fact]
        public void SetRulingFaction_AlsoEndsTerm_RulingChangePath()
        {
            var fake = new FakeElectionSystem { CurrentRulingFaction = FactionType.Engineering };
            var ruling = new RulingFactionService(fake);
            ruling.Initialize();
            try
            {
                Assert.True(ruling.TryConsumeVeto(out _));
                Assert.Equal(0, ruling.VetoesRemaining);

                // 政权交替：强设执政也触发任期边界（新政府 = 新任期）
                fake.SetRulingFaction(FactionType.Union);

                Assert.Equal(FactionType.Union, fake.CurrentRulingFaction);
                Assert.Equal(1, ruling.VetoesRemaining);
            }
            finally
            {
                ruling.Shutdown();
            }
        }
    }

    /// <summary>IElectionSystem 测试桩：可控地发射任期边界信号（真实 ElectionSystem.StartElection 依赖运行时 GameClock，无法进入单测）。</summary>
    internal class FakeElectionSystem : IElectionSystem
    {
        public FactionType CurrentRulingFaction { get; set; }
        public bool ElectionInProgress { get; set; }
        public float LastElectionTime { get; set; }
        public bool IsInitialized { get; set; }

        public event Action<FactionType> OnElectionStarted;
        public event Action<FactionType> OnElectionEnded;
        public event Action<FactionType, FactionType> OnRulingFactionChanged;
        public event Action<FactionType> OnTermEnded;

        public void Initialize() => IsInitialized = true;
        public void Shutdown() => IsInitialized = false;
        public void Update() { }
        public void StartElection() { }
        public void ForceElection() { }

        public void SetRulingFaction(FactionType faction)
        {
            if (CurrentRulingFaction == faction) return;
            var old = CurrentRulingFaction;
            CurrentRulingFaction = faction;
            OnRulingFactionChanged?.Invoke(old, faction);
            OnTermEnded?.Invoke(faction);
        }

        public void SetLastElectionTime(float time) => LastElectionTime = time;
        public float GetTimeUntilNextElection() => 0f;
        public bool IsElectionDue() => false;

        /// <summary>模拟一次选举周期结束（获胜者可为现任 = 连任，触发任期边界信号）。</summary>
        public void RaiseTermEnded(FactionType winner) => OnTermEnded?.Invoke(winner);
    }
}