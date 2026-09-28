using System;
using System.Collections.Generic;
using System.Linq;
using Larder.Core.Model;
using Xunit;

namespace Larder.Tests
{
    public class ComboSolverTests
    {
        internal static FoodStats F(string id, float hp, float st = 0, float eitr = 0, float burn = 1200, float regen = 1)
        {
            return new FoodStats(id, id, hp, st, eitr, burn, regen);
        }

        private static string Ids(IEnumerable<FoodStats> foods)
        {
            return string.Join(",", foods.Select(f => f.Id));
        }

        [Fact]
        public void HealthGoalPicksTheThreeHighestHealth()
        {
            var pool = new[] { F("a", 10), F("b", 50), F("c", 30), F("d", 40) };
            Assert.Equal("b,d,c", Ids(ComboSolver.Best(pool, Goal.Health)));
        }

        [Fact]
        public void BalancedIgnoresEitrAsPrimary()
        {
            var pool = new[] { F("melee", 60, 20), F("mage", 20, 20, 80), F("x", 30, 30), F("y", 25, 25) };
            // Balanced = hp + st: melee 80, x 60, y 50, mage 40.
            Assert.Equal("melee,x,y", Ids(ComboSolver.Best(pool, Goal.Balanced)));
        }

        [Fact]
        public void EitrGoalPutsEitrFirst()
        {
            var pool = new[] { F("melee", 60, 20), F("mage", 20, 20, 80), F("x", 30, 30), F("y", 25, 25) };
            Assert.Equal("mage", ComboSolver.Best(pool, Goal.Eitr)[0].Id);
        }

        [Fact]
        public void TieOnPrimaryIsBrokenBySecondaryThenRegenThenDuration()
        {
            var pool = new[]
            {
                F("lowSt", 50, 10), F("highSt", 50, 20),
                F("lowRegen", 50, 20, regen: 1), F("highRegen", 50, 20, regen: 3),
            };
            Assert.Equal("highRegen,highSt,lowRegen", Ids(ComboSolver.Best(pool, Goal.Health)));
            var byTime = new[] { F("short", 50, 20, burn: 600), F("long", 50, 20, burn: 1800) };
            Assert.Equal("long,short", Ids(ComboSolver.Best(byTime, Goal.Health)));
        }

        [Fact]
        public void SameFoodCountsOnce()
        {
            var pool = new[] { F("a", 50), F("a", 50), F("b", 10) };
            Assert.Equal("a,b", Ids(ComboSolver.Best(pool, Goal.Health)));
        }

        [Fact]
        public void FewerThanThreeReturnsWhatExists()
        {
            Assert.Empty(ComboSolver.Best(new FoodStats[0], Goal.Balanced));
            Assert.Single(ComboSolver.Best(new[] { F("a", 1) }, Goal.Balanced));
        }

        [Fact]
        public void TotalsAddBaseValues()
        {
            Totals t = Totals.Of(new[] { F("a", 50, 20, 10, regen: 2), F("b", 30, 40, 0, regen: 3) }, 25f, 75f);
            Assert.Equal(105f, t.Health);
            Assert.Equal(135f, t.Stamina);
            Assert.Equal(10f, t.Eitr);
            Assert.Equal(5f, t.Regen);
        }

        [Fact]
        public void MatchesBruteForceOnRandomPools()
        {
            var rng = new Random(1234);
            foreach (Goal goal in Enum.GetValues(typeof(Goal)))
            {
                for (int iter = 0; iter < 300; iter++)
                {
                    int n = rng.Next(0, 10);
                    var pool = new List<FoodStats>();
                    for (int i = 0; i < n; i++)
                        pool.Add(F("f" + i, rng.Next(0, 6) * 10, rng.Next(0, 6) * 10, rng.Next(0, 3) * 10,
                            rng.Next(1, 4) * 600, rng.Next(0, 4)));

                    Score got = Scoring.Of(goal, ComboSolver.Best(pool, goal));
                    Score best = Scoring.Of(goal, new FoodStats[0]);
                    foreach (List<FoodStats> combo in Subsets(pool, Math.Min(3, n), 0))
                    {
                        Score s = Scoring.Of(goal, combo);
                        if (s.CompareTo(best) > 0)
                            best = s;
                    }
                    Assert.Equal(0, got.CompareTo(best));
                }
            }
        }

        private static IEnumerable<List<FoodStats>> Subsets(List<FoodStats> pool, int k, int start)
        {
            if (k == 0)
            {
                yield return new List<FoodStats>();
                yield break;
            }
            for (int i = start; i <= pool.Count - k; i++)
            {
                foreach (List<FoodStats> rest in Subsets(pool, k - 1, i + 1))
                {
                    rest.Insert(0, pool[i]);
                    yield return rest;
                }
            }
        }
    }
}
