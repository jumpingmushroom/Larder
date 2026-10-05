using System;
using System.Collections.Generic;
using System.Diagnostics;
using Larder.Core.Model;

namespace Larder.Core
{
    internal sealed class PlanView
    {
        public Goal Goal;
        public Snapshot Snapshot;
        public StationLevels Stations;
        public List<FoodStats> Combo = new List<FoodStats>();
        public SlotAdvice Slots = new SlotAdvice();
        /// <summary>Player.m_foods as of this plan, for EatOrder (Eat N button).</summary>
        public List<ActiveFood> Active = new List<ActiveFood>();
        /// <summary>Shared names of foods directly in the bag (not via a feast item), for EatOrder.</summary>
        public HashSet<string> BagFoods = new HashSet<string>();
        public Totals Now;
        public Totals Planned;
        public CookAdvice Cook = new CookAdvice();
        public string Error;
        public double Millis;
    }

    /// <summary>Snapshot → model → view. Never throws: a failure becomes PlanView.Error and one log warning.</summary>
    internal static class Planner
    {
        public static PlanView Compute(Player player, Goal goal)
        {
            var view = new PlanView { Goal = goal };
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                FoodCatalog.EnsureBuilt();
                float radius = PluginConfig.Radius.Value;
                List<Piece> nearby = NearbyPieces.Collect(player.transform.position, radius);
                view.Snapshot = StockScanner.Take(player, nearby);
                view.Stations = StationScanner.Scan(player.transform.position, radius, nearby);
                view.BagFoods = StockScanner.BagFoodIds(player.GetInventory());
                List<ActiveFood> active = ActiveFoods.Read(player);
                view.Active = active;
                StockScanner.AddEaten(view.Snapshot, active);

                view.Combo = ComboSolver.Best(view.Snapshot.Pool, goal);
                view.Slots = SlotAdvisor.Advise(view.Combo, active, Game.m_foodRate);
                view.Planned = Totals.Of(view.Combo, player.m_baseHP, player.m_baseStamina);
                view.Now = new Totals { Health = player.GetMaxHealth(), Stamina = player.GetMaxStamina(), Eitr = player.GetMaxEitr() };

                var dishes = new List<Dish>();
                foreach (Dish d in FoodCatalog.Dishes)
                {
                    if (Discovery.DishKnown(player, d))
                        dishes.Add(d);
                }
                view.Cook = CookAdvisor.Advise(view.Snapshot.Pool, goal, dishes, FoodCatalog.Producers,
                    view.Snapshot.Stock, view.Stations, id => Discovery.ItemKnown(player, id));
            }
            catch (Exception e)
            {
                view.Error = e.GetType().Name + ": " + e.Message;
                LarderPlugin.WarnOnce("Larder: plan failed", e);
            }
            view.Millis = sw.Elapsed.TotalMilliseconds;
            if (PluginConfig.Verbose.Value)
                LarderPlugin.Log.LogInfo("Larder: planned in " + view.Millis.ToString("0.00") + " ms");
            return view;
        }
    }
}
