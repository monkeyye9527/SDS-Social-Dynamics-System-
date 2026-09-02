using System;
using System.Collections.Generic;
using Xunit;
using ONIModPack.Core.Config;
using ONIModPack.Core.Services;
using ONIModPack.Content.SocialResearch;

namespace ONIModPack.Tests
{
    public class SocialTreeConfigDefaultTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var cfg = new SocialTreeConfig();
            // 不调 Initialize：默认值即为代码默认
            Assert.True(cfg.EnableNpcAutoProgress);
            Assert.True(cfg.EnablePlayerLit);
            Assert.True(cfg.AllowSkipPrereqLight);
            Assert.Equal(1.6f, cfg.SkipPrereqCostMult);
            Assert.Equal(1.0f, cfg.InterveneCostMult);
            Assert.Equal(2, cfg.InterveneCooldownCycles);
            Assert.Equal(0.5f, cfg.BaseRate);
            Assert.Equal(0.45f, cfg.PopExponent);
            Assert.Equal(0.5f, cfg.SWPressure);
            Assert.Equal(0.3f, cfg.SWFaction);
            Assert.Equal(0.2f, cfg.SWBehavior);
            Assert.Equal(0.15f, cfg.SpendFractionPerTick);
            Assert.Equal(0.06f, cfg.ExpansionPerNode);
            Assert.Equal(1.0f, cfg.TierMulT1);
            Assert.Equal(1.15f, cfg.TierMulT2);
            Assert.Equal(1.35f, cfg.TierMulT3);
            Assert.Equal(1.6f, cfg.TierMulT4);
            Assert.Equal(1.9f, cfg.TierMulT5);
            Assert.Equal(0.3f, cfg.RushedEfficiencyPenalty);
            Assert.Equal(0.002f, cfg.RushedFrictionPerSec);
            Assert.Equal(0.002f, cfg.RushedOpinionDrop);
            Assert.Equal(2.0f, cfg.RemediationFavor);
            Assert.Equal(200f, cfg.ResearchBoost);
        }
    }

    public class SocialTreeConfigOverrideTests
    {
        private class FakeConfigManager : IConfigManager
        {
            public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
            public string ConfigDirectory => "fake";
            public bool IsInitialized { get; private set; }
            public event Action<string> OnConfigChanged;
            public bool IsModuleEnabled(string moduleId, bool defaultValue = false) => defaultValue;
            public void SetModuleEnabled(string moduleId, bool enabled) { }
            public T GetConfig<T>(string moduleId, string key, T defaultValue = default) where T : class => defaultValue;
            public T GetConfigValue<T>(string moduleId, string key, T defaultValue = default)
                => Values.TryGetValue(moduleId + "." + key, out var v) ? (T)v : defaultValue;
            public void SetConfigValue<T>(string moduleId, string key, T value) => Values[moduleId + "." + key] = value;
            public void Load() { }
            public void Save() { }
            public void Reload() { }
            public void Initialize() => IsInitialized = true;
            public void Shutdown() => IsInitialized = false;
            public void Dispose() { }
        }

        [Fact]
        public void Initialize_ReadsOverride_FromConfigManager()
        {
            ServiceRegistry.Clear();
            var fake = new FakeConfigManager();
            fake.SetConfigValue("Content.SocialResearch", "SocialTree.SkipPrereqCostMult", 2.0f);
            ServiceRegistry.Register<IConfigManager>(fake);

            var cfg = new SocialTreeConfig();
            cfg.Initialize();

            Assert.Equal(2.0f, cfg.SkipPrereqCostMult);
            Assert.Equal(1.0f, cfg.InterveneCostMult); // 未覆盖键保持默认
            ServiceRegistry.Clear();
        }
    }
}