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
        RefreshNow,
        /// <summary>Eating this now would push out the planned food named by BlockedBy; refresh that one first.</summary>
        RefreshFirst
    }

    public sealed class PlannedSlot
    {
        public readonly FoodStats Food;
        public readonly SlotState State;
        /// <summary>Active/RefreshNow: real seconds left. EatLater: real seconds until a slot frees. Otherwise 0.</summary>
        public readonly float Seconds;
        /// <summary>RefreshFirst: the Id of the planned food the game would replace. Otherwise null.</summary>
        public readonly string BlockedBy;

        public PlannedSlot(FoodStats food, SlotState state, float seconds, string blockedBy = null)
        {
            Food = food;
            State = state;
            Seconds = seconds;
            BlockedBy = blockedBy;
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
    /// a new one replaces GetMostDepletedFood(), the refreshable food (under half its duration) with
    /// the least time left among all active foods, planned ones included (Player.cs:2434-2445, 2515).
    /// If that would be a planned food, the new one waits (RefreshFirst) until the planned food is
    /// refreshed. Foods outside the plan are the ones to give up, in the order the game takes them.
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
            var plannedActive = new List<ActiveFood>();
            foreach (ActiveFood a in active)
            {
                if (planIds.Contains(a.Id))
                    plannedActive.Add(a);
                else
                    others.Add(a);
            }
            others.Sort((x, y) =>
            {
                int c = x.SecondsUntilRefreshable(rate).CompareTo(y.SecondsUntilRefreshable(rate));
                return c != 0 ? c : x.TimeLeft.CompareTo(y.TimeLeft);
            });
            foreach (ActiveFood o in others)
                advice.Others.Add(new OtherFood(o.Id, o.SecondsUntilRefreshable(rate)));

            // The planned food the game would replace first, if any is refreshable.
            ActiveFood plannedTarget = null;
            foreach (ActiveFood a in plannedActive)
            {
                if (a.CanEatAgain && (plannedTarget == null || a.TimeLeft < plannedTarget.TimeLeft))
                    plannedTarget = a;
            }

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
                // Others are sorted refreshable-first, least time left first, so others[next] is the
                // most depleted remaining one; a new food just eaten is full and never a target.
                ActiveFood other = next < others.Count ? others[next] : null;
                bool otherReplaceable = other != null && other.CanEatAgain;
                if (plannedTarget != null && (!otherReplaceable || plannedTarget.TimeLeft <= other.TimeLeft))
                {
                    advice.Planned.Add(new PlannedSlot(f, SlotState.RefreshFirst, 0f, plannedTarget.Id));
                    continue;
                }
                float wait = other != null ? other.SecondsUntilRefreshable(rate) : 0f;
                next++;
                advice.Planned.Add(new PlannedSlot(f, wait <= 0f ? SlotState.EatNow : SlotState.EatLater, wait));
            }
            return advice;
        }
    }
}
