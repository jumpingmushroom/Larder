using System.Collections.Generic;
using Larder.Core.Model;
using Xunit;

namespace Larder.Tests
{
    public class SlotAdvisorTests
    {
        private static FoodStats F(string id)
        {
            return ComboSolverTests.F(id, 50, burn: 1200);
        }

        private static readonly FoodStats[] Plan = { F("a"), F("b"), F("c") };

        [Fact]
        public void EmptyStomachEatsEverythingNow()
        {
            SlotAdvice s = SlotAdvisor.Advise(Plan, new ActiveFood[0], 1f);
            Assert.All(s.Planned, p => Assert.Equal(SlotState.EatNow, p.State));
            Assert.Empty(s.Others);
        }

        [Fact]
        public void PlannedFoodOverHalfIsActiveWithTimeLeft()
        {
            SlotAdvice s = SlotAdvisor.Advise(Plan, new[] { new ActiveFood("a", 900, 1200) }, 1f);
            Assert.Equal(SlotState.Active, s.Planned[0].State);
            Assert.Equal(900f, s.Planned[0].Seconds);
        }

        [Fact]
        public void PlannedFoodUnderHalfCanBeRefreshed()
        {
            SlotAdvice s = SlotAdvisor.Advise(Plan, new[] { new ActiveFood("a", 599, 1200) }, 1f);
            Assert.Equal(SlotState.RefreshNow, s.Planned[0].State);
        }

        [Fact]
        public void FullStomachWaitsForTheOtherFoodToDropUnderHalf()
        {
            var active = new[] { new ActiveFood("a", 1000, 1200), new ActiveFood("b", 1000, 1200), new ActiveFood("x", 960, 1200) };
            SlotAdvice s = SlotAdvisor.Advise(Plan, active, 1f);
            Assert.Equal(SlotState.EatLater, s.Planned[2].State);
            Assert.Equal(360f, s.Planned[2].Seconds);   // 960 - 600
            Assert.Equal("x", s.Others[0].Id);
            Assert.Equal(360f, s.Others[0].SecondsUntilFree);
        }

        [Fact]
        public void FullStomachWithReplaceableFoodEatsNow()
        {
            var active = new[] { new ActiveFood("a", 1000, 1200), new ActiveFood("b", 1000, 1200), new ActiveFood("x", 500, 1200) };
            SlotAdvice s = SlotAdvisor.Advise(Plan, active, 1f);
            Assert.Equal(SlotState.EatNow, s.Planned[2].State);
            Assert.Equal(0f, s.Others[0].SecondsUntilFree);
        }

        [Fact]
        public void SoonestFreeSlotGoesToTheFirstWaitingFood()
        {
            var active = new[] { new ActiveFood("a", 1000, 1200), new ActiveFood("x", 1100, 1200), new ActiveFood("y", 700, 1200) };
            SlotAdvice s = SlotAdvisor.Advise(Plan, active, 1f);
            Assert.Equal(100f, s.Planned[1].Seconds);   // y frees first
            Assert.Equal(500f, s.Planned[2].Seconds);   // then x
        }

        [Fact]
        public void FoodRateScalesRealTime()
        {
            SlotAdvice s = SlotAdvisor.Advise(Plan, new[] { new ActiveFood("a", 900, 1200) }, 2f);
            Assert.Equal(450f, s.Planned[0].Seconds);
        }

        [Theory]
        [InlineData(0f, "0s")]
        [InlineData(45f, "45s")]
        [InlineData(90f, "1m")]
        [InlineData(1500f, "25m")]
        [InlineData(3900f, "1h 05m")]
        public void DurationLabels(float seconds, string expected)
        {
            Assert.Equal(expected, Labels.Duration(seconds));
        }
    }
}
