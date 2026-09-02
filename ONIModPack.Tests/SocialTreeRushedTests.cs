using Xunit;
using ONIModPack.Content.SocialResearch;
using ONIModPack.Content.SocialDynamics.Emergence;
using ONIModPack.Content.SocialDynamics.Politics;

namespace ONIModPack.Tests
{
    /// <summary>
    /// Rushed 社会代价链的映射表与补课摘除行为（副作用落地经模块 tick，由映射表 + 进度状态保证）。
    /// </summary>
    public class SocialTreeRushedTests
    {
        [Fact]
        public void TagToAxis_Coverage()
        {
            Assert.Equal(SituationAxis.IdeologicalSplit, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Culture));
            Assert.Equal(SituationAxis.LaborTension, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Labor));
            Assert.Equal(SituationAxis.SocialUnrest, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Economy));
            Assert.Equal(SituationAxis.MoralFatigue, SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Governance));
            Assert.Null(SocialTreeDirectionResolver.MapTagToAxis(SocialTag.Mixed));
        }

        [Fact]
        public void TagToOpinionTopic_Coverage()
        {
            Assert.Equal(OpinionTopic.Religion, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Culture));
            Assert.Equal(OpinionTopic.WorkConditions, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Labor));
            Assert.Equal(OpinionTopic.Economy, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Economy));
            Assert.Equal(OpinionTopic.Welfare, SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Governance));
            Assert.Null(SocialTreeDirectionResolver.MapTagToOpinionTopic(SocialTag.Mixed));
        }

        [Fact]
        public void TagToFaction_Coverage()
        {
            Assert.Equal(FactionType.Belief, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Culture));
            Assert.Equal(FactionType.Union, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Labor));
            Assert.Equal(FactionType.Science, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Economy));
            Assert.Equal(FactionType.Engineering, SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Governance));
            Assert.Null(SocialTreeDirectionResolver.MapTagToFaction(SocialTag.Mixed));
        }

        [Fact]
        public void RemediationThreshold_IsOne()
        {
            // 补课完成阈值恒为 1：防止未来改动误改
            var progress = new SocialTreeProgress();
            progress.Initialize();
            progress.MarkRushed("x");
            Assert.True(progress.IsRushed("x"));
            progress.AddRemediation("x", 0.99f);
            Assert.True(progress.IsRushed("x"));
            progress.AddRemediation("x", 0.01f);
            Assert.False(progress.IsRushed("x"));
        }
    }
}