using System.Collections.Generic;
using ONIModPack.Content.Art;
using ONIModPack.Core.Utils;
using ONIModPack.Core;
using Xunit;

namespace ONIModPack.Tests
{
    public class YamlLoaderTests
    {
        [Fact]
        public void DeserializeNestedListAndDictionary()
        {
            const string yaml = @"
buildingName: test_building
slots:
  - id: slot_a
    type: interaction
    x: 0
    y: 1
    capacity: 50
tints:
  - name: Wood
    color: '#8B4513'
";

            var config = YamlLoader.Deserialize<SlotsYamlConfig>(yaml);

            Assert.NotNull(config);
            Assert.Equal("test_building", config.BuildingName);
            Assert.Single(config.Slots);
            Assert.Equal("slot_a", config.Slots[0].Id);
            Assert.Single(config.Tints);
            Assert.Equal("Wood", config.Tints[0].Name);
        }
    }

    public class PartitionedRegionalUpdaterTests
    {
        [Fact]
        public void PartitionsRefreshAcrossCycles()
        {
            var updater = new PartitionedRegionalUpdater<int>(regionSize: 4, refreshIntervalCycles: 1,
                (_, __) => 0);
            updater.SetPartitionCount(2);

            updater.GetOrCreate(0, 0);
            updater.GetOrCreate(8, 8);

            int refreshCount = 0;
            updater.TickCycle((_, __, ___) => refreshCount++);
            Assert.True(refreshCount >= 1);
        }
    }
}