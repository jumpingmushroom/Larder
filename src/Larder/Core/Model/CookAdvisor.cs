using System;
using System.Collections.Generic;

namespace Larder.Core.Model
{
    /// <summary>A food that can be made. MakeId is what a producer outputs (a feast's placeable item for feasts).</summary>
    public sealed class Dish
    {
        public readonly FoodStats Food;
        public readonly string MakeId;

        public Dish(FoodStats food, string makeId = null)
        {
            Food = food;
            MakeId = makeId ?? food.Id;
        }
    }

    public sealed class CookSuggestion
    {
        public readonly Dish Dish;
        public readonly CookPlan Plan;
        public readonly List<FoodStats> Combo;
        public readonly Score Score;

        public CookSuggestion(Dish dish, CookPlan plan, List<FoodStats> combo, Score score)
        {
            Dish = dish;
            Plan = plan;
            Combo = combo;
            Score = score;
        }
    }

    public sealed class CookAdvice
    {
        public CookSuggestion Ready;
        public CookSuggestion Almost;
    }

    /// <summary>
    /// The one dish to cook next (PLAN §2.1). A dish qualifies when adding it to the pool puts it
    /// in the best combo and raises the score. Ready = best qualifying dish you can make now.
    /// Almost = best one that beats Ready but is short of items or a station, where every missing
    /// item is one the player has seen.
    /// </summary>
    public static class CookAdvisor
    {
        public static CookAdvice Advise(IList<FoodStats> pool, Goal goal, IEnumerable<Dish> dishes,
            ProducerIndex producers, IDictionary<string, int> stock, IStationLevels stations, Func<string, bool> itemKnown)
        {
            var advice = new CookAdvice();
            Score baseScore = Scoring.Of(goal, ComboSolver.Best(pool, goal));
            var have = new HashSet<string>();
            foreach (FoodStats f in pool)
                have.Add(f.Id);

            var candidates = new List<CookSuggestion>();
            foreach (Dish dish in dishes)
            {
                if (dish == null || have.Contains(dish.Food.Id))
                    continue;
                var withDish = new List<FoodStats>(pool) { dish.Food };
                List<FoodStats> combo = ComboSolver.Best(withDish, goal);
                if (!combo.Contains(dish.Food))
                    continue;
                Score s = Scoring.Of(goal, combo);
                if (!(s > baseScore))
                    continue;
                CookPlan plan = CookPlanner.Plan(dish.MakeId, producers, stock, stations);
                if (plan != null)
                    candidates.Add(new CookSuggestion(dish, plan, combo, s));
            }

            foreach (CookSuggestion c in candidates)
            {
                if (c.Plan.ReadyNow && (advice.Ready == null || Beats(c, advice.Ready)))
                    advice.Ready = c;
            }

            foreach (CookSuggestion c in candidates)
            {
                if (c.Plan.ReadyNow)
                    continue;
                if (advice.Ready != null && !(c.Score > advice.Ready.Score))
                    continue;
                bool known = true;
                foreach (string id in c.Plan.MissingItems.Keys)
                {
                    if (!itemKnown(id))
                    {
                        known = false;
                        break;
                    }
                }
                if (known && (advice.Almost == null || BeatsAlmost(c, advice.Almost)))
                    advice.Almost = c;
            }
            return advice;
        }

        private static bool Beats(CookSuggestion a, CookSuggestion b)
        {
            int s = a.Score.CompareTo(b.Score);
            return s != 0 ? s > 0 : string.CompareOrdinal(a.Dish.Food.Id, b.Dish.Food.Id) < 0;
        }

        private static bool BeatsAlmost(CookSuggestion a, CookSuggestion b)
        {
            int s = a.Score.CompareTo(b.Score);
            if (s != 0)
                return s > 0;
            if (a.Plan.MissingUnits != b.Plan.MissingUnits)
                return a.Plan.MissingUnits < b.Plan.MissingUnits;
            return string.CompareOrdinal(a.Dish.Food.Id, b.Dish.Food.Id) < 0;
        }
    }
}
