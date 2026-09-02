using System.Collections.Generic;
using Xunit;
using ONIModPack.Core.Services;
using ONIModPack.Content.SocialResearch;

namespace ONIModPack.Tests
{
    public class SocialDevelopmentPoolRateTests
    {
        private void RegisterConfig()
        {
            ServiceRegistry.Clear();
            var config = new SocialTreeConfig();
            config.Initialize();
            ServiceRegistry.Register<SocialTreeConfig>(config);
        }

        [Fact]
        public void ComputeRate_ZeroPopulation_ReturnsBaseRate()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();

            // 0 人口按 1 计 → f(人口)=1
            float rate = pool.ComputeCurrentRate(0, 0f, 0f, 0f);
            // f(社会状态)=1+0=1 → rate = 0.5*1*1（v0.93：BaseRate 0.05→0.5，首个节点约 1 周期解锁）
            Assert.Equal(0.5f, rate, 4);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void ComputeRate_WithSocialFactors_AppliesMultipliers()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();

            float rate = pool.ComputeCurrentRate(16, 0.5f, 0.5f, 0.5f);
            // f(人口) = 16^0.45 ≈ 3.4820
            // f(社会状态) = 1 + 0.5*0.5 + 0.3*0.5 + 0.2*0.5 = 1.5
            // rate = 0.5 * 3.4820 * 1.5 ≈ 2.6115（v0.93：BaseRate 0.05→0.5）
            Assert.Equal(2.6115f, rate, 2);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void ComputeNodeCost_AppliesTierAndExpansion()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();

            // T1 node，0 点亮 → 500 * 1.0 * (1 + 0.06*0) = 500
            Assert.Equal(500f, pool.ComputeNodeCost(500, 1, 0), 1);
            // T2 node，5 点亮 → 800 * 1.15 * (1 + 0.06*5) = 920 * 1.3 = 1196
            Assert.Equal(1196f, pool.ComputeNodeCost(800, 2, 5), 1);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void CanSpend_ChecksBalance()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();
            pool.AddBalance(1000f);
            Assert.True(pool.CanSpend(500f));
            Assert.False(pool.CanSpend(1500f));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void TrySpend_ChargesBalance_Correctly()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();
            pool.AddBalance(1000f);
            bool ok = pool.TrySpend(600f);
            Assert.True(ok);
            Assert.Equal(400f, pool.CurrentBalance, 1);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void SaveLoad_RoundTrips_Balance()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();
            pool.AddBalance(1234.5f);

            var data = new Dictionary<string, object>();
            pool.Save(data);
            pool.Load(data);

            Assert.Equal(1234.5f, pool.CurrentBalance, 1);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void Load_NoBalanceKey_SetsZero()
        {
            RegisterConfig();
            var pool = new SocialDevelopmentPool();
            pool.Initialize();
            pool.AddBalance(100f);

            var data = new Dictionary<string, object>();
            pool.Load(data);

            Assert.Equal(0f, pool.CurrentBalance, 1);
            ServiceRegistry.Clear();
        }
    }
}