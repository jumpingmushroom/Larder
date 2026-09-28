using System;
using System.Collections.Generic;

namespace Larder.Core.Model
{
    public sealed class StationNeed
    {
        public readonly string StationId;
        public readonly int Level;

        public StationNeed(string stationId, int level)
        {
            StationId = stationId;
            Level = level;
        }
    }

    /// <summary>
    /// One item in a cook tree. FromStock came from bag and chests; the rest was made Crafts times
    /// via Via, or is Missing. Leaves have Via == null.
    /// </summary>
    public sealed class CookStep
    {
        public string ItemId;
        public int Need;
        public int FromStock;
        public int Missing;
        public Producer Via;
        public int Crafts;
        public bool StationMissing;
        public readonly List<CookStep> Inputs = new List<CookStep>();
    }

    public sealed class CookPlan
    {
        public CookStep Root;
        public readonly Dictionary<string, int> MissingItems = new Dictionary<string, int>();
        public readonly List<StationNeed> MissingStations = new List<StationNeed>();

        public bool ReadyNow
        {
            get { return MissingItems.Count == 0 && MissingStations.Count == 0; }
        }

        public int MissingUnits
        {
            get
            {
                int n = 0;
                foreach (int v in MissingItems.Values)
                    n += v;
                return n;
            }
        }

        internal void AddMissing(string id, int n)
        {
            int have;
            MissingItems.TryGetValue(id, out have);
            MissingItems[id] = have + n;
        }

        internal void AddStation(string id, int level)
        {
            for (int i = 0; i < MissingStations.Count; i++)
            {
                if (MissingStations[i].StationId != id)
                    continue;
                if (MissingStations[i].Level < level)
                    MissingStations[i] = new StationNeed(id, level);
                return;
            }
            MissingStations.Add(new StationNeed(id, level));
        }

        internal void Merge(CookPlan other)
        {
            foreach (KeyValuePair<string, int> kv in other.MissingItems)
                AddMissing(kv.Key, kv.Value);
            foreach (StationNeed s in other.MissingStations)
                AddStation(s.StationId, s.Level);
        }
    }

    /// <summary>
    /// How to make one of a dish from what's in bag and chests. Ingredients come from stock first;
    /// a shortfall is crafted if a producer exists and the chain is at most MaxDepth crafts deep
    /// (dish = 1). Stock is consumed across the whole tree so nothing is counted twice. Where
    /// several producers exist, the one that is ready, then needs fewest stations, then fewest
    /// missing items, wins.
    /// </summary>
    public static class CookPlanner
    {
        public const int MaxDepth = 3;

        private sealed class Ctx
        {
            public ProducerIndex Producers;
            public IStationLevels Stations;
            public int MaxDepth;
            public readonly HashSet<string> Path = new HashSet<string>();
        }

        public static CookPlan Plan(string itemId, ProducerIndex producers, IDictionary<string, int> stock,
            IStationLevels stations, int maxDepth = MaxDepth)
        {
            CookPlan best = null;
            foreach (Producer p in producers.For(itemId))
            {
                var ctx = new Ctx { Producers = producers, Stations = stations, MaxDepth = maxDepth };
                ctx.Path.Add(itemId);
                var plan = new CookPlan();
                plan.Root = Make(itemId, 1, p, ctx, new Dictionary<string, int>(stock), 1, plan);
                if (best == null || Better(plan, best))
                    best = plan;
            }
            return best;
        }

        private static bool Better(CookPlan a, CookPlan b)
        {
            if (a.ReadyNow != b.ReadyNow)
                return a.ReadyNow;
            if (a.MissingStations.Count != b.MissingStations.Count)
                return a.MissingStations.Count < b.MissingStations.Count;
            return a.MissingUnits < b.MissingUnits;
        }

        private static int Count(Dictionary<string, int> stock, string id)
        {
            int n;
            return stock.TryGetValue(id, out n) ? n : 0;
        }

        private static CookStep Make(string itemId, int need, Producer p, Ctx ctx, Dictionary<string, int> stock,
            int depth, CookPlan plan)
        {
            var step = new CookStep { ItemId = itemId, Need = need, Via = p, Crafts = (need + p.Yield - 1) / p.Yield };
            if (p.StationId.Length > 0 && ctx.Stations.Level(p.StationId) < p.StationLevel)
            {
                step.StationMissing = true;
                plan.AddStation(p.StationId, p.StationLevel);
            }
            foreach (Ingredient ing in p.AnyOneInput ? PickOne(p, step.Crafts, stock) : p.Inputs)
                step.Inputs.Add(Supply(ing.ItemId, ing.Amount * step.Crafts, ctx, stock, depth, plan));
            int surplus = step.Crafts * p.Yield - need;
            if (surplus > 0)
                stock[itemId] = Count(stock, itemId) + surplus;
            return step;
        }

        private static IList<Ingredient> PickOne(Producer p, int crafts, Dictionary<string, int> stock)
        {
            foreach (Ingredient ing in p.Inputs)
            {
                if (Count(stock, ing.ItemId) >= ing.Amount * crafts)
                    return new[] { ing };
            }
            return p.Inputs.Count > 0 ? new[] { p.Inputs[0] } : new Ingredient[0];
        }

        private static CookStep Supply(string itemId, int need, Ctx ctx, Dictionary<string, int> stock, int depth,
            CookPlan plan)
        {
            int have = Count(stock, itemId);
            int take = Math.Min(have, need);
            stock[itemId] = have - take;
            int shortfall = need - take;

            if (shortfall > 0 && depth < ctx.MaxDepth && !ctx.Path.Contains(itemId))
            {
                CookStep bestStep = null;
                CookPlan bestSub = null;
                Dictionary<string, int> bestStock = null;
                ctx.Path.Add(itemId);
                foreach (Producer p in ctx.Producers.For(itemId))
                {
                    var s = new Dictionary<string, int>(stock);
                    var sub = new CookPlan();
                    CookStep made = Make(itemId, shortfall, p, ctx, s, depth + 1, sub);
                    if (bestSub == null || Better(sub, bestSub))
                    {
                        bestStep = made;
                        bestSub = sub;
                        bestStock = s;
                    }
                }
                ctx.Path.Remove(itemId);
                if (bestStep != null)
                {
                    stock.Clear();
                    foreach (KeyValuePair<string, int> kv in bestStock)
                        stock[kv.Key] = kv.Value;
                    plan.Merge(bestSub);
                    bestStep.Need = need;
                    bestStep.FromStock = take;
                    return bestStep;
                }
            }

            if (shortfall > 0)
                plan.AddMissing(itemId, shortfall);
            return new CookStep { ItemId = itemId, Need = need, FromStock = take, Missing = shortfall };
        }
    }
}
