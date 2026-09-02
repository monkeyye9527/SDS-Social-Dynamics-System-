using System.Collections.Generic;
using Xunit;
using ONIModPack.Core.Services;
using ONIModPack.Content.SocialResearch;

namespace ONIModPack.Tests
{
    public class SocialTreeProgressTests
    {
        private void SetupServices()
        {
            ServiceRegistry.Clear();
            var config = new SocialTreeConfig();
            config.Initialize();
            ServiceRegistry.Register<SocialTreeConfig>(config);

            var db = new SocialResearchDatabase();
            db.Initialize();
            ServiceRegistry.Register<SocialResearchDatabase>(db);

            var tree = new SocialResearchTree();
            tree.Initialize();
            ServiceRegistry.Register<SocialResearchTree>(tree);

            var pool = new SocialDevelopmentPool();
            pool.Initialize();
            ServiceRegistry.Register<SocialDevelopmentPool>(pool);

            var unlocks = new SocialResearchUnlocks();
            unlocks.Initialize();
            ServiceRegistry.Register<SocialResearchUnlocks>(unlocks);
        }

        [Fact]
        public void NewNode_ProgressZero_NotRushed()
        {
            SetupServices();
            var progress = new SocialTreeProgress();
            progress.Initialize();

            Assert.Equal(0f, progress.GetProgress("X"));
            Assert.False(progress.IsRushed("X"));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void AddProgress_AccumulatesAndClampsToOne()
        {
            SetupServices();
            var progress = new SocialTreeProgress();
            progress.Initialize();

            progress.AddProgress("n1", 0.6f);
            progress.AddProgress("n1", 0.6f);

            Assert.Equal(1f, progress.GetProgress("n1"), 3);
            Assert.True(progress.IsReadyToMature("n1"));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void Rushed_MarkAndRemediate()
        {
            SetupServices();
            var progress = new SocialTreeProgress();
            progress.Initialize();
            progress.MarkRushed("n1");

            Assert.True(progress.IsRushed("n1"));
            Assert.Equal(0f, progress.GetRemediation("n1"));

            progress.AddRemediation("n1", 0.5f);
            Assert.Equal(0.5f, progress.GetRemediation("n1"), 3);
            Assert.True(progress.IsRushed("n1")); // 未满 → 仍 Rushed

            progress.AddRemediation("n1", 0.5f);
            Assert.True(progress.IsRemediationComplete("n1"));
            Assert.False(progress.IsRushed("n1")); // 补课完成 → 摘除
            ServiceRegistry.Clear();
        }

        [Fact]
        public void DirectionLock_SetAndQuery()
        {
            SetupServices();
            var progress = new SocialTreeProgress();
            progress.Initialize();
            progress.SetDirectionLock(SocialTag.Labor, true);

            Assert.True(progress.IsDirectionLocked(SocialTag.Labor));
            Assert.True(progress.IsDirectionLocked(SocialTag.Governance) == false);
            ServiceRegistry.Clear();
        }

        [Fact]
        public void SaveLoad_RoundTrips_AllState()
        {
            SetupServices();
            var progress = new SocialTreeProgress();
            progress.Initialize();
            progress.AddProgress("n1", 0.5f);
            progress.MarkRushed("n1");
            progress.AddRemediation("n1", 0.3f);
            progress.SetDirectionLock(SocialTag.Labor, true);

            var data = new Dictionary<string, object>();
            progress.Save(data);
            progress.Load(data);

            Assert.Equal(0.5f, progress.GetProgress("n1"), 3);
            Assert.True(progress.IsRushed("n1"));
            Assert.Equal(0.3f, progress.GetRemediation("n1"), 3);
            Assert.True(progress.IsDirectionLocked(SocialTag.Labor));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void AllocateProgress_WithBalance_MaturesEligibleNode()
        {
            SetupServices();
            var pool = ServiceResolver.RequireService<SocialDevelopmentPool>();
            pool.AddBalance(100000f); // 大幅充足余额

            var progress = new SocialTreeProgress();
            progress.Initialize();

            // SocialObservation 无依赖、T1、cost 500；单次分配 4% = 4000 → progress 达 8（>1）→ 成熟解锁
            bool matured = progress.AllocateProgress();
            Assert.True(matured);
            Assert.True(ServiceResolver.RequireService<SocialResearchUnlocks>().IsResearchUnlocked("SocialObservation"));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void AllocateProgress_SpendsNoMoreThanNodeNeed_RetainsExcessBudget()
        {
            SetupServices();
            var pool = ServiceResolver.RequireService<SocialDevelopmentPool>();
            pool.AddBalance(100000f); // 预算远超单节点所需

            var progress = new SocialTreeProgress();
            progress.Initialize();

            // 预算 100000 × 4% = 4000 可用；SocialObservation 缺口需 500。
            // 修复前 spendHere ≡ 整笔预算 → 余额减 4000；修复后只按需支出 → 余额减 500，保留 99500。
            progress.AllocateProgress();

            Assert.Equal(99500f, pool.CurrentBalance, 3);
            ServiceRegistry.Clear();
        }
    }
}