using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Xunit;
using ONIModPack.Content.BeliefSystem;

namespace ONIModPack.Tests
{
    /// <summary>
    /// 信仰/压力 overlay 着色管线回归（注入 cellHitEnumerator，不触碰 Grid/游戏运行时）：
    /// 信仰场扩散、冲突地带、高压力优先、无信仰无压力不产生颜色。
    /// </summary>
    public class FaithStressOverlayTests
    {
        /// <summary>9x9 恒定网格（cell 0..80），距离 = 切比雪夫，替代 Grid.OffsetCell。</summary>
        private static IEnumerable<FaithStressOverlayData.CellHit> GridHits(int center, int radius)
        {
            int cx = center % 9, cy = center / 9;
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || x > 8 || y < 0 || y > 8) continue;
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    yield return new FaithStressOverlayData.CellHit(y * 9 + x, dist);
                }
        }

        private static Dictionary<int, Color> Build(params FaithStressOverlayData.DupSample[] samples)
            => FaithStressOverlayData.BuildCellColors(samples, GridHits);

        [Fact]
        public void SingleBeliever_TintsNeighborhoodWithBeliefColor()
        {
            var colors = Build(new FaithStressOverlayData.DupSample
            {
                Cell = 40, Belief = BeliefType.FollowersOfTheFlame, StrengthPct = 80f, Stress = 0f,
            });

            Assert.True(colors.Count > 0);
            var expected = FaithStressOverlayData.BeliefColors[(int)BeliefType.FollowersOfTheFlame % FaithStressOverlayData.BeliefColors.Length];
            var center = colors[40];
            Assert.Equal(expected.r, center.r, 3);
            Assert.Equal(expected.g, center.g, 3);
            Assert.InRange(center.a, 0.15f, 0.55f);
        }

        [Fact]
        public void TwoFaithsOverlap_MarksConflict()
        {
            var colors = Build(
                new FaithStressOverlayData.DupSample { Cell = 36, Belief = BeliefType.CultOfTheCooler, StrengthPct = 80f },
                new FaithStressOverlayData.DupSample { Cell = 44, Belief = BeliefType.KeepersOfTheLight, StrengthPct = 80f });

            // 36 与 44 的信仰场在中线（40 一带）强度相当 → 冲突红
            Assert.Equal(FaithStressOverlayData.ConflictColor.r, colors[40].r, 3);
            Assert.Equal(FaithStressOverlayData.ConflictColor.g, colors[40].g, 3);
        }

        [Fact]
        public void HighStress_OverridesBeliefTint()
        {
            var colors = Build(new FaithStressOverlayData.DupSample
            {
                Cell = 40, Belief = BeliefType.ChurchOfTheCrystal, StrengthPct = 90f, Stress = 75f,
            });

            Assert.Equal(FaithStressOverlayData.StressHighColor.r, colors[40].r, 3);
            Assert.Equal(FaithStressOverlayData.StressHighColor.a, colors[40].a, 3);
        }

        [Fact]
        public void MidStress_Alone_ProducesWeakYellow()
        {
            var colors = Build(new FaithStressOverlayData.DupSample
            {
                Cell = 40, Belief = BeliefType.None, StrengthPct = 0f, Stress = 45f,
            });

            Assert.True(colors.ContainsKey(40));
            Assert.Equal(FaithStressOverlayData.StressMidColor.g, colors[40].g, 3);
        }

        [Fact]
        public void NoBeliefNoStress_ProducesNothing()
        {
            var colors = Build(new FaithStressOverlayData.DupSample
            {
                Cell = 40, Belief = BeliefType.None, StrengthPct = 0f, Stress = 10f,
            });
            Assert.Empty(colors);
        }

        [Fact]
        public void Falloff_DecreasesWithDistance()
        {
            var colors = Build(new FaithStressOverlayData.DupSample
            {
                Cell = 40, Belief = BeliefType.ScribesOfTheData, StrengthPct = 100f, Stress = 0f,
            });

            float near = colors[31].a; // dist 1
            float far = colors[4].a;   // dist 4
            Assert.True(near > far, $"near alpha {near} should exceed far {far}");
        }
    }
}
