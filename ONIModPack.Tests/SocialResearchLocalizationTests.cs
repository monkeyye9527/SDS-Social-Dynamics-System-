using System.Collections.Generic;
using Xunit;
using ONIModPack.Core.Localization;
using ONIModPack.Content.SocialResearch;

namespace ONIModPack.Tests
{
    /// <summary>社会研究树节点名本地化回归：每个节点必须注册 Research.&lt;Id&gt; 词表键，且中英文名非空、非 key 本身。</summary>
    public class SocialResearchLocalizationTests
    {
        [Fact]
        public void AllResearchNodes_HaveLocalizedNames()
        {
            var db = new SocialResearchDatabase();
            db.Initialize();

            var missing = new List<string>();
            foreach (var node in db.ResearchNodes.Values)
            {
                var key = ModStrings.Prefix + "Research." + node.Id;
                if (!ModStringDefaults.TryGet(key, true, out var zh) || string.IsNullOrEmpty(zh))
                    missing.Add(key + " (zh)");
                if (!ModStringDefaults.TryGet(key, false, out var en) || string.IsNullOrEmpty(en))
                    missing.Add(key + " (en)");
            }

            Assert.Empty(missing);
        }

        [Fact]
        public void NodeNames_AreLocalized_NotRawKeys()
        {
            ModStrings.ExternalLookup = null;
            var db = new SocialResearchDatabase();
            db.Initialize();

            foreach (var node in db.ResearchNodes.Values)
            {
                Assert.DoesNotContain("ONIModPack.", node.Name);
                Assert.NotEmpty(node.Name);
            }

            // 中文环境下抽查已知节点
            var observation = db.GetNode("SocialObservation");
            Assert.Equal("社会观察", observation.Name);
        }
    }
}
