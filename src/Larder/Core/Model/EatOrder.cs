using System.Collections.Generic;

namespace Larder.Core.Model
{
    /// <summary>
    /// Which planned foods "Eat N" would eat right now, in order, simulating each eat exactly like
    /// Player.EatFood/GetMostDepletedFood (PLAN §1.2, SlotAdvisor): refresh a planned food that's
    /// already active before eating a new one that might evict it.
    /// </summary>
    public static class EatOrder
    {
        public const int MaxEats = 3;

        /// <summary>Simulates up to MaxEats clicks: re-advises after each eat, picks RefreshNow before
        /// EatNow (plan order within a state), and returns the planned foods eaten, in order.</summary>
        public static List<FoodStats> Next(IList<FoodStats> plan, IList<ActiveFood> active, ISet<string> inBag, float foodRate)
        {
            var result = new List<FoodStats>();
            var sim = new List<ActiveFood>(active);
            var eaten = new HashSet<string>();

            for (int i = 0; i < MaxEats; i++)
            {
                List<FoodStats> reachable = Reachable(plan, sim, inBag);
                SlotAdvice advice = SlotAdvisor.Advise(reachable, sim, foodRate);
                PlannedSlot candidate = PickCandidate(advice, inBag, eaten);
                if (candidate == null)
                    break;

                result.Add(candidate.Food);
                eaten.Add(candidate.Food.Id);
                sim = Simulate(sim, candidate.Food);
            }

            return result;
        }

        /// <summary>A planned food that's neither active nor in the bag can't be eaten or refreshed
        /// this click, so it must not be handed a free slot or an eviction target ahead of a planned
        /// food that actually can be eaten (SlotAdvisor assigns slots in plan order).</summary>
        private static List<FoodStats> Reachable(IList<FoodStats> plan, List<ActiveFood> active, ISet<string> inBag)
        {
            var activeIds = new HashSet<string>();
            foreach (ActiveFood a in active)
                activeIds.Add(a.Id);

            var reachable = new List<FoodStats>();
            foreach (FoodStats f in plan)
            {
                if (activeIds.Contains(f.Id) || inBag.Contains(f.Id))
                    reachable.Add(f);
            }
            return reachable;
        }

        private static PlannedSlot PickCandidate(SlotAdvice advice, ISet<string> inBag, HashSet<string> eaten)
        {
            PlannedSlot refreshNow = null;
            PlannedSlot eatNow = null;
            foreach (PlannedSlot p in advice.Planned)
            {
                if (eaten.Contains(p.Food.Id) || !inBag.Contains(p.Food.Id))
                    continue;
                if (p.State == SlotState.RefreshNow && refreshNow == null)
                    refreshNow = p;
                else if (p.State == SlotState.EatNow && eatNow == null)
                    eatNow = p;
            }
            return refreshNow ?? eatNow;
        }

        /// <summary>Player.EatFood: refresh in place if already active, else take a free slot, else
        /// replace GetMostDepletedFood() (the refreshable active food, planned or not, with the least
        /// time left).</summary>
        private static List<ActiveFood> Simulate(List<ActiveFood> active, FoodStats eaten)
        {
            var next = new List<ActiveFood>(active);

            int mineIndex = next.FindIndex(a => a.Id == eaten.Id);
            if (mineIndex >= 0)
            {
                next[mineIndex] = new ActiveFood(eaten.Id, eaten.BurnTime, eaten.BurnTime);
                return next;
            }

            if (next.Count < SlotAdvisor.MaxFoods)
            {
                next.Add(new ActiveFood(eaten.Id, eaten.BurnTime, eaten.BurnTime));
                return next;
            }

            int targetIndex = -1;
            float targetTime = 0f;
            for (int i = 0; i < next.Count; i++)
            {
                ActiveFood a = next[i];
                if (a.CanEatAgain && (targetIndex < 0 || a.TimeLeft < targetTime))
                {
                    targetIndex = i;
                    targetTime = a.TimeLeft;
                }
            }
            if (targetIndex >= 0)
                next[targetIndex] = new ActiveFood(eaten.Id, eaten.BurnTime, eaten.BurnTime);
            return next;
        }
    }
}
