using Xunit;
using ONIModPack.Core.Services;
using ONIModPack.Content.SocialResearch;

namespace ONIModPack.Tests
{
    public class SocialTreeInterveneTests
    {
        private static void Setup(bool enablePlayer, bool allowSkip)
        {
            ServiceRegistry.Clear();
            var config = new SocialTreeConfig
            {
                EnablePlayerLit = enablePlayer,
                AllowSkipPrereqLight = allowSkip
            };
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

            var progress = new SocialTreeProgress();
            progress.Initialize();
            ServiceRegistry.Register<SocialTreeProgress>(progress);

            var unlocks = new SocialResearchUnlocks();
            unlocks.Initialize();
            ServiceRegistry.Register<SocialResearchUnlocks>(unlocks);
        }

        [Fact]
        public void PlayerIntervene_Disabled_Rejected()
        {
            Setup(enablePlayer: false, allowSkip: true);
            var unlocks = ServiceResolver.RequireService<SocialResearchUnlocks>();
            Assert.False(unlocks.InterveneNode("SocialObservation", false));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void SkipPrereq_NotAllowed_Rejected()
        {
            Setup(enablePlayer: true, allowSkip: false);
            var pool = ServiceResolver.RequireService<SocialDevelopmentPool>();
            pool.AddBalance(10000f);
            var unlocks = ServiceResolver.RequireService<SocialResearchUnlocks>();
            Assert.False(unlocks.InterveneNode("MassPsychology", skipPrereq: true));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void InsufficientBalance_Rejected()
        {
            Setup(enablePlayer: true, allowSkip: true);
            var pool = ServiceResolver.RequireService<SocialDevelopmentPool>();
            pool.AddBalance(100f); // < 500
            var unlocks = ServiceResolver.RequireService<SocialResearchUnlocks>();
            Assert.False(unlocks.InterveneNode("SocialObservation", false));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void ValidIntervene_WithoutSkip_UnlocksClean()
        {
            Setup(enablePlayer: true, allowSkip: true);
            var pool = ServiceResolver.RequireService<SocialDevelopmentPool>();
            pool.AddBalance(1000f);
            var unlocks = ServiceResolver.RequireService<SocialResearchUnlocks>();

            bool ok = unlocks.InterveneNode("SocialObservation", false);

            Assert.True(ok);
            Assert.True(unlocks.IsResearchUnlocked("SocialObservation"));
            Assert.False(unlocks.IsRushed("SocialObservation"));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void InterveneWithSkipPrereq_MarksRushed()
        {
            Setup(enablePlayer: true, allowSkip: true);
            var pool = ServiceResolver.RequireService<SocialDevelopmentPool>();
            pool.AddBalance(10000f);
            var unlocks = ServiceResolver.RequireService<SocialResearchUnlocks>();

            // MassPsychology 依赖 SocialObservation（未解锁）→ 跳过前置点亮
            bool ok = unlocks.InterveneNode("MassPsychology", skipPrereq: true);

            Assert.True(ok);
            Assert.True(unlocks.IsResearchUnlocked("MassPsychology"));
            Assert.True(unlocks.IsRushed("MassPsychology"));
            Assert.True(ServiceResolver.RequireService<SocialTreeProgress>().IsRushed("MassPsychology"));
            ServiceRegistry.Clear();
        }

        [Fact]
        public void DoesNotSkipPrereq_DependencyCheckBlocks()
        {
            Setup(enablePlayer: true, allowSkip: true);
            var pool = ServiceResolver.RequireService<SocialDevelopmentPool>();
            pool.AddBalance(10000f);
            var unlocks = ServiceResolver.RequireService<SocialResearchUnlocks>();

            // 不跳过前置时，依赖未满足 → 拒绝
            Assert.False(unlocks.InterveneNode("MassPsychology", skipPrereq: false));
            ServiceRegistry.Clear();
        }
    }
}