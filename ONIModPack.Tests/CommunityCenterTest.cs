using Xunit;
using ONIModPack.Content.Buildings;
using ONIModPack.Content.SocialDynamics;

namespace ONIModPack.Tests
{
    public class CommunityCenterTest
    {
        [Fact]
        public void CommunityCenterConfigDefaults()
        {
            Assert.Equal("CommunityCenter", CommunityCenterConfig.ID);
        }

        [Fact]
        public void PartyHeldEventStructure()
        {
            var evt = new PartyHeldEvent
            {
                CommunityCenterId = 123,
                ParticipantIds = new[] { 1, 2, 3 },
                Duration = 60f,
                HappinessBoost = 10f,
                LoyaltyBoost = 5f,
                SatisfactionBonus = 10f,
                InfluenceRadius = 5f
            };
            
            Assert.Equal(123, evt.CommunityCenterId);
            Assert.NotNull(evt.ParticipantIds);
            Assert.Equal(3, evt.ParticipantIds.Length);
            Assert.Equal(60f, evt.Duration);
            Assert.Equal(10f, evt.HappinessBoost);
            Assert.Equal(5f, evt.LoyaltyBoost);
            Assert.Equal(10f, evt.SatisfactionBonus);
            Assert.Equal(5f, evt.InfluenceRadius);
        }
    }
}