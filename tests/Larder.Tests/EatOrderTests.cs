using System.Collections.Generic;
using System.Linq;
using Larder.Core.Model;
using Xunit;

namespace Larder.Tests
{
    public class EatOrderTests
    {
        private static FoodStats F(string id)
        {
            return ComboSolverTests.F(id, 50, burn: 1200);
        }

        private static string Ids(IEnumerable<FoodStats> foods)
        {
            return string.Join(",", foods.Select(f => f.Id));
        }

        [Fact]
        public void EmptyStomachAllInBagEatsAllThreeInPlanOrder()
        {
            var plan = new[] { F("a"), F("b"), F("c") };
            List<FoodStats> next = EatOrder.Next(plan, new ActiveFood[0], new HashSet<string> { "a", "b", "c" }, 1f);
            Assert.Equal("a,b,c", Ids(next));
        }

        [Fact]
        public void PlannedFoodNotInBagIsSkipped()
        {
            var plan = new[] { F("a"), F("b"), F("c") };
            List<FoodStats> next = EatOrder.Next(plan, new ActiveFood[0], new HashSet<string> { "b", "c" }, 1f);
            Assert.Equal("b,c", Ids(next));
        }

        [Fact]
        public void RefreshesPlannedFoodBeforeNewFoodsEvictOthers()
        {
            var plan = new[] { F("A"), F("N1"), F("N2") };
            var active = new[]
            {
                new ActiveFood("A", 590, 1200),   // planned, under half: refreshable
                new ActiveFood("X", 300, 1200),   // other, under half: refreshable, most depleted
                new ActiveFood("Y", 400, 1200)    // other, under half: refreshable
            };
            var inBag = new HashSet<string> { "A", "N1", "N2" };
            List<FoodStats> next = EatOrder.Next(plan, active, inBag, 1f);
            Assert.Equal("A,N1,N2", Ids(next));
        }

        [Fact]
        public void FullStomachRefreshesBlockedPlannedFoodThenEvictsOther()
        {
            var plan = new[] { F("A"), F("B"), F("N") };
            var active = new[]
            {
                new ActiveFood("A", 120, 1200),   // planned, under half: the eviction target
                new ActiveFood("B", 1000, 1200),  // planned, over half: stays put
                new ActiveFood("X", 300, 1200)    // other, under half: refreshable but not the target while A is
            };
            var inBag = new HashSet<string> { "A", "B", "N" };
            List<FoodStats> next = EatOrder.Next(plan, active, inBag, 1f);
            Assert.Equal("A,N", Ids(next));
        }

        [Fact]
        public void FullStomachNothingRefreshableEatsNothing()
        {
            var plan = new[] { F("A"), F("B"), F("C") };
            var active = new[]
            {
                new ActiveFood("X", 800, 1200),
                new ActiveFood("Y", 900, 1200),
                new ActiveFood("Z", 1000, 1200)
            };
            var inBag = new HashSet<string> { "A", "B", "C" };
            List<FoodStats> next = EatOrder.Next(plan, active, inBag, 1f);
            Assert.Empty(next);
        }

        [Fact]
        public void NeverEatsMoreThanThreeAndNeverPicksTheSameFoodTwice()
        {
            var plan = new[] { F("a"), F("b"), F("c"), F("d") };
            var inBag = new HashSet<string> { "a", "b", "c", "d" };
            List<FoodStats> next = EatOrder.Next(plan, new ActiveFood[0], inBag, 1f);
            Assert.Equal(3, next.Count);
            Assert.Equal("a,b,c", Ids(next));
            Assert.Equal(next.Count, next.Select(f => f.Id).Distinct().Count());
        }

        [Fact]
        public void CountsAndEatsPastAnEarlierPlannedFoodThatsOnlyInAChest()
        {
            // A is planned and already active (full); X is an other food, over half. One free slot.
            // B is planned but only in a chest (not in inBag): it must not reserve the free slot
            // ahead of C, which is planned and in the bag.
            var plan = new[] { F("A"), F("B"), F("C") };
            var active = new[]
            {
                new ActiveFood("A", 1200, 1200),
                new ActiveFood("X", 700, 1200)
            };
            var inBag = new HashSet<string> { "A", "C" };
            List<FoodStats> next = EatOrder.Next(plan, active, inBag, 1f);
            Assert.Equal("C", Ids(next));
        }

        [Fact]
        public void RefreshNowBeatsEatNowRegardlessOfPlanOrder()
        {
            var plan = new[] { F("N"), F("A") };
            var active = new[] { new ActiveFood("A", 500, 1200) }; // planned, under half: refreshable
            var inBag = new HashSet<string> { "N", "A" };
            List<FoodStats> next = EatOrder.Next(plan, active, inBag, 1f);
            Assert.Equal("A,N", Ids(next));
        }

        [Fact]
        public void EvictsTheGloballyMostDepletedActiveFoodRegardlessOfListOrder()
        {
            // Full stomach: X, P, Y are all refreshable, and the most depleted (Y) is last in the
            // active list, not first. P is planned but not in the bag, so it can never be
            // refreshed; eating N1 must evict Y (the true global minimum, matching
            // Player.GetMostDepletedFood), not X. If it wrongly evicted X instead, Y (100, still
            // under P's 400) would remain and let N2 through as EatNow; evicting Y correctly
            // leaves X (590) as the only other food, which is over P's target, so N2 stays
            // permanently blocked (RefreshFirst) this click, since P can't be refreshed.
            var plan = new[] { F("P"), F("N1"), F("N2") };
            var active = new[]
            {
                new ActiveFood("X", 590, 1200),
                new ActiveFood("P", 400, 1200),
                new ActiveFood("Y", 100, 1200)
            };
            var inBag = new HashSet<string> { "N1", "N2" };
            List<FoodStats> next = EatOrder.Next(plan, active, inBag, 1f);
            Assert.Equal("N1", Ids(next));
        }
    }
}
