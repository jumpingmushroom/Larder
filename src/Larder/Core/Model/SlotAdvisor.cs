using System;
using System.Collections.Generic;

namespace Larder.Core.Model
{
    /// <summary>A food in one of the player's slots. TimeLeft is Player.Food.m_time (game seconds).</summary>
    public sealed class ActiveFood
    {
        public readonly string Id;
        public readonly float TimeLeft;
        public readonly float BurnTime;

        public ActiveFood(string id, float timeLeft, float burnTime)
        {
            Id = id;
            TimeLeft = timeLeft;
            BurnTime = burnTime;
        }

        /// <summary>Player.Food.CanEatAgain: under half its duration.</summary>
        public bool CanEatAgain
        {
            get { return TimeLeft < BurnTime / 2f; }
        }

        public float SecondsUntilRefreshable(float rate)
        {
            return CanEatAgain ? 0f : (TimeLeft - BurnTime / 2f) / rate;
        }
    }

    public enum SlotState
    {
        EatNow,
        EatLater,
        Active,
        RefreshNow
    }

    public sealed class PlannedSlot
    {
        public readonly FoodStats Food;
        public readonly SlotState State;
        /// <summary>Active: real seconds left. EatLater: real seconds until a slot frees. Otherwise 0.</summary>
        public readonly float Seconds;

        public PlannedSlot(FoodStats food, SlotState state, float seconds)
        {
            Food = food;
            State = state;
            Seconds = seconds;
        }
    }

    /// <summary>An active food that isn't in the plan, and when the game will let a new food replace it.</summary>
    public sealed class OtherFood
    {
        public readonly string Id;
        public readonly float SecondsUntilFree;

        public OtherFood(string id, float secondsUntilFree)
        {
            Id = id;
            SecondsUntilFree = secondsUntilFree;
        }
    }

    public sealed class SlotAdvice
    {
        public readonly List<PlannedSlot> Planned = new List<PlannedSlot>();
        public readonly List<OtherFood> Others = new List<OtherFood>();
    }

    /// <summary>
    /// Mirrors Player.CanEat / EatFood (PLAN §1.2): a free slot takes a new food; with three foods,
    /// a new one replaces a food that is under half its duration. Foods outside the plan are the
    /// ones to give up, soonest-replaceable first.
    /// </summary>
    public static class SlotAdvisor
    {
        public const int MaxFoods = 3;

        public static SlotAdvice Advise(IList<FoodStats> plan, IList<ActiveFood> active, float foodRate)
        {
            float rate = foodRate > 0f ? foodRate : 1f;
            var advice = new SlotAdvice();

            var planIds = new HashSet<string>();
            foreach (FoodStats f in plan)
                planIds.Add(f.Id);

            var others = new List<ActiveFood>();
            foreach (ActiveFood a in active)
            {
                if (!planIds.Contains(a.Id))
                    others.Add(a);
            }
            others.Sort((x, y) => x.SecondsUntilRefreshable(rate).CompareTo(y.SecondsUntilRefreshable(rate)));
            foreach (ActiveFood o in others)
                advice.Others.Add(new OtherFood(o.Id, o.SecondsUntilRefreshable(rate)));

            int free = Math.Max(0, MaxFoods - active.Count);
            int next = 0;
            foreach (FoodStats f in plan)
            {
                ActiveFood mine = null;
                foreach (ActiveFood a in active)
                {
                    if (a.Id == f.Id)
                        mine = a;
                }
                if (mine != null)
                {
                    advice.Planned.Add(new PlannedSlot(f, mine.CanEatAgain ? SlotState.RefreshNow : SlotState.Active, mine.TimeLeft / rate));
                    continue;
                }
                if (free > 0)
                {
                    free--;
                    advice.Planned.Add(new PlannedSlot(f, SlotState.EatNow, 0f));
                    continue;
                }
                float wait = next < others.Count ? others[next].SecondsUntilRefreshable(rate) : 0f;
                next++;
                advice.Planned.Add(new PlannedSlot(f, wait <= 0f ? SlotState.EatNow : SlotState.EatLater, wait));
            }
            return advice;
        }
    }
}
