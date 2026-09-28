using System;
using System.Collections.Generic;

namespace Larder.Core.Model
{
    /// <summary>
    /// Best three distinct foods for a goal. Every score component is a plain sum, so the triple
    /// with the best lexicographic sum is exactly the top three foods by their own lexicographic
    /// score; sorting is enough (checked against brute force in the tests).
    /// </summary>
    public static class ComboSolver
    {
        public const int Slots = 3;

        public static List<FoodStats> Best(IEnumerable<FoodStats> pool, Goal goal)
        {
            var seen = new HashSet<string>();
            var distinct = new List<FoodStats>();
            foreach (FoodStats f in pool)
            {
                if (f != null && seen.Add(f.Id))
                    distinct.Add(f);
            }
            distinct.Sort((x, y) =>
            {
                int c = Scoring.Of(goal, y).CompareTo(Scoring.Of(goal, x));
                return c != 0 ? c : string.CompareOrdinal(x.Id, y.Id);
            });
            return distinct.GetRange(0, Math.Min(Slots, distinct.Count));
        }
    }
}
